using Sekretujo.Core.Storage;
using Xunit;

namespace Sekretujo.Core.Tests;

public class VaultLockTests
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.vault");

    [Fact]
    public void TryAcquire_WhileAlreadyHeld_ReturnsNull()
    {
        using var first = VaultLock.TryAcquire(_path);

        Assert.NotNull(first);
        Assert.Null(VaultLock.TryAcquire(_path));
    }

    [Fact]
    public void TryAcquire_AfterRelease_SucceedsAndLeavesNoLockFile()
    {
        VaultLock.TryAcquire(_path)!.Dispose();

        var second = VaultLock.TryAcquire(_path);
        Assert.NotNull(second);
        second.Dispose();

        Assert.False(File.Exists(_path + ".lock"));
    }

    [Fact]
    public void TryAcquire_InMissingFolder_ThrowsInsteadOfReportingLocked()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "x.vault");

        Assert.Throws<DirectoryNotFoundException>(() => VaultLock.TryAcquire(missing));
    }
}
