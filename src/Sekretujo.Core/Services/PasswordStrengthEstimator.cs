using System.Linq;

namespace Sekretujo.Core.Services;

public enum PasswordStrength
{
    VeryWeak,
    Weak,
    Medium,
    Strong
}

public sealed class PasswordStrengthEstimator
{
    public PasswordStrength Estimate(string password)
    {
        if (string.IsNullOrEmpty(password))
            return PasswordStrength.VeryWeak;

        var varietyCount = 0;
        if (password.Any(char.IsUpper)) varietyCount++;
        if (password.Any(char.IsLower)) varietyCount++;
        if (password.Any(char.IsDigit)) varietyCount++;
        if (password.Any(c => !char.IsLetterOrDigit(c))) varietyCount++;

        var lengthScore = password.Length switch
        {
            < 8 => 0,
            < 12 => 1,
            < 16 => 2,
            _ => 3
        };
        var score = lengthScore + (varietyCount >= 3 ? 1 : 0);

        return score switch
        {
            <= 1 => PasswordStrength.VeryWeak,
            2 => PasswordStrength.Weak,
            3 => PasswordStrength.Medium,
            _ => PasswordStrength.Strong
        };
    }
}
