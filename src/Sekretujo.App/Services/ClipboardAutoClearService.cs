using System;
using System.Threading.Tasks;
using Avalonia.Input.Platform;

namespace Sekretujo.App.Services;

public sealed class ClipboardAutoClearService
{
    private IClipboard? _clipboard;
    private string? _pendingValue;

    public bool HasPending => _pendingValue is not null;

    public async Task CopyAndClear(IClipboard clipboard, string value, TimeSpan after)
    {
        await clipboard.SetTextAsync(value);
        _clipboard = clipboard;
        _pendingValue = value;
        _ = ClearLater(value, after);
    }

    private async Task ClearLater(string copiedValue, TimeSpan after)
    {
        await Task.Delay(after);
        if (ReferenceEquals(_pendingValue, copiedValue))
            await ClearNowAsync();
    }

    public async Task ClearNowAsync()
    {
        var (clipboard, value) = (_clipboard, _pendingValue);
        _clipboard = null;
        _pendingValue = null;
        if (clipboard is null || value is null) return;

        try
        {
            if (await clipboard.TryGetTextAsync() == value)
                await clipboard.ClearAsync();
        }
        catch (Exception)
        {
            // Best-effort: a clipboard failure must never block locking or closing the app.
        }
    }
}
