using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class UrlQueryAccountingTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Theory]
    [InlineData("urlencode", 127)]
    [InlineData("urlencode", 300)]
    [InlineData("urlencode", 600)]
    [InlineData("parse_qsl", 127)]
    [InlineData("parse_qsl", 300)]
    [InlineData("parse_qsl", 600)]
    [InlineData("parse_qs", 127)]
    [InlineData("parse_qs", 300)]
    [InlineData("parse_qs", 600)]
    public void AbandonedQueryConstructionReclaimsEveryCharge(string name, long cap)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = cap });
        Assert.True(LythonRuntime.UrllibParseModule.Instance.TryGetMember(name, out var member));
        var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
        object input = name == "urlencode"
            ? new PyList([new PyTuple([PyString.FromString("x"), PyString.FromString(new string('é', 30))])])
            : PyString.FromString("a=1&b=2&c=3&d=4");
        for (var attempt = 0; attempt < 50; attempt++)
        {
            Deny(callable, input, context);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            context.State.CallTemporaries.Sweep(full: true);
            Assert.Equal(context.State.CallTemporaries.CommittedBackingBytes, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        GC.KeepAlive(input);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Deny(LythonRuntime.ICallable callable, object input, LythonRuntime.ExecutionContext context)
    {
        var failure = Assert.Throws<LythonRuntimeException>(() => callable.Invoke([CallArgumentValue.Positional(input)], Span, context));
        Assert.Equal("MemoryError", failure.ExceptionType);
    }

    [Fact]
    public void FieldLimitPrecedesDecodingAndOutputAllocation()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 1 });
        Assert.True(LythonRuntime.UrllibParseModule.Instance.TryGetMember("parse_qsl", out var member));
        var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
        var failure = Assert.Throws<LythonRuntimeException>(() => callable.Invoke(
            [CallArgumentValue.Positional(PyString.FromString("a=%FF&b=2")),
             CallArgumentValue.Keyword("max_num_fields", BigInteger.Zero),
             CallArgumentValue.Keyword("errors", PyString.FromString("strict"))], Span, context));
        Assert.Equal("ValueError", failure.ExceptionType);
        Assert.Contains("Max number of fields", failure.Message);
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
