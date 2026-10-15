using System;
using System.IO;
using System.Text.Json;

namespace Sekretujo.App.Services;

public sealed class AppSettingsStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Sekretujo", "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new AppSettings();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (Exception)
        {
            return new AppSettings();
        }
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
