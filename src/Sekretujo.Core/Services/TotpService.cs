using OtpNet;
using QRCoder;

namespace Sekretujo.Core.Services;

public sealed class TotpCode
{
    public required string Code { get; init; }
    public required int SecondsRemaining { get; init; }
}

public sealed class TotpService
{
    public TotpCode GetCode(string base32Secret, DateTimeOffset? at = null)
    {
        try
        {
            var secretBytes = Base32Encoding.ToBytes(base32Secret);
            var totp = new Totp(secretBytes);
            var timestamp = (at ?? DateTimeOffset.UtcNow).UtcDateTime;
            var code = totp.ComputeTotp(timestamp);
            var remaining = totp.RemainingSeconds(timestamp);
            return new TotpCode { Code = code, SecondsRemaining = remaining };
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            throw new InvalidTotpSecretException("El secreto TOTP no es válido.", ex);
        }
    }

    public byte[] GenerateQrCodePng(string base32Secret, string label, string? issuer = null)
    {
        try
        {
            Base32Encoding.ToBytes(base32Secret);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            throw new InvalidTotpSecretException("El secreto TOTP no es válido.", ex);
        }

        var uri = $"otpauth://totp/{Uri.EscapeDataString(label)}?secret={base32Secret}";
        if (!string.IsNullOrWhiteSpace(issuer)) uri += $"&issuer={Uri.EscapeDataString(issuer)}";

        using var generator = new QRCodeGenerator();
        var qrData = generator.CreateQrCode(uri, QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(qrData).GetGraphic(20);
    }
}
