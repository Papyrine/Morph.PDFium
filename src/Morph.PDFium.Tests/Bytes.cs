/// <summary>
/// Exact comparisons of byte arrays: renders, saves, PNGs and pixels. TUnit's IsEquivalentTo
/// ignores order by default, so it passes for the same bytes in any order: an RGBA image
/// against its BGRA twin, or rows reversed. It is also slow on large arrays.
/// </summary>
static class Bytes
{
    public static async Task AreEqual(byte[] actual, byte[] expected)
    {
        await Assert.That(actual.Length).IsEqualTo(expected.Length);
        await Assert.That(FirstDifference(actual, expected)).IsEqualTo(-1);
    }

    public static async Task AreDifferent(byte[] actual, byte[] unexpected) =>
        await Assert.That(actual.AsSpan().SequenceEqual(unexpected)).IsFalse();

    // The index of the first byte that differs, or -1 when the arrays are the same.
    static int FirstDifference(byte[] actual, byte[] expected)
    {
        var common = actual.AsSpan().CommonPrefixLength(expected);
        if (common == actual.Length && common == expected.Length)
        {
            return -1;
        }

        return common;
    }
}
