namespace Sekretujo.Core.Storage;

public sealed class VaultCorruptException : Exception
{
    public VaultCorruptException(string message) : base(message) { }
}
