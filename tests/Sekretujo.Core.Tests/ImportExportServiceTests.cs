using Sekretujo.Core.Models;
using Sekretujo.Core.Services;
using Xunit;

namespace Sekretujo.Core.Tests;

public class ImportExportServiceTests
{
    [Fact]
    public void ExportCsv_Then_ImportCsv_RoundTripsEntryWithEmbeddedComma()
    {
        var service = new ImportExportService();
        var items = new List<PasswordItem>
        {
            new() { Title = "Bank", Username = "me", Password = "p@ss", Url = "https://bank.test",
                     Notes = "contains, a comma and \"quotes\"", Tags = { "finance" } }
        };

        var csv = service.ExportCsv(items);
        var imported = service.ImportCsv(csv);

        Assert.Single(imported);
        Assert.Equal("Bank", imported[0].Title);
        Assert.Equal("contains, a comma and \"quotes\"", imported[0].Notes);
        Assert.Equal("finance", imported[0].Tags[0]);
    }

    [Fact]
    public void ExportCsv_Then_ImportCsv_RoundTripsEntryWithEmbeddedNewline()
    {
        var service = new ImportExportService();
        var items = new List<PasswordItem>
        {
            new() { Title = "Multiline", Notes = "line one\nline two" }
        };

        var csv = service.ExportCsv(items);
        var imported = service.ImportCsv(csv);

        Assert.Single(imported);
        Assert.Equal("line one\nline two", imported[0].Notes);
    }

    [Fact]
    public void ExportCsv_NeutralizesLeadingFormulaCharacters_ExceptInPassword()
    {
        var service = new ImportExportService();
        var items = new List<PasswordItem> { new() { Title = "=2+2", Password = "@SUM(A1)" } };

        var csv = service.ExportCsv(items);

        Assert.Contains("'=2+2", csv);
        Assert.DoesNotContain("'@SUM(A1)", csv);
        Assert.Contains("@SUM(A1)", csv);
    }

    [Fact]
    public void ExportCsv_Then_ImportCsv_PreservesPasswordStartingWithDash()
    {
        var service = new ImportExportService();
        var items = new List<PasswordItem> { new() { Title = "Shop", Password = "-abc123" } };

        var csv = service.ExportCsv(items);
        var imported = service.ImportCsv(csv);

        Assert.Single(imported);
        Assert.Equal("-abc123", imported[0].Password);
    }

    [Theory]
    [InlineData("-list")]
    [InlineData("@john")]
    [InlineData("=2+2")]
    [InlineData("+34 600 000 000")]
    [InlineData("\tindented")]
    [InlineData("'=already quoted")]
    [InlineData("''-twice")]
    [InlineData("'plain quote")]
    [InlineData("old Mac\rline break")]
    public void ExportCsv_Then_ImportCsv_RoundTripsTextFieldsExactly(string value)
    {
        var service = new ImportExportService();
        var items = new List<PasswordItem>
        {
            new() { Title = value, Username = value, Password = value, Url = value, Notes = value, Tags = { value } }
        };

        var imported = service.ImportCsv(service.ExportCsv(items));

        Assert.Single(imported);
        Assert.Equal(value, imported[0].Title);
        Assert.Equal(value, imported[0].Username);
        Assert.Equal(value, imported[0].Password);
        Assert.Equal(value, imported[0].Url);
        Assert.Equal(value, imported[0].Notes);
        Assert.Equal(new[] { value }, imported[0].Tags);
    }

    [Fact]
    public void ExportCsv_Then_ImportCsv_RoundTripsTotpSecretAndFavorite()
    {
        var service = new ImportExportService();
        var items = new List<PasswordItem>
        {
            new() { Title = "With", TotpSecret = "JBSWY3DPEHPK3PXP", IsFavorite = true },
            new() { Title = "Without" }
        };

        var imported = service.ImportCsv(service.ExportCsv(items));

        Assert.Equal(2, imported.Count);
        Assert.Equal("JBSWY3DPEHPK3PXP", imported[0].TotpSecret);
        Assert.True(imported[0].IsFavorite);
        Assert.Null(imported[1].TotpSecret);
        Assert.False(imported[1].IsFavorite);
    }

    [Fact]
    public void ImportCsv_WithoutTotpAndFavoriteColumns_LeavesThemUnset()
    {
        var service = new ImportExportService();
        const string csv = "Title,Username,Password,Url,Notes,Tags\nOld,me,pw,https://old.test,note,a;b\n";

        var imported = service.ImportCsv(csv);

        Assert.Single(imported);
        Assert.Equal("Old", imported[0].Title);
        Assert.Equal(new[] { "a", "b" }, imported[0].Tags);
        Assert.Null(imported[0].TotpSecret);
        Assert.False(imported[0].IsFavorite);
    }

    [Fact]
    public void ImportBitwardenJson_ParsesLoginItems()
    {
        var service = new ImportExportService();
        const string json = """
        {
          "items": [
            {
              "name": "Example",
              "notes": "a note",
              "login": {
                "username": "alice",
                "password": "hunter2",
                "uris": [ { "uri": "https://example.com" } ]
              }
            }
          ]
        }
        """;

        var imported = service.ImportBitwardenJson(json);

        Assert.Single(imported);
        Assert.Equal("Example", imported[0].Title);
        Assert.Equal("alice", imported[0].Username);
        Assert.Equal("hunter2", imported[0].Password);
        Assert.Equal("https://example.com", imported[0].Url);
    }

    [Fact]
    public void ImportKeePassCsv_ParsesStandardColumns()
    {
        var service = new ImportExportService();
        const string csv = "\"Title\",\"User Name\",\"Password\",\"URL\",\"Notes\"\n" +
                            "\"Email\",\"bob\",\"pw123\",\"https://mail.test\",\"work mail\"\n";

        var imported = service.ImportKeePassCsv(csv);

        Assert.Single(imported);
        Assert.Equal("Email", imported[0].Title);
        Assert.Equal("bob", imported[0].Username);
        Assert.Equal("pw123", imported[0].Password);
    }
}
