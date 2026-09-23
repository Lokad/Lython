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
    public void Create_ReusesHoistedDictSignatures()
    {
        // N17: companion pin for the hoisted DictMembers statics.
        Assert.Same(
            LythonCallableSignature.Create("dict.get", ["key", "default"], 1),
            LythonCallableSignature.Create("dict.get", ["key", "default"], 1));
        Assert.Same(
            LythonCallableSignature.Create("dict.pop", ["key", "default"], 1),
            LythonCallableSignature.Create("dict.pop", ["key", "default"], 1));
        Assert.Same(
            LythonCallableSignature.Create("dict.setdefault", ["key", "default"], 1),
            LythonCallableSignature.Create("dict.setdefault", ["key", "default"], 1));
        Assert.Same(
            LythonCallableSignature.Create("dict.__contains__", ["item"]),
            LythonCallableSignature.Create("dict.__contains__", ["item"]));
        Assert.Same(
            LythonCallableSignature.Create("dict.__getitem__", ["index"]),
            LythonCallableSignature.Create("dict.__getitem__", ["index"]));
        Assert.Same(
            LythonCallableSignature.Create("dict.__setitem__", ["index", "value"]),
            LythonCallableSignature.Create("dict.__setitem__", ["index", "value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict.__delitem__", ["index"]),
            LythonCallableSignature.Create("dict.__delitem__", ["index"]));
        Assert.Same(
            LythonCallableSignature.Create("dict.__or__", ["value"]),
            LythonCallableSignature.Create("dict.__or__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict.__ror__", ["value"]),
            LythonCallableSignature.Create("dict.__ror__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict.__ior__", ["value"]),
            LythonCallableSignature.Create("dict.__ior__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict.__eq__", ["value"]),
            LythonCallableSignature.Create("dict.__eq__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict.__ne__", ["value"]),
            LythonCallableSignature.Create("dict.__ne__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict.__lt__", ["value"]),
            LythonCallableSignature.Create("dict.__lt__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict.__le__", ["value"]),
            LythonCallableSignature.Create("dict.__le__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict.__gt__", ["value"]),
            LythonCallableSignature.Create("dict.__gt__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict.__ge__", ["value"]),
            LythonCallableSignature.Create("dict.__ge__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict.__reversed__"),
            LythonCallableSignature.Create("dict.__reversed__"));
    }

    [Fact]
    public void Create_ReusesHoistedCounterSignatures()
    {
        // N17: companion pin for the hoisted CounterMembers statics.
        Assert.Same(
            LythonCallableSignature.Create("Counter.get", ["key", "default"], 1),
            LythonCallableSignature.Create("Counter.get", ["key", "default"], 1));
        Assert.Same(
            LythonCallableSignature.Create("Counter.most_common", ["n"], 0),
            LythonCallableSignature.Create("Counter.most_common", ["n"], 0));
        Assert.Same(
            LythonCallableSignature.Create("Counter.pop", ["key", "default"], 1),
            LythonCallableSignature.Create("Counter.pop", ["key", "default"], 1));
        Assert.Same(
            LythonCallableSignature.Create("Counter.__contains__", ["item"]),
            LythonCallableSignature.Create("Counter.__contains__", ["item"]));
        Assert.Same(
            LythonCallableSignature.Create("Counter.__getitem__", ["index"]),
            LythonCallableSignature.Create("Counter.__getitem__", ["index"]));
        Assert.Same(
            LythonCallableSignature.Create("Counter.__setitem__", ["index", "value"]),
            LythonCallableSignature.Create("Counter.__setitem__", ["index", "value"]));
        Assert.Same(
            LythonCallableSignature.Create("Counter.__delitem__", ["index"]),
            LythonCallableSignature.Create("Counter.__delitem__", ["index"]));
        Assert.Same(
            LythonCallableSignature.Create("Counter.__or__", ["value"]),
            LythonCallableSignature.Create("Counter.__or__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("Counter.__and__", ["value"]),
            LythonCallableSignature.Create("Counter.__and__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("Counter.__sub__", ["value"]),
            LythonCallableSignature.Create("Counter.__sub__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("Counter.__ror__", ["value"]),
            LythonCallableSignature.Create("Counter.__ror__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("Counter.__ior__", ["value"]),
            LythonCallableSignature.Create("Counter.__ior__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("Counter.__iand__", ["value"]),
            LythonCallableSignature.Create("Counter.__iand__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("Counter.__eq__", ["value"]),
            LythonCallableSignature.Create("Counter.__eq__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("Counter.__ne__", ["value"]),
            LythonCallableSignature.Create("Counter.__ne__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("Counter.__lt__", ["value"]),
            LythonCallableSignature.Create("Counter.__lt__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("Counter.__le__", ["value"]),
            LythonCallableSignature.Create("Counter.__le__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("Counter.__gt__", ["value"]),
            LythonCallableSignature.Create("Counter.__gt__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("Counter.__ge__", ["value"]),
            LythonCallableSignature.Create("Counter.__ge__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("Counter.__reversed__"),
            LythonCallableSignature.Create("Counter.__reversed__"));
    }

    [Fact]
    public void Create_ReusesHoistedTupleRangeNoneSignatures()
    {
        // N17: companion pin for the hoisted Tuple/Range/NoneMembers statics.
        Assert.Same(
            LythonCallableSignature.Create("tuple.__contains__", ["item"]),
            LythonCallableSignature.Create("tuple.__contains__", ["item"]));
        Assert.Same(
            LythonCallableSignature.Create("tuple.__getitem__", ["index"]),
            LythonCallableSignature.Create("tuple.__getitem__", ["index"]));
        Assert.Same(
            LythonCallableSignature.Create("tuple.__add__", ["value"]),
            LythonCallableSignature.Create("tuple.__add__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("tuple.__mul__", ["value"]),
            LythonCallableSignature.Create("tuple.__mul__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("tuple.__rmul__", ["value"]),
            LythonCallableSignature.Create("tuple.__rmul__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("tuple.__eq__", ["value"]),
            LythonCallableSignature.Create("tuple.__eq__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("tuple.__ne__", ["value"]),
            LythonCallableSignature.Create("tuple.__ne__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("tuple.__lt__", ["value"]),
            LythonCallableSignature.Create("tuple.__lt__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("tuple.__le__", ["value"]),
            LythonCallableSignature.Create("tuple.__le__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("tuple.__gt__", ["value"]),
            LythonCallableSignature.Create("tuple.__gt__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("tuple.__ge__", ["value"]),
            LythonCallableSignature.Create("tuple.__ge__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("range.index", ["value"]),
            LythonCallableSignature.Create("range.index", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("range.count", ["value"]),
            LythonCallableSignature.Create("range.count", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("range.__contains__", ["item"]),
            LythonCallableSignature.Create("range.__contains__", ["item"]));
        Assert.Same(
            LythonCallableSignature.Create("range.__getitem__", ["index"]),
            LythonCallableSignature.Create("range.__getitem__", ["index"]));
        Assert.Same(
            LythonCallableSignature.Create("range.__eq__", ["value"]),
            LythonCallableSignature.Create("range.__eq__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("range.__ne__", ["value"]),
            LythonCallableSignature.Create("range.__ne__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("range.__lt__", ["value"]),
            LythonCallableSignature.Create("range.__lt__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("range.__le__", ["value"]),
            LythonCallableSignature.Create("range.__le__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("range.__gt__", ["value"]),
            LythonCallableSignature.Create("range.__gt__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("range.__ge__", ["value"]),
            LythonCallableSignature.Create("range.__ge__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("range.__bool__"),
            LythonCallableSignature.Create("range.__bool__"));
        Assert.Same(
            LythonCallableSignature.Create("range.__reversed__"),
            LythonCallableSignature.Create("range.__reversed__"));
        Assert.Same(
            LythonCallableSignature.Create("None.__eq__", ["value"]),
            LythonCallableSignature.Create("None.__eq__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("None.__ne__", ["value"]),
            LythonCallableSignature.Create("None.__ne__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("None.__lt__", ["value"]),
            LythonCallableSignature.Create("None.__lt__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("None.__le__", ["value"]),
            LythonCallableSignature.Create("None.__le__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("None.__gt__", ["value"]),
            LythonCallableSignature.Create("None.__gt__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("None.__ge__", ["value"]),
            LythonCallableSignature.Create("None.__ge__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("None.__bool__"),
            LythonCallableSignature.Create("None.__bool__"));
    }

    [Fact]
    public void Create_ReusesHoistedBytesSignatures()
    {
        // N17: companion pin for the hoisted BytesMembers statics.
        Assert.Same(
            LythonCallableSignature.Create("bytes.decode", ["encoding", "errors"], 0),
            LythonCallableSignature.Create("bytes.decode", ["encoding", "errors"], 0));
        Assert.Same(
            LythonCallableSignature.Create("bytes.expandtabs", ["tabsize"], 0),
            LythonCallableSignature.Create("bytes.expandtabs", ["tabsize"], 0));
        Assert.Same(
            LythonCallableSignature.Create("bytes.split", ["sep", "maxsplit"], 0),
            LythonCallableSignature.Create("bytes.split", ["sep", "maxsplit"], 0));
        Assert.Same(
            LythonCallableSignature.Create("bytes.rsplit", ["sep", "maxsplit"], 0),
            LythonCallableSignature.Create("bytes.rsplit", ["sep", "maxsplit"], 0));
        Assert.Same(
            LythonCallableSignature.Create("bytes.__contains__", ["item"]),
            LythonCallableSignature.Create("bytes.__contains__", ["item"]));
        Assert.Same(
            LythonCallableSignature.Create("bytes.__getitem__", ["index"]),
            LythonCallableSignature.Create("bytes.__getitem__", ["index"]));
        Assert.Same(
            LythonCallableSignature.Create("bytes.__add__", ["value"]),
            LythonCallableSignature.Create("bytes.__add__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("bytes.__mul__", ["value"]),
            LythonCallableSignature.Create("bytes.__mul__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("bytes.__rmul__", ["value"]),
            LythonCallableSignature.Create("bytes.__rmul__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("bytes.__eq__", ["value"]),
            LythonCallableSignature.Create("bytes.__eq__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("bytes.__ne__", ["value"]),
            LythonCallableSignature.Create("bytes.__ne__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("bytes.__lt__", ["value"]),
            LythonCallableSignature.Create("bytes.__lt__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("bytes.__le__", ["value"]),
            LythonCallableSignature.Create("bytes.__le__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("bytes.__gt__", ["value"]),
            LythonCallableSignature.Create("bytes.__gt__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("bytes.__ge__", ["value"]),
            LythonCallableSignature.Create("bytes.__ge__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("bytes.__hash__"),
            LythonCallableSignature.Create("bytes.__hash__"));
    }

    [Fact]
    public void Create_ReusesHoistedDequeSetSignatures()
    {
        // N17: companion pin for the hoisted Deque/SetMembers statics.
        Assert.Same(
            LythonCallableSignature.Create("deque.insert", ["index", "value"]),
            LythonCallableSignature.Create("deque.insert", ["index", "value"]));
        Assert.Same(
            LythonCallableSignature.Create("deque.rotate", ["n"], 0),
            LythonCallableSignature.Create("deque.rotate", ["n"], 0));
        Assert.Same(
            LythonCallableSignature.Create("deque.__contains__", ["item"]),
            LythonCallableSignature.Create("deque.__contains__", ["item"]));
        Assert.Same(
            LythonCallableSignature.Create("deque.__getitem__", ["index"]),
            LythonCallableSignature.Create("deque.__getitem__", ["index"]));
        Assert.Same(
            LythonCallableSignature.Create("deque.__setitem__", ["index", "value"]),
            LythonCallableSignature.Create("deque.__setitem__", ["index", "value"]));
        Assert.Same(
            LythonCallableSignature.Create("deque.__delitem__", ["index"]),
            LythonCallableSignature.Create("deque.__delitem__", ["index"]));
        Assert.Same(
            LythonCallableSignature.Create("deque.__add__", ["value"]),
            LythonCallableSignature.Create("deque.__add__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("deque.__mul__", ["value"]),
            LythonCallableSignature.Create("deque.__mul__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("deque.__rmul__", ["value"]),
            LythonCallableSignature.Create("deque.__rmul__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("deque.__reversed__"),
            LythonCallableSignature.Create("deque.__reversed__"));
        Assert.Same(
            LythonCallableSignature.Create("set.__contains__", ["item"]),
            LythonCallableSignature.Create("set.__contains__", ["item"]));
        Assert.Same(
            LythonCallableSignature.Create("set.__or__", ["value"]),
            LythonCallableSignature.Create("set.__or__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("set.__and__", ["value"]),
            LythonCallableSignature.Create("set.__and__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("set.__sub__", ["value"]),
            LythonCallableSignature.Create("set.__sub__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("set.__xor__", ["value"]),
            LythonCallableSignature.Create("set.__xor__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("set.__ror__", ["value"]),
            LythonCallableSignature.Create("set.__ror__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("set.__rand__", ["value"]),
            LythonCallableSignature.Create("set.__rand__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("set.__rsub__", ["value"]),
            LythonCallableSignature.Create("set.__rsub__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("set.__rxor__", ["value"]),
            LythonCallableSignature.Create("set.__rxor__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("set.__ior__", ["value"]),
            LythonCallableSignature.Create("set.__ior__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("set.__iand__", ["value"]),
            LythonCallableSignature.Create("set.__iand__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("set.__isub__", ["value"]),
            LythonCallableSignature.Create("set.__isub__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("set.__ixor__", ["value"]),
            LythonCallableSignature.Create("set.__ixor__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("set.__eq__", ["value"]),
            LythonCallableSignature.Create("set.__eq__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("set.__ne__", ["value"]),
            LythonCallableSignature.Create("set.__ne__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("set.__lt__", ["value"]),
            LythonCallableSignature.Create("set.__lt__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("set.__le__", ["value"]),
            LythonCallableSignature.Create("set.__le__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("set.__gt__", ["value"]),
            LythonCallableSignature.Create("set.__gt__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("set.__ge__", ["value"]),
            LythonCallableSignature.Create("set.__ge__", ["value"]));
    }

    [Fact]
    public void Create_ReusesHoistedDefaultDictSignatures()
    {
        // N17: companion pin for the hoisted DefaultDictMembers statics.
        Assert.Same(
            LythonCallableSignature.Create("defaultdict.get", ["key", "default"], 1),
            LythonCallableSignature.Create("defaultdict.get", ["key", "default"], 1));
        Assert.Same(
            LythonCallableSignature.Create("defaultdict.pop", ["key", "default"], 1),
            LythonCallableSignature.Create("defaultdict.pop", ["key", "default"], 1));
        Assert.Same(
            LythonCallableSignature.Create("defaultdict.setdefault", ["key", "default"], 1),
            LythonCallableSignature.Create("defaultdict.setdefault", ["key", "default"], 1));
        Assert.Same(
            LythonCallableSignature.Create("defaultdict.__contains__", ["item"]),
            LythonCallableSignature.Create("defaultdict.__contains__", ["item"]));
        Assert.Same(
            LythonCallableSignature.Create("defaultdict.__getitem__", ["index"]),
            LythonCallableSignature.Create("defaultdict.__getitem__", ["index"]));
        Assert.Same(
            LythonCallableSignature.Create("defaultdict.__setitem__", ["index", "value"]),
            LythonCallableSignature.Create("defaultdict.__setitem__", ["index", "value"]));
        Assert.Same(
            LythonCallableSignature.Create("defaultdict.__delitem__", ["index"]),
            LythonCallableSignature.Create("defaultdict.__delitem__", ["index"]));
        Assert.Same(
            LythonCallableSignature.Create("defaultdict.__or__", ["value"]),
            LythonCallableSignature.Create("defaultdict.__or__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("defaultdict.__ror__", ["value"]),
            LythonCallableSignature.Create("defaultdict.__ror__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("defaultdict.__ior__", ["value"]),
            LythonCallableSignature.Create("defaultdict.__ior__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("defaultdict.__eq__", ["value"]),
            LythonCallableSignature.Create("defaultdict.__eq__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("defaultdict.__ne__", ["value"]),
            LythonCallableSignature.Create("defaultdict.__ne__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("defaultdict.__lt__", ["value"]),
            LythonCallableSignature.Create("defaultdict.__lt__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("defaultdict.__le__", ["value"]),
            LythonCallableSignature.Create("defaultdict.__le__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("defaultdict.__gt__", ["value"]),
            LythonCallableSignature.Create("defaultdict.__gt__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("defaultdict.__ge__", ["value"]),
            LythonCallableSignature.Create("defaultdict.__ge__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("defaultdict.__reversed__"),
            LythonCallableSignature.Create("defaultdict.__reversed__"));
    }

    [Fact]
    public void Create_ReusesHoistedDictViewSignatures()
    {
        // N17: companion pin for the hoisted DictViewMembers statics.
        Assert.Same(
            LythonCallableSignature.Create("ChainMap.items.__and__", ["value"]),
            LythonCallableSignature.Create("ChainMap.items.__and__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("ChainMap.items.__contains__", ["item"]),
            LythonCallableSignature.Create("ChainMap.items.__contains__", ["item"]));
        Assert.Same(
            LythonCallableSignature.Create("ChainMap.items.__eq__", ["value"]),
            LythonCallableSignature.Create("ChainMap.items.__eq__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("ChainMap.items.__ge__", ["value"]),
            LythonCallableSignature.Create("ChainMap.items.__ge__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("ChainMap.items.__gt__", ["value"]),
            LythonCallableSignature.Create("ChainMap.items.__gt__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("ChainMap.items.__le__", ["value"]),
            LythonCallableSignature.Create("ChainMap.items.__le__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("ChainMap.items.__lt__", ["value"]),
            LythonCallableSignature.Create("ChainMap.items.__lt__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("ChainMap.items.__ne__", ["value"]),
            LythonCallableSignature.Create("ChainMap.items.__ne__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("ChainMap.items.__or__", ["value"]),
            LythonCallableSignature.Create("ChainMap.items.__or__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("ChainMap.items.__rand__", ["value"]),
            LythonCallableSignature.Create("ChainMap.items.__rand__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("ChainMap.items.__ror__", ["value"]),
            LythonCallableSignature.Create("ChainMap.items.__ror__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("ChainMap.items.__rsub__", ["value"]),
            LythonCallableSignature.Create("ChainMap.items.__rsub__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("ChainMap.items.__rxor__", ["value"]),
            LythonCallableSignature.Create("ChainMap.items.__rxor__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("ChainMap.items.__sub__", ["value"]),
            LythonCallableSignature.Create("ChainMap.items.__sub__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("ChainMap.items.__xor__", ["value"]),
            LythonCallableSignature.Create("ChainMap.items.__xor__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("ChainMap.keys.__and__", ["value"]),
            LythonCallableSignature.Create("ChainMap.keys.__and__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("ChainMap.keys.__contains__", ["item"]),
            LythonCallableSignature.Create("ChainMap.keys.__contains__", ["item"]));
        Assert.Same(
            LythonCallableSignature.Create("ChainMap.keys.__eq__", ["value"]),
            LythonCallableSignature.Create("ChainMap.keys.__eq__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("ChainMap.keys.__ge__", ["value"]),
            LythonCallableSignature.Create("ChainMap.keys.__ge__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("ChainMap.keys.__gt__", ["value"]),
            LythonCallableSignature.Create("ChainMap.keys.__gt__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("ChainMap.keys.__le__", ["value"]),
            LythonCallableSignature.Create("ChainMap.keys.__le__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("ChainMap.keys.__lt__", ["value"]),
            LythonCallableSignature.Create("ChainMap.keys.__lt__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("ChainMap.keys.__ne__", ["value"]),
            LythonCallableSignature.Create("ChainMap.keys.__ne__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("ChainMap.keys.__or__", ["value"]),
            LythonCallableSignature.Create("ChainMap.keys.__or__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("ChainMap.keys.__rand__", ["value"]),
            LythonCallableSignature.Create("ChainMap.keys.__rand__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("ChainMap.keys.__ror__", ["value"]),
            LythonCallableSignature.Create("ChainMap.keys.__ror__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("ChainMap.keys.__rsub__", ["value"]),
            LythonCallableSignature.Create("ChainMap.keys.__rsub__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("ChainMap.keys.__rxor__", ["value"]),
            LythonCallableSignature.Create("ChainMap.keys.__rxor__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("ChainMap.keys.__sub__", ["value"]),
            LythonCallableSignature.Create("ChainMap.keys.__sub__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("ChainMap.keys.__xor__", ["value"]),
            LythonCallableSignature.Create("ChainMap.keys.__xor__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("ChainMap.values.__contains__", ["item"]),
            LythonCallableSignature.Create("ChainMap.values.__contains__", ["item"]));
        Assert.Same(
            LythonCallableSignature.Create("dict_items.__and__", ["value"]),
            LythonCallableSignature.Create("dict_items.__and__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict_items.__contains__", ["item"]),
            LythonCallableSignature.Create("dict_items.__contains__", ["item"]));
        Assert.Same(
            LythonCallableSignature.Create("dict_items.__eq__", ["value"]),
            LythonCallableSignature.Create("dict_items.__eq__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict_items.__ge__", ["value"]),
            LythonCallableSignature.Create("dict_items.__ge__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict_items.__gt__", ["value"]),
            LythonCallableSignature.Create("dict_items.__gt__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict_items.__le__", ["value"]),
            LythonCallableSignature.Create("dict_items.__le__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict_items.__lt__", ["value"]),
            LythonCallableSignature.Create("dict_items.__lt__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict_items.__ne__", ["value"]),
            LythonCallableSignature.Create("dict_items.__ne__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict_items.__or__", ["value"]),
            LythonCallableSignature.Create("dict_items.__or__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict_items.__rand__", ["value"]),
            LythonCallableSignature.Create("dict_items.__rand__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict_items.__reversed__"),
            LythonCallableSignature.Create("dict_items.__reversed__"));
        Assert.Same(
            LythonCallableSignature.Create("dict_items.__ror__", ["value"]),
            LythonCallableSignature.Create("dict_items.__ror__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict_items.__rsub__", ["value"]),
            LythonCallableSignature.Create("dict_items.__rsub__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict_items.__rxor__", ["value"]),
            LythonCallableSignature.Create("dict_items.__rxor__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict_items.__sub__", ["value"]),
            LythonCallableSignature.Create("dict_items.__sub__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict_items.__xor__", ["value"]),
            LythonCallableSignature.Create("dict_items.__xor__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict_keys.__and__", ["value"]),
            LythonCallableSignature.Create("dict_keys.__and__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict_keys.__contains__", ["item"]),
            LythonCallableSignature.Create("dict_keys.__contains__", ["item"]));
        Assert.Same(
            LythonCallableSignature.Create("dict_keys.__eq__", ["value"]),
            LythonCallableSignature.Create("dict_keys.__eq__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict_keys.__ge__", ["value"]),
            LythonCallableSignature.Create("dict_keys.__ge__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict_keys.__gt__", ["value"]),
            LythonCallableSignature.Create("dict_keys.__gt__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict_keys.__le__", ["value"]),
            LythonCallableSignature.Create("dict_keys.__le__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict_keys.__lt__", ["value"]),
            LythonCallableSignature.Create("dict_keys.__lt__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict_keys.__ne__", ["value"]),
            LythonCallableSignature.Create("dict_keys.__ne__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict_keys.__or__", ["value"]),
            LythonCallableSignature.Create("dict_keys.__or__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict_keys.__rand__", ["value"]),
            LythonCallableSignature.Create("dict_keys.__rand__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict_keys.__reversed__"),
            LythonCallableSignature.Create("dict_keys.__reversed__"));
        Assert.Same(
            LythonCallableSignature.Create("dict_keys.__ror__", ["value"]),
            LythonCallableSignature.Create("dict_keys.__ror__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict_keys.__rsub__", ["value"]),
            LythonCallableSignature.Create("dict_keys.__rsub__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict_keys.__rxor__", ["value"]),
            LythonCallableSignature.Create("dict_keys.__rxor__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict_keys.__sub__", ["value"]),
            LythonCallableSignature.Create("dict_keys.__sub__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict_keys.__xor__", ["value"]),
            LythonCallableSignature.Create("dict_keys.__xor__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("dict_values.__reversed__"),
            LythonCallableSignature.Create("dict_values.__reversed__"));
    }

    [Fact]
    public void Create_ReusesHoistedDecimalSignatures()
    {
        // N17: companion pin for the hoisted DecimalMembers statics.
        Assert.Same(
            LythonCallableSignature.Create("Decimal.quantize", ["exp", "rounding", "context"], requiredCount: 1),
            LythonCallableSignature.Create("Decimal.quantize", ["exp", "rounding", "context"], requiredCount: 1));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.normalize", ["context"], requiredCount: 0),
            LythonCallableSignature.Create("Decimal.normalize", ["context"], requiredCount: 0));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.sqrt", ["context"], requiredCount: 0),
            LythonCallableSignature.Create("Decimal.sqrt", ["context"], requiredCount: 0));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.exp", ["context"], requiredCount: 0),
            LythonCallableSignature.Create("Decimal.exp", ["context"], requiredCount: 0));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.ln", ["context"], requiredCount: 0),
            LythonCallableSignature.Create("Decimal.ln", ["context"], requiredCount: 0));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.log10", ["context"], requiredCount: 0),
            LythonCallableSignature.Create("Decimal.log10", ["context"], requiredCount: 0));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.to_integral_value", ["rounding", "context"], requiredCount: 0),
            LythonCallableSignature.Create("Decimal.to_integral_value", ["rounding", "context"], requiredCount: 0));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.to_integral_exact", ["rounding", "context"], requiredCount: 0),
            LythonCallableSignature.Create("Decimal.to_integral_exact", ["rounding", "context"], requiredCount: 0));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.to_integral", ["rounding", "context"], requiredCount: 0),
            LythonCallableSignature.Create("Decimal.to_integral", ["rounding", "context"], requiredCount: 0));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.compare", ["other", "context"], requiredCount: 1),
            LythonCallableSignature.Create("Decimal.compare", ["other", "context"], requiredCount: 1));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.scaleb", ["other", "context"], requiredCount: 1),
            LythonCallableSignature.Create("Decimal.scaleb", ["other", "context"], requiredCount: 1));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.fma", ["other", "third", "context"], requiredCount: 2),
            LythonCallableSignature.Create("Decimal.fma", ["other", "third", "context"], requiredCount: 2));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.logb", ["context"], requiredCount: 0),
            LythonCallableSignature.Create("Decimal.logb", ["context"], requiredCount: 0));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.compare_signal", ["other", "context"], requiredCount: 1),
            LythonCallableSignature.Create("Decimal.compare_signal", ["other", "context"], requiredCount: 1));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.remainder_near", ["other", "context"], requiredCount: 1),
            LythonCallableSignature.Create("Decimal.remainder_near", ["other", "context"], requiredCount: 1));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.min", ["other", "context"], requiredCount: 1),
            LythonCallableSignature.Create("Decimal.min", ["other", "context"], requiredCount: 1));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.max", ["other", "context"], requiredCount: 1),
            LythonCallableSignature.Create("Decimal.max", ["other", "context"], requiredCount: 1));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.min_mag", ["other", "context"], requiredCount: 1),
            LythonCallableSignature.Create("Decimal.min_mag", ["other", "context"], requiredCount: 1));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.max_mag", ["other", "context"], requiredCount: 1),
            LythonCallableSignature.Create("Decimal.max_mag", ["other", "context"], requiredCount: 1));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.as_tuple", []),
            LythonCallableSignature.Create("Decimal.as_tuple", []));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.adjusted", []),
            LythonCallableSignature.Create("Decimal.adjusted", []));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.to_eng_string", []),
            LythonCallableSignature.Create("Decimal.to_eng_string", []));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.radix", []),
            LythonCallableSignature.Create("Decimal.radix", []));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.canonical", []),
            LythonCallableSignature.Create("Decimal.canonical", []));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.conjugate", []),
            LythonCallableSignature.Create("Decimal.conjugate", []));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.__eq__", ["value"]),
            LythonCallableSignature.Create("Decimal.__eq__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.__ne__", ["value"]),
            LythonCallableSignature.Create("Decimal.__ne__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.__lt__", ["value"]),
            LythonCallableSignature.Create("Decimal.__lt__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.__le__", ["value"]),
            LythonCallableSignature.Create("Decimal.__le__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.__gt__", ["value"]),
            LythonCallableSignature.Create("Decimal.__gt__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.__ge__", ["value"]),
            LythonCallableSignature.Create("Decimal.__ge__", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.__bool__"),
            LythonCallableSignature.Create("Decimal.__bool__"));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.__hash__"),
            LythonCallableSignature.Create("Decimal.__hash__"));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.__int__"),
            LythonCallableSignature.Create("Decimal.__int__"));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.__float__"),
            LythonCallableSignature.Create("Decimal.__float__"));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.__trunc__"),
            LythonCallableSignature.Create("Decimal.__trunc__"));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.__floor__"),
            LythonCallableSignature.Create("Decimal.__floor__"));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.__ceil__"),
            LythonCallableSignature.Create("Decimal.__ceil__"));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.__round__"),
            LythonCallableSignature.Create("Decimal.__round__"));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.copy_sign", ["other"]),
            LythonCallableSignature.Create("Decimal.copy_sign", ["other"]));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.compare_total", ["other"]),
            LythonCallableSignature.Create("Decimal.compare_total", ["other"]));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.is_nan", []),
            LythonCallableSignature.Create("Decimal.is_nan", []));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.is_infinite", []),
            LythonCallableSignature.Create("Decimal.is_infinite", []));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.is_finite", []),
            LythonCallableSignature.Create("Decimal.is_finite", []));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.is_zero", []),
            LythonCallableSignature.Create("Decimal.is_zero", []));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.is_signed", []),
            LythonCallableSignature.Create("Decimal.is_signed", []));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.shift", ["other"]),
            LythonCallableSignature.Create("Decimal.shift", ["other"]));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.rotate", ["other"]),
            LythonCallableSignature.Create("Decimal.rotate", ["other"]));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.same_quantum", ["other"]),
            LythonCallableSignature.Create("Decimal.same_quantum", ["other"]));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.as_integer_ratio", []),
            LythonCallableSignature.Create("Decimal.as_integer_ratio", []));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.is_qnan", []),
            LythonCallableSignature.Create("Decimal.is_qnan", []));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.is_snan", []),
            LythonCallableSignature.Create("Decimal.is_snan", []));
        Assert.Same(
            LythonCallableSignature.Create("Decimal.is_canonical", []),
            LythonCallableSignature.Create("Decimal.is_canonical", []));
    }

    [Fact]
    public void Create_ReusesHoistedDualSignatures()
    {
        // N17: dual async twins whose names sit after the async lambda.
        Assert.Same(
            LythonCallableSignature.Create("tuple.index", ["value", "start", "stop"], 1),
            LythonCallableSignature.Create("tuple.index", ["value", "start", "stop"], 1));
        Assert.Same(
            LythonCallableSignature.Create("tuple.count", ["value"]),
            LythonCallableSignature.Create("tuple.count", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("deque.count", ["value"]),
            LythonCallableSignature.Create("deque.count", ["value"]));
        Assert.Same(
            LythonCallableSignature.Create("deque.index", ["value", "start", "stop"], 1),
            LythonCallableSignature.Create("deque.index", ["value", "start", "stop"], 1));
        Assert.Same(
            LythonCallableSignature.Create("deque.remove", ["value"]),
            LythonCallableSignature.Create("deque.remove", ["value"]));
    }

    [Fact]
    public void Create_ReusesHoistedRegexSignatures()
    {
        // N17: companion pin for the hoisted ReMatch/RePatternMembers statics.
        Assert.Same(
            LythonCallableSignature.Create("match.groups", ["default"], 0),
            LythonCallableSignature.Create("match.groups", ["default"], 0));
        Assert.Same(
            LythonCallableSignature.Create("match.groupdict", ["default"], 0),
            LythonCallableSignature.Create("match.groupdict", ["default"], 0));
        Assert.Same(
            LythonCallableSignature.Create("match.expand", ["template"]),
            LythonCallableSignature.Create("match.expand", ["template"]));
        Assert.Same(
            LythonCallableSignature.Create("match.start", ["group"], 0),
            LythonCallableSignature.Create("match.start", ["group"], 0));
        Assert.Same(
            LythonCallableSignature.Create("match.end", ["group"], 0),
            LythonCallableSignature.Create("match.end", ["group"], 0));
        Assert.Same(
            LythonCallableSignature.Create("match.span", ["group"], 0),
            LythonCallableSignature.Create("match.span", ["group"], 0));
        Assert.Same(
            LythonCallableSignature.Create("pattern.search", ["string", "pos", "endpos"], 1),
            LythonCallableSignature.Create("pattern.search", ["string", "pos", "endpos"], 1));
        Assert.Same(
            LythonCallableSignature.Create("pattern.match", ["string", "pos", "endpos"], 1),
            LythonCallableSignature.Create("pattern.match", ["string", "pos", "endpos"], 1));
        Assert.Same(
            LythonCallableSignature.Create("pattern.fullmatch", ["string", "pos", "endpos"], 1),
            LythonCallableSignature.Create("pattern.fullmatch", ["string", "pos", "endpos"], 1));
        Assert.Same(
            LythonCallableSignature.Create("pattern.findall", ["string", "pos", "endpos"], 1),
            LythonCallableSignature.Create("pattern.findall", ["string", "pos", "endpos"], 1));
        Assert.Same(
            LythonCallableSignature.Create("pattern.finditer", ["string", "pos", "endpos"], 1),
            LythonCallableSignature.Create("pattern.finditer", ["string", "pos", "endpos"], 1));
        Assert.Same(
            LythonCallableSignature.Create("pattern.sub", ["repl", "string", "count", "pos", "endpos"], 2),
            LythonCallableSignature.Create("pattern.sub", ["repl", "string", "count", "pos", "endpos"], 2));
        Assert.Same(
            LythonCallableSignature.Create("pattern.subn", ["repl", "string", "count", "pos", "endpos"], 2),
            LythonCallableSignature.Create("pattern.subn", ["repl", "string", "count", "pos", "endpos"], 2));
        Assert.Same(
            LythonCallableSignature.Create("pattern.split", ["string", "maxsplit", "pos", "endpos"], 1),
            LythonCallableSignature.Create("pattern.split", ["string", "maxsplit", "pos", "endpos"], 1));
    }

    [Fact]
    public void Create_ReusesHoistedDateTimeSignatures()
    {
        // N17: companion pin for the hoisted Date/Time/DateTime/Timezone statics.
        Assert.Same(
            LythonCallableSignature.Create("date.__format__", ["format_spec"]),
            LythonCallableSignature.Create("date.__format__", ["format_spec"]));
        Assert.Same(
            LythonCallableSignature.Create("date.strftime", ["format"]),
            LythonCallableSignature.Create("date.strftime", ["format"]));
        Assert.Same(
            LythonCallableSignature.Create("date.replace", ["year", "month", "day"], 0),
            LythonCallableSignature.Create("date.replace", ["year", "month", "day"], 0));
        Assert.Same(
            LythonCallableSignature.Create("time.isoformat", ["timespec"], 0),
            LythonCallableSignature.Create("time.isoformat", ["timespec"], 0));
        Assert.Same(
            LythonCallableSignature.Create("time.__format__", ["format_spec"]),
            LythonCallableSignature.Create("time.__format__", ["format_spec"]));
        Assert.Same(
            LythonCallableSignature.Create("time.strftime", ["format"]),
            LythonCallableSignature.Create("time.strftime", ["format"]));
        Assert.Same(
            LythonCallableSignature.Create("time.replace", ["hour", "minute", "second", "microsecond", "tzinfo", "fold"], 0),
            LythonCallableSignature.Create("time.replace", ["hour", "minute", "second", "microsecond", "tzinfo", "fold"], 0));
        Assert.Same(
            LythonCallableSignature.Create("datetime.astimezone", ["tz"], 0),
            LythonCallableSignature.Create("datetime.astimezone", ["tz"], 0));
        Assert.Same(
            LythonCallableSignature.Create("datetime.isoformat", ["sep", "timespec"], 0),
            LythonCallableSignature.Create("datetime.isoformat", ["sep", "timespec"], 0));
        Assert.Same(
            LythonCallableSignature.Create("datetime.__format__", ["format_spec"]),
            LythonCallableSignature.Create("datetime.__format__", ["format_spec"]));
        Assert.Same(
            LythonCallableSignature.Create("datetime.strftime", ["format"]),
            LythonCallableSignature.Create("datetime.strftime", ["format"]));
        Assert.Same(
            LythonCallableSignature.Create("datetime.replace", ["year", "month", "day", "hour", "minute", "second", "microsecond", "tzinfo", "fold"], 0),
            LythonCallableSignature.Create("datetime.replace", ["year", "month", "day", "hour", "minute", "second", "microsecond", "tzinfo", "fold"], 0));
        Assert.Same(
            LythonCallableSignature.Create("timezone.utcoffset", ["dt"]),
            LythonCallableSignature.Create("timezone.utcoffset", ["dt"]));
        Assert.Same(
            LythonCallableSignature.Create("timezone.tzname", ["dt"]),
            LythonCallableSignature.Create("timezone.tzname", ["dt"]));
        Assert.Same(
            LythonCallableSignature.Create("timezone.dst", ["dt"]),
            LythonCallableSignature.Create("timezone.dst", ["dt"]));
    }

    [Fact]
    public void Create_ReusesHoistedStrSignatures()
    {
        // N17: companion pin for the hoisted string-provider statics.
        Assert.Same(
            LythonCallableSignature.Create("str.encode", ["encoding", "errors"], 0),
            LythonCallableSignature.Create("str.encode", ["encoding", "errors"], 0));
        Assert.Same(
            LythonCallableSignature.Create("str.replace", ["old", "new", "count"], 2),
            LythonCallableSignature.Create("str.replace", ["old", "new", "count"], 2));
        Assert.Same(
            LythonCallableSignature.Create("str.startswith", ["prefix", "start", "end"], 1),
            LythonCallableSignature.Create("str.startswith", ["prefix", "start", "end"], 1));
        Assert.Same(
            LythonCallableSignature.Create("str.endswith", ["suffix", "start", "end"], 1),
            LythonCallableSignature.Create("str.endswith", ["suffix", "start", "end"], 1));
        Assert.Same(
            LythonCallableSignature.Create("str.join", ["iterable"]),
            LythonCallableSignature.Create("str.join", ["iterable"]));
        Assert.Same(
            LythonCallableSignature.Create("str.zfill", ["width"]),
            LythonCallableSignature.Create("str.zfill", ["width"]));
        Assert.Same(
            LythonCallableSignature.Create("str.center", ["width", "fillchar"], 1),
            LythonCallableSignature.Create("str.center", ["width", "fillchar"], 1));
        Assert.Same(
            LythonCallableSignature.Create("str.ljust", ["width", "fillchar"], 1),
            LythonCallableSignature.Create("str.ljust", ["width", "fillchar"], 1));
        Assert.Same(
            LythonCallableSignature.Create("str.rjust", ["width", "fillchar"], 1),
            LythonCallableSignature.Create("str.rjust", ["width", "fillchar"], 1));
        Assert.Same(
            LythonCallableSignature.Create("str.find", ["sub", "start", "end"], 1),
            LythonCallableSignature.Create("str.find", ["sub", "start", "end"], 1));
        Assert.Same(
            LythonCallableSignature.Create("str.index", ["sub", "start", "end"], 1),
            LythonCallableSignature.Create("str.index", ["sub", "start", "end"], 1));
        Assert.Same(
            LythonCallableSignature.Create("str.rfind", ["sub", "start", "end"], 1),
            LythonCallableSignature.Create("str.rfind", ["sub", "start", "end"], 1));
        Assert.Same(
            LythonCallableSignature.Create("str.rindex", ["sub", "start", "end"], 1),
            LythonCallableSignature.Create("str.rindex", ["sub", "start", "end"], 1));
        Assert.Same(
            LythonCallableSignature.Create("str.count", ["sub", "start", "end"], 1),
            LythonCallableSignature.Create("str.count", ["sub", "start", "end"], 1));
        Assert.Same(
            LythonCallableSignature.Create("str.partition", ["sep"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1),
            LythonCallableSignature.Create("str.partition", ["sep"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1));
        Assert.Same(
            LythonCallableSignature.Create("str.rpartition", ["sep"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1),
            LythonCallableSignature.Create("str.rpartition", ["sep"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1));
        Assert.Same(
            LythonCallableSignature.Create("str.removeprefix", ["prefix"]),
            LythonCallableSignature.Create("str.removeprefix", ["prefix"]));
        Assert.Same(
            LythonCallableSignature.Create("str.removesuffix", ["suffix"]),
            LythonCallableSignature.Create("str.removesuffix", ["suffix"]));
        Assert.Same(
            LythonCallableSignature.Create("str.format_map", ["mapping"]),
            LythonCallableSignature.Create("str.format_map", ["mapping"]));
        Assert.Same(
            LythonCallableSignature.Create("str.strip", ["chars"], 0),
            LythonCallableSignature.Create("str.strip", ["chars"], 0));
        Assert.Same(
            LythonCallableSignature.Create("str.lstrip", ["chars"], 0),
            LythonCallableSignature.Create("str.lstrip", ["chars"], 0));
        Assert.Same(
            LythonCallableSignature.Create("str.rstrip", ["chars"], 0),
            LythonCallableSignature.Create("str.rstrip", ["chars"], 0));
        Assert.Same(
            LythonCallableSignature.Create("str.split", ["sep", "maxsplit"], 0),
            LythonCallableSignature.Create("str.split", ["sep", "maxsplit"], 0));
        Assert.Same(
            LythonCallableSignature.Create("str.rsplit", ["sep", "maxsplit"], 0),
            LythonCallableSignature.Create("str.rsplit", ["sep", "maxsplit"], 0));
        Assert.Same(
            LythonCallableSignature.Create("str.expandtabs", ["tabsize"], 0),
            LythonCallableSignature.Create("str.expandtabs", ["tabsize"], 0));
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
