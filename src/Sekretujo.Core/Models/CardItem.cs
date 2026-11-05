namespace Sekretujo.Core.Models;

public sealed class CardItem : VaultEntry
{
    public string Holder { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string Expiry { get; set; } = string.Empty;
    public string Cvv { get; set; } = string.Empty;
    public string Pin { get; set; } = string.Empty;
    public string BankPhone { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}
