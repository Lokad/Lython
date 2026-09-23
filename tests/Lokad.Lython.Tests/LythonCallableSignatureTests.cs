using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class LythonCallableSignatureTests
{
    [Fact]
    public void Create_ReusesValidatedMetadataForEquivalentShapes()
    {
        var first = LythonCallableSignature.Create("test.cached_signature", ["value"], requiredCount: 0);
        var second = LythonCallableSignature.Create("test.cached_signature", ["value"], requiredCount: 0);
        var keywordOnly = LythonCallableSignature.Create(
            "test.cached_signature",
            ["value"],
            requiredCount: 0,
            maximumPositionalArgumentCount: 0);

        Assert.Same(first, second);
        var firstParameters = Assert.IsType<NamedCallableParameterLayout>(first.Parameters);
        var secondParameters = Assert.IsType<NamedCallableParameterLayout>(second.Parameters);
        Assert.Same(firstParameters.ParameterIndices, secondParameters.ParameterIndices);
        Assert.NotSame(first, keywordOnly);
    }

    [Fact]
    public void Create_ReusesHoistedListFamilySignatures()
    {
        // N17: the hoisted ListMembers statics rely on the interner handing back
        // the same instance for identical factory arguments; pin every hoisted shape
        // so an interner regression cannot silently multiply per-resolution instances.
        Assert.Same(
            LythonCallableSignature.Create("list.append", ["value"]),
            LythonCallableSignature.Create("list.append", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("list.extend", ["iterable"]),
            LythonCallableSignature.Create("list.extend", ["iterable"]));
        Assert.Same(
            LythonCallableSignature.Create("list.index", ["value", "start", "stop"], 1),
            LythonCallableSignature.Create("list.index", ["value", "start", "stop"], 1));
        Assert.Same(
            LythonCallableSignature.Create("list.count", ["value"]),
            LythonCallableSignature.Create("list.count", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("list.insert", ["index", "value"]),
            LythonCallableSignature.Create("list.insert", ["index", "value"]));
        Assert.Same(
            LythonCallableSignature.Create("list.remove", ["value"]),
            LythonCallableSignature.Create("list.remove", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("list.pop", ["index"], 0),
            LythonCallableSignature.Create("list.pop", ["index"], 0));
        Assert.Same(
            LythonCallableSignature.Create("list.sort", ["key", "reverse"], requiredCount: 0, maximumPositionalArgumentCount: 0),
            LythonCallableSignature.Create("list.sort", ["key", "reverse"], requiredCount: 0, maximumPositionalArgumentCount: 0));
        Assert.Same(
            LythonCallableSignature.Create("list.__contains__", ["item"]),
            LythonCallableSignature.Create("list.__contains__", ["item"]));
        Assert.Same(
            LythonCallableSignature.Create("list.__getitem__", ["index"]),
            LythonCallableSignature.Create("list.__getitem__", ["index"]));
        Assert.Same(
            LythonCallableSignature.Create("list.__setitem__", ["index", "value"]),
            LythonCallableSignature.Create("list.__setitem__", ["index", "value"]));
        Assert.Same(
            LythonCallableSignature.Create("list.__delitem__", ["index"]),
            LythonCallableSignature.Create("list.__delitem__", ["index"]));
        Assert.Same(
            LythonCallableSignature.Create("list.__add__", ["value"]),
            LythonCallableSignature.Create("list.__add__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("list.__mul__", ["value"]),
            LythonCallableSignature.Create("list.__mul__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("list.__rmul__", ["value"]),
            LythonCallableSignature.Create("list.__rmul__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("list.__eq__", ["value"]),
            LythonCallableSignature.Create("list.__eq__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("list.__ne__", ["value"]),
            LythonCallableSignature.Create("list.__ne__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("list.__lt__", ["value"]),
            LythonCallableSignature.Create("list.__lt__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("list.__le__", ["value"]),
            LythonCallableSignature.Create("list.__le__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("list.__gt__", ["value"]),
            LythonCallableSignature.Create("list.__gt__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("list.__ge__", ["value"]),
            LythonCallableSignature.Create("list.__ge__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("list.__reversed__"),
            LythonCallableSignature.Create("list.__reversed__"));
    }

    [Fact]
    public void Create_ReusesHoistedNumericAndHashSignatures()
    {
        // N17: companion pin for the hoisted Int/FloatMembers statics and the
        // remaining Create-based __hash__ methods (tuple/None/range).
        Assert.Same(
            LythonCallableSignature.Create("int.bit_length"),
            LythonCallableSignature.Create("int.bit_length"));
        Assert.Same(
            LythonCallableSignature.Create("int.bit_count"),
            LythonCallableSignature.Create("int.bit_count"));
        Assert.Same(
            LythonCallableSignature.Create("int.conjugate"),
            LythonCallableSignature.Create("int.conjugate"));
        Assert.Same(
            LythonCallableSignature.Create("int.as_integer_ratio"),
            LythonCallableSignature.Create("int.as_integer_ratio"));
        Assert.Same(
            LythonCallableSignature.Create("int.is_integer"),
            LythonCallableSignature.Create("int.is_integer"));
        Assert.Same(
            LythonCallableSignature.Create("int.__bool__"),
            LythonCallableSignature.Create("int.__bool__"));
        Assert.Same(
            LythonCallableSignature.Create("int.__hash__"),
            LythonCallableSignature.Create("int.__hash__"));
        Assert.Same(
            LythonCallableSignature.Create("int.__int__"),
            LythonCallableSignature.Create("int.__int__"));
        Assert.Same(
            LythonCallableSignature.Create("int.__index__"),
            LythonCallableSignature.Create("int.__index__"));
        Assert.Same(
            LythonCallableSignature.Create("int.__trunc__"),
            LythonCallableSignature.Create("int.__trunc__"));
        Assert.Same(
            LythonCallableSignature.Create("int.__floor__"),
            LythonCallableSignature.Create("int.__floor__"));
        Assert.Same(
            LythonCallableSignature.Create("int.__ceil__"),
            LythonCallableSignature.Create("int.__ceil__"));
        Assert.Same(
            LythonCallableSignature.Create("int.__float__"),
            LythonCallableSignature.Create("int.__float__"));
        Assert.Same(
            LythonCallableSignature.Create("int.__round__"),
            LythonCallableSignature.Create("int.__round__"));
        Assert.Same(
            LythonCallableSignature.Create("int.__eq__", ["value"]),
            LythonCallableSignature.Create("int.__eq__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("int.__ne__", ["value"]),
            LythonCallableSignature.Create("int.__ne__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("int.__lt__", ["value"]),
            LythonCallableSignature.Create("int.__lt__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("int.__le__", ["value"]),
            LythonCallableSignature.Create("int.__le__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("int.__gt__", ["value"]),
            LythonCallableSignature.Create("int.__gt__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("int.__ge__", ["value"]),
            LythonCallableSignature.Create("int.__ge__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("float.conjugate"),
            LythonCallableSignature.Create("float.conjugate"));
        Assert.Same(
            LythonCallableSignature.Create("float.as_integer_ratio"),
            LythonCallableSignature.Create("float.as_integer_ratio"));
        Assert.Same(
            LythonCallableSignature.Create("float.is_integer"),
            LythonCallableSignature.Create("float.is_integer"));
        Assert.Same(
            LythonCallableSignature.Create("float.hex"),
            LythonCallableSignature.Create("float.hex"));
        Assert.Same(
            LythonCallableSignature.Create("float.__bool__"),
            LythonCallableSignature.Create("float.__bool__"));
        Assert.Same(
            LythonCallableSignature.Create("float.__hash__"),
            LythonCallableSignature.Create("float.__hash__"));
        Assert.Same(
            LythonCallableSignature.Create("float.__int__"),
            LythonCallableSignature.Create("float.__int__"));
        Assert.Same(
            LythonCallableSignature.Create("float.__float__"),
            LythonCallableSignature.Create("float.__float__"));
        Assert.Same(
            LythonCallableSignature.Create("float.__trunc__"),
            LythonCallableSignature.Create("float.__trunc__"));
        Assert.Same(
            LythonCallableSignature.Create("float.__floor__"),
            LythonCallableSignature.Create("float.__floor__"));
        Assert.Same(
            LythonCallableSignature.Create("float.__ceil__"),
            LythonCallableSignature.Create("float.__ceil__"));
        Assert.Same(
            LythonCallableSignature.Create("float.__round__"),
            LythonCallableSignature.Create("float.__round__"));
        Assert.Same(
            LythonCallableSignature.Create("float.__eq__", ["value"]),
            LythonCallableSignature.Create("float.__eq__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("float.__ne__", ["value"]),
            LythonCallableSignature.Create("float.__ne__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("float.__lt__", ["value"]),
            LythonCallableSignature.Create("float.__lt__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("float.__le__", ["value"]),
            LythonCallableSignature.Create("float.__le__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("float.__gt__", ["value"]),
            LythonCallableSignature.Create("float.__gt__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("float.__ge__", ["value"]),
            LythonCallableSignature.Create("float.__ge__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("tuple.__hash__"),
            LythonCallableSignature.Create("tuple.__hash__"));
        Assert.Same(
            LythonCallableSignature.Create("None.__hash__"),
            LythonCallableSignature.Create("None.__hash__"));
        Assert.Same(
            LythonCallableSignature.Create("range.__hash__"),
            LythonCallableSignature.Create("range.__hash__"));
    }

    [Fact]
    public void Create_RejectsInvalidParameterMetadata()
    {
        Assert.Throws<ArgumentException>(() =>
            LythonCallableSignature.Create("test.duplicate_signature", ["value", "value"]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LythonCallableSignature.Create("test.invalid_signature", ["value"], requiredCount: 2));
    }

    [Fact]
    public void Create_ModelsUnnamedAndNamedParametersAsClosedLayouts()
    {
        var positional = LythonCallableSignature.Create("test.positional", requiredCount: 1);
        var named = LythonCallableSignature.Create("test.named", ["value"], requiredCount: 1);

        Assert.IsType<PositionalCallableParameterLayout>(positional.Parameters);
        var namedParameters = Assert.IsType<NamedCallableParameterLayout>(named.Parameters);
        Assert.Equal(1, namedParameters.RequiredCount);
        Assert.True(namedParameters.MaximumArgumentCount.IsBounded);
        Assert.Equal(1, namedParameters.MaximumArgumentCount.Maximum);
    }
}
