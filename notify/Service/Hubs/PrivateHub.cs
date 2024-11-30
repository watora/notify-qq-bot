using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Notify.Domain.Models;
using Notify.Service.ChatBot;

namespace Notify.Service.Hubs;

[Authorize(Roles = "Admin")]
public class PrivateHub : Hub
{
    private ChatBotOpenAI chatBotOpenAI;
    private ILogger<PrivateHub> logger;

    public PrivateHub(ChatBotOpenAI chatBotOpenAI, ILogger<PrivateHub> logger)
    {
        this.chatBotOpenAI = chatBotOpenAI;
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
        else
        {
            var input = new OpenAIChatInput { Model = model, Messages = new List<OpenAIChatInputMessage>() };
            input.Messages.Add(new OpenAIChatInputMessage { Role = "user", Name = identity.Name, Content = new List<OpenAIChatInputMessageContent>() });
            input.Messages[0].Content.Add(new OpenAIChatInputMessageContent { Type = "text", Text = message });
            try
            {
                var completion = await chatBotOpenAI.ChatCompletion(identity.Name, input);
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
}