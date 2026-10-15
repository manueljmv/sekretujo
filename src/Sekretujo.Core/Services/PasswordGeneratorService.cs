using System.Security.Cryptography;
using System.Text;

namespace Sekretujo.Core.Services;

public sealed class PasswordGeneratorOptions
{
    public int Length { get; set; } = 20;
    public bool UseUppercase { get; set; } = true;
    public bool UseLowercase { get; set; } = true;
    public bool UseDigits { get; set; } = true;
    public bool UseSymbols { get; set; } = true;
}

public sealed class PasswordGeneratorService
{
    private const string Uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lowercase = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%^&*-_=+?";

    public string Generate(PasswordGeneratorOptions options)
    {
        if (options.Length <= 0)
            throw new ArgumentException("La longitud debe ser mayor que cero.", nameof(options));

        var charset = BuildCharset(options);
        if (charset.Length == 0)
            throw new ArgumentException("Debe seleccionar al menos un tipo de carácter.", nameof(options));

        var result = new char[options.Length];
        for (var i = 0; i < options.Length; i++)
        {
            var index = RandomNumberGenerator.GetInt32(charset.Length);
            result[i] = charset[index];
        }
        return new string(result);
    }

    private static string BuildCharset(PasswordGeneratorOptions options)
    {
        var sb = new StringBuilder();
        if (options.UseUppercase) sb.Append(Uppercase);
        if (options.UseLowercase) sb.Append(Lowercase);
        if (options.UseDigits) sb.Append(Digits);
        if (options.UseSymbols) sb.Append(Symbols);
        return sb.ToString();
    }
}
