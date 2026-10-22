namespace Sekretujo.Core.Services;

public sealed record VirtualKeyboardKey(string Normal, string Shifted, int Row);

public static class VirtualKeyboardLayout
{
    public static List<VirtualKeyboardKey> CreateStandard() =>
    [
        new("`", "~", 0), new("1", "!", 0), new("2", "@", 0), new("3", "#", 0), new("4", "$", 0),
        new("5", "%", 0), new("6", "^", 0), new("7", "&", 0), new("8", "*", 0), new("9", "(", 0),
        new("0", ")", 0), new("-", "_", 0), new("=", "+", 0),

        new("q", "Q", 1), new("w", "W", 1), new("e", "E", 1), new("r", "R", 1), new("t", "T", 1),
        new("y", "Y", 1), new("u", "U", 1), new("i", "I", 1), new("o", "O", 1), new("p", "P", 1),
        new("[", "{", 1), new("]", "}", 1), new("\\", "|", 1),

        new("a", "A", 2), new("s", "S", 2), new("d", "D", 2), new("f", "F", 2), new("g", "G", 2),
        new("h", "H", 2), new("j", "J", 2), new("k", "K", 2), new("l", "L", 2), new(";", ":", 2),
        new("'", "\"", 2),

        new("z", "Z", 3), new("x", "X", 3), new("c", "C", 3), new("v", "V", 3), new("b", "B", 3),
        new("n", "N", 3), new("m", "M", 3), new(",", "<", 3), new(".", ">", 3), new("/", "?", 3),
    ];

    public static void Shuffle(IList<VirtualKeyboardKey> keys, Random? random = null)
    {
        random ??= Random.Shared;
        for (var i = keys.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (keys[i], keys[j]) = (keys[j], keys[i]);
        }
    }
}
