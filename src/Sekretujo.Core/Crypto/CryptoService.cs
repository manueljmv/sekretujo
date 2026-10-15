using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Sekretujo.Core.Crypto;

public sealed class CryptoService
{
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;

    public EncryptedPayload Encrypt(string masterPassword, byte[] plaintext)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = DeriveKey(masterPassword, salt);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aesGcm = new AesGcm(key, TagSize);
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);

        return new EncryptedPayload { Salt = salt, Nonce = nonce, Ciphertext = ciphertext, Tag = tag };
    }

    public byte[] Decrypt(string masterPassword, EncryptedPayload payload)
    {
        var key = DeriveKey(masterPassword, payload.Salt);
        var plaintext = new byte[payload.Ciphertext.Length];

        using var aesGcm = new AesGcm(key, TagSize);
        try
        {
            aesGcm.Decrypt(payload.Nonce, payload.Ciphertext, payload.Tag, plaintext);
        }
        catch (CryptographicException)
        {
            throw new VaultUnlockException("Contraseña incorrecta o archivo corrupto.");
        }

        return plaintext;
    }

    private static byte[] DeriveKey(string masterPassword, byte[] salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(masterPassword))
        {
            Salt = salt,
            DegreeOfParallelism = 4,
            MemorySize = 65536,
            Iterations = 3
        };
        return argon2.GetBytes(KeySize);
    }
}
