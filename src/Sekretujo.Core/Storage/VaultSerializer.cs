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
            var data = JsonSerializer.Deserialize<VaultData>(plaintext)
                       ?? throw new VaultCorruptException("El vault está vacío o malformado.");
            // Saving a newer schema with this version would silently drop the fields it does not know.
            if (data.SchemaVersion > VaultData.CurrentSchemaVersion)
                throw new VaultCorruptException("El vault fue creado con una versión más reciente de Sekretujo.");
            return data;
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
