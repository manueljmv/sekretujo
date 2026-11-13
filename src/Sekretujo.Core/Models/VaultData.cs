namespace Sekretujo.Core.Models;

public sealed class VaultData
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public List<VaultEntry> Entries { get; set; } = new();
    public List<Tag> Tags { get; set; } = new();
    public List<string> CollapsedTags { get; set; } = new();
    public string SearchText { get; set; } = string.Empty;
}
