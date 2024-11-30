using Notify.Domain.Config;

namespace Notify.Utils
{
    public static class Extension
    {
        static Dictionary<string, SemaphoreSlim> lockMap = new Dictionary<string, SemaphoreSlim>();

        public static async Task<T> Lock<T>(string lockKey, Func<Task<T>> func)
        {
            if (!lockMap.ContainsKey(lockKey))
            {
                lock (lockMap)
                {
                    if (!lockMap.ContainsKey(lockKey))
                    {
                        lockMap[lockKey] = new SemaphoreSlim(1);
                    }
                }
            }
            var lockObj = lockMap[lockKey];
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
}
