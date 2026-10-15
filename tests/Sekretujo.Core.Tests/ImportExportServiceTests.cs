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
