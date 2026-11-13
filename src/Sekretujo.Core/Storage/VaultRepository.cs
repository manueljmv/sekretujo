using System.Buffers.Binary;
using System.Security.Cryptography;
using Sekretujo.Core.Crypto;
using Sekretujo.Core.Models;

namespace Sekretujo.Core.Storage;

public sealed class VaultRepository
{
    // Current layout: [magic][1B format version][3 x int32 LE: Argon2id memory KiB, iterations, parallelism]
    // [salt][nonce][ciphertext][tag]. Everything before the nonce is AES-GCM associated data.
    // A file without the magic is the original layout, [salt][nonce][ciphertext][tag], with fixed KDF parameters.
    private const byte FormatVersion = 2;
    private const int KdfSize = 3 * sizeof(int);
    private const int LegacyMinSize = CryptoService.SaltSize + CryptoService.NonceSize + CryptoService.TagSize;

    // The header is only authenticated after the key is derived, so a crafted file must not be able to demand
    // an arbitrary amount of memory or time first.
    private const int MaxMemoryKiB = 4 * 1024 * 1024;
    private const int MaxIterations = 64;
    private const int MaxParallelism = 64;

    private static readonly KdfParameters LegacyKdf = new(65536, 3, 4);

    private static ReadOnlySpan<byte> Magic => "SEKRETUJO"u8;
    private static int KdfOffset => Magic.Length + 1;
    private static int HeaderSize => KdfOffset + KdfSize + CryptoService.SaltSize;

    private readonly CryptoService _crypto;
    private readonly VaultSerializer _serializer;

    public VaultRepository(CryptoService crypto, VaultSerializer serializer)
    {
        _crypto = crypto;
        _serializer = serializer;
    }

    public VaultKey CreateKey(string masterPassword) => _crypto.CreateKey(masterPassword);

    public void Save(string path, VaultKey key, VaultData data)
    {
        var header = BuildHeader(key);
        var plaintext = _serializer.Serialize(data);
        EncryptedPayload payload;
        try
        {
            payload = _crypto.Encrypt(key, plaintext, header);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }

        var tempPath = path + ".tmp";
        // A leftover temp file would keep its old permissions instead of the ones requested below.
        File.Delete(tempPath);
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows())
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

        using (var stream = new FileStream(tempPath, options))
        {
            stream.Write(header);
            stream.Write(payload.Nonce);
            stream.Write(payload.Ciphertext);
            stream.Write(payload.Tag);
            stream.Flush(flushToDisk: true);
        }

        if (File.Exists(path))
            File.Replace(tempPath, path, path + ".bak");
        else
            File.Move(tempPath, path);
    }

    public (VaultData Data, VaultKey Key) Load(string path, string masterPassword)
    {
        if (!File.Exists(path))
            throw new VaultCorruptException("No se encontró el archivo de vault.");

        var bytes = File.ReadAllBytes(path);
        var isCurrent = bytes.AsSpan().StartsWith(Magic);
        var bodyOffset = isCurrent ? HeaderSize : CryptoService.SaltSize;
        if (bytes.Length < (isCurrent ? HeaderSize + CryptoService.NonceSize + CryptoService.TagSize : LegacyMinSize))
            throw new VaultCorruptException("El archivo de vault está corrupto o incompleto.");

        var kdf = isCurrent ? ReadKdf(bytes) : LegacyKdf;
        var salt = bytes[(bodyOffset - CryptoService.SaltSize)..bodyOffset];
        var associatedData = isCurrent ? bytes[..HeaderSize] : null;
        var payload = new EncryptedPayload
        {
            Nonce = bytes[bodyOffset..(bodyOffset + CryptoService.NonceSize)],
            Ciphertext = bytes[(bodyOffset + CryptoService.NonceSize)..^CryptoService.TagSize],
            Tag = bytes[^CryptoService.TagSize..]
        };

        var key = _crypto.DeriveKey(masterPassword, salt, kdf);
        try
        {
            var plaintext = _crypto.Decrypt(key, payload, associatedData);
            try
            {
                return (_serializer.Deserialize(plaintext), key);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }
        catch
        {
            key.Dispose();
            throw;
        }
    }

    private static byte[] BuildHeader(VaultKey key)
    {
        var header = new byte[HeaderSize];
        Magic.CopyTo(header);
        header[Magic.Length] = FormatVersion;
        var kdf = header.AsSpan(KdfOffset);
        BinaryPrimitives.WriteInt32LittleEndian(kdf, key.Kdf.MemoryKiB);
        BinaryPrimitives.WriteInt32LittleEndian(kdf[4..], key.Kdf.Iterations);
        BinaryPrimitives.WriteInt32LittleEndian(kdf[8..], key.Kdf.Parallelism);
        key.Salt.CopyTo(header, KdfOffset + KdfSize);
        return header;
    }

    private static KdfParameters ReadKdf(byte[] bytes)
    {
        if (bytes[Magic.Length] != FormatVersion)
            throw new VaultCorruptException(
                "El vault usa un formato no compatible; puede haberse creado con una versión más reciente de Sekretujo.");

        var kdf = bytes.AsSpan(KdfOffset);
        var parameters = new KdfParameters(
            BinaryPrimitives.ReadInt32LittleEndian(kdf),
            BinaryPrimitives.ReadInt32LittleEndian(kdf[4..]),
            BinaryPrimitives.ReadInt32LittleEndian(kdf[8..]));

        if (parameters.Parallelism is < 1 or > MaxParallelism
            || parameters.Iterations is < 1 or > MaxIterations
            || parameters.MemoryKiB > MaxMemoryKiB
            || parameters.MemoryKiB < 8 * parameters.Parallelism)
            throw new VaultCorruptException("El archivo de vault está corrupto o incompleto.");

        return parameters;
    }
}
