using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class StringFormatPrimitiveTests
{
    // Keep primitive error/golden assertions on the production async API.
    private static Task<PyString> Format(PyString template, IReadOnlyList<object> positional,
        IReadOnlyDictionary<string, object>? keywords, Func<object, string, ValueTask<object>>? suffix,
        Func<GovernedByteBuilder, object, char?, string?, ValueTask>? append,
        MemoryGovernor? governor, LythonSourceSpan? span, bool forbidPositionalFields = false)
        => PyStringOps.FormatAsync(template, positional, keywords, suffix, append, governor, span,
            forbidPositionalFields).AsTask();

    [Fact]
    public async Task FormatWithDelegates_ServesConversionsNestedSpecsAndSharedNumbering()
    {
        var captured = new List<(object Value, char? Conversion, string? Spec)>();

        ValueTask FormatStub(GovernedByteBuilder output, object value, char? conversion, string? spec)
        {
            captured.Add((value, conversion, spec));
            var rendered = value switch
            {
                PyString text => text.AsString(),
                PyNone => "None",
                _ => value.ToString() ?? "None",
            };

            output.AppendString(conversion is null && spec is null
                ? rendered : $"<{rendered}|{conversion?.ToString() ?? "-"}|{spec ?? "-"}>");
            return default;
        }

        static ValueTask<object> PassThrough(object current, string suffix) => new(current);

        var manual = await Format(
            PyString.FromString("{1:{0}}|{0!r:>3}"),
            [PyString.FromString("a"), PyString.FromString("b")],
            null,
            PassThrough,
            FormatStub, governor: null, span: null);

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

        var automatic = await Format(
            PyString.FromString("{:{}}"),
            [PyString.FromString("a"), PyString.FromString("b")],
            null,
            PassThrough,
            FormatStub, governor: null, span: null);

        Assert.Equal("<a|-|b>", automatic.AsString());
        Assert.Equal(2, captured.Count);
        Assert.Equal("b", ((PyString)captured[0].Value).AsString());
        Assert.Equal("a", ((PyString)captured[1].Value).AsString());
        Assert.Equal("b", captured[1].Spec);
    }

    [Fact]
    public async Task FormatFieldSuffix_CarriesTrailingContentPastResolvedItems()
    {
        string? seenSuffix = null;

        var formatted = await Format(
            PyString.FromString("{0[a]b}"),
            [new Dictionary<string, object>(StringComparer.Ordinal) { ["a"] = new BigInteger(1) }],
            null,
            (current, suffix) =>
            {
                seenSuffix = suffix;
                return new ValueTask<object>(current);
            },
            (output, value, conversion, spec) => { output.AppendString("!"); return default; }, governor: null, span: null);

        Assert.Equal("!", formatted.AsString());
        Assert.Equal("[a]b", seenSuffix);
    }

    [Fact]
    public async Task FormatWithDelegates_RejectsPositionalFieldsForMappings()
    {
        var keywords = new Dictionary<string, object>(StringComparer.Ordinal) { ["name"] = "x" };

        var named = await Format(
            PyString.FromString("{name:>3}"),
            [],
            keywords,
            (current, suffix) => new(current),
            (output, value, conversion, spec) => { output.AppendString("!"); return default; }, governor: null, span: null);

        Assert.Equal("!", named.AsString());

        foreach (var template in new[] { "{}", "{0}" })
        {
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => Format(
                PyString.FromString(template),
                [],
                keywords,
                (current, suffix) => new(current),
                (output, value, conversion, spec) => { output.AppendString("!"); return default; }, governor: null, span: null,
                forbidPositionalFields: true));

            Assert.Equal("Format string contains positional fields", failure.Message);
        }
    }

    [Fact]
    public async Task FormatFieldSplitting_ReportsPythonShapedStructuralFailures()
    {
        var digits = await Assert.ThrowsAsync<InvalidOperationException>(() => Format(
            PyString.FromString("{" + new string('9', 19) + "}"),
            [new BigInteger(1)], null, null, null, governor: null, span: null));

        Assert.Equal("Too many decimal digits in format string", digits.Message);

        var conversion = await Assert.ThrowsAsync<InvalidOperationException>(() => Format(
            PyString.FromString("{0!{}}"),
            [new BigInteger(1)],
            null,
            (current, suffix) => new(current),
            (output, value, conversion, spec) => { output.AppendString("!"); return default; }, governor: null, span: null));

        Assert.Equal("Unknown conversion specifier {", conversion.Message);

        var recursion = await Assert.ThrowsAsync<InvalidOperationException>(() => Format(
            PyString.FromString("{0:{1:{2}}}"),
            [PyString.FromString("ab"), new BigInteger(5), new BigInteger(6)],
            null,
            (current, suffix) => new(current),
            (output, value, conversion, spec) => { output.AppendString("!"); return default; }, governor: null, span: null));

        Assert.Equal("Max string recursion exceeded", recursion.Message);
    }
}
