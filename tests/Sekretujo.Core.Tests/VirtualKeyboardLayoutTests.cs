using Sekretujo.Core.Services;
using Xunit;

namespace Sekretujo.Core.Tests;

public class VirtualKeyboardLayoutTests
{
    [Fact]
    public void CreateStandard_ReturnsFortySevenKeys()
    {
        var keys = VirtualKeyboardLayout.CreateStandard();
        Assert.Equal(47, keys.Count);
    }

    [Fact]
    public void CreateStandard_HasNoDuplicateNormalCharacters()
    {
        var keys = VirtualKeyboardLayout.CreateStandard();
        Assert.Equal(keys.Count, keys.Select(k => k.Normal).Distinct().Count());
    }

    [Fact]
    public void Shuffle_WithSeededRandom_PermutesButKeepsSameKeys()
    {
        var original = VirtualKeyboardLayout.CreateStandard();
        var shuffled = VirtualKeyboardLayout.CreateStandard();

        VirtualKeyboardLayout.Shuffle(shuffled, new Random(42));

        Assert.Equal(
            original.OrderBy(k => k.Normal).Select(k => k.Normal),
            shuffled.OrderBy(k => k.Normal).Select(k => k.Normal));
        Assert.NotEqual(
            original.Select(k => k.Normal).ToList(),
            shuffled.Select(k => k.Normal).ToList());
    }
}
