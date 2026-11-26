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
                : reader.TryMoveNext();
            Assert.False(ended.HasValue);
            Assert.Equal(0, governor.CurrentReservedBytes);

            async ValueTask<PyDict> Next()
            {
                var next = asynchronous ? await reader.TryMoveNextAsync()
                    : reader.TryMoveNext();
                Assert.True(next.HasValue);
                return Assert.IsType<PyDict>(next.Value);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, "adopt", "literal")]
    [InlineData(true, "expose", "literal")]
    [InlineData(false, "reenter", "literal")]
    [InlineData(true, "cancel", "literal")]
    [InlineData(true, "refuse", "literal")]
    [InlineData(false, "adopt", "transformed")]
    [InlineData(true, "expose", "transformed")]
    [InlineData(false, "reenter", "transformed")]
    [InlineData(true, "cancel", "transformed")]
    [InlineData(true, "refuse", "transformed")]
    [InlineData(false, "adopt", "staged")]
    [InlineData(true, "expose", "staged")]
    [InlineData(false, "reenter", "staged")]
    [InlineData(true, "reenter", "staged")]
    [InlineData(true, "cancel", "staged")]
    [InlineData(true, "refuse", "staged")]
    public async Task NativeRecordKeepsItsHeaderAndInputValidDuringAdmission(bool asynchronous, string outcome, string form)
    {
        var transformed = form == "transformed";
        var host = new MockLythonHost();
        host.SeedFile("/csv", transformed ? "a,b\n10,20\n30,\"4\"\"\"\n50,60\n" : "a,b\n10,20\n30,40\n50,60\n");
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var hook = new AdmissionHookMemoryBudget(owner);
        var context = new LythonRuntime.ExecutionContext(host, new()
        {
            MemoryBudget = hook, CancellationToken = cancellation.Token,
        });
        var file = await LythonRuntime.ExecutionContext.TextFileHandle.ForReadAsync("/csv", context,
            LythonRuntime.TextEncodingMode.Utf8, LythonRuntime.TextErrorMode.Strict, LythonRuntime.TextNewlineMode.TranslateUniversal);
        var options = new LythonRuntime.CsvOptions(PyStringOps.CommaLiteral, PyString.FromString("\""),
            LythonRuntime.CsvQuotingMode.Minimal, true, null, form == "staged", PyString.FromString("\n"), false);
        var source = new LythonRuntime.CsvRecordSource(file, options, Span, context);
        var reader = new LythonRuntime.CsvDictReaderObject(source, PyNone.Instance, PyNone.Instance, PyNone.Instance, context, Span);
        async ValueTask<PyIterationResult> Next() => asynchronous ? await reader.TryMoveNextAsync() : reader.TryMoveNext();
        try
        {
            var first = Assert.IsType<PyDict>((await Next()).Value);
            var key = Assert.IsType<PyString>(first.Keys.First());
            Assert.True(reader.TryGetMember("reader", out var raw));
            var rawReader = Assert.IsType<LythonRuntime.CsvReaderObject>(raw);
            var before = budget.ReservedBytes;
            var combined = PyList.EstimateCapacityBytes(2) + 2 * PyString.EstimateApproximateBytes(2) + PyDict.RecordKeys.CsvRecordShellBytes;
            var requests = new List<long>();
            PyString? replacement = null;
            hook.RefuseGrowth = amount =>
            {
                requests.Add(amount);
                return outcome == "refuse";
            };
            hook.AfterGrowth = _ =>
            {
                hook.AfterGrowth = null;
                if (outcome == "expose")
                {
                    Assert.True(reader.TryGetMember("fieldnames", out var fields));
                    replacement = PyString.FromString("a");
                    Assert.IsType<PyList>(fields)[0] = replacement;
                }
                else if (outcome == "reenter")
                {
                    var error = Assert.Throws<LythonRuntimeException>(() => rawReader.TryMoveNext());
                    Assert.Equal("reentrant text read", error.Message);
                }
                else if (outcome == "cancel") cancellation.Cancel();
            };

            if (outcome is "cancel" or "refuse")
            {
                var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () => { await Next(); });
                Assert.Equal(outcome == "cancel" ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance,
                    failure.Attribution?.Reason);
                if (outcome == "refuse") Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
                Assert.Equal(combined, Assert.Single(requests));
                Assert.Equal(before, budget.ReservedBytes);
            }
            else
            {
                var second = Assert.IsType<PyDict>((await Next()).Value);
                Assert.Equal(["30", transformed ? "4\"" : "40"], second.Values.Cast<PyString>().Select(v => v.AsString()));
                Assert.Same(replacement ?? key, second.Keys.First());
                Assert.Equal(combined, requests[0]);
                if (outcome != "expose") Assert.Single(requests);
                hook.RefuseGrowth = null;
                var rawThird = asynchronous ? await rawReader.TryMoveNextAsync() : rawReader.TryMoveNext();
                Assert.Equal(["50", "60"], Assert.IsType<PyList>(rawThird.Value).Cast<PyString>().Select(v => v.AsString()));
            }
            Assert.Same(key, first.Keys.First());
            Assert.Equal(["10", "20"], first.Values.Cast<PyString>().Select(v => v.AsString()));
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        finally
        {
            hook.AfterGrowth = null;
            hook.RefuseGrowth = null;
            context.State.CloseResources();
        }
        owner.Dispose();
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
