using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class ExecutableFrameStorageTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [Theory]
    [InlineData(false, false), InlineData(false, true)]
    [InlineData(true, false), InlineData(true, true)]
    public async Task RefusalPrecedesBulkWorkingArrayAllocation(bool caches, bool asynchronous)
    {
        const int capacity = 8192;
        var statements = Enumerable.Range(0, capacity)
            .Select(i => caches ? $"item.member{i}" : $"local{i}=0");
        var source = "effect=1\n" + string.Join('\n', statements) + "\nreturn 7";
        var frontend = LythonFrontend.Compile(source);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script!)).EntryPoint;
        Assert.True((caches ? code.MemberCacheCount : code.LocalNames.Count) >= capacity);
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            Assert.True(budget.TryReserve(4 * 1024 * 1024 - budget.ReservedBytes, out var blocker));
            using (blocker)
            {
                LythonRuntimeException? failure = null;
                var before = GC.GetAllocatedBytesForCurrentThread();
                try
                {
                    if (asynchronous)
                    {
                        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
                        Assert.True(pending.IsCompleted);
                        await pending;
                    }
                    else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
                }
                catch (LythonRuntimeException error) { failure = error; }
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                output.WriteLine($"Refused {(caches ? "cache" : "local")} frame, async={asynchronous}: {allocated} allocated bytes.");
                Assert.NotNull(failure);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
                Assert.InRange(failure.Attribution?.Requested ?? 0L, capacity * (long)IntPtr.Size, long.MaxValue);
                // A single reference array at this capacity exceeds 64 KiB on
                // supported x64 targets. Allow ample error/setup overhead while
                // proving that denied storage was not allocated first.
                Assert.True(allocated < capacity * 4L, $"Allocated {allocated} bytes before reporting refused admission.");
                Assert.False(context.Variables.ContainsKey("effect"));
                Assert.Null(context.CurrentExecutableFrame);
                Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
                Assert.Equal(0, context.Limits.CurrentRecursionDepth);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SequentialMethodCallsReturnWorkingStorageWithoutCollection(bool asynchronous)
    {
        var parameters = string.Join(',', Enumerable.Range(0, 20).Select(i => $"a{i}"));
        var arguments = string.Join(',', Enumerable.Repeat("self.value", 20));
        // The ordinary argument list grows operand storage while member-cache
        // storage and each repeated alias must remain funded.
        var source = $"def first({parameters}):return a0\nclass Worker:\n def run(self):return first({arguments})\nworker=Worker()\nworker.value=[7]";
        var frontend = LythonFrontend.Compile(source);
        var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
            else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
            var worker = Assert.IsType<PyInstance>(context.Variables["worker"]);
            Assert.True(worker.Type.TryGetOwnMember("run", out var function));
            var method = Assert.IsType<PyBoundMethod>(Assert.IsType<PyExecutableFunction>(function).Bind(worker));
            Assert.True(worker.TryGetOwnAttribute("value", out var expected));
            var baseline = budget.ReservedBytes;
            var span = new LythonSourceSpan(0, 1, 1, 1);
            for (var iteration = 0; iteration < 8; iteration++)
            {
                var result = asynchronous ? await method.InvokeAsync([], span, context)
                    : method.Invoke([], span, context);
                Assert.Same(expected, result);
                Assert.Equal(baseline, budget.ReservedBytes);
                Assert.Null(context.CurrentExecutableFrame);
                Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
                Assert.Equal(0, context.Limits.CurrentRecursionDepth);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
