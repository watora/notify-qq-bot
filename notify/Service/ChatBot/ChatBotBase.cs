using Microsoft.Extensions.Caching.Memory;
using Notify.Utils;

namespace Notify.Service.ChatBot;

public abstract class ChatBotBase
{
    protected IServiceProvider sp;
    protected ILogger logger;
    protected HttpClient httpClient;
    protected IConfiguration configuration;
    protected IMemoryCache memoryCache;

    public ChatBotBase(IServiceProvider sp, ILogger logger)
    {
        this.sp = sp;
        this.logger = logger;
        httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
        configuration = sp.GetRequiredService<IConfiguration>();
        memoryCache = sp.GetRequiredService<IMemoryCache>(); 
    }
}