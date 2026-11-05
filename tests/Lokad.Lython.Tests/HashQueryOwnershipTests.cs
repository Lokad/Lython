using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class HashQueryOwnershipTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task QueryHashFailureStopsRemovalAndKeepsStorageFunded(bool set, bool cancel)
    {
        var source = """
            namespace=globals()
            class Key:
             def __hash__(self):
              arm()
              return 17
            key=Key()
            container=
            """.TrimEnd() + (set ? "{key}" : "{key:'retained'}") + "\neffects=[]\ntry:\n " +
            (set ? "container.remove(key)" : "container.pop(key)") +
            "\nexcept BaseException:effects.append('caught')\nfinally:return 'hidden'\n";
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
        foreach (var asynchronous in new[] { false, true })
        {
            var budget = new LythonMemoryBudget(1024 * 1024);
            using var owner = budget.CreateScope();
            using var cancellation = new CancellationTokenSource();
            var context = new Context(new MockLythonHost(), new LythonRunOptions
            { MemoryBudget = owner, CancellationToken = cancellation.Token });
            var arm = new Arm(cancellation, cancel, budget.LimitBytes!.Value);
            context.Variables["arm"] = arm;
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            {
                if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
                else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
            });
            if (cancel) Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
            else
            {
                Assert.Same(arm.Refusal, error);
                Assert.Equal(arm.Origin, error.Span);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
            }
            Assert.Equal(2, arm.Calls);
            Assert.Empty(Assert.IsType<PyList>(context.Variables["effects"]));
            if (set) Assert.Equal(1, Assert.IsType<PySet>(context.Variables["container"]).Count);
            else Assert.Equal(1, Assert.IsType<PyDict>(context.Variables["container"]).Count);
            Assert.True(budget.ReservedBytes > 0);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.State.CurrentInterpreterDepth);
            Assert.Equal(0, context.State.CurrentRecursionDepth);
            owner.Dispose();
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    private sealed class Arm(CancellationTokenSource cancellation, bool cancel, long requested) : LythonRuntime.ICallable
    {
        internal int Calls;
        internal LythonRuntimeException? Refusal;
        internal LythonSourceSpan Origin = new(31, 4, 2, 1);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            if (++Calls == 2)
            {
                if (cancel) cancellation.Cancel();
                else
                {
                    try { context.MemoryGovernor.Reserve(requested, Origin); }
                    catch (LythonRuntimeException error) { Refusal = error; }
                }
            }
            return PyNone.Instance;
        }
    }
}
