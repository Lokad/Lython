using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class StringFormatPrimitiveTests
{
    [Fact]
    public void FormatWithDelegates_ServesConversionsNestedSpecsAndSharedNumbering()
    {
        var captured = new List<(object Value, char? Conversion, string? Spec)>();

        PyString FormatStub(object value, char? conversion, string? spec)
        {
            captured.Add((value, conversion, spec));
            var rendered = value switch
            {
                PyString text => text.AsString(),
                PyNone => "None",
                _ => value.ToString() ?? "None",
            };

            return conversion is null && spec is null
                ? PyString.FromString(rendered)
                : PyString.FromString($"<{rendered}|{conversion?.ToString() ?? "-"}|{spec ?? "-"}>");
        }

        static object PassThrough(object current, string suffix) => current;

        var manual = PyStringOps.Format(
            PyString.FromString("{1:{0}}|{0!r:>3}"),
            [PyString.FromString("a"), PyString.FromString("b")],
            null,
            PassThrough,
            FormatStub);

        Assert.Equal("<b|-|a>|<a|r|>3>", manual.AsString());
        Assert.Equal(3, captured.Count);
        Assert.Equal("a", ((PyString)captured[0].Value).AsString());
        Assert.Null(captured[0].Conversion);
        Assert.Null(captured[0].Spec);
        Assert.Equal("b", ((PyString)captured[1].Value).AsString());
        Assert.Null(captured[1].Conversion);
        Assert.Equal("a", captured[1].Spec);
        Assert.Equal("a", ((PyString)captured[2].Value).AsString());
        Assert.Equal('r', captured[2].Conversion);
        Assert.Equal(">3", captured[2].Spec);

        captured.Clear();

        var automatic = PyStringOps.Format(
            PyString.FromString("{:{}}"),
            [PyString.FromString("a"), PyString.FromString("b")],
            null,
            PassThrough,
            FormatStub);

        Assert.Equal("<a|-|b>", automatic.AsString());
        Assert.Equal(2, captured.Count);
        Assert.Equal("b", ((PyString)captured[0].Value).AsString());
        Assert.Equal("a", ((PyString)captured[1].Value).AsString());
        Assert.Equal("b", captured[1].Spec);
    }

    [Fact]
    public void FormatFieldSuffix_CarriesTrailingContentPastResolvedItems()
    {
        string? seenSuffix = null;

        var formatted = PyStringOps.Format(
            PyString.FromString("{0[a]b}"),
            [new Dictionary<string, object>(StringComparer.Ordinal) { ["a"] = new BigInteger(1) }],
            null,
            (current, suffix) =>
            {
                seenSuffix = suffix;
                return current;
            },
            (value, conversion, spec) => PyString.FromString("!"));

        Assert.Equal("!", formatted.AsString());
        Assert.Equal("[a]b", seenSuffix);
    }

    [Fact]
    public void FormatWithDelegates_RejectsPositionalFieldsForMappings()
    {
        var keywords = new Dictionary<string, object>(StringComparer.Ordinal) { ["name"] = "x" };

        var named = PyStringOps.Format(
            PyString.FromString("{name:>3}"),
            [],
            keywords,
            (current, suffix) => current,
            (value, conversion, spec) => PyString.FromString("!"));

        Assert.Equal("!", named.AsString());

        foreach (var template in new[] { "{}", "{0}" })
        {
            var failure = Assert.Throws<InvalidOperationException>(() => PyStringOps.Format(
                PyString.FromString(template),
                [],
                keywords,
                (current, suffix) => current,
                (value, conversion, spec) => PyString.FromString("!"),
                forbidPositionalFields: true));

            Assert.Equal("Format string contains positional fields", failure.Message);
        }
    }

    [Fact]
    public void FormatFieldSplitting_ReportsPythonShapedStructuralFailures()
    {
        var digits = Assert.Throws<InvalidOperationException>(() => PyStringOps.Format(
            PyString.FromString("{" + new string('9', 19) + "}"),
            [new BigInteger(1)]));

        Assert.Equal("Too many decimal digits in format string", digits.Message);

        var conversion = Assert.Throws<InvalidOperationException>(() => PyStringOps.Format(
            PyString.FromString("{0!{}}"),
            [new BigInteger(1)],
            null,
            (current, suffix) => current,
            (value, conversion, spec) => PyString.FromString("!")));

        Assert.Equal("Unknown conversion specifier {", conversion.Message);

        var recursion = Assert.Throws<InvalidOperationException>(() => PyStringOps.Format(
            PyString.FromString("{0:{1:{2}}}"),
            [PyString.FromString("ab"), new BigInteger(5), new BigInteger(6)],
            null,
            (current, suffix) => current,
            (value, conversion, spec) => PyString.FromString("!")));

        Assert.Equal("Max string recursion exceeded", recursion.Message);
    }
}