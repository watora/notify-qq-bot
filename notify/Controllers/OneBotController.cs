using Microsoft.AspNetCore.Mvc;
using Notify.Domain.Models;
using Notify.Service.OneBot;

namespace notify.Controllers;

[ApiController]
[Route("api/onebot")]
public class OneBotController : ControllerBase
{
    private OneBotEvent oneBotEvent;
    private IConfiguration configuration;
    private string authToken;
    private ILogger<OneBotController> logger;
    private IHostEnvironment hostEnvironment;

    public OneBotController(OneBotEvent oneBotEvent, IConfiguration configuration, ILogger<OneBotController> logger, IHostEnvironment hostEnvironment)
    {
        this.oneBotEvent = oneBotEvent;
        this.configuration = configuration;
        var token = configuration["ONEBOT_EVENT_TOKEN"];
        authToken = $"Bearer {token}";
        this.logger = logger;
        this.hostEnvironment = hostEnvironment;
    }

    [HttpPost("event")]
    public async Task<ActionResult> HandleEvent([FromBody] OneBotEventBase eventBase)
    {
        //check token
        var auth = HttpContext.Request.Headers.Authorization.FirstOrDefault();
        if (!hostEnvironment.IsDevelopment() && auth != authToken)
        {
            logger.LogInformation($"unauthorized one bot message, auth:{auth}");
            return Unauthorized();
        }
        using (var reader = new StreamReader(HttpContext.Request.Body))
        {
            var postData = reader.ReadToEnd();
            try
            {
                await oneBotEvent.HandleEvent(eventBase, postData);
            }
            catch (Exception e)
            {
                logger.LogError(e, "handle one bot message err");
                return Problem();
            }
        }
        return Ok();
    }
}
