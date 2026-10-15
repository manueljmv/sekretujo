using OtpNet;

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
}
