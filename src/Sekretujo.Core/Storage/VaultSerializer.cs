using System.Text.Json;
using Sekretujo.Core.Models;

namespace Sekretujo.Core.Storage;

public sealed class VaultSerializer
{
    public byte[] Serialize(VaultData data) => JsonSerializer.SerializeToUtf8Bytes(data);

    public VaultData Deserialize(byte[] plaintext)
    {
        try
        {
            return JsonSerializer.Deserialize<VaultData>(plaintext)
                   ?? throw new VaultCorruptException("El vault está vacío o malformado.");
        }
        catch (VaultCorruptException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new VaultCorruptException("No se pudo interpretar el contenido del vault.", ex);
        }
    }
}
