namespace Sekretujo.Core.Crypto;

public sealed class VaultUnlockException : Exception
{
    public VaultUnlockException(string message) : base(message) { }
}
