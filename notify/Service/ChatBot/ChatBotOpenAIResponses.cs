using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Notify.Domain.Models;
using Notify.Utils;

namespace Notify.Service.ChatBot;

/// <summary>标准非流式 Responses API，使用本地缓存维护无状态多轮对话。</summary>
public class ChatBotOpenAIResponses : ChatBotBase
{
    private readonly IHttpClientFactory clientFactory;
    private readonly Uri responseEndpoint;
    private readonly string? token;
    private static readonly JsonSerializerOptions JsonOptions = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    private const int MaximumHistoryTurns = 25;

    public ChatBotOpenAIResponses(IServiceProvider sp, ILogger<ChatBotOpenAIResponses> logger) : base(sp, logger)
    {
        clientFactory = sp.GetRequiredService<IHttpClientFactory>();
        var options = chatBotOption.Value;
        var endpoint = options.OpenAIResponsesEndpoint ?? options.OpenAIEndpoint ?? "https://api.openai.com";
        endpoint = endpoint.TrimEnd('/');
        responseEndpoint = new Uri(endpoint.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? $"{endpoint}/responses" : $"{endpoint}/v1/responses", UriKind.Absolute);
        token = options.OpenAIResponsesToken ?? options.OpenAIToken;
    }

    public async Task<OpenAIResponse?> Chat(string uniqueKey, OpenAIResponseInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(uniqueKey) || input == null || string.IsNullOrWhiteSpace(input.Model) || input.Input.Count == 0)
            return null;
        if (input.MaxOutputTokens <= 0)
            throw new ArgumentOutOfRangeException(nameof(input), "MaxOutputTokens must be positive.");

        // Include endpoint, requested model and instructions; a returned model snapshot must not change the key.
        var keyData = JsonSerializer.Serialize(new[] { responseEndpoint.AbsoluteUri, input.Model, uniqueKey, input.Instructions });
        var key = "openai_responses:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(keyData)));
        return await Extension.Lock(key, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var history = memoryCache.Get<ConversationHistory>(key);
            // Start a fresh conversation rather than trimming the prefix of encrypted reasoning items.
            if (history?.Turns >= MaximumHistoryTurns)
                history = null;
            var items = history?.Items.ToList() ?? new List<JsonElement>();
            items.AddRange(input.Input.Select(item => JsonSerializer.SerializeToElement(item)));
            var payload = new
            {
                model = input.Model,
                input = items,
                instructions = input.Instructions,
                max_output_tokens = input.MaxOutputTokens,
                store = false,
                stream = false,
                include = new[] { "reasoning.encrypted_content" }
            };

            using var client = clientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, responseEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(payload, options: JsonOptions);
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Responses API failed: HTTP {Status}", (int)response.StatusCode);
                return null;
            }

            OpenAIResponse? result;
            try
            {
                result = await response.Content.ReadFromJsonAsync<OpenAIResponse>(cancellationToken);
            }
            catch (JsonException)
            {
                logger.LogWarning("Responses API returned invalid JSON");
                return null;
            }
            if (result == null || result.Status != "completed" || string.IsNullOrWhiteSpace(result.Id)
                || result.Error is { ValueKind: not JsonValueKind.Null } || string.IsNullOrWhiteSpace(result.GetReplyText()))
            {
                logger.LogWarning("Responses API returned no completed text response: {Status}, {ResponseId}", result?.Status, result?.Id);
                return null;
            }

            // Commit only successful turns; failed attempts must not contaminate the next request.
            items.AddRange(result.Output!.Select(item => item.Clone()));
            memoryCache.Set(key, new ConversationHistory(items, (history?.Turns ?? 0) + 1),
                new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromHours(12) });
            return result;
        });
    }

    private sealed record ConversationHistory(List<JsonElement> Items, int Turns);
}
