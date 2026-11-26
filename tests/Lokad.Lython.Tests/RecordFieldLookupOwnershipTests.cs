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
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task FieldConversionKeepsTheCalleeCapturedBeforeCacheAdmission(bool asynchronous, bool refuse)
    {
        const string source = """
            global conversion
            conversion = int
            def get(record): return conversion(record['a'])
            first = get(row)
            second = get(row)
            conversion = int
            return first, second, get(row)
            """;
        var compiled = ExecutableScript.Compile(LythonFrontend.Compile(source).Script!).EntryPoint;
        var function = Assert.Single(compiled.FunctionBindings()).CodeObject;
        var cacheBytes = LythonRuntime.LookupCacheBytes(function.LookupCacheCount);
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = hook });
            var key = PyString.FromString("a", context.MemoryGovernor, Span);
            var previous = PyString.FromString("7", context.MemoryGovernor, Span);
            var replacement = PyString.FromString("20", context.MemoryGovernor, Span);
            var original = new PyDict(context.MemoryGovernor, Span);
            original.SetItem(key, previous);
            var layout = original.ShareRecordKeys(context, Span)!;
            var row = layout.CreateRecord(context, Span, 1);
            row.SetRecordItem(key, previous, context, Span);
            context.Variables["row"] = row;
            Assert.True(context.TryGetBuiltin("len", out var length));

            var rebound = false;
            if (refuse) hook.RefuseGrowth = bytes => bytes == cacheBytes;
            else hook.AfterGrowth = bytes =>
            {
                if (bytes != cacheBytes) return;
                hook.AfterGrowth = null;
                rebound = true;
                context.Variables["conversion"] = length;
                row.SetItem(key, replacement);
            };

            try
            {
                if (refuse)
                {
                    var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () => { _ = await Run(); });
                    Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
                    Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
                    Assert.Same(previous, row.GetItem(key));
                }
                else
                {
                    var result = Assert.IsType<PyTuple>(await Run());
                    Assert.Equal([new BigInteger(20), new BigInteger(2), new BigInteger(20)], result.Cast<BigInteger>());
                    Assert.Same(replacement, row.GetItem(key));
                }
                Assert.Equal(!refuse, rebound);
                Assert.Same(previous, original.GetItem(key));
                Assert.Null(context.CurrentExecutableFrame);
                Assert.Equal(0, context.State.CurrentInterpreterDepth);
                Assert.Equal(0, context.State.CurrentRecursionDepth);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            finally { context.State.CloseResources(); }

            ValueTask<object?> Run() => asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(compiled, context)
                : new(LythonRuntime.ExecuteExecutableCodeObject(compiled, context, default, null));
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("replace", false)] [InlineData("replace", true)]
    [InlineData("delete", false)] [InlineData("delete", true)]
    [InlineData("detach", false)] [InlineData("detach", true)]
    [InlineData("cancel", false)] [InlineData("cancel", true)]
    [InlineData("refuse", false)] [InlineData("refuse", true)]
    public void CacheAdmissionRechecksCurrentRecordAndUnwinds(string outcome, bool helper)
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
            var frontend = LythonFrontend.Compile(helper
                ? "def read(row):\n value = row['a']\n return value\nreturn read(row)"
                : "for _ in range(1):\n value = row['a']\nreturn value");
            var code = ExecutableScript.Compile(frontend.Script!).EntryPoint;
            var lookupCode = helper ? Assert.Single(code.FunctionBindings()).CodeObject : code;
            var changed = false;
            if (outcome == "refuse") hook.RefuseGrowth = bytes => bytes == 32L + 64L * lookupCode.LookupCacheCount;
            else hook.AfterGrowth = bytes =>
            {
                if (bytes != 32L + 64L * lookupCode.LookupCacheCount) return;
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
