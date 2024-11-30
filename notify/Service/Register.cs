using Notify.Service.ChatBot;
using Notify.Service.Manager;
using Notify.Service.OneBot;
using Notify.Service.RSS;

namespace Notify.Service;

public static class NotifyServiceRegister
{
    public static IServiceCollection AddNotifyServices(this IServiceCollection services)
    {
        services.AddSingleton<OneBotApi>();
        services.AddSingleton<RSSNotifyBilibili>();
        services.AddSingleton<RSSNotifyYoutube>();
        services.AddSingleton<ChatBotOpenAI>();
        services.AddScoped<RSSManager>();
        services.AddScoped<ChatBotManager>();
        services.AddScoped<OneBotEvent>();
        services.AddHostedService<RSSHostService>();
        return services;
    }
}