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
    [InlineData(false, "read")] [InlineData(true, "read")]
    [InlineData(false, "cancel")] [InlineData(true, "cancel")]
    [InlineData(false, "refuse")] [InlineData(true, "refuse")]
    public async Task InterleavedGeneratorsReadTheirLiveRecordLayouts(bool asynchronous, string outcome)
    {
        const string source = """
            def read(row):
                for _ in range(2):
                    yield row['a']
            return read(left_row), read(right_row)
            """;
        var compiled = ExecutableScript.Compile(LythonFrontend.Compile(source).Script!).EntryPoint;
        var function = Assert.Single(compiled.FunctionBindings()).CodeObject;
        var cacheBytes = LythonRuntime.LookupCacheBytes(function.LookupCacheCount);
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new()
            { MemoryBudget = hook, CancellationToken = cancellation.Token });
            var key = PyString.FromString("a", context.MemoryGovernor, Span);
            var other = PyString.FromString("b", context.MemoryGovernor, Span);
            var leftTemplate = new PyDict(context.MemoryGovernor, Span);
            leftTemplate.SetItem(key, BigInteger.Zero);
            var rightTemplate = new PyDict(context.MemoryGovernor, Span);
            rightTemplate.SetItem(other, BigInteger.Zero);
            rightTemplate.SetItem(key, BigInteger.Zero);
            var left = CreateRow(leftTemplate, 1);
            var right = CreateRow(rightTemplate, 2);
            context.Variables["left_row"] = left;
            context.Variables["right_row"] = right;
            try
            {
                var result = asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(compiled, context)
                    : LythonRuntime.ExecuteExecutableCodeObject(compiled, context, default, null);
                var generators = Assert.IsType<PyTuple>(result);
                var first = Assert.IsType<LythonRuntime.PyGenerator>(generators[0]);
                var second = Assert.IsType<LythonRuntime.PyGenerator>(generators[1]);
                var admissions = 0;
                hook.AfterGrowth = bytes =>
                {
                    if (bytes != cacheBytes) return;
                    admissions++;
                    if (outcome == "cancel") cancellation.Cancel();
                };
                if (outcome == "refuse") hook.RefuseGrowth = bytes => bytes == cacheBytes;

                if (outcome == "read")
                {
                    Assert.Equal(BigInteger.One, (await Advance(first)).Value);
                    Assert.Equal(new BigInteger(2), (await Advance(second)).Value);
                    left.SetItem(key, new BigInteger(9));
                    Assert.Equal(new BigInteger(9), (await Advance(first)).Value);
                    Assert.Equal(new BigInteger(2), (await Advance(second)).Value);
                    Assert.False((await Advance(first)).HasValue);
                    Assert.False((await Advance(second)).HasValue);
                    Assert.Equal(1, admissions);
                }
                else
                {
                    var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () => { _ = await Advance(first); });
                    if (outcome == "refuse") Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
                    else Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
                    Assert.Equal(BigInteger.One, left.GetItem(key));
                    Assert.Equal(new BigInteger(2), right.GetItem(key));
                }

                Assert.Null(context.CurrentExecutableFrame);
                Assert.Equal(0, context.State.CurrentInterpreterDepth);
                Assert.Equal(0, context.State.CurrentRecursionDepth);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            finally { context.State.CloseResources(); }

            PyDict CreateRow(PyDict template, int value)
            {
                var layout = template.ShareRecordKeys(context, Span)!;
                var row = layout.CreateRecord(context, Span, 2);
                row.SetRecordItem(key, new BigInteger(value), context, Span);
                return row;
            }
        }
        Assert.Equal(0, budget.ReservedBytes);

        ValueTask<PyIterationResult> Advance(LythonRuntime.PyGenerator generator)
            => asynchronous ? generator.TryMoveNextAsync() : new(generator.TryMoveNext());
    }

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
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task ConvertedDictionaryUpdateHonorsLookupCacheAdmissionBeforePublication(bool asynchronous, bool refuse)
    {
        const string source = """
            global conversion
            conversion = int

            def update(totals, category, row):
                totals[category] = totals.get(category, 0) + conversion(row['a'])

            update(totals, name, record)
            first = totals[name]
            update(totals, name, record)
            second = totals[name]
            conversion = int
            update(totals, name, record)
            return first, second, totals[name]
            """;
        var code = ExecutableScript.Compile(LythonFrontend.Compile(source).Script!).EntryPoint;
        var function = Assert.Single(code.FunctionBindings()).CodeObject;
        var cacheBytes = LythonRuntime.LookupCacheBytes(function.LookupCacheCount);
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = hook });
            var key = PyString.FromString("a", context.MemoryGovernor, Span);
            var label = PyString.FromString("service", context.MemoryGovernor, Span);
            var previous = PyString.FromString("7", context.MemoryGovernor, Span);
            var changed = PyString.FromString("20", context.MemoryGovernor, Span);
            var original = new PyDict(context.MemoryGovernor, Span);
            original.SetItem(key, previous);
            var layout = original.ShareRecordKeys(context, Span)!;
            var row = layout.CreateRecord(context, Span, 1);
            row.SetRecordItem(key, previous, context, Span);

            var totals = new PyDict(context.MemoryGovernor, Span);
            var retained = (object)new BigInteger(1000);
            totals.SetItem(label, retained);
            context.Variables["record"] = row;
            context.Variables["totals"] = totals;
            context.Variables["name"] = label;
            Assert.True(context.TryGetBuiltin("len", out var length));

            var reentered = false;
            if (refuse) hook.RefuseGrowth = bytes => bytes == cacheBytes;
            else hook.AfterGrowth = bytes =>
            {
                if (bytes != cacheBytes) return;
                hook.AfterGrowth = null;
                reentered = true;
                totals.SetItem(label, new BigInteger(44444));
                row.SetItem(key, changed);
                context.Variables["conversion"] = length;
            };

            try
            {
                if (refuse)
                {
                    var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () => { _ = await Run(); });
                    Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
                    Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
                    Assert.Same(retained, totals.GetItem(label));
                    Assert.Same(previous, row.GetItem(key));
                }
                else
                {
                    var result = Assert.IsType<PyTuple>(await Run());
                    // The shared lookup cache is first admitted before the get call.
                    // Both the updated dictionary value and live converter are observed.
                    Assert.Equal([new BigInteger(44446), new BigInteger(44448), new BigInteger(44468)], result.Cast<BigInteger>());
                    Assert.Same(changed, row.GetItem(key));
                }
                Assert.Equal(!refuse, reentered);
                Assert.Null(context.CurrentExecutableFrame);
                Assert.Equal(0, context.State.CurrentInterpreterDepth);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            finally { context.State.CloseResources(); }

            ValueTask<object?> Run() => asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
                : new(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
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
