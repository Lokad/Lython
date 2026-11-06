using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class HashQueryOwnershipTests
{
    [Fact]
    public void ProtocolStorageRefusalPrecedesSetPublication()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var hook = new AdmissionHookMemoryBudget(owner);
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = hook });
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(
            "class Key:\n def __hash__(self):return 17\nkey=Key()\ncontainer=set()\n").Script!)).EntryPoint;
        LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
        var container = Assert.IsType<PySet>(context.Variables["container"]);
        var baseline = budget.ReservedBytes;
        var observedCount = -1;
        hook.RefuseGrowth = bytes =>
        {
            if (bytes <= 0) return false;
            observedCount = container.Count;
            return true;
        };
        // Context-free storage insertion is also used by native construction;
        // it must admit native capacity/metadata before publishing a retained member.
        var failure = Assert.Throws<LythonRuntimeException>(() => container.Add(context.Variables["key"]));
        Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
        Assert.Equal(0, observedCount);
        Assert.Equal(0, container.Count);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(baseline, budget.ReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public async Task QueryProtocolFailureStopsRemovalAndKeepsStorageFunded(bool set, bool cancel, bool equality)
    {
        var source = """
            namespace=globals()
            class Key:
             def __hash__(self):
              HASH_CALLBACK
              return 17
             def __eq__(self,other):
              EQUALITY_CALLBACK
              return True
            key=Key()
            container=
            """.Replace("HASH_CALLBACK", equality ? "pass" : "arm()")
            .Replace("EQUALITY_CALLBACK", equality ? "arm()" : "pass").TrimEnd() +
            (set ? "{key}" : "{key:'retained'}") + "\nquery=" + (equality ? "Key()" : "key") + "\neffects=[]\ntry:\n " +
            (set ? "container.remove(query)" : "container.pop(query)") +
            "\nexcept BaseException:effects.append('caught')\nfinally:return 'hidden'\n";
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
        foreach (var asynchronous in new[] { false, true })
        {
            var budget = new LythonMemoryBudget(1024 * 1024);
            using var owner = budget.CreateScope();
            using var cancellation = new CancellationTokenSource();
            var context = new Context(new MockLythonHost(), new LythonRunOptions
            { MemoryBudget = owner, CancellationToken = cancellation.Token });
            var arm = new Arm(cancellation, cancel, budget.LimitBytes!.Value, equality ? 1 : 2);
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
            Assert.Equal(equality ? 1 : 2, arm.Calls);
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

    private sealed class Arm(CancellationTokenSource cancellation, bool cancel, long requested, int failAtCall) : LythonRuntime.ICallable
    {
        internal int Calls;
        internal LythonRuntimeException? Refusal;
        internal LythonSourceSpan Origin = new(31, 4, 2, 1);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            if (++Calls == failAtCall)
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
