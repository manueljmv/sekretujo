using Sekretujo.Core.Crypto;
using Sekretujo.Core.Models;
using Sekretujo.Core.Storage;
using Xunit;

namespace Sekretujo.Core.Tests;

public class VaultRepositoryTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.vault");
    private readonly VaultRepository _repository = new(new CryptoService(), new VaultSerializer());

    [Fact]
    public void Save_Then_Load_WithCorrectPassword_RoundTrips()
    {
        var data = new VaultData { Entries = { new NoteItem { Title = "Note", Body = "content" } } };

        _repository.Save(_path, "correct-password", data);
        var loaded = _repository.Load(_path, "correct-password");

        Assert.Single(loaded.Entries);
        Assert.Equal("Note", loaded.Entries[0].Title);
    }

    [Fact]
    public void Load_WithWrongPassword_ThrowsVaultUnlockException()
    {
        _repository.Save(_path, "correct-password", new VaultData());

        Assert.Throws<VaultUnlockException>(() => _repository.Load(_path, "wrong-password"));
    }

    [Fact]
    public void Load_TruncatedFile_ThrowsVaultCorruptException()
    {
        File.WriteAllBytes(_path, new byte[10]);

        Assert.Throws<VaultCorruptException>(() => _repository.Load(_path, "any-password"));
    }

    [Fact]
    public void Save_NeverLeavesTempFileBehind()
    {
        _repository.Save(_path, "correct-password", new VaultData());

        Assert.False(File.Exists(_path + ".tmp"));
        Assert.True(File.Exists(_path));
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}
