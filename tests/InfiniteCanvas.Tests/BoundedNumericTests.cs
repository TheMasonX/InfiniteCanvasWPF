using InfiniteCanvas.Core;

namespace InfiniteCanvas.Tests;

[TestFixture]
public class BoundedNumericTests
{
    [TestCase("7", 0, 8, 7)]
    [TestCase("8", 0, 8, 8)]
    [TestCase("12", 0, 8, 8)]
    [TestCase("-3", 0, 8, 0)]
    [TestCase("1", 1, 2000, 1)]
    [TestCase("2000", 1, 2000, 2000)]
    [TestCase("2147483647", 0, int.MaxValue, 2147483647)]
    [TestCase("-1", 0, int.MaxValue, 0)]
    public void Integer_TryParse_ClampsToRange(string text, double minimum, double maximum, int expected)
    {
        var parsed = BoundedNumeric.TryParse(text, NumericKind.Integer, minimum, maximum, out var value);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(value, Is.EqualTo((double)expected));
        });
    }

    [TestCase("5.5", 0, 8)]
    [TestCase("5,5", 0, 8)]
    [TestCase("", 0, 8)]
    [TestCase("   ", 0, 8)]
    [TestCase("abc", 0, 8)]
    [TestCase("2147483648", 0, int.MaxValue)]
    public void Integer_TryParse_RejectsInvalidInput(string text, double minimum, double maximum)
    {
        Assert.That(BoundedNumeric.TryParse(text, NumericKind.Integer, minimum, maximum, out _), Is.False);
    }

    [TestCase("0.5", 0, 1, 0.5)]
    [TestCase("1.5", 0, 1, 1)]
    [TestCase("-0.5", 0, 1, 0)]
    [TestCase("0.05", 0.01, 8, 0.05)]
    [TestCase("8", 0.01, 8, 8)]
    [TestCase("2.5", 0.1, 8, 2.5)]
    [TestCase("0.6", 0, 1, 0.6)]
    public void Double_TryParse_ClampsToRange(string text, double minimum, double maximum, double expected)
    {
        var parsed = BoundedNumeric.TryParse(text, NumericKind.Double, minimum, maximum, out var value);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(value, Is.EqualTo(expected));
        });
    }

    [TestCase("NaN", 0, 1)]
    [TestCase("Infinity", 0, 1)]
    [TestCase("-Infinity", 0, 1)]
    [TestCase("", 0, 1)]
    [TestCase("   ", 0, 1)]
    [TestCase("abc", 0, 1)]
    [TestCase("1,5", 0, 8)]
    public void Double_TryParse_RejectsInvalidInput(string text, double minimum, double maximum)
    {
        Assert.That(BoundedNumeric.TryParse(text, NumericKind.Double, minimum, maximum, out _), Is.False);
    }

    [TestCase(7.4, "7")]
    [TestCase(7.6, "8")]
    [TestCase(5, "5")]
    public void Format_Integer_RoundsAndUsesInvariantCulture(double value, string expected)
    {
        Assert.That(BoundedNumeric.Format(value, NumericKind.Integer), Is.EqualTo(expected));
    }

    [TestCase(2.5, "2.5")]
    [TestCase(0.6, "0.6")]
    [TestCase(1, "1")]
    [TestCase(0.05, "0.05")]
    public void Format_Double_UsesInvariantCultureShortestForm(double value, string expected)
    {
        Assert.That(BoundedNumeric.Format(value, NumericKind.Double), Is.EqualTo(expected));
    }
}
