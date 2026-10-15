using Sekretujo.Core.Models;
using Sekretujo.Core.Storage;
using Xunit;

namespace Sekretujo.Core.Tests;

public class VaultDataSerializationTests
{
    [Fact]
    public void Serialize_Then_Deserialize_RoundTripsMixedEntries()
    {
        var serializer = new VaultSerializer();
        var data = new VaultData
        {
            Tags = { new Tag { Name = "work" } },
            Entries =
            {
                new PasswordItem { Title = "GitHub", Username = "manuel", Password = "s3cr3t", Url = "https://github.com" },
                new NoteItem { Title = "Recovery codes", Body = "1234-5678" }
            }
        };

        var bytes = serializer.Serialize(data);
        var roundTripped = serializer.Deserialize(bytes);

        Assert.Equal(2, roundTripped.Entries.Count);
        var password = Assert.IsType<PasswordItem>(roundTripped.Entries[0]);
        Assert.Equal("GitHub", password.Title);
        Assert.Equal("manuel", password.Username);
        var note = Assert.IsType<NoteItem>(roundTripped.Entries[1]);
        Assert.Equal("1234-5678", note.Body);
        Assert.Equal("work", roundTripped.Tags[0].Name);
    }

    [Fact]
    public void Deserialize_InvalidJson_ThrowsVaultCorruptException()
    {
        var serializer = new VaultSerializer();

        Assert.Throws<VaultCorruptException>(() => serializer.Deserialize(new byte[] { 1, 2, 3 }));
    }

    [Fact]
    public void Deserialize_EntryMissingTypeDiscriminator_ThrowsVaultCorruptException()
    {
        var serializer = new VaultSerializer();
        var json = """{"SchemaVersion":1,"Entries":[{"Title":"No type"}],"Tags":[]}""";

        Assert.Throws<VaultCorruptException>(() => serializer.Deserialize(System.Text.Encoding.UTF8.GetBytes(json)));
    }
}
