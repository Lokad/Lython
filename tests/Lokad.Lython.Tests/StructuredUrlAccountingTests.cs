using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class StructuredUrlAccountingTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Theory]
    [InlineData("urlsplit", 127)]
    [InlineData("urlsplit", 300)]
    [InlineData("urlsplit", 600)]
    [InlineData("urlparse", 127)]
    [InlineData("urlparse", 300)]
    [InlineData("urlparse", 600)]
    [InlineData("urlunsplit", 127)]
    [InlineData("urlunsplit", 300)]
    [InlineData("urlunsplit", 600)]
    [InlineData("SplitResult", 127)]
    [InlineData("SplitResult", 300)]
    [InlineData("SplitResult", 380)]
    public void DeniedConstructionReleasesAbandonedOwnership(string name, long cap)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = cap });
        Assert.True(LythonRuntime.UrllibParseModule.Instance.TryGetMember(name, out var member));
        var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
        var path = PyString.FromString("/" + new string('é', 500));
        CallArgumentValue[] arguments = name switch
        {
            "SplitResult" => [CallArgumentValue.Positional(PyString.FromString("http")), CallArgumentValue.Positional(PyString.FromString("X")),
                CallArgumentValue.Positional(path), CallArgumentValue.Positional(PyString.Empty), CallArgumentValue.Positional(PyString.Empty)],
            "urlunsplit" => [CallArgumentValue.Positional(new PyTuple([PyString.FromString("http"), PyString.FromString("X"), path, PyString.Empty, PyString.Empty]))],
            _ => [CallArgumentValue.Positional(PyString.FromString("http://X" + path.AsString() + ";p?q#f"))],
        };
        for (var attempt = 0; attempt < 20; attempt++)
        {
            Deny(callable, arguments, context);
            Sweep(context);
            Assert.Equal(context.State.CallTemporaries.CommittedBackingBytes, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        GC.KeepAlive(arguments);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Deny(LythonRuntime.ICallable callable, CallArgumentValue[] arguments, LythonRuntime.ExecutionContext context)
    {
        var error = Assert.Throws<LythonRuntimeException>(() => callable.Invoke(arguments, Span, context));
        Assert.Equal("MemoryError", error.ExceptionType);
    }

    [Fact]
    public void AbandonedResultsAndPropertiesReleaseOnlyTheirOwnStorage()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 64 * 1024 });
        for (var attempt = 0; attempt < 100; attempt++)
        {
            Consume(context);
            Sweep(context);
            Assert.Equal(context.State.CallTemporaries.CommittedBackingBytes, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Consume(LythonRuntime.ExecutionContext context)
    {
        Assert.True(LythonRuntime.UrllibParseModule.Instance.TryGetMember("urlparse", out var parser));
        var record = Assert.IsType<LythonRuntime.UrllibParseModule.UrlResult>(((LythonRuntime.ICallable)parser).Invoke(
            [CallArgumentValue.Positional(PyString.FromString("http://u:p@EXAMPLE:80/a;b?q#f"))], Span, context));
        foreach (var name in new[] { "username", "password", "hostname", "port", "geturl", "_asdict", "encode", "_replace" })
        {
            Assert.True(record.TryGetMember(name, context, Span, out var value));
            if (value is LythonRuntime.ICallable method) _ = method.Invoke([], Span, context);
        }
        _ = PyIndexing.ReadSlice(record, BigInteger.One, new BigInteger(4), PyNone.Instance, Span, context);
        GC.KeepAlive(record);
    }

    private static void Sweep(LythonRuntime.ExecutionContext context)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        context.State.CallTemporaries.Sweep(full: true);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 127)]
    [InlineData(false, 200)]
    [InlineData(true, 1)]
    [InlineData(true, 127)]
    [InlineData(true, 200)]
    public void MetadataDenialRefundsAndPublishesNoDefaults(bool parameters, long cap)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = cap });
        Assert.True(LythonRuntime.UrllibParseModule.Instance.TryGetMember(parameters ? "ParseResult" : "SplitResult", out var value));
        var type = Assert.IsAssignableFrom<IPyContextualDynamicAttributes>(value);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var error = Assert.Throws<LythonRuntimeException>(() => type.TryGetMember("_field_defaults", context, Span, out _));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Null(context.State.UrlSplitFieldDefaults);
            Assert.Null(context.State.UrlParseFieldDefaults);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    [Fact]
    public void ReassemblyLengthDenialPrecedesBuilderAllocation()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxStringLength = 8 });
        var values = new PyTuple([PyString.FromString("http"), PyString.FromString("x"), PyString.FromString("/a"), PyString.Empty, PyString.Empty]);
        Assert.True(LythonRuntime.UrllibParseModule.Instance.TryGetMember("urlunsplit", out var member));
        var error = Assert.Throws<LythonRuntimeException>(() => ((LythonRuntime.ICallable)member).Invoke([CallArgumentValue.Positional(values)], Span, context));
        Assert.Contains("maximum string length", error.Message);
        Sweep(context);
        Assert.Equal(context.State.CallTemporaries.CommittedBackingBytes, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        GC.KeepAlive(values);
    }
}
