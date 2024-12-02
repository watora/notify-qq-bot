using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Notify.Domain.Models;
using Notify.Service.OneBot;
using SQLitePCL;

namespace notify.Controllers;

[ApiController]
[Route("api/onebot")]
public class OneBotController : ControllerBase
{
    private OneBotEvent oneBotEvent;
    private IConfiguration configuration;
    private string secret;
    private ILogger<OneBotController> logger;
    private IHostEnvironment hostEnvironment;

    public OneBotController(OneBotEvent oneBotEvent, IConfiguration configuration, ILogger<OneBotController> logger, IHostEnvironment hostEnvironment)
    {
        this.oneBotEvent = oneBotEvent;
        this.configuration = configuration;
        secret = configuration["ONEBOT_EVENT_SECRET"] ?? "";
        this.logger = logger;
        this.hostEnvironment = hostEnvironment;
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
            if ($"sha1={Encoding.UTF8.GetString(calcSign)}" != sign)
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
}
