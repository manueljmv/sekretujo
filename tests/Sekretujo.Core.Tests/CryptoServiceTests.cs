using System.Text;
using Sekretujo.Core.Crypto;
using Xunit;

namespace Sekretujo.Core.Tests;

public class CryptoServiceTests
{
    private readonly CryptoService _crypto = new();

    [Fact]
    public void Encrypt_Then_Decrypt_WithSameKey_RoundTrips()
    {
        var plaintext = Encoding.UTF8.GetBytes("hello vault");
        using var key = _crypto.CreateKey("correct-password");

        var payload = _crypto.Encrypt(key, plaintext);
        var decrypted = _crypto.Decrypt(key, payload);

        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void DeriveKey_WithSamePasswordSaltAndParameters_DecryptsWhatAnotherKeyEncrypted()
    {
        using var key = _crypto.CreateKey("correct-password");
        var payload = _crypto.Encrypt(key, Encoding.UTF8.GetBytes("secret"));

        using var rederived = _crypto.DeriveKey("correct-password", key.Salt, key.Kdf);

        Assert.Equal("secret", Encoding.UTF8.GetString(_crypto.Decrypt(rederived, payload)));
    }

    [Fact]
    public void Decrypt_WithKeyFromWrongPassword_ThrowsVaultUnlockException()
    {
        using var key = _crypto.CreateKey("correct-password");
        var payload = _crypto.Encrypt(key, Encoding.UTF8.GetBytes("secret"));

        using var wrong = _crypto.DeriveKey("wrong-password", key.Salt, key.Kdf);

        Assert.Throws<VaultUnlockException>(() => _crypto.Decrypt(wrong, payload));
    }

    [Fact]
    public void DeriveKey_WithEmptyPassword_ThrowsVaultUnlockException()
    {
        Assert.Throws<VaultUnlockException>(() => _crypto.CreateKey(string.Empty));
    }

    [Fact]
    public void Decrypt_WithTamperedCiphertext_ThrowsVaultUnlockException()
    {
        using var key = _crypto.CreateKey("correct-password");
        var payload = _crypto.Encrypt(key, Encoding.UTF8.GetBytes("secret"));
        payload.Ciphertext[0] ^= 0xFF;

        Assert.Throws<VaultUnlockException>(() => _crypto.Decrypt(key, payload));
    }

    [Fact]
    public void Decrypt_WithDifferentAssociatedData_ThrowsVaultUnlockException()
    {
        using var key = _crypto.CreateKey("correct-password");
        var payload = _crypto.Encrypt(key, Encoding.UTF8.GetBytes("secret"), new byte[] { 1, 2, 3 });

        Assert.Throws<VaultUnlockException>(() => _crypto.Decrypt(key, payload, new byte[] { 1, 2, 4 }));
    }

    [Fact]
    public void Encrypt_UsesFreshNonceEachTime()
    {
        using var key = _crypto.CreateKey("correct-password");
        var plaintext = Encoding.UTF8.GetBytes("secret");

        var first = _crypto.Encrypt(key, plaintext);
        var second = _crypto.Encrypt(key, plaintext);

        Assert.NotEqual(first.Nonce, second.Nonce);
        Assert.NotEqual(first.Ciphertext, second.Ciphertext);
    }

    [Fact]
    public void Encrypt_WithDisposedKey_ThrowsObjectDisposedException()
    {
        var key = _crypto.CreateKey("correct-password");
        key.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _crypto.Encrypt(key, new byte[1]));
    }
}
