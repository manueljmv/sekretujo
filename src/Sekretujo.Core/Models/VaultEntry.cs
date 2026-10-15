using System.Text.Json.Serialization;

namespace Sekretujo.Core.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(PasswordItem), "password")]
[JsonDerivedType(typeof(NoteItem), "note")]
public abstract class VaultEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public bool IsFavorite { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;
}
