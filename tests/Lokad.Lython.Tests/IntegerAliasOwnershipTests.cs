using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class IntegerAliasOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(8, 4, 2, 1);

    [Theory]
    [InlineData("int")]
    [InlineData("abs")]
    [InlineData("round")]
    public void BuiltinAliasesReuseTheActualOwner(string name)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        Assert.True(context.TryGetBuiltin(name, out var builtin));
        var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(builtin);
        object receiver = BigInteger.One << 4000;
        LythonRuntime.OwnFreshInteger(receiver, context.MemoryGovernor, Span);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var result = callable.Invoke([CallArgumentValue.Positional(receiver)], Span, context);
        Assert.Same(receiver, result);
        Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        GC.KeepAlive(receiver);
    }

    [Theory]
    [InlineData("__int__")]
    [InlineData("__index__")]
    [InlineData("__trunc__")]
    [InlineData("__floor__")]
    [InlineData("__ceil__")]
    [InlineData("__round__")]
    public void MemberAliasesReuseTheActualOwner(string member)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        object receiver = BigInteger.One << 4000;
        LythonRuntime.OwnFreshInteger(receiver, context.MemoryGovernor, Span);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.True(LythonRuntime.IntMembers.TryGetMember(receiver, member, out var value));
        var result = Assert.IsAssignableFrom<LythonRuntime.ICallable>(value).Invoke([], Span, context);
        Assert.Same(receiver, result);
        Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        GC.KeepAlive(receiver);
    }

    [Theory]
    [InlineData("abs")]
    [InlineData("round")]
    public void ChangedMagnitudesStillOwnTheirNewPayload(string name)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        Assert.True(context.TryGetBuiltin(name, out var builtin));
        var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(builtin);
        object receiver = -(BigInteger.One << 4000);
        LythonRuntime.OwnFreshInteger(receiver, context.MemoryGovernor, Span);
        var arguments = name == "abs"
            ? new[] { CallArgumentValue.Positional(receiver) }
            : new[] { CallArgumentValue.Positional(receiver), CallArgumentValue.Positional(new BigInteger(-4)) };
        var result = callable.Invoke(arguments, Span, context);
        Assert.NotSame(receiver, result);
        Assert.IsType<BigInteger>(result);
        GC.KeepAlive(receiver);
        GC.KeepAlive(result);
    }
}
