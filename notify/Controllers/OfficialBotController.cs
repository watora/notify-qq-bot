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

    public OfficialBotController(IOptions<OfficialBotOption> options, OfficialBotSignature signature, OfficialBotEventQueue queue)
    {
        this.options = options.Value;
        this.signature = signature;
        this.queue = queue;
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
