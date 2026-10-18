using System.Numerics;
using System.Text.Json;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class SumStateAllocationDiagnostic
{
    private static readonly LythonSourceSpan Span = new(0, 0, 1, 1);

    [Fact]
    public async Task Capture()
    {
        var records = new List<object>();
        foreach (var kind in new[] { "bytes", "tuple", "custom", "list" })
        foreach (var asynchronous in new[] { false, true })
        {
            var samples = new List<long>();
            for (var sample = -8; sample < 8; sample++)
            {
                var budget = new LythonMemoryBudget(8 * 1024 * 1024);
                using (var owner = budget.CreateScope())
                {
                    var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
                    object[] values = [BigInteger.One, new BigInteger(2), new BigInteger(3), new BigInteger(4)];
                    var custom = new Source(values);
                    var payload = Enumerable.Range(0, 4096).Select(i => (byte)(i % 251)).ToArray();
                    object input = kind == "bytes" ? new PyBytes(payload, context.MemoryGovernor, Span)
                        : kind == "tuple" ? new PyTuple(values, context.MemoryGovernor, Span) : custom;
                    var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(context.State.BuiltinVariables[kind == "list" ? "list" : "sum"]);
                    CallArgumentValue[] arguments = [CallArgumentValue.Positional(input)];
                    object result = PyNone.Instance;
                    var thread = Environment.CurrentManagedThreadId;
                    var start = GC.GetAllocatedBytesForCurrentThread();
                    for (var i = 0; i < 128; i++)
                    {
                        if (asynchronous)
                        {
                            var pending = callable.InvokeAsync(arguments, Span, context);
                            Assert.True(pending.IsCompletedSuccessfully);
                            result = await pending;
                        }
                        else result = callable.Invoke(arguments, Span, context);
                    }
                    var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
                    Assert.Equal(thread, Environment.CurrentManagedThreadId);
                    if (kind == "list")
                    {
                        Assert.Equal(values, Assert.IsType<PyList>(result));
                        Assert.True(budget.ReservedBytes >= 128 * 4 * IntPtr.Size);
                    }
                    else Assert.Equal(new BigInteger(kind == "bytes" ? payload.Sum(x => (int)x) : 10), Assert.IsType<BigInteger>(result));
                    Assert.Equal(kind is "custom" or "list" ? 128 : 0, custom.CleanupCalls);
                    Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                    Assert.Null(context.CurrentExecutableFrame);
                    Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
                    Assert.Equal(0, context.Limits.CurrentRecursionDepth);
                    if (sample >= 0) samples.Add(allocated);
                }
                Assert.Equal(0, budget.ReservedBytes);
            }
            records.Add(new { kind, asynchronous, samples });
        }
        File.WriteAllText(Environment.GetEnvironmentVariable("LYTHON_SUM_STATE_ALLOCATION")!, JsonSerializer.Serialize(records));
    }

    private sealed class Source(object[] values) : IPyAsyncIterableValue
    {
        internal int CleanupCalls;
        public IEnumerable<object> Iterate()
        {
            try { foreach (var value in values) yield return value; }
            finally { CleanupCalls++; }
        }
        public async IAsyncEnumerable<object> IterateAsync()
        {
            await Task.CompletedTask;
            try { foreach (var value in values) yield return value; }
            finally { CleanupCalls++; }
        }
    }
}
