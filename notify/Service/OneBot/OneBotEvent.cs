using System.Text;
using System.Text.Json;
using Notify.Domain.Config;
using Notify.Domain.Models;
using Notify.Domain.Utils;
using Notify.Service.ChatBot;
using Notify.Service.Manager;
using Notify.Utils;
using SixLabors.ImageSharp;

namespace Notify.Service.OneBot;

public class OneBotEvent
{
    private ChatBotOpenAI chatBotOpenAI;
    private ChatBotAnthropic chatBotAnthropic;
    private ILogger<OneBotEvent> logger;
    private OneBotApi oneBotApi;
    private ChatBotManager chatBotManager;
    private IHostEnvironment hostEnvironment;
    private HttpClient httpClient;

    public OneBotEvent(IServiceProvider provider, ILogger<OneBotEvent> logger, IHostEnvironment hostEnvironment)
    {
        chatBotOpenAI = provider.GetRequiredService<ChatBotOpenAI>();
        chatBotAnthropic = provider.GetRequiredService<ChatBotAnthropic>();
        this.logger = logger;
        oneBotApi = provider.GetRequiredService<OneBotApi>();
        chatBotManager = provider.GetRequiredService<ChatBotManager>();
        this.hostEnvironment = hostEnvironment;
        httpClient = provider.GetRequiredService<HttpClient>();
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
    /// 展开消息里引用的内容 不支持嵌套引用
    /// </summary>
    /// <returns></returns>
    private async Task<bool> ExpandReferenceMessage(OneBotEventMessage onebotEvent)
    {
        var expanded = new List<OneBotMessageItem>();
        foreach (var item in onebotEvent.Message)
        {
            if (item.Type == OneBotMessageType.CQReply.ToCustomString())
            {
                if (!string.IsNullOrEmpty(item.Data.Id))
                {
                    var reference = await oneBotApi.GetMessageById(item.Data.Id);
                    if (reference != null)
                    {
                        var nested = reference.Message.Any(r => r.Type == OneBotMessageType.CQReply.ToCustomString());
                        if (nested)
                        {
                            return true;
                        }
                        expanded.AddRange(reference.Message);
                    }
                }
            }
            else
            {
                expanded.Add(item);
            }
        }
        onebotEvent.Message = expanded;
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
        var nested = await ExpandReferenceMessage(onebotEvent);
        if (nested)
        {
            msg = TextMsg(onebotEvent, "不支持包含嵌套引用的消息");
        }
        else
        {
            if (chatConfig.Provider == Consts.ChatProviderOpenAI)
            {
                msg = await HandleOpenAIChatMessage(onebotEvent, chatConfig);
            }
            else if (chatConfig.Provider == Consts.ChatProviderAnthropic)
            {
                msg = await HandleAnthropicChatMessage(onebotEvent, chatConfig);
            }
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
    /// 处理OpenAI聊天消息
    /// </summary>
    private async Task<OneBotMessage?> HandleOpenAIChatMessage(OneBotEventMessage onebotEvent, ChatConfigDO chatConfig)
    {
        var input = new OpenAIChatInput
        {
            Model = chatConfig.Model!,
            Messages = new List<OpenAIChatInputMessage>() { await ConvertToOpenAIChatInputMessage(onebotEvent) },
        };
        try
        {
            var completion = await chatBotOpenAI.ChatCompletion($"{chatConfig.TargetType}_{chatConfig.TargetId}", input);
            if (completion != null)
            {
                return ConvertOpenAIChatCompletionToOneBotMessage(onebotEvent, completion);
            }
            else
            {
                return TextMsg(onebotEvent, "处理异常，请检查日志");
            }
        }
        catch (LockTimeoutException)
        {
            return TextMsg(onebotEvent, "处理中，请勿重复提交");
        }
    }

    /// <summary>
    /// 处理Anthropic聊天消息
    /// </summary>
    private async Task<OneBotMessage?> HandleAnthropicChatMessage(OneBotEventMessage onebotEvent, ChatConfigDO chatConfig)
    {
        var input = new AnthropicChatInput
        {
            Model = chatConfig.Model!,
            Messages = new List<AnthropicChatInputMessage>() { await ConvertToAnthropicChatInputMessage(onebotEvent) },
            MaxTokens = 2048,
        };
        try
        {
            var chatResponse = await chatBotAnthropic.Chat($"{chatConfig.TargetType}_{chatConfig.TargetId}", input);
            if (chatResponse != null)
            {
                return ConvertOpenAIChatCompletionToOneBotMessage(onebotEvent, chatResponse);
            }
            else
            {
                return TextMsg(onebotEvent, "处理异常，请检查日志");
            }
        }
        catch (LockTimeoutException)
        {
            return TextMsg(onebotEvent, "处理中，请勿重复提交");
        }
    }


    /// <summary>
    /// 转换onebot事件到anthropic的输入
    /// </summary>
    /// <param name="onebotEvent"></param>
    /// <returns></returns>
    private async Task<AnthropicChatInputMessage> ConvertToAnthropicChatInputMessage(OneBotEventMessage onebotEvent)
    {
        var newMsg = new AnthropicChatInputMessage
        {
            Role = "user",
            Content = new List<AnthropicChatInputMessageContent>()
        };
        foreach (var item in onebotEvent.Message)
        {
            if (item.Type == OneBotMessageType.Text.ToCustomString())
            {
                newMsg.Content.Add(new AnthropicChatInputMessageContent
                {
                    Type = "text",
                    Text = item.Data.Text!.Trim()
                });
            }
            else if (item.Type == OneBotMessageType.CQImage.ToCustomString())
            {
                var file = await oneBotApi.GetImage(item.Data.File!);
                if (file == null)
                {
                    continue;
                }
                var mediaType = ImageHelper.GetMediaType(Convert.FromBase64String(file.Base64));
                if (!string.IsNullOrEmpty(mediaType))
                {
                    newMsg.Content.Add(new AnthropicChatInputMessageContent
                    {
                        Type = "image",
                        Source = new AnthropicChatInputContentSource
                        {
                            Type = "base64",
                            MediaType = mediaType,
                            Data = file.Base64
                        }
                    });
                }
            }
        }
        return newMsg;
    }

    /// <summary>
    /// 转换onebot事件到openai的输入
    /// </summary>
    /// <param name="onebotEvent"></param>
    /// <returns></returns>
    private async Task<OpenAIChatInputMessage> ConvertToOpenAIChatInputMessage(OneBotEventMessage onebotEvent)
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
                var file = await oneBotApi.GetImage(item.Data.File!);
                if (file == null)
                {
                    continue;
                }
                var mediaType = ImageHelper.GetMediaType(Convert.FromBase64String(file.Base64));
                if (!string.IsNullOrEmpty(mediaType))
                {
                    newMsg.Content.Add(new OpenAIChatInputMessageContent
                    {
                        Type = "image_url",
                        ImageUrl = new OpenAIChatInputContentImage
                        {
                            Url = $"data:{mediaType};base64,{file.Base64}"
                        }
                    });
                }
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
        var content = $"{chatCompletion.Model}\n" + chatCompletion.Choices[0].Message.Content;
        return TextMsg(onebotEvent, content);
    }

    /// <summary>
    /// 转换anthropic的结果到onebot消息
    /// </summary>
    /// <returns></returns>
    public OneBotMessage? ConvertOpenAIChatCompletionToOneBotMessage(OneBotEventMessage onebotEvent, AnthropicChatResponse chatResponse)
    {
        if (chatResponse.Content.Count == 0)
        {
            return null;
        }
        var messages = new List<string>
        {
            $"{chatResponse.Model}\n"
        };
        messages.AddRange(chatResponse.Content.Where(r => r.Type == "text").Select(r => r.Text!));
        return TextMsg(onebotEvent, messages.ToArray());
    }

    /// <summary>
    /// 组装文本消息
    /// </summary>
    /// <param name="onebotEvent"></param>
    /// <param name="messages"></param>
    /// <returns></returns>
    public OneBotMessage? TextMsg(OneBotEventMessage onebotEvent, params string[] messages)
    {
        var obMsg = new OneBotMessage();
        obMsg.Items.Add(new OneBotMessageItem
        {
            Type = OneBotMessageType.CQReply.ToCustomString(),
            Data = new OneBotMessageItemData
            {
                Id = onebotEvent.MessageId.ToString()
            }
        });
        foreach (var msg in messages)
        {
            obMsg.Items.Add(new OneBotMessageItem
            {
                Type = OneBotMessageType.Text.ToCustomString(),
                Data = new OneBotMessageItemData
                {
                    Text = msg
                }
            });
        }
        return obMsg;
    }
}