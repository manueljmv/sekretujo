using Sekretujo.Core.Services;
using Xunit;

namespace Sekretujo.Core.Tests;

public class PasswordStrengthEstimatorTests
{
    [Fact]
    public void Estimate_EmptyPassword_IsVeryWeak()
    {
        var estimator = new PasswordStrengthEstimator();

        Assert.Equal(PasswordStrength.VeryWeak, estimator.Estimate(""));
    }

    [Fact]
    public void Estimate_ShortSingleCaseLetters_IsVeryWeak()
    {
        var estimator = new PasswordStrengthEstimator();

        Assert.Equal(PasswordStrength.VeryWeak, estimator.Estimate("abc"));
    }

    [Fact]
    public void Estimate_TwelveCharsSingleCharacterClass_IsWeak()
    {
        var estimator = new PasswordStrengthEstimator();

        Assert.Equal(PasswordStrength.Weak, estimator.Estimate("abcdefghijkl"));
    }

    [Fact]
    public void Estimate_TwelveCharsMixedCase_IsMedium()
    {
        var estimator = new PasswordStrengthEstimator();

        Assert.Equal(PasswordStrength.Medium, estimator.Estimate("Abcdefghij12"));
    }

    [Fact]
    public void Estimate_LongWithAllCharacterClasses_IsStrong()
    {
        var estimator = new PasswordStrengthEstimator();

        Assert.Equal(PasswordStrength.Strong, estimator.Estimate("Abcdefghij1234!@"));
    }
}
