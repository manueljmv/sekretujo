using System;
using System.Threading.Tasks;
using Avalonia.Input.Platform;

namespace Sekretujo.App.Services;

public sealed class ClipboardAutoClearService
{
    public async Task CopyAndClear(IClipboard clipboard, string value, TimeSpan after)
    {
        await clipboard.SetTextAsync(value);
        _ = ClearLater(clipboard, value, after);
    }

    private static async Task ClearLater(IClipboard clipboard, string copiedValue, TimeSpan after)
    {
        await Task.Delay(after);
        var current = await clipboard.TryGetTextAsync();
        if (current == copiedValue)
            await clipboard.ClearAsync();
    }
}
