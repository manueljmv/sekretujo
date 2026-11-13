using System.Buffers.Binary;
using Sekretujo.Core.Crypto;
using Sekretujo.Core.Models;
using Sekretujo.Core.Storage;
using Xunit;

namespace Sekretujo.Core.Tests;

public class VaultRepositoryTests : IDisposable
{
    private const string Password = "correct-password";
    private const int FormatVersionOffset = 9;
    private const int KdfMemoryOffset = 10;
    private const int KdfIterationsOffset = 14;

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.vault");
    private readonly VaultRepository _repository = new(new CryptoService(), new VaultSerializer());

    private void SaveNew(VaultData data)
    {
        using var key = _repository.CreateKey(Password);
        _repository.Save(_path, key, data);
    }

    private VaultData LoadData(string path, string password)
    {
        var (data, key) = _repository.Load(path, password);
        key.Dispose();
        return data;
    }

    [Fact]
    public void Save_Then_Load_WithCorrectPassword_RoundTrips()
    {
        SaveNew(new VaultData { Entries = { new NoteItem { Title = "Note", Body = "content" } } });

        var loaded = LoadData(_path, Password);

        Assert.Single(loaded.Entries);
        Assert.Equal("Note", loaded.Entries[0].Title);
    }

    [Fact]
    public void Load_ReturnsKeyThatSavesWithoutThePassword()
    {
        SaveNew(new VaultData());
        var (data, key) = _repository.Load(_path, Password);
        using (key)
        {
            data.Entries.Add(new NoteItem { Title = "Added later" });
            _repository.Save(_path, key, data);
        }

        Assert.Equal("Added later", LoadData(_path, Password).Entries[0].Title);
    }

    [Fact]
    public void Load_WithWrongPassword_ThrowsVaultUnlockException()
    {
        SaveNew(new VaultData());

        Assert.Throws<VaultUnlockException>(() => _repository.Load(_path, "wrong-password"));
    }

    [Fact]
    public void Load_TruncatedFile_ThrowsVaultCorruptException()
    {
        File.WriteAllBytes(_path, new byte[10]);

        Assert.Throws<VaultCorruptException>(() => _repository.Load(_path, "any-password"));
    }

    [Fact]
    public void Load_FileWithOnlyTheHeader_ThrowsVaultCorruptException()
    {
        SaveNew(new VaultData());
        File.WriteAllBytes(_path, File.ReadAllBytes(_path)[..40]);

        Assert.Throws<VaultCorruptException>(() => _repository.Load(_path, Password));
    }

    [Fact]
    public void Load_LegacyHeaderlessFile_OpensAndIsUpgradedOnNextSave()
    {
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "legacy-v1.vault"), _path);

        var (data, key) = _repository.Load(_path, "legacy-password");
        using (key)
        {
            Assert.Equal(2, data.Entries.Count);
            var login = Assert.IsType<PasswordItem>(data.Entries[0]);
            Assert.Equal("old-format-pw", login.Password);
            Assert.Equal("written by the headerless format", Assert.IsType<NoteItem>(data.Entries[1]).Body);

            _repository.Save(_path, key, data);
        }

        Assert.StartsWith("SEKRETUJO", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(_path)[..9]));
        Assert.Equal(2, LoadData(_path, "legacy-password").Entries.Count);
        Assert.Equal(2, LoadData(_path + ".bak", "legacy-password").Entries.Count);
    }

    [Fact]
    public void Load_LegacyHeaderlessFile_WithWrongPassword_ThrowsVaultUnlockException()
    {
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "legacy-v1.vault"), _path);

        Assert.Throws<VaultUnlockException>(() => _repository.Load(_path, "wrong-password"));
    }

    [Fact]
    public void Load_WithTamperedHeader_ThrowsVaultUnlockException()
    {
        SaveNew(new VaultData());
        var bytes = File.ReadAllBytes(_path);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(KdfIterationsOffset), 2);
        File.WriteAllBytes(_path, bytes);

        Assert.Throws<VaultUnlockException>(() => _repository.Load(_path, Password));
    }

    [Fact]
    public void Load_WithUnknownFormatVersion_ThrowsVaultCorruptException()
    {
        SaveNew(new VaultData());
        var bytes = File.ReadAllBytes(_path);
        bytes[FormatVersionOffset] = 3;
        File.WriteAllBytes(_path, bytes);

        Assert.Throws<VaultCorruptException>(() => _repository.Load(_path, Password));
    }

    [Fact]
    public void Load_WithOutOfRangeKdfParameters_ThrowsVaultCorruptExceptionWithoutDerivingAKey()
    {
        SaveNew(new VaultData());
        var bytes = File.ReadAllBytes(_path);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(KdfMemoryOffset), int.MaxValue);
        File.WriteAllBytes(_path, bytes);

        Assert.Throws<VaultCorruptException>(() => _repository.Load(_path, Password));
    }

    [Fact]
    public void Save_NeverLeavesTempFileBehind()
    {
        SaveNew(new VaultData());

        Assert.False(File.Exists(_path + ".tmp"));
        Assert.True(File.Exists(_path));
    }

    [Fact]
    public void Save_OverExistingVault_KeepsPreviousVersionAsBackup()
    {
        using var key = _repository.CreateKey(Password);
        _repository.Save(_path, key, new VaultData { Entries = { new NoteItem { Title = "first" } } });
        Assert.False(File.Exists(_path + ".bak"));

        _repository.Save(_path, key, new VaultData { Entries = { new NoteItem { Title = "second" } } });
        _repository.Save(_path, key, new VaultData { Entries = { new NoteItem { Title = "third" } } });

        Assert.Equal("third", LoadData(_path, Password).Entries[0].Title);
        Assert.Equal("second", LoadData(_path + ".bak", Password).Entries[0].Title);
    }

    [Fact]
    public void Save_CreatesFileAccessibleOnlyByItsOwner()
    {
        if (OperatingSystem.IsWindows()) return;

        SaveNew(new VaultData());
        SaveNew(new VaultData());

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(_path));
    }

    [Fact]
    public void Save_WithDisposedKey_ThrowsAndLeavesExistingVaultIntact()
    {
        var key = _repository.CreateKey(Password);
        _repository.Save(_path, key, new VaultData { Entries = { new NoteItem { Title = "kept" } } });
        key.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _repository.Save(_path, key, new VaultData()));
        Assert.Equal("kept", LoadData(_path, Password).Entries[0].Title);
    }

    public void Dispose()
    {
        foreach (var suffix in new[] { "", ".bak", ".tmp" })
            File.Delete(_path + suffix);
    }
}
