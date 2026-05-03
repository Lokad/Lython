using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class SemanticOpsSubsystemTests
{
    [Fact]
    public void TruthinessEqualityComparisonAndContainment_AreOwnedByFocusedOps()
    {
        Assert.False(PyTruthiness.IsTruthy(PyNone.Instance));
        Assert.True(PyTruthiness.IsTruthy(new PyList([BigInteger.One])));

        Assert.True(PyEquality.AreEqual(BigInteger.One, true));
        Assert.True(PyEquality.AreEqual(PyString.FromString("x"), "x"));
        Assert.False(PyEquality.AreEqual(PyString.FromString("x"), "y"));

        Assert.Equal(0, PyComparison.Compare(PyString.FromString("a"), "a", null!));
        Assert.True(PyContainment.Contains(new PyTuple([PyString.FromString("a"), BigInteger.One]), true, null!));
    }

    [Fact]
    public void MemberResolution_ReturnsKnownMembersAndRejectsMissingOnes()
    {
        var text = PyString.FromString("abc");

        Assert.True(PyMemberAccess.TryResolve(text, "upper", out var upper));
        Assert.IsAssignableFrom<object>(upper);
        Assert.False(PyMemberAccess.TryResolve(text, "missing", out _));
    }
}
