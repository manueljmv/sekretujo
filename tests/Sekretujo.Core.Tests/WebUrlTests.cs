using Sekretujo.Core.Services;
using Xunit;

namespace Sekretujo.Core.Tests;

public class WebUrlTests
{
    [Theory]
    [InlineData("https://bank.test/login", "https://bank.test/login")]
    [InlineData("http://router.test", "http://router.test/")]
    [InlineData("example.com", "https://example.com/")]
    [InlineData("  example.com/login  ", "https://example.com/login")]
    [InlineData("localhost:8080", "https://localhost:8080/")]
    public void Normalize_ReturnsHttpUrl_AddingHttpsWhenSchemeIsMissing(string input, string expected) =>
        Assert.Equal(expected, WebUrl.Normalize(input)?.AbsoluteUri);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://files.test")]
    [InlineData("javascript:alert(1)")]
    [InlineData("vscode://open?x=1")]
    public void Normalize_RejectsEmptyAndNonHttpInput(string input) =>
        Assert.Null(WebUrl.Normalize(input));

    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\System32\\calc.exe")]
    [InlineData("\\\\server\\share\\run.bat")]
    public void Normalize_NeverYieldsLocalFileUri(string input)
    {
        var uri = WebUrl.Normalize(input);

        Assert.True(uri is null || uri.Scheme is "http" or "https");
    }
}
