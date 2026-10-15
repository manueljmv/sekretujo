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
        byte[] secretBytes;
        try
        {
            secretBytes = Base32Encoding.ToBytes(base32Secret);
        }
        catch (Exception ex)
        {
            throw new InvalidTotpSecretException("El secreto TOTP no es Base32 válido.", ex);
        }

        var totp = new Totp(secretBytes);
        var timestamp = (at ?? DateTimeOffset.UtcNow).UtcDateTime;
        var code = totp.ComputeTotp(timestamp);
        var remaining = totp.RemainingSeconds(timestamp);
        return new TotpCode { Code = code, SecondsRemaining = remaining };
    }
}
