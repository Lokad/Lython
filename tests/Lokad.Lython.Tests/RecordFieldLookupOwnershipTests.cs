using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class RecordFieldLookupOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 1, 1);

    [Theory]
    [InlineData("replace")]
    [InlineData("delete")]
    [InlineData("detach")]
    [InlineData("cancel")]
    [InlineData("refuse")]
    public void CacheAdmissionRechecksCurrentRecordAndUnwinds(string outcome)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new()
            {
                MemoryBudget = hook, CancellationToken = cancellation.Token,
            });
            var field = PyString.FromString("a", context.MemoryGovernor, Span);
            var first = new PyDict(context.MemoryGovernor, Span);
            first.SetItem(field, BigInteger.One);
            var layout = first.ShareRecordKeys(context, Span)!;
            var second = layout.CreateRecord(context, Span, 1);
            second.SetRecordItem(field, new BigInteger(2), context, Span);
            context.Variables["row"] = second;
            var frontend = LythonFrontend.Compile("for _ in range(1):\n value = row['a']\nreturn value");
            var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script!)).EntryPoint;
            var changed = false;
            if (outcome == "refuse") hook.RefuseGrowth = bytes => bytes == 32L + 64L * code.LookupCacheCount;
            else hook.AfterGrowth = bytes =>
            {
                if (bytes != 32L + 64L * code.LookupCacheCount) return;
                hook.AfterGrowth = null;
                changed = true;
                if (outcome == "replace") second.SetItem(field, new BigInteger(20));
                else if (outcome == "delete") second.Remove(field);
                else if (outcome == "detach") second.Clear();
                else cancellation.Cancel();
            };
            if (outcome == "replace")
                Assert.Equal(new BigInteger(20), LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
            else
            {
                var error = Assert.Throws<LythonRuntimeException>(() =>
                    LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
                if (outcome == "refuse")
                {
                    Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
                    Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                }
                else if (outcome == "cancel") Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
                else Assert.Equal("KeyError", error.ExceptionType);
            }
            Assert.Equal(outcome != "refuse", changed);
            Assert.Equal(BigInteger.One, first.GetItem(field));
            if (outcome is "refuse" or "cancel") Assert.Equal(new BigInteger(2), second.GetItem(field));
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.State.CurrentInterpreterDepth);
            Assert.Equal(0, context.State.CurrentRecursionDepth);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
