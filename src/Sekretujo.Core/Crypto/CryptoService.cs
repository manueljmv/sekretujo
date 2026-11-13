using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Sekretujo.Core.Crypto;

public sealed class CryptoService
{
    public const int SaltSize = 16;
    public const int NonceSize = 12;
    public const int TagSize = 16;
    private const int KeySize = 32;
    private const string UnlockFailedMessage = "Contraseña incorrecta o archivo corrupto.";

    public VaultKey CreateKey(string masterPassword) =>
        DeriveKey(masterPassword, RandomNumberGenerator.GetBytes(SaltSize), KdfParameters.Default);

    public VaultKey DeriveKey(string masterPassword, byte[] salt, KdfParameters kdf)
    {
        if (string.IsNullOrEmpty(masterPassword))
            throw new VaultUnlockException(UnlockFailedMessage);

        var passwordBytes = Encoding.UTF8.GetBytes(masterPassword);
        byte[]? derived = null;
        try
        {
            using var argon2 = new Argon2id(passwordBytes)
            {
                Salt = salt,
                DegreeOfParallelism = kdf.Parallelism,
                MemorySize = kdf.MemoryKiB,
                Iterations = kdf.Iterations
            };
            derived = argon2.GetBytes(KeySize);

            // Pinned so the GC never relocates the key and leaves copies that VaultKey.Dispose cannot zero.
            var key = GC.AllocateUninitializedArray<byte>(KeySize, pinned: true);
            derived.CopyTo(key, 0);
            return new VaultKey(key, salt, kdf);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
            if (derived is not null) CryptographicOperations.ZeroMemory(derived);
        }
    }

    public EncryptedPayload Encrypt(VaultKey key, byte[] plaintext, byte[]? associatedData = null)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aesGcm = new AesGcm(key.Bytes, TagSize);
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);

        return new EncryptedPayload { Nonce = nonce, Ciphertext = ciphertext, Tag = tag };
    }

    public byte[] Decrypt(VaultKey key, EncryptedPayload payload, byte[]? associatedData = null)
    {
        var plaintext = new byte[payload.Ciphertext.Length];

        try
        {
            using var aesGcm = new AesGcm(key.Bytes, TagSize);
            aesGcm.Decrypt(payload.Nonce, payload.Ciphertext, payload.Tag, plaintext, associatedData);
        }
        catch (CryptographicException)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw new VaultUnlockException(UnlockFailedMessage);
        }

        return plaintext;
    }
}
