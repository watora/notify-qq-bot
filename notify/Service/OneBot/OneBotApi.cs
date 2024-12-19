using System.Text.Json;
using System.Web;
using Notify.Domain.Config;
using Notify.Domain.Models;
using Notify.Domain.Utils;
using Notify.Utils;
using SixLabors.ImageSharp;

namespace Notify.Service.OneBot;

public class OneBotApi
{
    private HttpClient httpClient;
    private string oneBotUrl;
    private ILogger<OneBotApi> logger;

    public OneBotApi(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<OneBotApi> logger)
    {
        httpClient = httpClientFactory.CreateClient();
        oneBotUrl = configuration["OneBot:Url"] ?? "";
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
    /// 根据消息id获取消息内容
    /// </summary>
    /// <returns></returns>
    public async Task<OneBotEventMessage?> GetMessageById(string messageId)
    {
        var resp = await httpClient.GetAsync($"{this.oneBotUrl}/get_msg?message_id={messageId}");
        if (resp.IsSuccessStatusCode)
        {
            var content = await resp.Content.ReadAsStringAsync();
            var respObj = JsonSerializer.Deserialize<OneBotResp<OneBotEventMessage>>(content);
            if (respObj == null || respObj.RetCode != 0)
            {
                logger.LogWarning($"get message error, code:{respObj?.RetCode}");
                return null;
            }
            return respObj.Data;
        }
        return null;
    }

    /// <summary>
    /// 获取图片
    /// </summary>
    /// <returns></returns>
    public async Task<OneBotImageData?> GetImage(string file)
    {
        var resp = await httpClient.GetAsync($"{this.oneBotUrl}/get_image?file={HttpUtility.UrlEncode(file)}");
        if (resp.IsSuccessStatusCode)
        {
            var content = await resp.Content.ReadAsStringAsync();
            var respObj = JsonSerializer.Deserialize<OneBotResp<OneBotImageData>>(content);
            if (respObj == null || respObj.RetCode != 0 || respObj.Data == null)
            {
                logger.LogWarning($"get image error, code:{respObj?.RetCode}");
                return null;
            }
            var image = await Image.LoadAsync(respObj.Data.File.Replace("/app/.config/QQ", "./napcat"));
            using var stream = new MemoryStream();
            image.SaveAsJpeg(stream);
            respObj.Data.Base64 = Convert.ToBase64String(stream.ToArray().AsSpan());
            respObj.Data.MimeType = "image/jpeg";
            return respObj.Data;
        }
        return null;
    }
}