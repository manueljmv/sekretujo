using System;
using System.IO;
using System.Text.Json;

namespace Sekretujo.App.Services;

public sealed class AppSettingsStore
{
    private static readonly string FilePath = Path.Combine(
        // Without Create, a missing ~/.config yields an empty string and the file lands in the working directory.
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create),
        "Sekretujo", "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new AppSettings();
            var json = File.ReadAllText(FilePath);
            return Sanitize(JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings());
        }
        catch (Exception)
        {
            return new AppSettings();
        }
    }

    // The file can be edited by hand; the ranges match the controls that write these values.
    private static AppSettings Sanitize(AppSettings settings)
    {
        settings.AutoLockMinutes = Math.Clamp(settings.AutoLockMinutes, 1, 120);
        settings.ClipboardClearSeconds = Math.Clamp(settings.ClipboardClearSeconds, 5, 300);
        settings.GeneratorLength = Math.Clamp(settings.GeneratorLength, 4, 128);
        if (!Enum.IsDefined(settings.LastTypeFilter))
            settings.LastTypeFilter = ViewModels.EntryTypeFilter.All;
        return settings;
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(directory);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings));
        }
        catch (Exception)
        {
            // Settings persistence is best-effort; a failure here must never block using the app.
        }
    }
}
