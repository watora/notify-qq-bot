using System.Threading.Channels;
using Microsoft.Extensions.Caching.Memory;
using Notify.Domain.Models;

namespace Notify.Service.OfficialBot;

/// <summary>单实例内存队列；队列满时不 ACK，让平台稍后重投。</summary>
public class OfficialBotEventQueue : IDisposable
{
    private readonly object gate = new();
    private readonly MemoryCache accepted = new(new MemoryCacheOptions { SizeLimit = 10000 });
    private readonly Channel<OfficialBotEventMessage> channel = Channel.CreateBounded<OfficialBotEventMessage>(
        new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.Wait });

    public bool TryEnqueue(OfficialBotEventMessage message)
    {
        lock (gate)
        {
            var key = message.DeduplicationKey;
            if (accepted.TryGetValue(key, out _))
                return true;
            if (!channel.Writer.TryWrite(message))
                return false;
            accepted.Set(key, true, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2), Size = 1 });
            return true;
        }
    }

    public IAsyncEnumerable<OfficialBotEventMessage> ReadAllAsync(CancellationToken cancellationToken) =>
        channel.Reader.ReadAllAsync(cancellationToken);

    public void Forget(OfficialBotEventMessage message)
    {
        lock (gate)
            accepted.Remove(message.DeduplicationKey);
    }

    public void Dispose()
    {
        channel.Writer.TryComplete();
        accepted.Dispose();
    }
}

public class OfficialBotHostService : BackgroundService
{
    private readonly OfficialBotEventQueue queue;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<OfficialBotHostService> logger;

    public OfficialBotHostService(OfficialBotEventQueue queue, IServiceScopeFactory scopeFactory, ILogger<OfficialBotHostService> logger)
    {
        this.queue = queue;
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Consume(stoppingToken)));

    private async Task Consume(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<OfficialBotEvent>().HandleEvent(message, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                queue.Forget(message);
                logger.LogError(ex, "Handle official bot message failed: {MessageId}", message.Id);
            }
        }
    }
}
