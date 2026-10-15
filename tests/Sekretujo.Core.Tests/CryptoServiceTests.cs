using System.Text;
using Sekretujo.Core.Crypto;
using Xunit;

namespace Sekretujo.Core.Tests;

public class CryptoServiceTests
{
    [Fact]
    public void Encrypt_Then_Decrypt_WithCorrectPassword_RoundTrips()
    {
        var crypto = new CryptoService();
        var plaintext = Encoding.UTF8.GetBytes("hello vault");

        var payload = crypto.Encrypt("correct-password", plaintext);
        var decrypted = crypto.Decrypt("correct-password", payload);

        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Decrypt_WithWrongPassword_ThrowsVaultUnlockException()
    {
        var crypto = new CryptoService();
        var payload = crypto.Encrypt("correct-password", Encoding.UTF8.GetBytes("secret"));

        Assert.Throws<VaultUnlockException>(() => crypto.Decrypt("wrong-password", payload));
    }

    [Fact]
    public void Decrypt_WithTamperedCiphertext_ThrowsVaultUnlockException()
    {
        var crypto = new CryptoService();
        var payload = crypto.Encrypt("correct-password", Encoding.UTF8.GetBytes("secret"));
        payload.Ciphertext[0] ^= 0xFF;

        Assert.Throws<VaultUnlockException>(() => crypto.Decrypt("correct-password", payload));
    }
}
