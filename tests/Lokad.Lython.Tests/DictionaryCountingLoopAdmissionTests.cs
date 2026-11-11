using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class DictionaryCountingLoopAdmissionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RequiredGrowthResumesOnceAndRefusalKeepsItsFirstOrigin(bool asynchronous)
    {
        const long limit = 2 * 1024 * 1024;
        const string source = """
            counts=initial_counts;words=initial_words;word=None;total=0
            for unused in []: pass
            arm()
            try:
                for word in words: counts[word]=counts.get(word,0)+1;total+=1
            except MemoryError:
                mark()
            finally:
                return 'hidden'
            """;
        var budget = new LythonMemoryBudget(limit);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner })
            { MirrorLocalStores = true };
            // Growth retains the storage owner's original source, as on the
            // ordinary executable path. Mirror locals so unwind can be observed.
            var span = new LythonSourceSpan(source.IndexOf("counts[word]", StringComparison.Ordinal), "counts[word]".Length, 4, 20);
            var counts = new PyDict(context.MemoryGovernor, span);
            foreach (var key in new[] { "alpha", "bravo", "café", "delta" })
                counts.SetItem(PyString.FromString(key, context.MemoryGovernor, span), BigInteger.One);
            var first = PyString.FromString("alpha", context.MemoryGovernor, span);
            var missing = PyString.FromString("echo", context.MemoryGovernor, span);
            var words = new PyList([first, first, missing], context.MemoryGovernor, span);
            context.Variables["initial_counts"] = counts;
            context.Variables["initial_words"] = words;
            using var arm = new Arm(budget);
            var mark = new Mark();
            context.Variables["arm"] = arm;
            context.Variables["mark"] = mark;
            var frontend = LythonFrontend.Compile(source);
            Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
            var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script!));
            Assert.Contains(code.EntryPoint.Instructions, i => i.OpCode == ExecutableOpCode.ForNextDictionaryAccumulation);
            var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            {
                if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context);
                else LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null);
            });
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
            Assert.Same(failure, context.MemoryGovernor.AdmissionFailure);
            var origin = Assert.IsType<LythonSourceSpan>(failure.Span);
            Assert.Same(span, origin);
            Assert.Equal("counts[word]", source.Substring(origin.Start, origin.Length));
            Assert.Equal(1, arm.Calls);
            Assert.True(counts.TryGetValue(first, out var updated));
            Assert.Equal(new BigInteger(3), updated);
            Assert.False(counts.TryGetValue(missing, out _));
            Assert.Same(missing, context.Variables["word"]);
            Assert.Equal(new BigInteger(2), context.Variables["total"]);
            Assert.Equal(0, mark.Calls);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(0, context.State.CurrentInterpreterDepth);
            Assert.Null(context.CurrentExecutableFrame);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class Arm(LythonMemoryBudget budget) : LythonRuntime.ICallable, IDisposable
    {
        private ILythonMemoryReservation? _blocker;
        internal int Calls;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Calls++;
            Assert.True(budget.TryReserve(budget.LimitBytes!.Value - budget.ReservedBytes, out _blocker));
            return PyNone.Instance;
        }
        public void Dispose() => _blocker?.Dispose();
    }

    private sealed class Mark : LythonRuntime.ICallable
    {
        internal int Calls;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Calls++;
            return PyNone.Instance;
        }
    }
}
