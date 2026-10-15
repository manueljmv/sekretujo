namespace Sekretujo.App.Services;

public sealed class AppSettings
{
    public int AutoLockMinutes { get; set; } = 5;
    public int ClipboardClearSeconds { get; set; } = 20;
    public string? LastVaultPath { get; set; }
    public int GeneratorLength { get; set; } = 24;
    public bool GeneratorUseUppercase { get; set; } = true;
    public bool GeneratorUseLowercase { get; set; } = true;
    public bool GeneratorUseDigits { get; set; } = true;
    public bool GeneratorUseSymbols { get; set; } = true;
}
