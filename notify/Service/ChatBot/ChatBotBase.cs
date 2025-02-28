using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Notify.Domain.Config.Options;

namespace Notify.Service.ChatBot;

public abstract class ChatBotBase
{
    protected IServiceProvider sp;
    protected ILogger logger;
    protected IConfiguration configuration;
    protected IMemoryCache memoryCache;
    protected IOptionsSnapshot<ChatBotOption> chatBotOption;

    public ChatBotBase(IServiceProvider sp, ILogger logger)
    {
        this.sp = sp;
        this.logger = logger;
        configuration = sp.GetRequiredService<IConfiguration>();
        memoryCache = sp.GetRequiredService<IMemoryCache>();
        chatBotOption = sp.GetRequiredService<IOptionsSnapshot<ChatBotOption>>();
    }
}