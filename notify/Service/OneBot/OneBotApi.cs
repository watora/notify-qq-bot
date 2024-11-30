using System.Text.Json;
using Notify.Domain.Config;
using Notify.Domain.Models;
using Notify.Domain.Utils;

namespace Notify.Service.OneBot;

public class OneBotApi
{
    private HttpClient httpClient;
    private string oneBotUrl;
    private ILogger<OneBotApi> logger;

    public OneBotApi(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<OneBotApi> logger)
    {
        this.httpClient = httpClientFactory.CreateClient();
        this.oneBotUrl = configuration["OneBot:Url"] ?? "";
        this.logger = logger;
    }

    /// <summary>
    /// 发送消息
    /// </summary>
    /// <param name="groupId"></param>
    /// <param name="userId"></param>
    /// <param name="messageType">消息类型</param>
    /// <param name="message"></param>
    /// <returns></returns>
    public async Task<bool> SendMessage(string Id, string messageType, OneBotMessage message)
    {
        var groupId = messageType == Consts.MsgTargetTypeGroup ? Id : "";
        var userId = messageType == Consts.MsgTargetTypePrivate ? Id : "";
        var req = new
        {
            message_type = messageType,
            group_id = groupId,
            user_id = userId,
            message = message.Items,
        };
        Console.WriteLine($"send_msg  req:{JsonSerializer.Serialize(req)}");
        var resp = await this.httpClient.PostAsJsonAsync($"{this.oneBotUrl}/send_msg", req);
        if (resp.IsSuccessStatusCode)
        {
            var def = new { retcode = 0 };
            var respObj = NotifyExtension.DeserializeAnonymousType(await resp.Content.ReadAsStringAsync(), def);
            return respObj.retcode == 0;
        }
        else
        {
            logger.LogWarning($"send_msg failed, code:{resp.StatusCode}");
        }
        return false;

    }

    /// <summary>
    /// QQ是否在线
    /// </summary>
    /// <returns></returns>
    public async Task<bool> IsOnline()
    {
        var resp = await httpClient.GetAsync($"{oneBotUrl}/get_status");
        if (resp.IsSuccessStatusCode)
        {
            var def = new { online = false, good = false };
            var respObj = NotifyExtension.DeserializeAnonymousType(await resp.Content.ReadAsStringAsync(), def);
            return respObj.good && respObj.online;
        }
        else
        {
            logger.LogWarning($"get_status failed, code:{resp.StatusCode}");
        }
        return false;
    }

    /// <summary>
    /// 获取当前登录信息
    /// </summary>
    /// <returns></returns>
    public async Task<(long, string)> GetLoginInfo()
    {
        var resp = await httpClient.GetAsync($"{oneBotUrl}/get_login_info");
        if (resp.IsSuccessStatusCode)
        {
            var def = new { user_id = 0L, nickname = "" };
            var respObj = NotifyExtension.DeserializeAnonymousType(await resp.Content.ReadAsStringAsync(), def);
            return (respObj.user_id, respObj.nickname);
        }
        else
        {
            logger.LogWarning($"get_login_info failed, code:{resp.StatusCode}");
        }
        return (0, "");
    }
}