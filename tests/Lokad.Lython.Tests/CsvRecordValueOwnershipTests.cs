using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CsvRecordValueOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 1, 1);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletedRowsRemainIndependentThroughMutationCopyAndClear(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            var governor = context.MemoryGovernor;
            var names = new PyList([PyString.FromString("a"), PyString.FromString("b"), PyString.FromString("c")], governor, Span);
            var input = new PyList([PyString.FromString("one,two,three\n"), PyString.FromString("four,five,six\n")], governor, Span);
            var options = new LythonRuntime.CsvOptions(PyStringOps.CommaLiteral, PyString.FromString("\""),
                LythonRuntime.CsvQuotingMode.Minimal, true, null, false, PyString.FromString("\n"), false);
            var source = new LythonRuntime.CsvRecordSource(input, options, Span, context);
            var reader = new LythonRuntime.CsvDictReaderObject(source, names, PyNone.Instance, PyNone.Instance, context, Span);
            var first = await Next();
            var second = await Next();
            using var ambient = PyStructuralGuard.PushAmbient(context, Span);
            Assert.True(second.TryGetValue(names[0], out var saved));
            var retained = Assert.IsType<PyString>(saved);
            Assert.True(second.Remove(names[1]));
            second.SetItem(PyString.FromString("new"), new BigInteger(7));
            var copy = new PyDict(second, governor, Span);
            Assert.Equal(["a", "c", "new"], copy.Keys.Cast<PyString>().Select(k => k.AsString()));
            var before = governor.CurrentCommittedBytes;
            second.Clear();
            Assert.Empty(second.Items);
            // Clear retains this dictionary's backing capacity and its charge.
            Assert.Equal(before, governor.CurrentCommittedBytes);
            Assert.Equal("four", retained.AsString());
            Assert.Equal(["one", "two", "three"], first.Values.Cast<PyString>().Select(v => v.AsString()));
            Assert.Equal(3, copy.Count);
            Assert.True(copy.TryGetValue(names[0], out var copied));
            Assert.Same(retained, copied);
            var ended = asynchronous ? await reader.TryMoveNextAsync()
                : reader.TryMoveNext(out var value) ? PyIterationResult.Yield(value) : PyIterationResult.End;
            Assert.False(ended.HasValue);
            Assert.Equal(0, governor.CurrentReservedBytes);

            async ValueTask<PyDict> Next()
            {
                var next = asynchronous ? await reader.TryMoveNextAsync()
                    : reader.TryMoveNext(out var value) ? PyIterationResult.Yield(value) : PyIterationResult.End;
                Assert.True(next.HasValue);
                return Assert.IsType<PyDict>(next.Value);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("shrink")]
    [InlineData("cancel")]
    [InlineData("refuse")]
    public void AdmissionRetainsTheInputAndRechecksHeaderIdentity(string outcome)
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
            var governor = context.MemoryGovernor;
            var a = PyString.FromString("a", governor, Span);
            var b = PyString.FromString("b", governor, Span);
            var equalA = PyString.FromString("a", governor, Span);
            var fields = new PyList([a, b], governor, Span);
            var first = new PyDict(governor, Span);
            first.SetItem(a, BigInteger.One);
            first.SetItem(b, new BigInteger(2));
            var layout = first.ShareRecordKeys(context, Span)!;
            governor.Reserve(PyList.EstimateCapacityBytes(2), Span);
            object[] values = [new BigInteger(3), new BigInteger(4)];
            governor.Commit(PyList.EstimateCapacityBytes(2));
            var before = budget.ReservedBytes;
            if (outcome == "refuse") hook.RefuseGrowth = _ => true;
            else hook.AfterGrowth = _ =>
            {
                hook.AfterGrowth = null;
                if (outcome == "identity") fields[0] = equalA;
                else if (outcome == "shrink") fields.RemoveAt(1);
                else cancellation.Cancel();
            };
            if (outcome is "cancel" or "refuse")
            {
                var error = Assert.Throws<LythonRuntimeException>(() => layout.TryAdoptRecord(fields, values, context, Span));
                Assert.Equal(outcome == "cancel" ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance,
                    error.Attribution?.Reason);
                if (outcome == "refuse") Assert.Same(governor.AdmissionFailure, error);
            }
            else Assert.Null(layout.TryAdoptRecord(fields, values, context, Span));
            Assert.Equal(before, budget.ReservedBytes);
            Assert.Equal(0, governor.CurrentReservedBytes);
            Assert.Equal([new BigInteger(3), new BigInteger(4)], values.Cast<BigInteger>());
            Assert.Equal(2, first.Count);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
