using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class StructuredUrlAccountingTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Theory]
    [InlineData(300)]
    [InlineData(380)]
    public void SplitResultBorrowsExistingFieldsAndKeepsItsOwnStorageCharged(long cap)
    {
        var budget = new LythonMemoryBudget(cap);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = owner });
        var path = PyString.FromString("/" + new string('é', 500));
        Assert.True(LythonRuntime.UrllibParseModule.Instance.TryGetMember("SplitResult", out var constructor));
        var result = Assert.IsType<LythonRuntime.UrllibParseModule.UrlResult>(
            Assert.IsAssignableFrom<LythonRuntime.ICallable>(constructor).Invoke([
                CallArgumentValue.Positional(PyString.FromString("http")),
                CallArgumentValue.Positional(PyString.FromString("X")),
                CallArgumentValue.Positional(path), CallArgumentValue.Positional(PyString.Empty),
                CallArgumentValue.Positional(PyString.Empty)], Span, context));
        Assert.True(budget.ReservedBytes > 0);
        Assert.Same(path, result.GetIndex(2));
        GC.KeepAlive(result);
        result = null!;
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

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
    public void DeniedConstructionBatchesReleaseOnlyTheirOwnAllowance(string name, long cap)
    {
        var budget = new LythonMemoryBudget(cap);
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
        for (var attempt = 0; attempt < 8; attempt++)
        {
            using (var owner = budget.CreateScope())
            {
                var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                    new LythonRunOptions { MemoryBudget = owner });
                Deny(callable, arguments, context);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            Assert.Equal(0, budget.ReservedBytes);
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
    public void UrlResultBatchesReturnTemporaryAllowanceAfterCleanup()
    {
        var budget = new LythonMemoryBudget(512 * 1024);
        for (var batch = 0; batch < 8; batch++)
        {
            using (var owner = budget.CreateScope())
            {
                var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                    new LythonRunOptions { MemoryBudget = owner });
                Consume(context);
                Assert.True(budget.ReservedBytes > 0);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                GC.KeepAlive(context.State.UrlSplitCache);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Fact]
    public void CacheRetainsAtMost128ResultsAndClearReleasesItsInfrastructure()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        for (var i = 0; i < 129; i++)
        {
            CacheOne(context, i);
            Assert.Equal(Math.Min(i + 1, 128), context.State.UrlSplitCache!.Count);
        }
        var cache = context.State.UrlSplitCache!;
        var infrastructure = cache.CommittedBytes;
        Assert.Equal(96 + 128 * (80 + 168), infrastructure);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        cache.Clear();
        Assert.Equal(0, cache.Count);
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes < before);
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes > 0);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CacheOne(LythonRuntime.ExecutionContext context, int index)
    {
        Assert.True(LythonRuntime.UrllibParseModule.Instance.TryGetMember("urlsplit", out var parser));
        _ = ((LythonRuntime.ICallable)parser).Invoke(
            [CallArgumentValue.Positional(PyString.FromString("http://x/" + index))], Span, context);
    }

    [Fact]
    public void CacheHitsReleaseTransientKeyCopies()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        CacheOne(context, 0);
        Sweep(context);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        for (var i = 0; i < 100; i++)
        {
            CacheOne(context, 0);
            Sweep(context);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(1, context.State.UrlSplitCache!.Count);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    [Theory]
    [InlineData(700)]
    [InlineData(800)]
    [InlineData(900)]
    public void DeniedCacheInsertionPublishesNoPartialCacheAndBatchCleanupReturnsAllowance(long cap)
    {
        var budget = new LythonMemoryBudget(cap);
        for (var batch = 0; batch < 8; batch++)
        {
            using (var owner = budget.CreateScope())
            {
                var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                    new LythonRunOptions { MemoryBudget = owner });
                var error = Assert.Throws<LythonRuntimeException>(() => CacheOne(context, 0));
                Assert.Equal("MemoryError", error.ExceptionType);
                Assert.Null(context.State.UrlSplitCache);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            Assert.Equal(0, budget.ReservedBytes);
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
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 127)]
    [InlineData(true, 1)]
    [InlineData(true, 127)]
    public void MetadataDenialRefundsAndPublishesNoDefaults(bool parameters, long cap)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(cap) });
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
}
