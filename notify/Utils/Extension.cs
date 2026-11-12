using Notify.Domain.Config;

namespace Notify.Utils;

public static class Extension
{
    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> lockMap = new();

    public static async Task<T> Lock<T>(string lockKey, Func<Task<T>> func)
    {
        var lockObj = lockMap.GetOrAdd(lockKey, _ => new SemaphoreSlim(1));
        var succeed = await lockObj.WaitAsync(1000);
        if (!succeed)
        {
            throw new LockTimeoutException();
        }
        try
        {
            return await func();
        }
        finally
        {
            lockObj.Release();
        }
    }
}
