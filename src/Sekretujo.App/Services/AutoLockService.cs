using System;
using System.Threading;

namespace Sekretujo.App.Services;

public sealed class AutoLockService : IDisposable
{
    private Timer? _timer;
    private TimeSpan _timeout = TimeSpan.FromMinutes(5);

    public event Action? TimedOut;

    public void Start(TimeSpan timeout)
    {
        _timeout = timeout;
        _timer?.Dispose();
        _timer = new Timer(_ => TimedOut?.Invoke(), null, _timeout, Timeout.InfiniteTimeSpan);
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public void NotifyActivity()
    {
        if (_timer is null) return;
        _timer.Change(_timeout, Timeout.InfiniteTimeSpan);
    }

    public void Dispose() => _timer?.Dispose();
}
