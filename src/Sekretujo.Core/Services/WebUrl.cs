namespace Sekretujo.Core.Services;

public static class WebUrl
{
    // Only http(s) may reach the OS launcher: a URL can come from an imported file, and file: or custom
    // schemes would open local files or other applications.
    public static Uri? Normalize(string input)
    {
        var text = input.Trim();
        if (text.Length == 0) return null;
        if (!text.Contains("://")) text = "https://" + text;

        return Uri.TryCreate(text, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri
            : null;
    }
}
