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
                state.JsonRecordLayoutAtDepth(0) = (first.ShareRecordKeys(context, span), false);
            }
            var snapshot = state.JsonRecordLayoutAtDepth(0);
            var replacement = new PyDict(context.MemoryGovernor, span);
            replacement.SetItem(retained, PyNone.Instance);
            replacement.SetItem(PyString.FromString("other-name", context.MemoryGovernor, span), PyNone.Instance);
            var before = budget.ReservedBytes;
            Assert.True(budget.TryReserve(limit - before, out var blocker));
            using (blocker)
            {
                var failure = Assert.Throws<LythonRuntimeException>(() =>
                    state.JsonRecordLayoutAtDepth(0) = (replacement.ShareRecordKeys(context, span), false));
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
                Assert.Same(failure, context.MemoryGovernor.AdmissionFailure);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal(limit, budget.ReservedBytes);
                Assert.Equal(snapshot, state.JsonRecordLayoutAtDepth(0));
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
        var code = ExecutableScript.Compile(LythonFrontend.Compile(
            "import json\nreturn json.loads(source)", regexProvider: Utf8RegexProvider.Instance).Script!);
        var bytes = Encoding.UTF8.GetBytes("""[{"alpha":1},{"\u0061lpha":2},{"café😀":3},{"caf\u00e9\ud83d\ude00":4},{},{"café😀":5},{"":6},{"":7}]""");
        context.Variables["source"] = PyString.FromOwnedUtf8(bytes, context.MemoryGovernor, null);
        async Task<PyList> Read() => Assert.IsType<PyList>(asynchronous
            ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context)
            : LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null));
        var first = await Read();
        var dictionaries = first.Select(item => Assert.IsType<PyDict>(item)).ToArray();
        var keys = dictionaries.Where(dict => dict.Count != 0)
            .Select(dict => Assert.IsType<PyString>(dict.Items.Single().Key)).ToArray();
        Assert.Same(keys[0], keys[1]);
        Assert.Same(keys[2], keys[3]);
        Assert.Empty(dictionaries[4].Items);
        Assert.Same(keys[3], keys[4]);
        Assert.Same(keys[5], keys[6]);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedNestedRecordsReuseFundedKeysWithoutSharingValues(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            const string source = """
                import json
                return (json.loads('{"details":{"quantity":1},"label":"first"}'),
                        json.loads('{"details":{"quantity":2},"label":"second"}'))
                """;
            var code = ExecutableScript.Compile(LythonFrontend.Compile(source).Script!).EntryPoint;
            var result = Assert.IsType<PyTuple>(asynchronous
                ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
                : LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
            var first = Assert.IsType<PyDict>(result[0]);
            var second = Assert.IsType<PyDict>(result[1]);
            var firstDetails = Assert.IsType<PyDict>(first.Items.First().Value);
            var secondDetails = Assert.IsType<PyDict>(second.Items.First().Value);
            Assert.Same(first.Keys.First(), second.Keys.First());
            Assert.Same(firstDetails.Keys.Single(), secondDetails.Keys.Single());

            var sharedKey = firstDetails.Keys.Single();
            firstDetails.SetItem(sharedKey, PyNone.Instance);
            Assert.Equal(new System.Numerics.BigInteger(2), secondDetails.GetItem(sharedKey));
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.True(budget.ReservedBytes > 0);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
