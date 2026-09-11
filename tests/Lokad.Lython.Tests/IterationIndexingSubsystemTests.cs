using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class IterationIndexingSubsystemTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 1, 1);

    [Fact]
    public void PyIteration_UsesRuntimeIterableProtocols()
    {
        var items = PyIteration.ToSequence(PyString.FromString("ab"), Span).ToArray();

        Assert.Equal(2, items.Length);
        Assert.Equal("a", ((PyString)items[0]).AsString());
        Assert.Equal("b", ((PyString)items[1]).AsString());
    }

    [Fact]
    public void PyIndexing_UsesOwnedIndexAndSliceProtocols()
    {
        var list = new PyList([new BigInteger(10), new BigInteger(20), new BigInteger(30)]);
        var tuple = new PyTuple([PyString.FromString("x"), PyString.FromString("y")]);

        Assert.Equal(new BigInteger(20), PyIndexing.ReadIndex(list, new BigInteger(1), Span));
        Assert.Equal("y", ((PyString)PyIndexing.ReadIndex(tuple, new BigInteger(-1), Span)).AsString());

        var slice = Assert.IsType<PyList>(PyIndexing.ReadSlice(list, new BigInteger(0), new BigInteger(2), null, Span));
        Assert.Equal([new BigInteger(10), new BigInteger(20)], slice.ToArray());
    }

    [Fact]
    public void NormalizeIndex_NamesReceiversLikePython()
    {
        Assert.Equal(1, PyIndexing.NormalizeIndex(new BigInteger(1), 3, Span, PyIndexing.IndexTargetName.List));

        var list = Assert.Throws<LythonRuntimeException>(() => PyIndexing.NormalizeIndex(PyString.FromString("x"), 3, Span, PyIndexing.IndexTargetName.List));
        Assert.Equal("list indices must be integers or slices, not str", list.Message);

        var tuple = Assert.Throws<LythonRuntimeException>(() => PyIndexing.NormalizeIndex(1.5, 3, Span, PyIndexing.IndexTargetName.Tuple));
        Assert.Equal("tuple indices must be integers or slices, not float", tuple.Message);

        var text = Assert.Throws<LythonRuntimeException>(() => PyIndexing.NormalizeIndex(PyNone.Instance, 3, Span, PyIndexing.IndexTargetName.Text));
        Assert.Equal("string indices must be integers, not 'NoneType'", text.Message);

        var sequence = Assert.Throws<LythonRuntimeException>(() => PyIndexing.NormalizeIndex(PyString.FromString("x"), 3, Span, PyIndexing.IndexTargetName.Sequence));
        Assert.Equal("sequence index must be integer, not 'str'", sequence.Message);

        var pop = Assert.Throws<LythonRuntimeException>(() => PyIndexing.NormalizePopIndex(PyString.FromString("x"), 3, Span));
        Assert.Equal("'str' object cannot be interpreted as an integer", pop.Message);

        var legacy = Assert.Throws<LythonRuntimeException>(() => PyIndexing.NormalizeIndex(PyString.FromString("x"), 3, Span));
        Assert.Equal("Indices must be integers.", legacy.Message);

        Assert.Equal(PyIndexing.IndexTargetName.List, PyIndexing.TargetKind(new PyList([])));
        Assert.Equal(PyIndexing.IndexTargetName.Text, PyIndexing.TargetKind(PyString.FromString("x")));
        Assert.Equal(PyIndexing.IndexTargetName.Unnamed, PyIndexing.TargetKind(new BigInteger(1)));
    }

    [Fact]
    public void DictionaryMutationDuringIterationRaisesCatchableRuntimeError()
    {
        var result = new LythonEngine().Run(
            """
values = {"a": 1}
try:
    for key in values:
        values["b"] = 2
except RuntimeError:
    return "caught"
return "missed"
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("caught", result.ReturnValue);
    }
}
