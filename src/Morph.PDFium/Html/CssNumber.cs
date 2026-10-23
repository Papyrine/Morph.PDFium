using System.Globalization;

/// <summary>
/// A number as HTML and CSS write it: rounded to two decimals, with no trailing zeros and in
/// the invariant culture. Formatted straight into the builder or string it is interpolated
/// into, rather than through a string of its own.
/// </summary>
readonly struct CssNumber : ISpanFormattable
{
    readonly double value;

    // Two decimals of a point is well below a pixel at any sensible zoom.
    public CssNumber(double value)
    {
        var rounded = Math.Round(value, 2);
        // Also turns -0, which would print as "-0", into 0.
        if (rounded == 0)
        {
            rounded = 0;
        }

        this.value = rounded;
    }

    public bool IsZero => value == 0;

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        value.TryFormat(destination, out charsWritten, "0.##", CultureInfo.InvariantCulture);

    public string ToString(string? format, IFormatProvider? provider) =>
        ToString();

    public override string ToString() =>
        value.ToString("0.##", CultureInfo.InvariantCulture);
}
