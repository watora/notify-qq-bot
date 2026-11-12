using System.Text;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Notify.Domain.Config.Options;
using Notify.Domain.Models;
using Notify.Service.OfficialBot;

namespace notify.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/officialbot")]
public class OfficialBotController : ControllerBase
{
    private readonly OfficialBotOption options;
    private readonly OfficialBotSignature signature;
    private readonly OfficialBotEventQueue queue;
    private readonly ILogger<OfficialBotController> logger;

    public OfficialBotController(IOptions<OfficialBotOption> options, OfficialBotSignature signature, OfficialBotEventQueue queue,
        ILogger<OfficialBotController> logger)
    {
        this.options = options.Value;
        this.signature = signature;
        this.queue = queue;
        this.logger = logger;
    }

    [HttpPost("sendmessage")]
    [RequestSizeLimit(1024 * 1024)]
    public async Task<IActionResult> SendMessage([FromBody] OfficialBotSendRequest request,
        [FromServices] OfficialBotApi api, CancellationToken cancellationToken)
    {
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.SendSecret))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { success = false, message = "External sending is not enabled." });
        if (!AuthenticationHeaderValue.TryParse(Request.Headers.Authorization.ToString(), out var authorization)
            || !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrEmpty(authorization.Parameter)
            || !CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(authorization.Parameter)),
                SHA256.HashData(Encoding.UTF8.GetBytes(options.SendSecret))))
        {
            Response.Headers.WWWAuthenticate = "Bearer";
            return Unauthorized(new { success = false, message = "Invalid sending credential." });
        }

        if (string.IsNullOrWhiteSpace(request.TargetId) || request.MessageType is not ("private" or "group")
            || request.Message == null || request.Message.MsgType is not (0 or 2 or 7))
            return BadRequest(new { success = false, message = "Provide target_id, message_type (private/group), and message (msg_type 0/2/7)." });

        try
        {
            var mappings = request.MessageType == "private" ? options.PrivateTargetMappings : options.GroupTargetMappings;
            var targetId = mappings.TryGetValue(request.TargetId, out var mapped) ? mapped : request.TargetId;
            if (string.IsNullOrWhiteSpace(targetId))
                return BadRequest(new { success = false, message = "The target mapping must contain a non-empty OpenID." });
            var sent = await api.SendMessage(targetId, request.MessageType, request.Message, cancellationToken);
            return sent ? Ok(new { success = true })
                : StatusCode(StatusCodes.Status502BadGateway, new { success = false, message = "QQ rejected the message." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("External official bot send timed out");
            return StatusCode(StatusCodes.Status504GatewayTimeout, new { success = false, message = "QQ request timed out; delivery is uncertain." });
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or JsonException)
        {
            logger.LogError(ex, "External official bot send failed");
            return StatusCode(StatusCodes.Status502BadGateway, new { success = false, message = "QQ request failed." });
        }
    }

    [HttpPost("event")]
    [RequestSizeLimit(1024 * 1024)]
    public async Task<IActionResult> HandleEvent(CancellationToken cancellationToken)
    {
        if (!options.Enabled)
            return NotFound();
        if (string.IsNullOrWhiteSpace(options.AppId) || string.IsNullOrWhiteSpace(options.AppSecret))
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        var appId = Request.Headers["X-Bot-Appid"].ToString();
        if (!string.IsNullOrEmpty(appId) && appId != options.AppId)
            return Unauthorized();

        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, cancellationToken);
        var body = buffer.ToArray();
        try
        {
            var payload = JsonSerializer.Deserialize<OfficialBotPayload>(body);
            if (payload == null)
                return BadRequest();

            // The platform's initial URL validation has no signature headers.
            if (payload.Op == 13)
            {
                if (payload.Data.ValueKind != JsonValueKind.Object
                    || !payload.Data.TryGetProperty("plain_token", out var plainToken)
                    || plainToken.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(plainToken.GetString())
                    || !payload.Data.TryGetProperty("event_ts", out var eventTs)
                    || eventTs.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(eventTs.GetString()))
                    return BadRequest();
                return Ok(new { plain_token = plainToken.GetString(), signature = signature.SignChallenge(eventTs.GetString()!, plainToken.GetString()!) });
            }

            if (!signature.Verify(Request.Headers["X-Signature-Timestamp"].ToString(), body,
                    Request.Headers["X-Signature-Ed25519"].ToString()))
                return Unauthorized();
            if (payload.Op != 0)
                return BadRequest();

            logger.LogInformation("Receive official bot event: type {EventType}, id {EventId}, body {Body}",
                payload.Type, payload.Id, Encoding.UTF8.GetString(body));

            if (OfficialBotEventMessage.IsMessageEvent(payload.Type))
            {
                if (payload.Data.ValueKind != JsonValueKind.Object)
                    return BadRequest();
                var message = payload.Data.Deserialize<OfficialBotEventMessage>();
                if (message == null)
                    return BadRequest();
                message.EventType = payload.Type!;
                if (string.IsNullOrWhiteSpace(message.Id) || string.IsNullOrWhiteSpace(message.TargetId)
                    || message.Author == null || (message.TargetType == "group" && string.IsNullOrWhiteSpace(message.Author.MemberOpenId)))
                    return BadRequest();
                if (!queue.TryEnqueue(message))
                    return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            // ACK immediately; AI requests run in a hosted service with their own DI scope.
            return Ok(new { op = 12, d = 0 });
        }
        catch (JsonException)
        {
            return BadRequest();
        }
    }
}
