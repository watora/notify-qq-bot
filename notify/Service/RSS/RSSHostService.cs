
using System.Diagnostics;

namespace Notify.Service.RSS;

public class RSSHostService : IHostedService
{
    private IServiceProvider serviceProvider;
    private System.Timers.Timer? timer;
    private ILogger<RSSHostService> logger;

    public RSSHostService(IServiceProvider serviceProvider, ILogger<RSSHostService> logger)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (timer == null)
        {
            timer = new System.Timers.Timer(TimeSpan.FromMinutes(5));
            timer.Elapsed += (sender, e) => handle();
        }
        timer.Start();
        return Task.CompletedTask;
    }

    private async void handle() 
    {
        try
        {
            var sw = new Stopwatch();
            sw.Start();
            logger.LogDebug("rss notify loop start");
            await handleBilibili(serviceProvider);
            await handleYoutube(serviceProvider);
            await handleCopymanga(serviceProvider);
            sw.Stop();
            logger.LogDebug($"rss notify loop end, used:{sw.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "rss notify loop err");
        }

    }

    private async Task handleBilibili(IServiceProvider provider)
    {
        var bilibili = provider.GetRequiredService<RSSNotifyBilibili>();
        await bilibili.CheckLiveStatusAndSendMessage(true);
        await bilibili.CheckNewDynamicAndSendMessage(true);
    }

    private async Task handleYoutube(IServiceProvider provider) 
    {
        var youtube = provider.GetRequiredService<RSSNotifyYoutube>();
        await youtube.CheckLiveStatusAndSendMessage(true);
    }

    private async Task handleCopymanga(IServiceProvider provider) 
    {
        var copymanga = provider.GetRequiredService<RSSNotifyCopymanga>();
        await copymanga.CheckMangaUpdateAndSendMessage(true);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (timer != null)
        {
            timer.Stop();
        }
        return Task.CompletedTask;
    }
}