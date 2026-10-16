using System;
using System.Threading;

namespace Sekretujo.App.Services;

public sealed class AutoLockService : IDisposable
{
    private Timer? _timer;
    private TimeSpan _timeout = TimeSpan.FromMinutes(5);
    private DateTimeOffset? _deadline;

    public event Action? TimedOut;

    public TimeSpan? Remaining
    {
        get
        {
            if (_deadline is not { } deadline) return null;
            var remaining = deadline - DateTimeOffset.UtcNow;
            return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }
    }

    public void Start(TimeSpan timeout)
    {
        _timeout = timeout;
        _timer?.Dispose();
        _deadline = DateTimeOffset.UtcNow + _timeout;
        _timer = new Timer(_ => TimedOut?.Invoke(), null, _timeout, Timeout.InfiniteTimeSpan);
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        _deadline = null;
    }

    public void NotifyActivity()
    {
        if (_timer is null) return;
        _deadline = DateTimeOffset.UtcNow + _timeout;
        _timer.Change(_timeout, Timeout.InfiniteTimeSpan);
    }

    public void Dispose() => _timer?.Dispose();
}
