
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Notify.Domain.Models;
using Notify.Utils;

namespace Notify.Service.ChatBot;

public class ChatBotAnthropic : ChatBotBase
{
    private string endpoint;
    private string token;

    public ChatBotAnthropic(IServiceProvider sp, ILogger logger) : base(sp, logger)
    {
        endpoint = chatBotOption.Value.AnthropicEndpoint!;
        token = chatBotOption.Value.AnthropicToken!;
        httpClient.DefaultRequestHeaders.Add("x-api-key", token);
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
    private void FillHistoryChat(string uniqueKey, AnthropicChatInput input)
    {
        var key = $"{input.Model}_{uniqueKey}";
        var messages = memoryCache.Get<List<AnthropicChatInputMessage>>(key);
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
    private void SaveChatResponse(string uniqueKey, AnthropicChatResponse? chatResponse)
    {
        if (chatResponse != null && chatResponse.Content.Count > 0)
        {
            var key = $"{chatResponse.Model}_{uniqueKey}";
            var messages = memoryCache.Get<List<AnthropicChatInputMessage>>(key);
            if (messages != null)
            {
                var inputContent = new List<AnthropicChatInputMessageContent>();
                foreach (var content in chatResponse.Content.Where(r => r.Type == "text"))
                {
                    inputContent.Add(new AnthropicChatInputMessageContent
                    {
                        Type = "text",
                        Text = content.Text!,
                    });
                }
                messages.Add(new AnthropicChatInputMessage
                {
                    Role = chatResponse.Role,
                    Content = inputContent,
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
    public async Task<AnthropicChatResponse?> Chat(string uniqueKey, AnthropicChatInput input)
    {
        if (string.IsNullOrEmpty(uniqueKey) || input == null || string.IsNullOrEmpty(input.Model) || input.Messages.Count == 0)
        {
            return null;
        }
        return await Extension.Lock(uniqueKey, async () =>
        {
            FillHistoryChat(uniqueKey, input);
            logger.LogDebug($"call chat completion, req:{JsonSerializer.Serialize(input)}");
            var resp = await httpClient.PostAsJsonAsync("v1/messages", input);
            var respStr = await resp.Content.ReadAsStringAsync();
            logger.LogDebug($"call chat completion, resp:{respStr}");
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogInformation($"call openai chat completions failed, code:{resp.StatusCode}");
                return null;
            }
            var chatResponse = JsonSerializer.Deserialize<AnthropicChatResponse>(respStr);
            SaveChatResponse(uniqueKey, chatResponse);
            return chatResponse;
        });
    }
}