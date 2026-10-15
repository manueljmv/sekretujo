namespace Sekretujo.Core.Models;

public sealed class Tag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
}
