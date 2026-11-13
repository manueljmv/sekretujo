namespace Sekretujo.Core.Crypto;

public sealed class EncryptedPayload
{
    public required byte[] Nonce { get; init; }
    public required byte[] Ciphertext { get; set; }
    public required byte[] Tag { get; init; }
}
