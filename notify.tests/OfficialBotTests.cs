using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using notify.Controllers;
using Notify.Domain.Config.Options;
using Notify.Domain.Models;
using Notify.Repository;
using Notify.Repository.Entity;
using Notify.Service.ChatBot;
using Notify.Service.Manager;
using Notify.Service.OfficialBot;
using Xunit;

namespace Notify.Tests;

public partial class OfficialBotTests
{
    private static IOptions<OfficialBotOption> Options(bool enabled = true) => Microsoft.Extensions.Options.Options.Create(new OfficialBotOption
    {
        Enabled = enabled, AppId = "test-app", AppSecret = "naOC0ocQE3shWLAfffVLB1rhYPG7"
    });

    [Fact]
    public void SignatureMatchesGoReferenceAndRejectsTampering()
    {
        var signature = new OfficialBotSignature(Options());
        const string body = "{ \"op\": 0,\"d\": {}, \"t\": \"GATEWAY_EVENT_NAME\"}";
        // Generated independently with Go crypto/ed25519 and the official repeated-secret algorithm.
        // The documentation's printed signature does not match its printed body; preserve these exact bytes.
        const string expected = "2eb9983ebb8bb209e78fd095942f58e442656656e7975d01e64f9023a84b7c964290fdd40e5500c33867ccfe9563b7e0b6bac0e1d42c13e787b304fd51f71102";
        Assert.Equal(expected, signature.SignChallenge("1725442341", body));
        Assert.True(signature.Verify("1725442341", Encoding.UTF8.GetBytes(body), expected));
        Assert.False(signature.Verify("1725442342", Encoding.UTF8.GetBytes(body), expected));
        Assert.False(signature.Verify("1725442341", Encoding.UTF8.GetBytes(body + " "), expected));
        Assert.False(signature.Verify("1725442341", Encoding.UTF8.GetBytes(body), new string('z', 128)));
    }

    [Theory]
    [InlineData("C2C_MESSAGE_CREATE", "private", "user-openid")]
    [InlineData("GROUP_AT_MESSAGE_CREATE", "group", "group-openid")]
    [InlineData("GROUP_MESSAGE_CREATE", "group", "group-openid")]
    public async Task SignedWebhookRoutesAndDeduplicates(string eventType, string targetType, string targetId)
    {
        using var queue = new OfficialBotEventQueue();
        var raw = EventBody(eventType);
        for (var i = 0; i < 2; i++)
        {
            var result = Assert.IsType<OkObjectResult>(await Controller(raw, queue).HandleEvent(default));
            var ack = JsonSerializer.SerializeToElement(result.Value);
            Assert.Equal(12, ack.GetProperty("op").GetInt32());
            Assert.Equal(0, ack.GetProperty("d").GetInt32());
        }
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await using var reader = queue.ReadAllAsync(timeout.Token).GetAsyncEnumerator();
        Assert.True(await reader.MoveNextAsync());
        Assert.Equal(targetType, reader.Current.TargetType);
        Assert.Equal(targetId, reader.Current.TargetId);
        Assert.Equal("msg-1", reader.Current.Id);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await reader.MoveNextAsync());
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task WebhookRejectsMissingSignatureOrWrongApp(bool signed, bool correctApp)
    {
        using var queue = new OfficialBotEventQueue();
        var controller = Controller(EventBody("C2C_MESSAGE_CREATE"), queue, signed);
        if (!correctApp)
            controller.Request.Headers["X-Bot-Appid"] = "other-app";
        Assert.IsType<UnauthorizedResult>(await controller.HandleEvent(default));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{\"d\":{}}")]
    [InlineData("{\"op\":13,\"d\":{}}")]
    [InlineData("{\"op\":0,\"t\":\"C2C_MESSAGE_CREATE\",\"d\":{\"id\":\"x\",\"author\":{\"member_openid\":\"not-a-user-openid\"}}}")]
    public async Task WebhookRejectsMalformedPayloads(string body)
    {
        using var queue = new OfficialBotEventQueue();
        Assert.IsType<BadRequestResult>(await Controller(body, queue).HandleEvent(default));
    }

    [Fact]
    public async Task ChallengeWorksWithoutHeadersAndDisabledBotDoesNotAcceptCallbacks()
    {
        using var queue = new OfficialBotEventQueue();
        const string body = "{\"op\":13,\"d\":{\"plain_token\":\"challenge\",\"event_ts\":\"123\"}}";
        var result = Assert.IsType<OkObjectResult>(await Controller(body, queue, false).HandleEvent(default));
        var json = JsonSerializer.SerializeToElement(result.Value);
        Assert.Equal("challenge", json.GetProperty("plain_token").GetString());
        Assert.True(new OfficialBotSignature(Options()).Verify("123", Encoding.UTF8.GetBytes("challenge"), json.GetProperty("signature").GetString()!));
        Assert.IsType<NotFoundResult>(await Controller(body, queue, false, false).HandleEvent(default));
    }

    [Fact]
    public async Task UnknownSignedEventIsAcknowledged()
    {
        using var queue = new OfficialBotEventQueue();
        Assert.IsType<OkObjectResult>(await Controller("{\"op\":0,\"t\":\"FRIEND_ADD\",\"d\":{}}", queue).HandleEvent(default));
    }

    [Fact]
    public async Task FullQueueReturns503AndDoesNotRememberRejectedMessage()
    {
        using var queue = new OfficialBotEventQueue();
        for (var i = 0; i < 100; i++)
            Assert.True(queue.TryEnqueue(new OfficialBotEventMessage { Id = i.ToString() }));
        var rejected = Assert.IsType<StatusCodeResult>(await Controller(EventBody("C2C_MESSAGE_CREATE"), queue).HandleEvent(default));
        Assert.Equal(503, rejected.StatusCode);
        await using var reader = queue.ReadAllAsync(default).GetAsyncEnumerator();
        Assert.True(await reader.MoveNextAsync());
        Assert.IsType<OkObjectResult>(await Controller(EventBody("C2C_MESSAGE_CREATE"), queue).HandleEvent(default));
    }

    [Fact]
    public void DeduplicationDistinguishesMessageIndexAndSequence()
    {
        var message = new OfficialBotEventMessage { Id = "id", EventType = "GROUP_MESSAGE_CREATE", GroupOpenId = "g" };
        var initial = message.DeduplicationKey;
        message.EventType = "GROUP_AT_MESSAGE_CREATE";
        Assert.Equal(initial, message.DeduplicationKey);
        message.MsgSeq = 2;
        Assert.NotEqual(initial, message.DeduplicationKey);
        var next = message.DeduplicationKey;
        message.MessageScene = new OfficialBotMessageScene { Ext = new() { "msg_idx=unique==" } };
        Assert.NotEqual(next, message.DeduplicationKey);
    }

    [Fact]
    public async Task SendUsesOfficialRoutesAuthenticationAndCachesTokenUnderConcurrency()
    {
        var handler = new FakeHttpHandler();
        using var api = Api(handler);
        var tasks = Enumerable.Range(0, 12).Select(i => api.SendMessage("open-id", i % 2 == 0 ? "private" : "group",
            new OfficialBotMessage { Content = "hello", MsgId = "incoming-id", MsgSeq = i + 1 }));
        Assert.All(await Task.WhenAll(tasks), value => Assert.True(value));
        Assert.Equal(1, handler.TokenCalls);
        Assert.Equal(12, handler.Messages.Count);
        Assert.Contains(handler.Messages, r => r.Uri.AbsolutePath == "/v2/users/open-id/messages");
        Assert.Contains(handler.Messages, r => r.Uri.AbsolutePath == "/v2/groups/open-id/messages");
        Assert.All(handler.Messages, r =>
        {
            Assert.Equal("api.bot.qq.com", r.Uri.Host);
            Assert.Equal("QQBot token-1", r.Authorization);
            Assert.Equal("incoming-id", r.Body.GetProperty("msg_id").GetString());
            Assert.Equal(0, r.Body.GetProperty("msg_type").GetInt32());
            Assert.False(r.Body.TryGetProperty("event_id", out _));
        });
    }

    [Fact]
    public async Task UnauthorizedRefreshesTokenAndRetriesOnceWithSameSequence()
    {
        var handler = new FakeHttpHandler { RejectFirst = true };
        using var api = Api(handler);
        Assert.True(await api.SendPrivateMessage("u", new OfficialBotMessage { Content = "reply", MsgId = "m", MsgSeq = 3 }));
        Assert.Equal(2, handler.TokenCalls);
        Assert.Equal(new[] { "QQBot token-1", "QQBot token-2" }, handler.Messages.Select(x => x.Authorization));
        Assert.All(handler.Messages, x => Assert.Equal(3, x.Body.GetProperty("msg_seq").GetInt32()));
    }

    [Theory]
    [InlineData("{\"err_code\":40034005,\"message\":\"expired\"}", 200)]
    [InlineData("{\"code\":22009,\"message\":\"limited\"}", 429)]
    [InlineData("{}", 200)]
    [InlineData("not json", 500)]
    [InlineData("{\"code\":11243}", 401)]
    public async Task SendDoesNotReportFailuresAsSuccess(string response, int status)
    {
        var handler = new FakeHttpHandler { SendBody = response, SendStatus = (HttpStatusCode)status };
        using var api = Api(handler);
        Assert.False(await api.SendGroupMessage("g", new OfficialBotMessage { Content = "hello" }));
        Assert.Equal(status == 401 ? 2 : 1, handler.Messages.Count);
    }

    [Fact]
    public async Task ActiveMessageOmitsReplyIdsAndInvalidTargetIsRejected()
    {
        var handler = new FakeHttpHandler();
        using var api = Api(handler);
        Assert.True(await api.SendPrivateMessage("u", new OfficialBotMessage { Content = "hello" }));
        Assert.False(handler.Messages.Single().Body.TryGetProperty("msg_id", out _));
        await Assert.ThrowsAsync<ArgumentException>(() => api.SendMessage("u", "channel", new OfficialBotMessage { Content = "x" }));
        await Assert.ThrowsAsync<ArgumentException>(() => api.SendPrivateMessage("u", new OfficialBotMessage { Content = "x", MsgId = "m", EventId = "e" }));
    }

    [Theory]
    [InlineData("C2C_MESSAGE_CREATE", "OpenAI")]
    [InlineData("GROUP_AT_MESSAGE_CREATE", "Anthropic")]
    [InlineData("GROUP_MESSAGE_CREATE", "OpenAI")]
    [InlineData("C2C_MESSAGE_CREATE", "OpenAIResponses")]
    [InlineData("GROUP_AT_MESSAGE_CREATE", "OpenAIResponses")]
    public async Task ChatUsesOpenIdConfigAndRepliesThroughOfficialApi(string eventType, string provider)
    {
        await using var db = new NotifyContext(":memory:");
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        var message = JsonSerializer.Deserialize<OfficialBotPayload>(EventBody(eventType))!.Data.Deserialize<OfficialBotEventMessage>()!;
        message.EventType = eventType;
        db.ChatConfig.Add(new ChatConfig { TargetId = message.TargetId, TargetType = message.TargetType, IsActive = true,
            Provider = provider, Model = "test-model", Comment = "test", Creator = "test" });
        await db.SaveChangesAsync();

        var handler = new FakeHttpHandler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IHttpClientFactory>(handler);
        services.AddSingleton(Options());
        services.Configure<ChatBotOption>(o => { o.OpenAIEndpoint = "https://ai.example"; o.AnthropicEndpoint = "https://ai.example"; });
        services.AddScoped(_ => db);
        services.AddScoped<ChatBotManager>();
        services.AddScoped<ChatBotOpenAI>();
        services.AddScoped<ChatBotOpenAIResponses>();
        services.AddScoped<ChatBotAnthropic>();
        services.AddSingleton<OfficialBotApi>();
        services.AddScoped<OfficialBotEvent>();
        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<OfficialBotEvent>().HandleEvent(message);
        var sent = Assert.Single(handler.Messages);
        Assert.Equal("msg-1", sent.Body.GetProperty("msg_id").GetString());
        Assert.Contains("AI reply", sent.Body.GetProperty("content").GetString());
        Assert.Equal($"/v2/{(message.TargetType == "private" ? "users" : "groups")}/{message.TargetId}/messages", sent.Uri.AbsolutePath);
    }

    private static string EventBody(string type) => JsonSerializer.Serialize(new
    {
        op = 0, t = type, id = "event-id",
        d = new { id = "msg-1", content = "你好", group_openid = "group-openid", author = new { user_openid = "user-openid", member_openid = "member-openid" } }
    });

    private static OfficialBotController Controller(string body, OfficialBotEventQueue queue, bool signed = true, bool enabled = true)
    {
        var signature = new OfficialBotSignature(Options(enabled));
        var controller = new OfficialBotController(Options(enabled), signature, queue)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        if (signed)
        {
            controller.Request.Headers["X-Signature-Timestamp"] = "1725442341";
            controller.Request.Headers["X-Signature-Ed25519"] = signature.SignChallenge("1725442341", body);
        }
        return controller;
    }

    private static OfficialBotApi Api(FakeHttpHandler handler) => new(handler, Options(), NullLogger<OfficialBotApi>.Instance);

    private sealed class FakeHttpHandler : HttpMessageHandler, IHttpClientFactory
    {
        public int TokenCalls;
        public bool RejectFirst;
        public string SendBody = "{\"id\":\"sent-id\",\"timestamp\":\"2026-11-12T12:00:00+08:00\"}";
        public HttpStatusCode SendStatus = HttpStatusCode.OK;
        public string UploadBody = "{\"file_info\":\"uploaded-file\",\"ttl\":300}";
        public HttpStatusCode UploadStatus = HttpStatusCode.OK;
        public bool RejectFirstUpload;
        public ConcurrentQueue<(Uri Uri, string? Authorization, JsonElement Body)> Uploads = new();
        public ConcurrentQueue<(Uri Uri, string? Authorization, JsonElement Body)> Messages = new();
        public HttpClient CreateClient(string name) => new(this, false);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = JsonSerializer.Deserialize<JsonElement>(await request.Content!.ReadAsStringAsync(cancellationToken));
            if (request.RequestUri!.AbsolutePath == "/app/getAppAccessToken")
            {
                Assert.Equal("test-app", body.GetProperty("appId").GetString());
                Assert.True(body.TryGetProperty("clientSecret", out _));
                var count = Interlocked.Increment(ref TokenCalls);
                return Response($"{{\"access_token\":\"token-{count}\",\"expires_in\":\"7200\"}}");
            }
            if (request.RequestUri.Host == "ai.example")
            {
                if (request.RequestUri.AbsolutePath == "/v1/responses")
                    return Response("{\"id\":\"resp-id\",\"model\":\"test-model\",\"status\":\"completed\",\"output\":[{\"type\":\"message\",\"role\":\"assistant\",\"content\":[{\"type\":\"output_text\",\"text\":\"AI reply\"}]}]}");
                return request.RequestUri.AbsolutePath.EndsWith("completions")
                    ? Response("{\"id\":\"completion-id\",\"model\":\"test-model\",\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"AI reply\"}}]}")
                    : Response("{\"id\":\"reply-id\",\"type\":\"message\",\"stop_reason\":\"end_turn\",\"usage\":{\"input_tokens\":1,\"output_tokens\":2},\"model\":\"test-model\",\"role\":\"assistant\",\"content\":[{\"type\":\"text\",\"text\":\"AI reply\"}]}");
            }
            if (request.RequestUri.AbsolutePath.EndsWith("/files"))
            {
                Uploads.Enqueue((request.RequestUri, request.Headers.Authorization?.ToString(), body));
                return RejectFirstUpload && Uploads.Count == 1
                    ? Response("{}", HttpStatusCode.Unauthorized) : Response(UploadBody, UploadStatus);
            }
            Messages.Enqueue((request.RequestUri, request.Headers.Authorization?.ToString(), body));
            return RejectFirst && Messages.Count == 1 ? Response("{}", HttpStatusCode.Unauthorized) : Response(SendBody, SendStatus);
        }

        private static HttpResponseMessage Response(string json, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
