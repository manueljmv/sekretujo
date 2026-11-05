namespace Sekretujo.Core.Services;

public enum ExpiryStatus
{
    Valid,
    Expired,
    InvalidFormat
}

public static class CardInfo
{
    private const string MaskedGroup = "••••";

    public static string Digits(string number) => new(number.Where(char.IsAsciiDigit).ToArray());

    public static string LastFour(string number)
    {
        var digits = Digits(number);
        return digits.Length < 4 ? string.Empty : digits[^4..];
    }

    public static string MaskedNumber(string number)
    {
        var lastFour = LastFour(number);
        return $"{MaskedGroup} {MaskedGroup} {MaskedGroup} {(lastFour.Length == 0 ? MaskedGroup : lastFour)}";
    }

    public static bool IsValidNumber(string number)
    {
        var digits = Digits(number);
        if (digits.Length is < 12 or > 19) return false;

        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var digit = digits[digits.Length - 1 - i] - '0';
            if (i % 2 == 1)
            {
                digit *= 2;
                if (digit > 9) digit -= 9;
            }
            sum += digit;
        }
        return sum % 10 == 0;
    }

    public static ExpiryStatus GetExpiryStatus(string expiry, DateOnly today)
    {
        var parts = new string(expiry.Where(c => !char.IsWhiteSpace(c)).ToArray()).Split('/');
        if (parts.Length != 2
            || parts[0].Length is < 1 or > 2 || !parts[0].All(char.IsAsciiDigit)
            || parts[1].Length is not (2 or 4) || !parts[1].All(char.IsAsciiDigit))
            return ExpiryStatus.InvalidFormat;

        var month = int.Parse(parts[0]);
        if (month is < 1 or > 12) return ExpiryStatus.InvalidFormat;
        var year = int.Parse(parts[1]);
        if (parts[1].Length == 2) year += 2000;

        return (year, month).CompareTo((today.Year, today.Month)) < 0 ? ExpiryStatus.Expired : ExpiryStatus.Valid;
    }
}
