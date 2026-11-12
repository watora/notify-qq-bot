using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notify.Domain.Config.Options;
using Notify.Domain.Models;
using Notify.Service.ChatBot;
using Xunit;

namespace Notify.Tests;

public class ChatBotResponsesTests
{
    private const string Completed = """
        {"id":"resp_1","status":"completed","model":"model-snapshot","output":[
          {"type":"reasoning","id":"rs_1","summary":[],"encrypted_content":"opaque-reasoning"},
          {"type":"message","id":"msg_1","role":"assistant","status":"completed","phase":"final_answer",
           "content":[{"type":"output_text","text":"hello ","annotations":[]},{"type":"output_text","text":"world","annotations":[]}]}]}
        """;

    [Theory]
    [InlineData("https://example.com", "/v1/responses")]
    [InlineData("https://example.com/", "/v1/responses")]
    [InlineData("https://example.com/v1/", "/v1/responses")]
    [InlineData("https://example.com/proxy/v1", "/proxy/v1/responses")]
    public async Task UsesStandardResponsesProtocol(string endpoint, string path)
    {
        var handler = new Handler();
        await using var services = Services(handler, endpoint);
        await using var scope = services.CreateAsyncScope();
        var input = OpenAIResponseInput.FromText("requested-model", "question");
        input.Instructions = "Be concise.";
        input.MaxOutputTokens = 512;
        var result = await scope.ServiceProvider.GetRequiredService<ChatBotOpenAIResponses>().Chat("session", input);
        Assert.Equal("hello world", result!.OutputText);
        var sent = Assert.Single(handler.Requests);
        Assert.Equal(path, sent.Uri.AbsolutePath);
        Assert.Equal("Bearer responses-token", sent.Authorization);
        Assert.Equal("requested-model", sent.Body.GetProperty("model").GetString());
        Assert.Equal("Be concise.", sent.Body.GetProperty("instructions").GetString());
        Assert.Equal(512, sent.Body.GetProperty("max_output_tokens").GetInt32());
        Assert.False(sent.Body.GetProperty("store").GetBoolean());
        Assert.False(sent.Body.GetProperty("stream").GetBoolean());
        Assert.False(sent.Body.TryGetProperty("messages", out _));
        Assert.Equal("input_text", sent.Body.GetProperty("input")[0].GetProperty("content")[0].GetProperty("type").GetString());
        Assert.Equal("reasoning.encrypted_content", sent.Body.GetProperty("include")[0].GetString());
    }

    [Fact]
    public async Task HistorySurvivesScopesPreservesAllOutputAndUsesRequestedModelKey()
    {
        var handler = new Handler();
        await using var services = Services(handler);
        var input = OpenAIResponseInput.FromText("requested-alias", "first");
        await using (var scope = services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ChatBotOpenAIResponses>().Chat("session", input);
        await using (var scope = services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ChatBotOpenAIResponses>().Chat("session", OpenAIResponseInput.FromText("requested-alias", "second"));
        Assert.Single(input.Input);
        var history = handler.Requests.Last().Body.GetProperty("input");
        Assert.Equal(4, history.GetArrayLength());
        Assert.Equal("first", history[0].GetProperty("content")[0].GetProperty("text").GetString());
        Assert.Equal("opaque-reasoning", history[1].GetProperty("encrypted_content").GetString());
        Assert.Equal("final_answer", history[2].GetProperty("phase").GetString());
        Assert.Equal("second", history[3].GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Theory]
    [InlineData("{\"error\":{\"code\":\"rate_limit_exceeded\"}}", 429)]
    [InlineData("{\"id\":\"r\",\"status\":\"failed\",\"output\":[]}", 200)]
    [InlineData("{\"id\":\"r\",\"status\":\"incomplete\",\"incomplete_details\":{\"reason\":\"max_output_tokens\"}}", 200)]
    [InlineData("{\"id\":\"r\",\"status\":\"completed\",\"output\":[]}", 200)]
    [InlineData("not-json", 200)]
    public async Task FailedTurnDoesNotPolluteHistory(string failure, int status)
    {
        var handler = new Handler();
        await using var services = Services(handler);
        await using var scope = services.CreateAsyncScope();
        var bot = scope.ServiceProvider.GetRequiredService<ChatBotOpenAIResponses>();
        Assert.NotNull(await bot.Chat("session", OpenAIResponseInput.FromText("model", "first")));
        handler.Body = failure;
        handler.Status = (HttpStatusCode)status;
        Assert.Null(await bot.Chat("session", OpenAIResponseInput.FromText("model", "failed-turn")));
        handler.Body = Completed;
        handler.Status = HttpStatusCode.OK;
        Assert.NotNull(await bot.Chat("session", OpenAIResponseInput.FromText("model", "retry")));
        var input = handler.Requests.Last().Body.GetProperty("input");
        Assert.Equal(4, input.GetArrayLength());
        Assert.DoesNotContain("failed-turn", input.GetRawText());
        Assert.Contains("first", input.GetRawText());
    }

    [Fact]
    public async Task SessionsModelsAndInstructionsHaveSeparateHistory()
    {
        var handler = new Handler();
        await using var services = Services(handler);
        await using var scope = services.CreateAsyncScope();
        var bot = scope.ServiceProvider.GetRequiredService<ChatBotOpenAIResponses>();
        await bot.Chat("one", OpenAIResponseInput.FromText("a", "first"));
        await bot.Chat("two", OpenAIResponseInput.FromText("a", "second"));
        await bot.Chat("one", OpenAIResponseInput.FromText("b", "third"));
        var changed = OpenAIResponseInput.FromText("a", "fourth");
        changed.Instructions = "different instructions";
        await bot.Chat("one", changed);
        Assert.All(handler.Requests, request => Assert.Equal(1, request.Body.GetProperty("input").GetArrayLength()));
    }

    [Fact]
    public async Task ImagesUseFlatInputImageUrlAndRefusalsAreDisplayed()
    {
        var handler = new Handler { Body = """
            {"id":"r","status":"completed","output":[{"type":"message","role":"assistant","content":[{"type":"refusal","refusal":"Cannot help with that."}]}]}
            """ };
        await using var services = Services(handler);
        await using var scope = services.CreateAsyncScope();
        var input = OpenAIResponseInput.FromText("model", "describe");
        input.Input[0].Content.Add(new() { Type = "input_image", ImageUrl = "data:image/png;base64,test", Detail = "auto" });
        var result = await scope.ServiceProvider.GetRequiredService<ChatBotOpenAIResponses>().Chat("session", input);
        Assert.Equal("Cannot help with that.", result!.GetReplyText());
        Assert.Equal("", result.OutputText);
        var image = handler.Requests.Single().Body.GetProperty("input")[0].GetProperty("content")[1];
        Assert.Equal("input_image", image.GetProperty("type").GetString());
        Assert.Equal("data:image/png;base64,test", image.GetProperty("image_url").GetString());
    }

    [Fact]
    public async Task ConcurrentTurnsSerializeWithoutLosingHistory()
    {
        var handler = new Handler { Delay = true };
        await using var services = Services(handler);
        await using var scope = services.CreateAsyncScope();
        var bot = scope.ServiceProvider.GetRequiredService<ChatBotOpenAIResponses>();
        var results = await Task.WhenAll(
            bot.Chat("session", OpenAIResponseInput.FromText("model", "first")),
            bot.Chat("session", OpenAIResponseInput.FromText("model", "second")));
        Assert.All(results, value => Assert.NotNull(value));
        Assert.Equal(new[] { 1, 4 }, handler.Requests.Select(x => x.Body.GetProperty("input").GetArrayLength()));
    }

    [Fact]
    public async Task LongConversationResetsWholeReasoningPrefix()
    {
        var handler = new Handler();
        await using var services = Services(handler);
        await using var scope = services.CreateAsyncScope();
        var bot = scope.ServiceProvider.GetRequiredService<ChatBotOpenAIResponses>();
        for (var i = 0; i < 26; i++)
            Assert.NotNull(await bot.Chat("session", OpenAIResponseInput.FromText("model", $"turn-{i}")));
        Assert.Equal(1, handler.Requests.Last().Body.GetProperty("input").GetArrayLength());
    }

    [Fact]
    public async Task FallsBackToExistingOpenAiSettingsAndSupportsCancellation()
    {
        var handler = new Handler();
        await using var services = Services(handler, useFallback: true);
        await using var scope = services.CreateAsyncScope();
        var bot = scope.ServiceProvider.GetRequiredService<ChatBotOpenAIResponses>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bot.Chat("session", OpenAIResponseInput.FromText("model", "cancelled"), new CancellationToken(true)));
        Assert.Empty(handler.Requests);
        Assert.NotNull(await bot.Chat("session", OpenAIResponseInput.FromText("model", "hello")));
        var request = Assert.Single(handler.Requests);
        Assert.Equal("fallback.example", request.Uri.Host);
        Assert.Equal("Bearer fallback-token", request.Authorization);
    }

    private static ServiceProvider Services(Handler handler, string endpoint = "https://example.com", bool useFallback = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IHttpClientFactory>(handler);
        services.Configure<ChatBotOption>(o =>
        {
            if (useFallback)
            {
                o.OpenAIEndpoint = "https://fallback.example";
                o.OpenAIToken = "fallback-token";
            }
            else
            {
                o.OpenAIResponsesEndpoint = endpoint;
                o.OpenAIResponsesToken = "responses-token";
            }
        });
        services.AddScoped<ChatBotOpenAIResponses>();
        return services.BuildServiceProvider();
    }

    private sealed class Handler : HttpMessageHandler, IHttpClientFactory
    {
        public ConcurrentQueue<(Uri Uri, string? Authorization, JsonElement Body)> Requests = new();
        public string Body = Completed;
        public HttpStatusCode Status = HttpStatusCode.OK;
        public bool Delay;
        public HttpClient CreateClient(string name) => new(this, false);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Enqueue((request.RequestUri!, request.Headers.Authorization?.ToString(),
                JsonSerializer.Deserialize<JsonElement>(await request.Content!.ReadAsStringAsync(cancellationToken))));
            if (Delay)
                await Task.Delay(30, cancellationToken);
            return new HttpResponseMessage(Status) { Content = new StringContent(Body, Encoding.UTF8, "application/json") };
        }
    }
}
