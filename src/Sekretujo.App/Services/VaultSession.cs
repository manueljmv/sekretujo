using Sekretujo.Core.Crypto;
using Sekretujo.Core.Models;
using Sekretujo.Core.Storage;

namespace Sekretujo.App.Services;

public sealed class VaultSession
{
    private VaultLock? _lock;

    public VaultData? Data { get; private set; }
    public string? FilePath { get; private set; }
    public VaultKey? Key { get; private set; }

    public bool IsOpen => Data is not null;

    public void Open(string filePath, VaultKey key, VaultData data, VaultLock vaultLock)
    {
        Close();
        FilePath = filePath;
        Key = key;
        Data = data;
        _lock = vaultLock;
    }

    public void Close()
    {
        Data = null;
        FilePath = null;
        Key?.Dispose();
        Key = null;
        _lock?.Dispose();
        _lock = null;
    }
}
