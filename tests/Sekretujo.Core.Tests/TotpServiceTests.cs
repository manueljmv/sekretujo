using Sekretujo.Core.Services;
using Xunit;

namespace Sekretujo.Core.Tests;

public class TotpServiceTests
{
    private const string ValidSecret = "JBSWY3DPEHPK3PXP";

    [Fact]
    public void GetCode_ReturnsSixDigitCode()
    {
        var service = new TotpService();

        var result = service.GetCode(ValidSecret, DateTimeOffset.UtcNow);

        Assert.Equal(6, result.Code.Length);
        Assert.All(result.Code, c => Assert.True(char.IsDigit(c)));
    }

    [Fact]
    public void GetCode_IsDeterministic_ForSameSecretAndTimestamp()
    {
        var service = new TotpService();
        var at = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var first = service.GetCode(ValidSecret, at);
        var second = service.GetCode(ValidSecret, at);

        Assert.Equal(first.Code, second.Code);
    }

    [Fact]
    public void GetCode_DiffersAcrossTimeWindows()
    {
        var service = new TotpService();
        var windowOne = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var windowTwo = windowOne.AddSeconds(60);

        var first = service.GetCode(ValidSecret, windowOne);
        var second = service.GetCode(ValidSecret, windowTwo);

        Assert.NotEqual(first.Code, second.Code);
    }

    [Fact]
    public void GetCode_WithInvalidBase32Secret_ThrowsInvalidTotpSecretException()
    {
        var service = new TotpService();

        Assert.Throws<InvalidTotpSecretException>(() => service.GetCode("not-valid-base32!!"));
    }

    [Fact]
    public void GetCode_WithSecretThatDecodesTooShort_ThrowsInvalidTotpSecretException()
    {
        var service = new TotpService();

        Assert.Throws<InvalidTotpSecretException>(() => service.GetCode("A"));
    }
}
