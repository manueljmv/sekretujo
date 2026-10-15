namespace Sekretujo.App.Services;

public sealed class AppSettings
{
    public int AutoLockMinutes { get; set; } = 5;
    public int ClipboardClearSeconds { get; set; } = 20;
}
