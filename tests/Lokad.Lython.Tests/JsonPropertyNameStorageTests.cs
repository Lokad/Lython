using System.Text;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class JsonPropertyNameStorageTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RefusalPreservesFundedNamesAndReturnsAllowanceWhenOwnershipEnds(bool populated)
    {
        const long limit = 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            var state = context.State;
            var span = new LythonSourceSpan(0, 0, 0, 0);
            var retained = PyString.FromString("retained-name", context.MemoryGovernor, span);
            if (populated)
            {
                var first = new PyDict(context.MemoryGovernor, span);
                first.SetItem(retained, PyNone.Instance);
                state.JsonRecordLayout = first.ShareRecordKeys(context, span);
            }
            var snapshot = state.JsonRecordLayout;
            var replacement = new PyDict(context.MemoryGovernor, span);
            replacement.SetItem(retained, PyNone.Instance);
            replacement.SetItem(PyString.FromString("other-name", context.MemoryGovernor, span), PyNone.Instance);
            var before = budget.ReservedBytes;
            Assert.True(budget.TryReserve(limit - before, out var blocker));
            using (blocker)
            {
                var failure = Assert.Throws<LythonRuntimeException>(() =>
                    state.JsonRecordLayout = replacement.ShareRecordKeys(context, span));
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
                Assert.Same(failure, context.MemoryGovernor.AdmissionFailure);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal(limit, budget.ReservedBytes);
                Assert.Same(snapshot, state.JsonRecordLayout);
                Assert.Equal(2, replacement.Count);
                Assert.Same(retained, replacement.Keys.First());
                Assert.Equal("retained-name", retained.AsString());
            }
            Assert.Equal(before, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedKeysOwnTheirBytesAndKeepMutableRecordsIndependent(bool asynchronous)
    {
        using var budget = new LythonMemoryBudget(1024 * 1024).CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = budget });
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(
            "import json\nreturn json.loads(source)", regexProvider: Utf8RegexProvider.Instance).Script!));
        var bytes = Encoding.UTF8.GetBytes("""[{"alpha":1},{"\u0061lpha":2},{"café😀":3},{"caf\u00e9\ud83d\ude00":4},{"":5},{"":6}]""");
        context.Variables["source"] = PyString.FromOwnedUtf8(bytes, context.MemoryGovernor, null);
        async Task<PyList> Read() => Assert.IsType<PyList>(asynchronous
            ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context)
            : LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null));
        var first = await Read();
        var dictionaries = first.Select(item => Assert.IsType<PyDict>(item)).ToArray();
        var keys = dictionaries.Select(dict => Assert.IsType<PyString>(dict.Items.Single().Key)).ToArray();
        Assert.Same(keys[0], keys[1]);
        Assert.Same(keys[2], keys[3]);
        Assert.Same(keys[4], keys[5]);
        Assert.NotSame(dictionaries[0], dictionaries[1]);
        dictionaries[0].SetItem(keys[0], PyNone.Instance);
        Assert.NotSame(PyNone.Instance, dictionaries[1].Items.Single().Value);

        // Mutating the white-box input backing must not change retained names
        // or corrupt a hint used by a later decode in this execution.
        bytes[3] = (byte)'z';
        var second = await Read();
        var next = second.Select(item => Assert.IsType<PyDict>(item)).ToArray();
        Assert.Equal("alpha", keys[0].AsString());
        Assert.Equal("zlpha", Assert.IsType<PyString>(next[0].Items.Single().Key).AsString());
        Assert.Equal("alpha", Assert.IsType<PyString>(next[1].Items.Single().Key).AsString());
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.State.CurrentInterpreterDepth);
    }
}
