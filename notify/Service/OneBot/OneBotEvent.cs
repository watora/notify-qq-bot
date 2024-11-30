using System.Net;
using System.Text.Json;
using AntDesign;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.EntityFrameworkCore;
using Notify.Domain.Config;
using Notify.Domain.Models;
using Notify.Domain.Utils;
using Notify.Repository;
using Notify.Repository.Entity;
using Notify.Service.ChatBot;
using Notify.Service.Manager;

namespace Notify.Service.OneBot;

public class OneBotEvent
{
    private ChatBotOpenAI chatBotOpenAI;
    private ILogger<OneBotEvent> logger;
    private OneBotApi oneBotApi;
    private ChatBotManager chatBotManager;
    private IHostEnvironment hostEnvironment;

    public OneBotEvent(ChatBotOpenAI chatBotOpenAI, OneBotApi oneBotApi, ILogger<OneBotEvent> logger, ChatBotManager chatBotManager, IHostEnvironment hostEnvironment)
    {
        this.chatBotOpenAI = chatBotOpenAI;
        this.logger = logger;
        this.oneBotApi = oneBotApi;
        this.chatBotManager = chatBotManager;
        this.hostEnvironment = hostEnvironment;
    }

    /// <summary>
    /// 获取聊天相关配置
    /// </summary>
    /// <param name="onebotEvent"></param>
    /// <returns></returns>
    private async Task<ChatConfigDO?> GetChatConfig(OneBotEventMessage onebotEvent)
    {
        var targetId = onebotEvent.GroupId;
        if (onebotEvent.MessageType == Consts.MsgTargetTypePrivate)
        {
            targetId = onebotEvent.UserId;
        }
        var config = await chatBotManager.GetChatConfig(targetId.ToString(), onebotEvent.MessageType, true, 1, 1);
        if (config.Item1 == null || config.Item1.Count > 0)
        {
            logger.LogInformation($"chat config not exist, targetId:{targetId} targetType:{onebotEvent.MessageType}");
            return null;
        }
        return config.Item1[0];
    }

    /// <summary>
    /// onebot事件处理入口
    /// </summary>
    /// <param name="eventBase"></param>
    /// <param name="rawData"></param>
    /// <returns></returns>
    public async Task HandleEvent(OneBotEventBase eventBase, string rawData)
    {
        switch (eventBase.PostType)
        {
            case "message":
                var msg = JsonSerializer.Deserialize<OneBotEventMessage>(rawData);
                if (msg != null && await NeedReply(msg))
                {
                    await HandleMessageEvent(msg);
                }
                break;
        }
    }

    /// <summary>
    /// 是否需要回复
    /// </summary>
    /// <returns></returns>
    public async Task<bool> NeedReply(OneBotEventMessage onebotEvent)
    {
        // 私聊
        if (onebotEvent.MessageType == Consts.MsgTargetTypePrivate)
        {
            return onebotEvent.SubType == "friend";
        }
        foreach (var item in onebotEvent.Message.Items)
        {
            var at = item.Data[OneBotMessageDataKey.QQ.ToCustomString()];
            // 群聊只回复被@的消息
            if (item.Type == OneBotMessageType.CQAt.ToCustomString() && !string.IsNullOrEmpty(at))
            {
                var loginInfo = await oneBotApi.GetLoginInfo();
                if (loginInfo.Item1.ToString() == at)
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// 处理聊天事件
    /// </summary>
    /// <param name="onebotEvent"></param>
    /// <returns></returns>
    private async Task HandleMessageEvent(OneBotEventMessage onebotEvent)
    {
        var chatConfig = await GetChatConfig(onebotEvent);
        if (chatConfig == null)
        {
            return;
        }
        OneBotMessage? msg = null;
        if (chatConfig.Provider == Consts.ChatProviderOpenAI)
        {
            msg = await HandleOpenAIChatMessage(onebotEvent, chatConfig);
        }
        if (msg != null)
        {
            if (onebotEvent.MessageType == Consts.MsgTargetTypeGroup)
            {
                await oneBotApi.SendMessage(onebotEvent.GroupId.ToString(), Consts.MsgTargetTypeGroup, msg);
            }
            else
            {
                await oneBotApi.SendMessage(onebotEvent.UserId.ToString(), Consts.MsgTargetTypePrivate, msg);
            }
        }
    }

    /// <summary>
    /// 处理命令消息
    /// </summary>
    private async Task<OneBotMessage?> HandleOpenAIChatMessage(OneBotEventMessage onebotEvent, ChatConfigDO chatConfig)
    {
        var input = new OpenAIChatInput
        {
            Model = chatConfig.Model!,
            Messages = new List<OpenAIChatInputMessage>() { ConvertToOpenAIChatInputMessage(onebotEvent) },
        };
        try
        {
            var completion = await chatBotOpenAI.ChatCompletion(chatConfig.TargetId, input);
            if (completion != null)
            {
                return ConvertOpenAIChatCompletionToOneBotMessage(onebotEvent, completion);
            }
        }
        catch (LockTimeoutException)
        {
            return BusyMsg();
        }
        return null;
    }

    /// <summary>
    /// 转换onebot事件到openai的输入
    /// </summary>
    /// <param name="onebotEvent"></param>
    /// <returns></returns>
    private OpenAIChatInputMessage ConvertToOpenAIChatInputMessage(OneBotEventMessage onebotEvent)
    {
        var newMsg = new OpenAIChatInputMessage
        {
            Role = "user",
            Name = onebotEvent.Sender.NickName,
            Content = new List<OpenAIChatInputMessageContent>()
        };
        foreach (var item in onebotEvent.Message.Items)
        {
            if (item.Type == OneBotMessageType.Text.ToCustomString())
            {
                newMsg.Content.Add(new OpenAIChatInputMessageContent
                {
                    Type = "text",
                    Text = item.Data[OneBotMessageDataKey.Text.ToCustomString()]
                });
            }
            else if (item.Type == OneBotMessageType.CQImage.ToString())
            {
                newMsg.Content.Add(new OpenAIChatInputMessageContent
                {
                    Type = "image_url",
                    ImageUrl = item.Data[OneBotMessageDataKey.Url.ToCustomString()]
                });
            }
        }
        return newMsg;
    }

    /// <summary>
    /// 转换openai的结果到onebot消息
    /// </summary>
    /// <param name="chatCompletion"></param>
    /// <returns></returns>
    public OneBotMessage? ConvertOpenAIChatCompletionToOneBotMessage(OneBotEventMessage onebotEvent, OpenAIChatCompletion chatCompletion)
    {
        if (chatCompletion.Choices.Count == 0)
        {
            return null;
        }
        var choice = chatCompletion.Choices[0];
        var obMsg = new OneBotMessage();
        if (hostEnvironment.IsDevelopment())
        {
            obMsg.Items.Add(new OneBotMessageItem
            {
                Type = OneBotMessageType.Text.ToCustomString(),
                Data = new Dictionary<string, string> {
                    {OneBotMessageDataKey.Text.ToCustomString(), $"model:{chatCompletion.Model} contextLength:${chatCompletion.ContextLength}💠"},
                }
            });
        }
        // 对原来消息的引用
        obMsg.Items.Add(new OneBotMessageItem
        {
            Type = OneBotMessageType.CQReply.ToCustomString(),
            Data = new Dictionary<string, string> {
                {OneBotMessageDataKey.Id.ToCustomString(), onebotEvent.MessageId.ToString()},
            }
        });
        // 回复的内容
        obMsg.Items.Add(new OneBotMessageItem
        {
            Type = OneBotMessageType.Text.ToCustomString(),
            Data = new Dictionary<string, string> {
                {OneBotMessageDataKey.Text.ToCustomString(), choice.Message.Content},
            }
        });
        return obMsg;
    }

    public OneBotMessage? BusyMsg()
    {
        var obMsg = new OneBotMessage();
        obMsg.Items.Add(new OneBotMessageItem
        {
            Type = OneBotMessageType.Text.ToCustomString(),
            Data = new Dictionary<string, string> {
                {OneBotMessageDataKey.Text.ToCustomString(), "处理中，请勿重复调用"},
            }
        });
        return obMsg;
    }
}