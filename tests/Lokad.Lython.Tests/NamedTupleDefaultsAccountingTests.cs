using System.Numerics;
using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

// Prebuilt aliases isolate validation storage from guest-produced allocation.
// Excess defaults must be consumed, but their tail need not be kept.
[Collection("AllocationSensitive")]
public sealed class NamedTupleDefaultsAccountingTests
{
    private static readonly Func<CallArgumentValue[], LythonSourceSpan, Context, object> Create = typeof(LythonRuntime)
        .GetMethod("NamedTuple", BindingFlags.NonPublic | BindingFlags.Static)!
        .CreateDelegate<Func<CallArgumentValue[], LythonSourceSpan, Context, object>>();
    private static readonly Func<CallArgumentValue[], LythonSourceSpan, Context, ValueTask<object>> CreateAsync = typeof(LythonRuntime)
        .GetMethod("NamedTupleAsync", BindingFlags.NonPublic | BindingFlags.Static)!
        .CreateDelegate<Func<CallArgumentValue[], LythonSourceSpan, Context, ValueTask<object>>>();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task OversizedDefaultsDrainWithoutProportionalStorage(bool asynchronous) => Verify(asynchronous, 64);

    [Theory]
    [Trait("Category", "Stress")]
    [InlineData(false)]
    [InlineData(true)]
    public Task MillionDefaultsDrainWithoutProportionalStorage(bool asynchronous) => Verify(asynchronous, 1000000);

    private static async Task Verify(bool asynchronous, int count)
    {
        var budget = new LythonMemoryBudget(65536);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        object shared = BigInteger.One;
        var yielded = 0;
        var disposed = 0;
        IEnumerable<object> Values()
        {
            try { for (var i = 0; i < count; i++) { yielded++; yield return shared; } }
            finally { disposed++; }
        }
        CallArgumentValue[] arguments = [CallArgumentValue.Positional(PyString.FromString("P")),
            CallArgumentValue.Positional(PyString.FromString("a b")), CallArgumentValue.Keyword("defaults", Values())];
        for (var pass = 0; pass < 2; pass++)
        {
            yielded = disposed = 0;
            var before = GC.GetTotalAllocatedBytes(true);
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            {
                if (asynchronous) await CreateAsync(arguments, new(0, 0, 0, 0), context);
                else Create(arguments, new(0, 0, 0, 0), context);
            });
            var allocated = GC.GetTotalAllocatedBytes(true) - before;
            Assert.Equal("TypeError", error.ExceptionType);
            Assert.Equal(count, yielded);
            Assert.Equal(1, disposed);
            Assert.Equal(0, context.MemoryGovernor.CurrentAccountedBytes);
            if (pass == 1) Assert.True(allocated < 65536, $"Excess defaults allocated {allocated} bytes.");
        }
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
