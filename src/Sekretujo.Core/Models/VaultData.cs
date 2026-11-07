namespace Sekretujo.Core.Models;

public sealed class VaultData
{
    public int SchemaVersion { get; set; } = 1;
    public List<VaultEntry> Entries { get; set; } = new();
    public List<Tag> Tags { get; set; } = new();
    public List<string> CollapsedTags { get; set; } = new();
    public string SearchText { get; set; } = string.Empty;
}
