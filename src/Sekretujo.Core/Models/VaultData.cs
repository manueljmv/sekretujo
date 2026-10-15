namespace Sekretujo.Core.Models;

public sealed class VaultData
{
    public int SchemaVersion { get; set; } = 1;
    public List<VaultEntry> Entries { get; set; } = new();
    public List<Tag> Tags { get; set; } = new();
}
