
using System.Diagnostics;

namespace Notify.Service.RSS;

public class RSSHostService : IHostedService
{
    private IServiceProvider serviceProvider;
    private System.Timers.Timer? timer;
    private ILogger<RSSHostService> logger;
    private bool firstCheck;

    public RSSHostService(IServiceProvider serviceProvider, ILogger<RSSHostService> logger)
    {
        this.serviceProvider = serviceProvider;
        firstCheck = true;
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
            logger.LogInformation("rss notify loop start");
            using var scope = serviceProvider.CreateScope();
            await handleBilibili(scope);
            await handleYoutube(scope);
            sw.Stop();
            logger.LogInformation($"rss notify loop end, used:{sw.ElapsedMilliseconds}ms");
            firstCheck = false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "rss notify loop err");
        }

    }

    private async Task handleBilibili(IServiceScope scope)
    {
        var bilibili = scope.ServiceProvider.GetRequiredService<RSSNotifyBilibili>();
        await bilibili.CheckLiveStatusAndSendMessage(!firstCheck);
        await bilibili.CheckNewDynamicAndSendMessage(!firstCheck);
    }

    private async Task handleYoutube(IServiceScope scope) 
    {
        var youtube = scope.ServiceProvider.GetRequiredService<RSSNotifyYoutube>();
        await youtube.CheckLiveStatusAndSendMessage(!firstCheck);
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