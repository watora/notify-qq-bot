using System.Text.Json;
using Notify.Domain.Config;
using Notify.Domain.Models;
using Notify.Domain.Utils;
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
        if (config.Item1 == null || config.Item1.Count == 0)
        {
            logger.LogInformation($"chat config not exist, targetId:{targetId} targetType:{onebotEvent.MessageType}");
            return null;
        }
        return config.Item1[0];
    }

    /// <summary>
    /// onebot事件处理入口
    /// </summary>
    /// <param name="onebotEvent"></param>
    /// <returns></returns>
    public async Task HandleEvent(string rawBody)
    {
        var eventBase = JsonSerializer.Deserialize<OneBotEventBase>(rawBody);
        if (eventBase == null)
        {
            return;
        }
        switch (eventBase.PostType)
        {
            case "message":
                var eventMessage = JsonSerializer.Deserialize<OneBotEventMessage>(rawBody);
                if (eventMessage != null && NeedReply(eventMessage))
                {
                    await HandleMessageEvent(eventMessage);
                }
                break;
        }
    }

    /// <summary>
    /// 是否需要回复
    /// </summary>
    /// <returns></returns>
    public bool NeedReply(OneBotEventMessage onebotEvent)
    {
        // 私聊
        if (onebotEvent.MessageType == Consts.MsgTargetTypePrivate)
        {
            return true;
        }
        // 群聊
        foreach (var item in onebotEvent.Message)
        {
            var at = item.Data.QQ;
            // 群聊只回复被@的消息
            if (item.Type == OneBotMessageType.CQAt.ToCustomString() && !string.IsNullOrEmpty(at))
            {
                if (onebotEvent.SelfId.ToString() == at)
                {
                    return true;
                }
            }
        }
        logger.LogInformation("no need to handle message");
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
            var completion = await chatBotOpenAI.ChatCompletion(chatConfig.TargetId!, input);
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
            Name = onebotEvent.Sender.UserId.ToString(),
            Content = new List<OpenAIChatInputMessageContent>()
        };
        foreach (var item in onebotEvent.Message)
        {
            if (item.Type == OneBotMessageType.Text.ToCustomString())
            {
                newMsg.Content.Add(new OpenAIChatInputMessageContent
                {
                    Type = "text",
                    Text = item.Data.Text!.Trim()
                });
            }
            else if (item.Type == OneBotMessageType.CQImage.ToCustomString())
            {
                newMsg.Content.Add(new OpenAIChatInputMessageContent
                {
                    Type = "image_url",
                    ImageUrl = new OpenAIChatInputContentImage
                    {
                        Url = item.Data.Url!
                    }
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
        // 对原来消息的引用
        obMsg.Items.Add(new OneBotMessageItem
        {
            Type = OneBotMessageType.CQReply.ToCustomString(),
            Data = new OneBotMessageItemData
            {
                Id = onebotEvent.MessageId.ToString()
            }
        });
        if (hostEnvironment.IsDevelopment())
        {
            obMsg.Items.Add(new OneBotMessageItem
            {
                Type = OneBotMessageType.Text.ToCustomString(),
                Data = new OneBotMessageItemData
                {
                    Text = $"🔹model:{chatCompletion.Model} contextLength:{chatCompletion.ContextLength}🔹\n"
                }
            });
        }
        // 回复的内容
        obMsg.Items.Add(new OneBotMessageItem
        {
            Type = OneBotMessageType.Text.ToCustomString(),
            Data = new OneBotMessageItemData
            {
                Text = choice.Message.Content,
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
            Data = new OneBotMessageItemData
            {
                Text = "处理中，请勿重复调用"
            }
        });
        return obMsg;
    }
}