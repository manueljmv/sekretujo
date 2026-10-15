using Sekretujo.Core.Crypto;
using Sekretujo.Core.Models;

namespace Sekretujo.Core.Storage;

public sealed class VaultRepository
{
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int MinFileSize = SaltSize + NonceSize + TagSize;

    private readonly CryptoService _crypto;
    private readonly VaultSerializer _serializer;

    public VaultRepository(CryptoService crypto, VaultSerializer serializer)
    {
        _crypto = crypto;
        _serializer = serializer;
    }

    public void Save(string path, string masterPassword, VaultData data)
    {
        var plaintext = _serializer.Serialize(data);
        var payload = _crypto.Encrypt(masterPassword, plaintext);

        using var buffer = new MemoryStream();
        buffer.Write(payload.Salt);
        buffer.Write(payload.Nonce);
        buffer.Write(payload.Ciphertext);
        buffer.Write(payload.Tag);

        var tempPath = path + ".tmp";
        File.WriteAllBytes(tempPath, buffer.ToArray());
        File.Move(tempPath, path, overwrite: true);
    }

    public VaultData Load(string path, string masterPassword)
    {
        if (!File.Exists(path))
            throw new VaultCorruptException("No se encontró el archivo de vault.");

        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < MinFileSize)
            throw new VaultCorruptException("El archivo de vault está corrupto o incompleto.");

        var salt = bytes[..SaltSize];
        var nonce = bytes[SaltSize..(SaltSize + NonceSize)];
        var tag = bytes[^TagSize..];
        var ciphertext = bytes[(SaltSize + NonceSize)..^TagSize];

        var payload = new EncryptedPayload { Salt = salt, Nonce = nonce, Ciphertext = ciphertext, Tag = tag };
        var plaintext = _crypto.Decrypt(masterPassword, payload);
        return _serializer.Deserialize(plaintext);
    }
}
