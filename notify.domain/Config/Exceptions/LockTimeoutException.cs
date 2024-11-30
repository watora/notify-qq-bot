namespace Notify.Domain.Config;

public class LockTimeoutException : Exception
{
    public LockTimeoutException() { }

    public LockTimeoutException(string message) : base(message) { }

    public LockTimeoutException(string message, Exception inner) : base(message, inner) { }
}