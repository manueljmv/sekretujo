using Sekretujo.Core.Models;

namespace Sekretujo.App.Services;

public sealed class VaultSession
{
    public VaultData? Data { get; private set; }
    public string? FilePath { get; private set; }
    public string? MasterPassword { get; private set; }

    public bool IsOpen => Data is not null;

    public void Open(string filePath, string masterPassword, VaultData data)
    {
        FilePath = filePath;
        MasterPassword = masterPassword;
        Data = data;
    }

    public void Close()
    {
        Data = null;
        FilePath = null;
        MasterPassword = null;
    }
}
