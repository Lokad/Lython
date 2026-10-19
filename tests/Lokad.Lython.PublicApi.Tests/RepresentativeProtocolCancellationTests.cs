using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RepresentativeProtocolCancellationTests
{
    [Theory]
    [InlineData("csv-summary", false)]
    [InlineData("csv-summary", true)]
    [InlineData("json-summary", false)]
    [InlineData("json-summary", true)]
    public async Task ProtocolCheckpointCancellationPreventsCompletedReport(string name, bool asynchronous)
    {
        var original = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "RepresentativeScripts", name + ".py"));
        string source, input, inputPath, reportPath;
        int checkpoints;
        if (name == "csv-summary")
        {
            source = "def checked_lines(file):\n    for line in file:\n        print('checkpoint')\n        yield line\n" +
                original.Replace("csv.DictReader(source)", "csv.DictReader(checked_lines(source))");
            input = "kind,amount,note\nred,3,\"café,\nquoted\"\nblue,2,😀\n";
            // Header and first quoted physical line have been fed; cancel while
            // acquiring the closing line of the still-pending CSV record.
            inputPath = "/input.csv"; reportPath = "/report.csv"; checkpoints = 3;
        }
        else
        {
            source = "def inspect(record):\n    print('checkpoint')\n    return record\n" +
                original.Replace("json.loads(line)", "json.loads(line, object_hook=inspect)");
            // The inner object hook runs before the parent object is complete.
            input = "{\"nested\":{\"value\":1},\"kind\":\"red\",\"amount\":3}\n{\"kind\":\"blue\",\"amount\":2}\n";
            inputPath = "/input.jsonl"; reportPath = "/report.json"; checkpoints = 1;
        }
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        var delayed = asynchronous ? new DelayedLythonHost() : null;
        var mock = asynchronous ? null : new MockLythonHost();
        ILythonHost host = (ILythonHost?)delayed ?? mock!;
        if (delayed is not null) delayed.SeedFile(inputPath, input); else mock!.SeedFile(inputPath, input);
        using var cancellation = new CancellationTokenSource();
        var writes = 0;
        void CancelAtCheckpoint()
        {
            // print writes the text and newline separately; cancel on the
            // newline so the complete checkpoint remains in retained capture.
            if (++writes == checkpoints * 2) cancellation.Cancel();
        }
        if (delayed is not null) delayed.OnStandardOutputWrite = CancelAtCheckpoint;
        else mock!.OnStandardOutputWrite = CancelAtCheckpoint;
        var options = new LythonRunOptions { CancellationToken = cancellation.Token, MemoryBudget = new LythonMemoryBudget(262144) };
        var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
        if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
        Assert.False(result.Success);
        Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        Assert.Equal(string.Concat(Enumerable.Repeat("checkpoint\n", checkpoints)), result.StandardOutput);
        Assert.Empty(result.StandardError);
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, 262144);
        Assert.False(await host.ExistsAsync(reportPath, CancellationToken.None));
    }
}
