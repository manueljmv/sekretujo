namespace Sekretujo.Core.Models;

public sealed class PasswordItem : VaultEntry
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? TotpSecret { get; set; }
    public string Notes { get; set; } = string.Empty;
}
