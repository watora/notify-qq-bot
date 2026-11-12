using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Notify.Domain.Models;
using Notify.Service.ChatBot;

namespace Notify.Service.Hubs;

[Authorize(Roles = "Admin")]
public class PrivateHub : Hub
{
    private readonly IServiceProvider services;
    private ILogger<PrivateHub> logger;

    public PrivateHub(IServiceProvider services, ILogger<PrivateHub> logger)
    {
        this.services = services;
        this.logger = logger;
    }

    public async Task ChatToBot(string model, string message)
    {
        var identity = this.Context.User?.Identity;
        if (identity == null || !identity.IsAuthenticated || string.IsNullOrEmpty(identity.Name))
        {
            logger.LogInformation("private hub unauthorized");
            await Clients.Caller.SendAsync("ReplyFromBot", "unauthorized");
        }
        else if (model.StartsWith("claude")) 
        {
            var input = new AnthropicChatInput { Model = model, Messages = new List<AnthropicChatInputMessage>(), MaxTokens = 2048 };
            input.Messages.Add(new AnthropicChatInputMessage { Role = "user", Content = new List<AnthropicChatInputMessageContent>() });
            input.Messages[0].Content.Add(new AnthropicChatInputMessageContent { Type = "text", Text = message });
            try
            {
                var chatResponse = await services.GetRequiredService<ChatBotAnthropic>().Chat(identity.Name, input);
                if (chatResponse != null)
                {
                    foreach (var content in chatResponse.Content.Where(r => r.Type == "text")) 
                    {
                        await Clients.Caller.SendAsync("ReplyFromBot", content.Text);
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "private hub unexpected expection");
                await Clients.Caller.SendAsync("ReplyFromBot", "meet unexpected expection");
            }

        }
        else
        {
            var input = new OpenAIChatInput { Model = model, Messages = new List<OpenAIChatInputMessage>() };
            input.Messages.Add(new OpenAIChatInputMessage { Role = "user", Name = identity.Name, Content = new List<OpenAIChatInputMessageContent>() });
            input.Messages[0].Content.Add(new OpenAIChatInputMessageContent { Type = "text", Text = message });
            try
            {
                var completion = await services.GetRequiredService<ChatBotOpenAI>().ChatCompletion(identity.Name, input);
                if (completion != null)
                {
                    await Clients.Caller.SendAsync("ReplyFromBot", completion.Choices[0].Message.Content);
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "private hub unexpected expection");
                await Clients.Caller.SendAsync("ReplyFromBot", "meet unexpected expection");
            }
        }
    }

    public async Task ChatToResponsesBot(string model, string message)
    {
        var identity = Context.User?.Identity;
        if (identity == null || !identity.IsAuthenticated || string.IsNullOrEmpty(identity.Name))
        {
            await Clients.Caller.SendAsync("ReplyFromBot", "unauthorized");
            return;
        }
        if (string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(message))
        {
            await Clients.Caller.SendAsync("ReplyFromBot", "请填写模型名称和消息内容");
            return;
        }
        try
        {
            var response = await services.GetRequiredService<ChatBotOpenAIResponses>()
                .Chat($"web_{identity.Name}", OpenAIResponseInput.FromText(model, message), Context.ConnectionAborted);
            await Clients.Caller.SendAsync("ReplyFromBot", response?.GetReplyText() ?? "处理异常，请检查日志");
        }
        catch (OperationCanceledException) when (Context.ConnectionAborted.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Private hub Responses API chat failed");
            await Clients.Caller.SendAsync("ReplyFromBot", "处理异常，请检查日志");
        }
    }
}
