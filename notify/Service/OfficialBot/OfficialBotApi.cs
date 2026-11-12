using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Notify.Domain.Config;
using Notify.Domain.Config.Options;
using Notify.Domain.Models;

namespace Notify.Service.OfficialBot;

public class OfficialBotApi : IDisposable
{
    private readonly IHttpClientFactory clientFactory;
    private readonly OfficialBotOption options;
    private readonly ILogger<OfficialBotApi> logger;
    private readonly SemaphoreSlim tokenLock = new(1, 1);
    private string? accessToken;
    private DateTimeOffset refreshAt;

    public OfficialBotApi(IHttpClientFactory clientFactory, IOptions<OfficialBotOption> options, ILogger<OfficialBotApi> logger)
    {
        this.clientFactory = clientFactory;
        this.options = options.Value;
        this.logger = logger;
    }

    public Task<bool> SendPrivateMessage(string userOpenId, OfficialBotMessage message, CancellationToken cancellationToken = default) =>
        SendMessage(userOpenId, Consts.MsgTargetTypePrivate, message, cancellationToken);

    public Task<bool> SendGroupMessage(string groupOpenId, OfficialBotMessage message, CancellationToken cancellationToken = default) =>
        SendMessage(groupOpenId, Consts.MsgTargetTypeGroup, message, cancellationToken);

    /// <summary>不传 MsgId/EventId 为主动消息；多次回复同一消息时由调用方递增 MsgSeq。</summary>
    public async Task<bool> SendMessage(string id, string messageType, OfficialBotMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(message);
        var targetPath = GetTargetPath(id, messageType);
        if (message.MsgSeq < 1)
            throw new ArgumentOutOfRangeException(nameof(message), "MsgSeq must be positive.");
        if (!string.IsNullOrEmpty(message.MsgId) && !string.IsNullOrEmpty(message.EventId))
            throw new ArgumentException("MsgId and EventId are mutually exclusive.", nameof(message));
        if (message.IsWakeup == true && (messageType != Consts.MsgTargetTypePrivate || !string.IsNullOrEmpty(message.MsgId) || !string.IsNullOrEmpty(message.EventId)))
            throw new ArgumentException("Wakeup messages are private and cannot carry MsgId/EventId.", nameof(message));
        if (message.MsgType == 0 && string.IsNullOrWhiteSpace(message.Content))
            throw new ArgumentException("Text messages require content.", nameof(message));
        if (message.MsgType == 2 && (message.Markdown == null || !string.IsNullOrEmpty(message.Content)))
            throw new ArgumentException("Markdown messages require markdown and empty content.", nameof(message));
        if (message.MsgType == 7 && string.IsNullOrWhiteSpace(message.Media?.FileInfo))
            throw new ArgumentException("Media messages require file_info from the matching upload API.", nameof(message));

        var result = await Post($"{targetPath}/messages", message, cancellationToken);
        return !string.IsNullOrWhiteSpace(result?.Id);
    }

    /// <summary>由 QQ 下载公网图片并返回 file_info；上传本身不发送消息。</summary>
    public async Task<string?> UploadImage(string id, string messageType, string url, CancellationToken cancellationToken = default)
    {
        var targetPath = GetTargetPath(id, messageType);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
            throw new ArgumentException("Image URL must be an absolute HTTP or HTTPS URL.", nameof(url));
        var result = await Post($"{targetPath}/files", new { file_type = 1, url, srv_send_msg = false }, cancellationToken);
        return string.IsNullOrWhiteSpace(result?.FileInfo) ? null : result.FileInfo;
    }

    private static string GetTargetPath(string id, string messageType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var resource = messageType switch
        {
            Consts.MsgTargetTypePrivate => "users",
            Consts.MsgTargetTypeGroup => "groups",
            _ => throw new ArgumentException("Message type must be private or group.", nameof(messageType))
        };
        return $"v2/{resource}/{Uri.EscapeDataString(id)}";
    }

    private async Task<ApiResponse?> Post(string path, object payload, CancellationToken cancellationToken)
    {
        if (!options.Enabled)
        {
            logger.LogWarning("Official bot is disabled; request {Path} was not sent", path);
            return null;
        }

        using var client = clientFactory.CreateClient();
        var token = await GetAccessToken(cancellationToken);
        // Only retry an explicit authentication rejection; a timeout may already have sent a message.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"{options.ApiEndpoint.TrimEnd('/')}/{path}");
            request.Headers.Authorization = new AuthenticationHeaderValue("QQBot", token);
            request.Content = JsonContent.Create(payload);
            using var response = await client.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
            {
                token = await GetAccessToken(cancellationToken, token);
                continue;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            try
            {
                var result = JsonSerializer.Deserialize<ApiResponse>(body);
                if (response.IsSuccessStatusCode && result != null && result.Code == 0 && result.ErrorCode == 0
                    && (path.EndsWith("/files", StringComparison.Ordinal)
                        ? !string.IsNullOrWhiteSpace(result.FileInfo) : !string.IsNullOrWhiteSpace(result.Id)))
                    return result;
                logger.LogWarning("Official bot request {Path} failed: HTTP {Status}, code {Code}, trace {Trace}",
                    path, (int)response.StatusCode, result?.ErrorCode is > 0 ? result.ErrorCode : result?.Code, result?.TraceId);
            }
            catch (JsonException)
            {
                logger.LogWarning("Official bot returned an invalid response for {Path}: HTTP {Status}", path, (int)response.StatusCode);
            }
            return null;
        }
        return null;
    }

    private async Task<string> GetAccessToken(CancellationToken cancellationToken, string? rejectedToken = null)
    {
        await tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (accessToken != null && DateTimeOffset.UtcNow < refreshAt && accessToken != rejectedToken)
                return accessToken;

            using var client = clientFactory.CreateClient();
            var requestedAt = DateTimeOffset.UtcNow;
            using var response = await client.PostAsJsonAsync(options.TokenEndpoint,
                new { appId = options.AppId, clientSecret = options.AppSecret }, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Official bot token request failed: HTTP {(int)response.StatusCode}.");
            var result = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
            if (string.IsNullOrWhiteSpace(result?.AccessToken) || result.ExpiresIn <= 0)
                throw new InvalidOperationException("Official bot token response is missing a valid token or expiry.");
            accessToken = result.AccessToken;
            refreshAt = requestedAt.AddSeconds(result.ExpiresIn - Math.Min(60, result.ExpiresIn / 2.0));
            return accessToken;
        }
        finally
        {
            tokenLock.Release();
        }
    }

    public void Dispose() => tokenLock.Dispose();

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }
        [JsonPropertyName("expires_in")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int ExpiresIn { get; set; }
    }

    private sealed class ApiResponse
    {
        [JsonPropertyName("file_info")]
        public string? FileInfo { get; set; }
        [JsonPropertyName("id")]
        public string? Id { get; set; }
        [JsonPropertyName("code")]
        public int Code { get; set; }
        [JsonPropertyName("err_code")]
        public int ErrorCode { get; set; }
        [JsonPropertyName("trace_id")]
        public string? TraceId { get; set; }
    }
}
