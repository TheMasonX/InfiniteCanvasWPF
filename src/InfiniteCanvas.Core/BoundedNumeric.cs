using System.Globalization;

namespace InfiniteCanvas.Core;

/// <summary>
/// The numeric kind that a bounded slider text box edits.
/// </summary>
public enum NumericKind
{
    /// <summary>
    /// The value is a whole number.
    /// </summary>
    Integer,

    /// <summary>
    /// The value is a finite decimal number.
    /// </summary>
    Double
}

/// <summary>
/// Parses, clamps, and formats numeric edits for bounded slider text boxes.
/// </summary>
/// <remarks>
/// This type is the single parse, clamp, and format path shared by the slider
/// and the text box of a <c>SliderTextBox</c>. Keep it free of WPF dependencies
/// so the behavior is unit-testable from the core test project.
/// </remarks>
public static class BoundedNumeric
{
    /// <summary>
    /// Parses and clamps a numeric edit to the configured range.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="kind">The numeric kind.</param>
    /// <param name="minimum">The inclusive minimum value.</param>
    /// <param name="maximum">The inclusive maximum value.</param>
    /// <param name="value">The parsed and clamped value when the result is true.</param>
    /// <returns>True when the text parses for the numeric kind; otherwise false.</returns>
    public static bool TryParse(string? text, NumericKind kind, double minimum, double maximum, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        switch (kind)
        {
            case NumericKind.Integer:
                if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                {
                    return false;
                }

                value = Math.Clamp(intValue, (int)Math.Ceiling(minimum), (int)Math.Floor(maximum));
                return true;
            case NumericKind.Double:
                if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue)
                    || !double.IsFinite(doubleValue))
                {
                    return false;
                }

                value = Math.Clamp(doubleValue, minimum, maximum);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Formats a value for display in the numeric edit box.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <param name="kind">The numeric kind.</param>
    /// <returns>The invariant-culture display text.</returns>
    public static string Format(double value, NumericKind kind)
    {
        return kind == NumericKind.Integer
            ? ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture)
            : value.ToString(CultureInfo.InvariantCulture);
    }
}
