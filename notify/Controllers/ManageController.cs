using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Notify.Domain.Config;
using Notify.Service.Manager;
using Notify.Service.OneBot;
using Notify.Service.RSS;
using Notify.Domain.Models;
using Notify.Domain.Models.Request;
using Notify.Domain.Models.Response;
using Notify.Repository.Entity;

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
    private EntityConfigManager entityConfigManager;
    private ILogger<ManageController> logger;

    public ManageController(IServiceProvider sp, ILogger<ManageController> logger)
    {

        rssNotifyYoutube = sp.GetRequiredService<RSSNotifyYoutube>();
        oneBotApi = sp.GetRequiredService<OneBotApi>();
        rssManager = sp.GetRequiredService<RSSManager>();
        this.logger = logger;
        chatBotManager = sp.GetRequiredService<ChatBotManager>();
        entityConfigManager = sp.GetRequiredService<EntityConfigManager>();
    }

    [AllowAnonymous]
    [HttpGet("health")]
    public ActionResult Health()
    {
        return Ok();
    }

    #region entity_config
    [HttpGet("get-entity-config")]
    public async Task<ActionResult<CommonResponse<Page<EntityConfigDO>>>> GetEntityConfig([FromQuery] int page, [FromQuery] int size, [FromQuery] string? key)
    {
        try
        {
            (var configs, var total) = await entityConfigManager.GetEntityConfig(page, size, key);
            return new CommonResponse<Page<EntityConfigDO>>(new Page<EntityConfigDO>(configs, page * size < total, total));
        }
        catch (Exception e)
        {
            logger.LogError(e, "failed to get entity config");
            return new CommonResponse<Page<EntityConfigDO>>(ErrorCodes.GetEntityConfigError);
        }
    }

    [HttpPost("save-entity-config")]
    public async Task<ActionResult<CommonResponse>> SaveEntityConfig([FromBody] SaveConfigsRequest<EntityConfigDO> request)
    {
        var creator = HttpContext.User.Identity!.Name!;
        try
        {
            await entityConfigManager.SaveEntityConfig(request.Configs, creator);
            return new CommonResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "failed to save rss config");
            return new CommonResponse(ErrorCodes.SaveEntityConfigError);
        }
    }

    [HttpPost("del-entity-config")]
    public async Task<CommonResponse> DelEntityConfig([FromBody] DelEntityConfigRequest request)
    {
        try
        {
            await entityConfigManager.DeleteEntityConfig(request.Key);
            return new CommonResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "failed to delete entity config");
            return new CommonResponse(ErrorCodes.SaveEntityConfigError);
        }
    }
    #endregion

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
    public async Task<ActionResult<CommonResponse>> SaveRSSConfig([FromBody] SaveConfigsRequest<RSSConfigDO> request)
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
    public async Task<ActionResult<CommonResponse>> SaveChatConfig([FromBody] SaveConfigsRequest<ChatConfigDO> request)
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
