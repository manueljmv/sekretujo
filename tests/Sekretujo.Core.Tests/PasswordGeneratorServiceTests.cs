using Sekretujo.Core.Services;
using Xunit;

namespace Sekretujo.Core.Tests;

public class PasswordGeneratorServiceTests
{
    [Fact]
    public void Generate_ReturnsStringOfRequestedLength()
    {
        var generator = new PasswordGeneratorService();
        var options = new PasswordGeneratorOptions { Length = 24, UseUppercase = true, UseDigits = true };

        var password = generator.Generate(options);

        Assert.Equal(24, password.Length);
    }

    [Fact]
    public void Generate_WithNoCharsetSelected_ThrowsArgumentException()
    {
        var generator = new PasswordGeneratorService();
        var options = new PasswordGeneratorOptions
        {
            Length = 10,
            UseUppercase = false,
            UseLowercase = false,
            UseDigits = false,
            UseSymbols = false
        };

        Assert.Throws<ArgumentException>(() => generator.Generate(options));
    }

    [Fact]
    public void Generate_WithZeroLength_ThrowsArgumentException()
    {
        var generator = new PasswordGeneratorService();
        var options = new PasswordGeneratorOptions { Length = 0, UseLowercase = true };

        Assert.Throws<ArgumentException>(() => generator.Generate(options));
    }

    [Fact]
    public void Generate_OnlyUsesSelectedCharsets()
    {
        var generator = new PasswordGeneratorService();
        var options = new PasswordGeneratorOptions
        {
            Length = 50,
            UseUppercase = false,
            UseLowercase = false,
            UseDigits = true,
            UseSymbols = false
        };

        var password = generator.Generate(options);

        Assert.All(password, c => Assert.True(char.IsDigit(c)));
    }
}
