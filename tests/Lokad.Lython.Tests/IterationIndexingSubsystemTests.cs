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
