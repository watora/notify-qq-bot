using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Notify.Domain.Config.Options;
using Notify.Service.OneBot;
using Notify.Service.RSS;

namespace notify.Controllers;

[ApiController]
[Route("api/onebot")]
public class OneBotController : ControllerBase
{
    private OneBotEvent oneBotEvent;
    private string secret;
    private ILogger<OneBotController> logger;

    public OneBotController(OneBotEvent oneBotEvent, IOptionsSnapshot<OneBotOption> onebotOption, ILogger<OneBotController> logger)
    {
        this.oneBotEvent = oneBotEvent;
        secret = onebotOption.Value.PostSecret ?? "";
        this.logger = logger;
    }

    [HttpPost("event")]
    public async Task<ActionResult> HandleEvent()
    {
        using var reader = new StreamReader(HttpContext.Request.Body);
        var rawBody = await reader.ReadToEndAsync();
        logger.LogInformation($"receive one bot event, body:{rawBody}");
        if (!string.IsNullOrEmpty(secret))
        {
            var sign = HttpContext.Request.Headers["X-Signature"];
            if (string.IsNullOrEmpty(sign))
            {
                logger.LogInformation($"unauthorized one bot event");
                return Unauthorized();
            }
            var secretBytes = Encoding.UTF8.GetBytes(secret);
            var hmacsha1 = new HMACSHA1(secretBytes);
            var calcSign = hmacsha1.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
            if (!string.Equals($"sha1={Convert.ToHexString(calcSign)}", sign, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation($"one bot event invalid sign:{sign}");
                return Unauthorized();
            }
        }
        try
        {
            await oneBotEvent.HandleEvent(rawBody);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "handle one bot message err");
            return Problem();
        }
        return Ok();
    }

    [HttpGet("test")]
    public async Task<ActionResult> Test([FromServices] RSSNotifyCopymanga copymanga) 
    {
        await copymanga.CheckMangaUpdateAndSendMessage(true);
        return Ok();
    }
}
