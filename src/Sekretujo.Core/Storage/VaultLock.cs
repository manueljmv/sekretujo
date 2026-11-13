namespace Sekretujo.Core.Storage;

// Held while a vault is open so a second instance cannot open the same file and overwrite this one's saves.
// The lock is the OS-level exclusive handle, not the file's existence, so a crash never leaves a stale lock.
public sealed class VaultLock : IDisposable
{
    private readonly FileStream? _stream;

    private VaultLock(FileStream? stream) => _stream = stream;

    // Returns null when another process holds the lock.
    public static VaultLock? TryAcquire(string vaultPath)
    {
        try
        {
            return new VaultLock(new FileStream(vaultPath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite,
                FileShare.None, bufferSize: 1, FileOptions.DeleteOnClose));
        }
        catch (UnauthorizedAccessException)
        {
            // A folder that cannot be written to cannot be saved to either, so there is nothing to protect.
            return new VaultLock(null);
        }
        catch (IOException ex) when (ex is not DirectoryNotFoundException)
        {
            return null;
        }
    }

    public void Dispose() => _stream?.Dispose();
}
