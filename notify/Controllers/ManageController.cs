using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Notify.Domain.Config;
using Notify.Service.Manager;
using Notify.Service.OneBot;
using Notify.Service.RSS;
using Notify.Domain.Models;
using Notify.Domain.Models.Request;
using Notify.Domain.Models.Response;

namespace notify.Controllers;

[ApiController]
[Authorize(Roles = Consts.RoleAdmin)]
[Route("api/manage")]
public class ManageController : ControllerBase
{
    private RSSNotifyYoutube rssNotifyYoutube;
    private OneBotApi oneBotApi;
    private RSSManager rssManager;
    private ChatBotManager chatBotManager;
    private ILogger<ManageController> logger;

    public ManageController(RSSNotifyYoutube rssNotifyYoutube, OneBotApi oneBotApi, RSSManager rssManager, ChatBotManager chatBotManager, ILogger<ManageController> logger)
    {
        this.rssNotifyYoutube = rssNotifyYoutube;
        this.oneBotApi = oneBotApi;
        this.rssManager = rssManager;
        this.logger = logger;
        this.chatBotManager = chatBotManager;
    }

    [AllowAnonymous]
    [HttpGet("health")]
    public ActionResult Health()
    {
        return Ok();
    }

    [AllowAnonymous]
    [HttpGet("test/youtube/live")]
    public async Task<ActionResult<OneBotMessage>> TestYoutube([FromQuery(Name = "subscribe_id")] string subscribeId)
    {
        var channel = rssNotifyYoutube.GetLiveInfo(subscribeId);
        if (channel == null)
        {
            return NoContent();
        }
        var msg = rssNotifyYoutube.BuildLiveOneBotMessage(channel);
        if (msg == null)
        {
            return NoContent();
        }
        await oneBotApi.SendMessage("153624354", Consts.MsgTargetTypeGroup, msg);
        return msg;
    }

#region rss_config
    [HttpGet("get-rss-config/{id}")]
    public async Task<ActionResult<CommonResponse<RSSConfigDO>>> GetRSSConfigById([FromRoute] long id)
    {
        try
        {
            var config = await rssManager.GetRSSConfigById(id);
            if (config == null)
            {
                return new CommonResponse<RSSConfigDO>(ErrorCodes.RSSConfigNotExist);
            }
            return new CommonResponse<RSSConfigDO>(config);
        }
        catch (Exception e)
        {
            logger.LogError(e, "failed to get rss config");
            return new CommonResponse<RSSConfigDO>(ErrorCodes.GetRSSConfigError);
        }
    }

    [HttpGet("get-rss-config")]
    public async Task<ActionResult<CommonResponse<GetRSSConfigResponse>>> GetRSSConfig([FromQuery] int page, [FromQuery] int size,
    [FromQuery(Name = "subscribe_channel")] string subscribeChannel, [FromQuery] bool? active)
    {
        try
        {
            (var configs, var total) = await rssManager.GetRSSConfig(page, size, subscribeChannel, active);
            return new CommonResponse<GetRSSConfigResponse>(new GetRSSConfigResponse { RSSConfigs = configs, HasNext = page * size < total, TotalCount = total });
        }
        catch (Exception e)
        {
            logger.LogError(e, "failed to get rss config");
            return new CommonResponse<GetRSSConfigResponse>(ErrorCodes.GetRSSConfigError);
        }
    }

    [HttpPost("save-rss-config")]
    public async Task<ActionResult<CommonResponse>> SaveRSSConfig([FromBody] SaveRSSConfigRequest request)
    {
        try
        {
            foreach (var config in request.Configs.Where(r => !r.Id.HasValue))
            {
                config.Creator = HttpContext.User.Identity!.Name!;
            }
            await rssManager.SaveRSSConfig(request.Configs);
            return new CommonResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "failed to save rss config");
            return new CommonResponse(ErrorCodes.SaveRSSConfigError);
        }
    }

    [HttpPost("set-rss-config-status")]
    public async Task<CommonResponse> SetRSSConfig([FromBody] SetConfigStatusRequest request)
    {
        try
        {
            await rssManager.SetRSSConfigStatus(request.Id, request.IsActive);
            return new CommonResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "failed to save rss config");
            return new CommonResponse(ErrorCodes.SaveRSSConfigError);
        }
    }
#endregion

#region chat_config
    [HttpGet("get-chat-config/{id}")]
    public async Task<ActionResult<CommonResponse<ChatConfigDO>>> GetChatConfigById([FromRoute] long id)
    {
        try
        {
            var config = await chatBotManager.GetChatConfigById(id);
            if (config == null)
            {
                return new CommonResponse<ChatConfigDO>(ErrorCodes.ChatConfigNotExist);
            }
            return new CommonResponse<ChatConfigDO>(config);
        }
        catch (Exception e)
        {
            logger.LogError(e, "failed to get chat config");
            return new CommonResponse<ChatConfigDO>(ErrorCodes.GetChatConfigError);
        }
    }

    [HttpGet("get-chat-config")]
    public async Task<ActionResult<CommonResponse<GetChatConfigResponse>>> GetChatConfig([FromQuery] int page, [FromQuery] int size,
    [FromQuery(Name = "target_id")] string? targetId, [FromQuery] bool? active)
    {
        try
        {
            (var configs, var total) = await chatBotManager.GetChatConfig(targetId, null, active, page, size);
            return new CommonResponse<GetChatConfigResponse>(new GetChatConfigResponse { ChatConfigs = configs, HasNext = page * size < total, TotalCount = total });
        }
        catch (Exception e)
        {
            logger.LogError(e, "failed to get rss config");
            return new CommonResponse<GetChatConfigResponse>(ErrorCodes.GetChatConfigError);
        }
    }

    [HttpPost("save-chat-config")]
    public async Task<ActionResult<CommonResponse>> SaveChatConfig([FromBody] SaveChatConfigRequest request)
    {
        try
        {
            foreach (var config in request.Configs.Where(r => !r.Id.HasValue))
            {
                config.Creator = HttpContext.User.Identity!.Name!;
            }
            await chatBotManager.SaveChatConfig(request.Configs);
            return new CommonResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "failed to save chat config");
            return new CommonResponse(ErrorCodes.SaveRSSConfigError);
        }
    }

    [HttpPost("set-chat-config-status")]
    public async Task<CommonResponse> SetChatConfig([FromBody] SetConfigStatusRequest request)
    {
        try
        {
            await chatBotManager.SetChatConfigStatus(request.Id, request.IsActive);
            return new CommonResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "failed to save chat config");
            return new CommonResponse(ErrorCodes.SaveChatConfigError);
        }
    }
#endregion
}
