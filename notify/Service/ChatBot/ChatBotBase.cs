using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Notify.Domain.Config.Options;
using Notify.Utils;

namespace Notify.Service.ChatBot;

public abstract class ChatBotBase
{
    protected IServiceProvider sp;
    protected ILogger logger;
    protected HttpClient httpClient;
    protected IConfiguration configuration;
    protected IMemoryCache memoryCache;
    protected IOptionsSnapshot<ChatBotOption> chatBotOption;

    public ChatBotBase(IServiceProvider sp, ILogger logger)
    {
        this.sp = sp;
        this.logger = logger;
        httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
        configuration = sp.GetRequiredService<IConfiguration>();
        memoryCache = sp.GetRequiredService<IMemoryCache>();
        chatBotOption = sp.GetRequiredService<IOptionsSnapshot<ChatBotOption>>();
    }
}