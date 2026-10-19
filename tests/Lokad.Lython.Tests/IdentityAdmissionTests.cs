using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
namespace Lokad.Lython.Tests;

public sealed class IdentityAdmissionTests
{
    [Fact]
    public void GetObjectIdAdmissionDenialIsAtomic()
    {
        // Deny the identity shell before publishing it.
        var root = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(63) },regexProvider:Utf8RegexProvider.Instance);
        var governor = root.MemoryGovernor;
        var failure = Assert.Throws<LythonRuntimeException>(() => root.State.GetObjectId(new object()));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void GetObjectIdPublishesIdentityOnceFunded()
    {
        var root = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = null },regexProvider:Utf8RegexProvider.Instance);
        var key = new object();
        var first = root.State.GetObjectId(key);
        Assert.Equal(first, root.State.GetObjectId(key));
        GC.KeepAlive(key);
    }

}
