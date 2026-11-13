using System.Security.Cryptography;

namespace Sekretujo.Core.Crypto;

public sealed class VaultKey : IDisposable
{
    private readonly byte[] _key;
    private bool _disposed;

    internal VaultKey(byte[] key, byte[] salt, KdfParameters kdf)
    {
        _key = key;
        Salt = salt;
        Kdf = kdf;
    }

    public byte[] Salt { get; }
    public KdfParameters Kdf { get; }

    // Throwing after disposal keeps a zeroed key from ever encrypting a vault.
    internal byte[] Bytes => _disposed ? throw new ObjectDisposedException(nameof(VaultKey)) : _key;

    public void Dispose()
    {
        _disposed = true;
        CryptographicOperations.ZeroMemory(_key);
    }
}
