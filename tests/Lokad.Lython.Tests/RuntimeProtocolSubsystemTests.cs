using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class RuntimeProtocolSubsystemTests
{
    [Fact]
    public void TruthinessProtocol_IsOwnedByCoreRuntimeValues()
    {
        IPyTruthyValue emptyText = PyString.Empty;
        IPyTruthyValue nonEmptyList = new PyList([new BigInteger(1)]);
        IPyTruthyValue emptyTuple = PyTuple.Empty;
        IPyTruthyValue dict = new PyDict();
        IPyTruthyValue set = new PySet([PyString.FromString("x")]);

        Assert.False(emptyText.IsTruthy());
        Assert.True(nonEmptyList.IsTruthy());
        Assert.False(emptyTuple.IsTruthy());
        Assert.False(dict.IsTruthy());
        Assert.True(set.IsTruthy());
    }

    [Fact]
    public void SequenceProtocol_CreateSlice_PreservesConcreteSequenceType()
    {
        IPySequenceValue list = new PyList([new BigInteger(1), new BigInteger(2), new BigInteger(3)]);
        IPySequenceValue tuple = new PyTuple([PyString.FromString("a"), PyString.FromString("b")]);

        var listSlice = list.CreateSlice([list.GetItem(1), list.GetItem(2)]);
        var tupleSlice = tuple.CreateSlice([tuple.GetItem(1)]);

        Assert.IsType<PyList>(listSlice);
        Assert.IsType<PyTuple>(tupleSlice);
        Assert.Equal([new BigInteger(2), new BigInteger(3)], ((PyList)listSlice).ToArray());
        Assert.Equal("b", ((PyString)((PyTuple)tupleSlice)[0].RequireNotNull()).AsString());
    }

    [Fact]
    public void MutableSequenceProtocol_SetAndRemove_AffectUnderlyingList()
    {
        IMutablePySequenceValue list = new PyList([new BigInteger(1), new BigInteger(2), new BigInteger(3)]);

        list.SetItem(1, new BigInteger(9));
        list.RemoveAt(0);

        Assert.Equal([new BigInteger(9), new BigInteger(3)], ((PyList)list).ToArray());
    }
}
