using System.Text;
using System.Text.Json;
using Sekretujo.Core.Models;

namespace Sekretujo.Core.Services;

public sealed class ImportExportService
{
    public string ExportCsv(IEnumerable<PasswordItem> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Title,Username,Password,Url,Notes,Tags");
        foreach (var item in items)
        {
            sb.AppendLine(string.Join(',',
                CsvField(item.Title), CsvField(item.Username), CsvField(item.Password),
                CsvField(item.Url), CsvField(item.Notes), CsvField(string.Join(';', item.Tags))));
        }
        return sb.ToString();
    }

    public List<PasswordItem> ImportCsv(string csv)
    {
        var rows = ParseCsvRows(csv);
        var items = new List<PasswordItem>();
        foreach (var fields in rows.Skip(1))
        {
            if (fields.Count == 0 || fields.All(string.IsNullOrEmpty)) continue;
            items.Add(new PasswordItem
            {
                Title = Field(fields, 0),
                Username = Field(fields, 1),
                Password = Field(fields, 2),
                Url = Field(fields, 3),
                Notes = Field(fields, 4),
                Tags = Field(fields, 5) is { Length: > 0 } tags ? tags.Split(';').ToList() : new()
            });
        }
        return items;
    }

    public List<PasswordItem> ImportBitwardenJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var result = new List<PasswordItem>();
        foreach (var element in document.RootElement.GetProperty("items").EnumerateArray())
        {
            if (!element.TryGetProperty("login", out var login)) continue;

            var uri = login.TryGetProperty("uris", out var uris) && uris.GetArrayLength() > 0
                ? uris[0].GetProperty("uri").GetString() ?? string.Empty
                : string.Empty;

            result.Add(new PasswordItem
            {
                Title = element.GetProperty("name").GetString() ?? string.Empty,
                Username = GetStringOrEmpty(login, "username"),
                Password = GetStringOrEmpty(login, "password"),
                Url = uri,
                Notes = GetStringOrEmpty(element, "notes")
            });
        }
        return result;
    }

    public List<PasswordItem> ImportKeePassCsv(string csv)
    {
        var rows = ParseCsvRows(csv);
        if (rows.Count == 0) return new();

        var headers = rows[0];
        int IndexOf(string name) => headers.FindIndex(h => h.Equals(name, StringComparison.OrdinalIgnoreCase));
        var titleIdx = IndexOf("Title");
        var userIdx = IndexOf("User Name");
        var passIdx = IndexOf("Password");
        var urlIdx = IndexOf("URL");
        var notesIdx = IndexOf("Notes");

        var items = new List<PasswordItem>();
        foreach (var fields in rows.Skip(1))
        {
            if (fields.Count == 0 || fields.All(string.IsNullOrEmpty)) continue;
            items.Add(new PasswordItem
            {
                Title = Field(fields, titleIdx),
                Username = Field(fields, userIdx),
                Password = Field(fields, passIdx),
                Url = Field(fields, urlIdx),
                Notes = Field(fields, notesIdx)
            });
        }
        return items;
    }

    private static string GetStringOrEmpty(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) ? value.GetString() ?? string.Empty : string.Empty;

    private static string Field(List<string> fields, int index) =>
        index >= 0 && index < fields.Count ? fields[index] : string.Empty;

    private static string CsvField(string value)
    {
        if (value.Length > 0 && "=+-@\t".Contains(value[0]))
            value = "'" + value;

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private static List<List<string>> ParseCsvRows(string csv)
    {
        var rows = new List<List<string>>();
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var rowHasContent = false;

        for (var i = 0; i < csv.Length; i++)
        {
            var c = csv[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < csv.Length && csv[i + 1] == '"') { current.Append('"'); i++; }
                    else inQuotes = false;
                }
                else current.Append(c);
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    rowHasContent = true;
                    break;
                case ',':
                    fields.Add(current.ToString());
                    current.Clear();
                    rowHasContent = true;
                    break;
                case '\r':
                    break;
                case '\n':
                    if (rowHasContent || current.Length > 0 || fields.Count > 0)
                    {
                        fields.Add(current.ToString());
                        rows.Add(fields);
                        fields = new List<string>();
                        current.Clear();
                        rowHasContent = false;
                    }
                    break;
                default:
                    current.Append(c);
                    rowHasContent = true;
                    break;
            }
        }

        if (current.Length > 0 || fields.Count > 0)
        {
            fields.Add(current.ToString());
            rows.Add(fields);
        }

        return rows;
    }
}
