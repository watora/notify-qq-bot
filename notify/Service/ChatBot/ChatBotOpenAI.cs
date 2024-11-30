using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Notify.Domain.Models;
using Notify.Utils;

namespace Notify.Service.ChatBot;

public class ChatBotOpenAI : ChatBotBase
{
    private string endpoint;
    private string token;

    public ChatBotOpenAI(IServiceProvider sp, ILogger<ChatBotOpenAI> logger) : base(sp, logger)
    {
        endpoint = configuration["OPENAI_ENDPOINT"] ?? "";
        token = configuration["OPENAI_TOKEN"] ?? "";
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (!endpoint.EndsWith("/"))
        {
            endpoint += "/";
        }
        httpClient.BaseAddress = new Uri(endpoint);
    }

    /// <summary>
    /// 填充历史对话
    /// </summary>
    /// <param name="uniqueKey"></param>
    /// <param name="input"></param>
    private void FillHistoryChat(string uniqueKey, OpenAIChatInput input)
    {
        var key = uniqueKey + input.Model;
        var messages = memoryCache.Get<List<OpenAIChatInputMessage>>(key);
        if (messages != null)
        {
            messages.AddRange(input.Messages);
            input.Messages = messages;
        }
        else
        {
            memoryCache.Set(key, input.Messages, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromHours(12) });
        }
    }

    /// <summary>
    /// 缓存对话结果
    /// </summary>
    /// <param name="uniqueKey"></param>
    /// <param name="chatCompletion"></param>
    private void SaveChatCompletion(string uniqueKey, OpenAIChatCompletion? chatCompletion)
    {
        if (chatCompletion != null && chatCompletion.Choices.Count > 0)
        {
            var key = uniqueKey + chatCompletion.Model;
            var messages = memoryCache.Get<List<OpenAIChatInputMessage>>(key);
            var msg = chatCompletion.Choices[0].Message;
            if (messages != null)
            {
                chatCompletion.ContextLength = messages.Count;
                messages.Add(new OpenAIChatInputMessage
                {
                    Role = msg.Role,
                    Content = new List<OpenAIChatInputMessageContent> { new OpenAIChatInputMessageContent { Type = "text", Text = msg.Content } }
                });
                // 当存储条数>100时，丢弃前50条
                if (messages.Count > 100)
                {
                    logger.LogInformation($"[{key}]chat history exceed limit, drop");
                    messages.RemoveRange(0, 50);
                }
            }
        }
    }

    /// <summary>
    /// 进行对话
    /// </summary>
    /// <param name="uniqueKey">唯一key，用来关联历史对话</param>
    /// <param name="input"></param>
    /// <returns></returns>
    public async Task<OpenAIChatCompletion?> ChatCompletion(string uniqueKey, OpenAIChatInput input)
    {
        if (string.IsNullOrEmpty(uniqueKey) || input == null || string.IsNullOrEmpty(input.Model) || input.Messages.Count == 0)
        {
            return null;
        }
        return await Extension.Lock(uniqueKey, async () =>
        {
            FillHistoryChat(uniqueKey, input);
            logger.LogDebug($"call chat completion, req:{JsonSerializer.Serialize(input)}");
            var resp = await httpClient.PostAsJsonAsync("v1/chat/completions", input);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogInformation($"call openai chat completions failed, code:{resp.StatusCode}");
                return null;
            }
            var respStr = await resp.Content.ReadAsStringAsync();
            logger.LogDebug($"call chat completion, resp:{respStr}");
            var chatCompletion = JsonSerializer.Deserialize<OpenAIChatCompletion>(respStr);
            SaveChatCompletion(uniqueKey, chatCompletion);
            return chatCompletion;
        });
    }
}