using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class RepresentativeLeaseTests
{
    [Theory]
    [InlineData("csv-summary", false)]
    [InlineData("csv-summary", true)]
    [InlineData("json-summary", false)]
    [InlineData("json-summary", true)]
    public async Task MidParserCancellationClosesSourceAndReleasesAbandonedScratch(string name, bool asynchronous)
    {
        var original = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "RepresentativeScripts", name + ".py"));
        var csv = name == "csv-summary";
        var source = csv
            ? "def checked_lines(file):\n    for line in file:\n        print('checkpoint')\n        yield line\n" + original.Replace("csv.DictReader(source)", "csv.DictReader(checked_lines(source))")
            : "def inspect(record):\n    print('checkpoint')\n    return record\n" + original.Replace("json.loads(line)", "json.loads(line, object_hook=inspect)");
        var input = csv ? "kind,amount,note\nred,3,\"café,\nquoted\"\n" : "{\"nested\":{\"value\":1},\"kind\":\"red\",\"amount\":3}\n";
        var path = csv ? "/input.csv" : "/input.jsonl";
        var delayed = asynchronous ? new DelayedLythonHost() : null;
        var mock = asynchronous ? null : new MockLythonHost();
        ILythonHost host = (ILythonHost?)delayed ?? mock!;
        if (delayed is not null) delayed.SeedFile(path, input); else mock!.SeedFile(path, input);
        using var cancellation = new CancellationTokenSource();
        var writes = 0;
        void CancelCheckpoint() { if (++writes == (csv ? 6 : 2)) cancellation.Cancel(); }
        if (delayed is not null) delayed.OnStandardOutputWrite = CancelCheckpoint; else mock!.OnStandardOutputWrite = CancelCheckpoint;
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions
        {
            CancellationToken = cancellation.Token, MaxExecutionMemoryBytes = 262144,
        });
        var frontend = LythonFrontend.Compile(source);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        var script = LoweredScript.Lower(frontend.Script!);
        LythonRuntimeException error;
        if (asynchronous)
            error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await LythonRuntime.ExecuteStatementsAsync(script.Statements, context));
        else error = Assert.Throws<LythonRuntimeException>(() => LythonRuntime.ExecuteStatements(script.Statements, context));
        Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
        Assert.True(LythonRuntime.TryResolveRuntimeMember(context.Variables["source"], "closed", context, new(0, 0, 0, 0), out var closed));
        Assert.Equal(true, closed);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
        GC.Collect();
        context.State.CallTemporaries.Sweep(full: true);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.True(context.MemoryGovernor.PeakAccountedBytes <= 262144);
    }

    public static IEnumerable<object[]> Cases()
    {
        foreach (var name in new[] { "csv-summary", "json-summary" })
        foreach (var invalid in new[] { false, true })
        foreach (var asynchronous in new[] { false, true }) yield return [name, invalid, asynchronous];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task CompleteParserPathsReleaseLeasesButKeepEscapedStorage(string name, bool invalid, bool asynchronous)
    {
        var original = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "RepresentativeScripts", name + ".py")).Replace("\r\n", "\n");
        string input, path, expected;
        if (name == "csv-summary")
        {
            original = original.Replace("        amount = int(row['amount'])",
                "        if escaped is None:\n            escaped = row['note']\n        amount = int(row['amount'])");
            input = "kind,amount,note\nred,3,\"café,\nquoted\"\nblue," + (invalid ? "bad" : "2") + ",last\n";
            path = "/input.csv"; expected = "café,\nquoted";
        }
        else
        {
            original = original.Replace("            amount = record['amount']",
                "            if escaped is None:\n                escaped = record['kind']\n            amount = record['amount']");
            input = "{\"kind\":\"red\",\"amount\":3}\n" + (invalid ? "{\"amount\":invalid}\n" : "{\"kind\":\"blue\",\"amount\":2}\n");
            path = "/input.jsonl"; expected = "red";
        }
        var source = "escaped = None\ntry:\n" + string.Join('\n', original.Split('\n').Select(line => "    " + line)) +
            "\nexcept ValueError:\n    pass\nreturn source.closed\n";
        var frontend = LythonFrontend.Compile(source);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        var script = LoweredScript.Lower(frontend.Script!);
        var delayed = asynchronous ? new DelayedLythonHost() : null;
        var mock = asynchronous ? null : new MockLythonHost();
        ILythonHost host = (ILythonHost?)delayed ?? mock!;
        if (delayed is not null) delayed.SeedFile(path, input); else mock!.SeedFile(path, input);
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 262144 });
        var flow = asynchronous ? await LythonRuntime.ExecuteStatementsAsync(script.Statements, context)
            : LythonRuntime.ExecuteStatements(script.Statements, context);
        Assert.NotNull(flow.Return);
        Assert.Equal(true, flow.Return!.Value);
        if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
        // A body conversion error leaves the Python CSV iterator resumable.
        // Its still-funded parser scratch is reclaimed on abandonment, rather
        // than disposing an iterator that a caller could have retained.
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        var escaped = Assert.IsType<PyString>(context.Variables["escaped"]);
        GC.Collect();
        context.State.CallTemporaries.Sweep(full: true);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(expected, escaped.AsString());
        Assert.Same(context.MemoryGovernor, escaped.OwnerMemoryGovernor);
        Assert.True(ChargeReclamationPool.IsTrackedValue(escaped));
        Assert.True(((IPyOwnershipSnapshot)escaped).TrySnapshotOwnership(out var charge));
        Assert.True(charge > 0);
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes >= charge);
        Assert.True(context.MemoryGovernor.PeakAccountedBytes <= 262144);
        GC.KeepAlive(context);
        GC.KeepAlive(escaped);
    }
}
