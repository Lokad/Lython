using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class IntegerViewOwnershipTests
{
    [Theory]
    [InlineData("real")]
    [InlineData("numerator")]
    [InlineData("conjugate")]
    [InlineData("as_integer_ratio")]
    public void ExactIntegerViewsReturnTheOwnedBox(string member)
    {
        var span = new LythonSourceSpan(8, 4, 2, 1);
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        object receiver = BigInteger.One << 4000;
        LythonRuntime.OwnFreshInteger(receiver, context.MemoryGovernor, span);
        Assert.True(LythonRuntime.IntMembers.TryGetMember(receiver, member, out var value));
        if (value is LythonRuntime.ICallable callable) value = callable.Invoke([], span, context);
        if (value is PyTuple ratio) value = ratio[0];
        Assert.Same(receiver, value);
        GC.KeepAlive(receiver);
    }
}
