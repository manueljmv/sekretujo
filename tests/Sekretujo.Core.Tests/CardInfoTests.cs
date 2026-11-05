using Sekretujo.Core.Services;
using Xunit;

namespace Sekretujo.Core.Tests;

public class CardInfoTests
{
    private static readonly DateOnly Today = new(2026, 11, 5);

    [Theory]
    [InlineData("4111-1111 1111 1111", "4111111111111111")]
    [InlineData("ab12", "12")]
    public void Digits_KeepsOnlyDigits(string number, string expected) =>
        Assert.Equal(expected, CardInfo.Digits(number));

    [Theory]
    [InlineData("4111 1111 1111 1234", "1234")]
    [InlineData("12", "")]
    public void LastFour_ReturnsLastFourDigitsOrEmpty(string number, string expected) =>
        Assert.Equal(expected, CardInfo.LastFour(number));

    [Theory]
    [InlineData("4111111111111234", "•••• •••• •••• 1234")]
    [InlineData("", "•••• •••• •••• ••••")]
    public void MaskedNumber_ShowsOnlyLastFour(string number, string expected) =>
        Assert.Equal(expected, CardInfo.MaskedNumber(number));

    [Theory]
    [InlineData("4111 1111 1111 1111", true)]
    [InlineData("4111-1111-1111-1111", true)]
    [InlineData("4111 1111 1111 1112", false)]
    [InlineData("0000", false)]
    [InlineData("00000000000000000000", false)]
    public void IsValidNumber_RequiresLengthAndLuhn(string number, bool expected) =>
        Assert.Equal(expected, CardInfo.IsValidNumber(number));

    [Theory]
    [InlineData("11/26", ExpiryStatus.Valid)]
    [InlineData("8/28", ExpiryStatus.Valid)]
    [InlineData("08/2028", ExpiryStatus.Valid)]
    [InlineData("08 / 28", ExpiryStatus.Valid)]
    [InlineData("10/26", ExpiryStatus.Expired)]
    [InlineData("10/2026", ExpiryStatus.Expired)]
    [InlineData("13/28", ExpiryStatus.InvalidFormat)]
    [InlineData("00/28", ExpiryStatus.InvalidFormat)]
    [InlineData("0828", ExpiryStatus.InvalidFormat)]
    [InlineData("08/2", ExpiryStatus.InvalidFormat)]
    [InlineData("", ExpiryStatus.InvalidFormat)]
    public void GetExpiryStatus_ClassifiesExpiry(string expiry, ExpiryStatus expected) =>
        Assert.Equal(expected, CardInfo.GetExpiryStatus(expiry, Today));
}
