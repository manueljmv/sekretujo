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
    public void Serialize_Then_Deserialize_RoundTripsCardItem()
    {
        var serializer = new VaultSerializer();
        var data = new VaultData
        {
            Entries =
            {
                new CardItem
                {
                    Title = "Visa", Holder = "M J M", Number = "4111 1111 1111 1111", Expiry = "08/28",
                    Cvv = "123", Pin = "9876", BankPhone = "900102801", Notes = "límite"
                }
            }
        };

        var roundTripped = serializer.Deserialize(serializer.Serialize(data));

        var card = Assert.IsType<CardItem>(Assert.Single(roundTripped.Entries));
        Assert.Equal("Visa", card.Title);
        Assert.Equal("M J M", card.Holder);
        Assert.Equal("4111 1111 1111 1111", card.Number);
        Assert.Equal("08/28", card.Expiry);
        Assert.Equal("123", card.Cvv);
        Assert.Equal("9876", card.Pin);
        Assert.Equal("900102801", card.BankPhone);
        Assert.Equal("límite", card.Notes);
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
