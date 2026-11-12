using Microsoft.Extensions.Options;
using Notify.Domain.Config;
using Notify.Domain.Config.Options;
using Notify.Domain.Models;
using Notify.Service.ChatBot;
using Notify.Service.Manager;

namespace Notify.Service.OfficialBot;

public class OfficialBotEvent
{
    private readonly IServiceProvider services;
    private readonly OfficialBotApi api;
    private readonly ILogger<OfficialBotEvent> logger;

    public OfficialBotEvent(IServiceProvider services, OfficialBotApi api, ILogger<OfficialBotEvent> logger)
    {
        this.services = services;
        this.api = api;
        this.logger = logger;
    }

    public async Task HandleEvent(OfficialBotEventMessage message, CancellationToken cancellationToken = default)
    {
        if (!OfficialBotEventMessage.IsMessageEvent(message.EventType) || message.Author?.Bot == true)
            return;

        logger.LogInformation("Receive official bot {EventType}: target {TargetId}, message {MessageId}",
            message.EventType, message.TargetId, message.Id);
        var (configs, _) = await services.GetRequiredService<ChatBotManager>()
            .GetChatConfig(message.TargetId, message.TargetType, true, 1, 1);
        var config = configs.FirstOrDefault();
        if (config == null)
            return;

        var content = message.Content?.Trim();
        string? reply;
        if (string.IsNullOrWhiteSpace(content))
        {
            // Attachments remain available in the event model; this chat adapter handles text.
            reply = "目前官方机器人聊天支持文本消息，请发送文字内容。";
        }
        else
        {
            var appId = services.GetRequiredService<IOptions<OfficialBotOption>>().Value.AppId;
            var historyKey = $"officialbot_{appId}_{message.TargetType}_{message.TargetId}";
            try
            {
                reply = await CreateReply(config, historyKey, content, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (LockTimeoutException)
            {
                reply = "处理中，请勿重复提交";
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Official bot chat failed: {MessageId}", message.Id);
                reply = "处理异常，请检查日志";
            }
        }
        if (reply != null && !await api.SendMessage(message.TargetId, message.TargetType,
                new OfficialBotMessage { Content = reply, MsgId = message.Id, MsgSeq = 1 }, cancellationToken))
            throw new InvalidOperationException("Official bot reply was rejected; see API log for status and code.");
    }

    private async Task<string?> CreateReply(ChatConfigDO config, string historyKey, string content, CancellationToken cancellationToken)
    {
        if (config.Provider == Consts.ChatProviderOpenAIResponses)
        {
            var result = await services.GetRequiredService<ChatBotOpenAIResponses>()
                .Chat(historyKey, OpenAIResponseInput.FromText(config.Model!, content), cancellationToken);
            return result != null ? $"{result.Model ?? config.Model}\n{result.GetReplyText()}" : "处理异常，请检查日志";
        }
        if (config.Provider == Consts.ChatProviderOpenAI)
        {
            var result = await services.GetRequiredService<ChatBotOpenAI>().ChatCompletion(historyKey, new OpenAIChatInput
            {
                Model = config.Model!,
                Messages = new List<OpenAIChatInputMessage>
                {
                    new() { Role = "user", Content = new List<OpenAIChatInputMessageContent> { new() { Type = "text", Text = content } } }
                }
            });
            return result?.Choices.Count > 0 ? $"{result.Model}\n{result.Choices[0].Message.Content}" : "处理异常，请检查日志";
        }
        if (config.Provider == Consts.ChatProviderAnthropic)
        {
            var result = await services.GetRequiredService<ChatBotAnthropic>().Chat(historyKey, new AnthropicChatInput
            {
                Model = config.Model!,
                MaxTokens = 2048,
                Messages = new List<AnthropicChatInputMessage>
                {
                    new() { Role = "user", Content = new List<AnthropicChatInputMessageContent> { new() { Type = "text", Text = content } } }
                }
            });
            return result?.Content.Count > 0
                ? $"{result.Model}\n" + string.Join("\n", result.Content.Where(x => x.Type == "text").Select(x => x.Text))
                : "处理异常，请检查日志";
        }
        return null;
    }
}
