using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RepresentativeScriptTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var name in new[] { "csv-summary", "text-summary", "json-summary", "file-manifest", "file-search" })
            foreach (var repetitions in new[] { 0, 16, 1024 })
                foreach (var asynchronous in new[] { false, true })
                    yield return [name, repetitions, asynchronous];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task CompleteJobPublishesIndependentGoldenFiles(string name, int repetitions, bool asynchronous)
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "RepresentativeScripts", name + ".py"));
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
        var inputs = new Dictionary<string, byte[]>();
        var outputs = new Dictionary<string, byte[]>();
        string stdout;
        byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);
        string Repeat(string value) => string.Concat(Enumerable.Repeat(value, repetitions));
        switch (name)
        {
            case "csv-summary":
                inputs["/input.csv"] = Utf8("kind,amount,note\n" + Repeat("red,3,\"café,\nquoted\"\nblue,2,😀\nred,-1,skip\nblue,5,ok\n"));
                outputs["/report.csv"] = Utf8("kind,total\n" + (repetitions == 0 ? "" : $"blue,{7 * repetitions}\nred,{3 * repetitions}\n"));
                stdout = $"{3 * repetitions}\n";
                break;
            case "text-summary":
                inputs["/input.txt"] = Utf8(Repeat("Alpha café 😀\nalpha BETA café\n\n"));
                outputs["/report.txt"] = Utf8(repetitions == 0 ? "" : $"alpha={2 * repetitions}\nbeta={repetitions}\ncafé={2 * repetitions}\n😀={repetitions}\n");
                stdout = $"{6 * repetitions}\n";
                break;
            case "json-summary":
                inputs["/input.jsonl"] = Utf8(Repeat("{\"kind\":\"red\",\"amount\":3}\n{\"kind\":\"blue\",\"amount\":2}\n{\"kind\":\"red\",\"amount\":-1}\n{\"kind\":\"blue\",\"amount\":5}\n\n"));
                outputs["/report.json"] = Utf8(repetitions == 0 ? "{}\n" : $"{{\"blue\":{7 * repetitions},\"red\":{3 * repetitions}}}\n");
                stdout = $"{3 * repetitions}\n";
                break;
            case "file-search":
                inputs["/input.txt"] = Utf8(repetitions == 0 ? "" : Repeat("ordinary café line\n")
                    + "NEEDLE: chosen café 😀\n" + Repeat("unread trailing line\n"));
                outputs["/matches.txt"] = Utf8(repetitions == 0 ? "" : "NEEDLE: chosen café 😀\n");
                stdout = $"{(repetitions == 0 ? 0 : repetitions + 1)}\n";
                break;
            default:
                var bytes = Enumerable.Range(0, repetitions).SelectMany(_ => new byte[] { 0, 127, 128, 255 }).ToArray();
                inputs["/inbox/a.bin"] = bytes;
                inputs["/inbox/é.txt"] = Utf8("café 😀\n");
                outputs["/outbox/a.bin"] = bytes;
                outputs["/outbox/é.txt"] = inputs["/inbox/é.txt"];
                outputs["/manifest.json"] = Utf8($"[{{\"checksum\":{510 * repetitions},\"name\":\"a.bin\",\"size\":{4 * repetitions}}},{{\"checksum\":1383,\"name\":\"\\u00e9.txt\",\"size\":11}}]\n");
                stdout = "2\n";
                break;
        }

        // This is one finite invocation; guest temporaries are conservatively
        // retained within it. The separate batch test checks sustained reuse.
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = repetitions <= 16 ? 262144 : 8 * 1024 * 1024 };
        LythonExecutionResult result;
        if (asynchronous)
        {
            var host = new DelayedLythonHost();
            foreach (var input in inputs)
                if (input.Key.StartsWith("/inbox/", StringComparison.Ordinal)) host.SeedBytes(input.Key, input.Value);
                else host.SeedFile(input.Key, Encoding.UTF8.GetString(input.Value));
            result = await script.RunAsync(host, options);
            Assert.True(host.CompletedAsynchronously > 0);
            Assert.True(result.Success, result.Failure?.Message);
            foreach (var output in outputs) Assert.Equal(output.Value, ReadOutput(output.Key, host.ReadText, host.ReadBytes));
        }
        else
        {
            var host = new MockLythonHost();
            foreach (var input in inputs)
                if (input.Key.StartsWith("/inbox/", StringComparison.Ordinal)) host.SeedBytes(input.Key, input.Value);
                else host.SeedFile(input.Key, Encoding.UTF8.GetString(input.Value));
            result = script.Run(host, options);
            Assert.True(result.Success, result.Failure?.Message);
            foreach (var output in outputs) Assert.Equal(output.Value, ReadOutput(output.Key, host.ReadText, host.ReadBytes));
        }
        Assert.Equal(stdout, result.StandardOutput);
        Assert.Empty(result.StandardError);
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, options.MaxExecutionMemoryBytes!.Value.Bytes);
        result.Dispose();
    }

    [Theory]
    [InlineData("csv-summary", false)]
    [InlineData("text-summary", false)]
    [InlineData("json-summary", false)]
    [InlineData("file-manifest", false)]
    [InlineData("csv-summary", true)]
    [InlineData("text-summary", true)]
    [InlineData("json-summary", true)]
    [InlineData("file-manifest", true)]
    public Task HostBatchesReturnSharedAccountingToTheCompiledBaseline(string name, bool asynchronous)
        => VerifyHostBatches(name, asynchronous, 8);

    [Theory]
    [Trait("Category", "Stress")]
    [InlineData("csv-summary", true)]
    [InlineData("text-summary", true)]
    [InlineData("json-summary", true)]
    [InlineData("file-manifest", true)]
    public Task LongerHostBatchesReturnSharedAccountingToBaseline(string name, bool asynchronous)
        => VerifyHostBatches(name, asynchronous, 32);

    private static async Task VerifyHostBatches(string name, bool asynchronous, int batches)
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "RepresentativeScripts", name + ".py"));
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MemoryBudget = budget };
        for (var batch = 0; batch < batches; batch++)
        {
            var csv = "kind,amount,note\n" + string.Concat(Enumerable.Repeat("red,3,ok\nblue,2,ok\n", 16));
            var text = string.Concat(Enumerable.Repeat("Alpha café 😀\nalpha BETA café\n", 16));
            var json = string.Concat(Enumerable.Repeat("{\"kind\":\"red\",\"amount\":3}\n{\"kind\":\"blue\",\"amount\":2}\n", 16));
            LythonExecutionResult result;
            string output;
            if (asynchronous)
            {
                var host = new DelayedLythonHost();
                host.SeedFile("/input.csv", csv);
                host.SeedFile("/input.txt", text);
                host.SeedFile("/input.jsonl", json);
                host.SeedBytes("/inbox/a.bin", [0, 127, 128, 255]);
                result = await script.RunAsync(host, options);
                Assert.True(host.CompletedAsynchronously > 0);
                output = name switch { "csv-summary" => host.ReadText("/report.csv"), "text-summary" => host.ReadText("/report.txt"),
                    "json-summary" => host.ReadText("/report.json"), _ => host.ReadText("/manifest.json") };
            }
            else
            {
                var host = new MockLythonHost();
                host.SeedFile("/input.csv", csv);
                host.SeedFile("/input.txt", text);
                host.SeedFile("/input.jsonl", json);
                host.SeedBytes("/inbox/a.bin", [0, 127, 128, 255]);
                result = script.Run(host, options);
                output = name switch { "csv-summary" => host.ReadText("/report.csv"), "text-summary" => host.ReadText("/report.txt"),
                    "json-summary" => host.ReadText("/report.json"), _ => host.ReadText("/manifest.json") };
            }
            using (result)
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(name switch { "csv-summary" => "kind,total\nblue,32\nred,48\n", "text-summary" => "alpha=32\nbeta=16\ncafé=32\n😀=16\n",
                    "json-summary" => "{\"blue\":32,\"red\":48}\n", _ => "[{\"checksum\":510,\"name\":\"a.bin\",\"size\":4}]\n" }, output);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.InRange(budget.PeakBytes, baseline + 1, 512 * 1024);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static byte[] ReadOutput(string path, Func<string, string> readText, Func<string, byte[]> readBytes)
        => path.StartsWith("/outbox/", StringComparison.Ordinal) ? readBytes(path) : Encoding.UTF8.GetBytes(readText(path));

    [Theory]
    [InlineData("csv-summary", "/report.csv")]
    [InlineData("text-summary", "/report.txt")]
    [InlineData("json-summary", "/report.json")]
    [InlineData("file-manifest", "/manifest.json")]
    public async Task CancelledPendingPublicationPreservesPreviousFileBytes(string name, string reportPath)
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "RepresentativeScripts", name + ".py"));
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        var host = new DelayedLythonHost();
        host.SeedFile("/input.csv", "kind,amount,note\nred,3,café\nblue,2,😀\n");
        host.SeedFile("/input.txt", "Alpha café 😀\n");
        host.SeedFile("/input.jsonl", "{\"kind\":\"red\",\"amount\":3}\n");
        host.SeedBytes("/inbox/a.bin", [0, 127, 128, 255]);
        host.SeedBytes("/inbox/é.txt", Encoding.UTF8.GetBytes("café 😀\n"));
        const string previous = "previous café 😀\n";
        host.SeedFile(reportPath, previous);
        var started = host.PauseWriteUntilCancellation(reportPath);
        using var cancellation = new CancellationTokenSource();
        var run = script.RunAsync(host, new LythonRunOptions
        {
            CancellationToken = cancellation.Token,
            MaxExecutionMemoryBytes = 262144,
        });
        try
        {
            await started.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(run.IsCompleted);
            Assert.True(host.CompletedAsynchronously > 0);
            Assert.Equal(Encoding.UTF8.GetBytes(previous), Encoding.UTF8.GetBytes(host.ReadText(reportPath)));
        }
        finally { cancellation.Cancel(); }
        var result = await run.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(result.Success);
        Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        Assert.Empty(result.StandardOutput);
        Assert.Empty(result.StandardError);
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, 262144);
        Assert.True(result.Consumption?.HostCalls > 0);
        Assert.Equal(Encoding.UTF8.GetBytes(previous), Encoding.UTF8.GetBytes(host.ReadText(reportPath)));
        // Each mediated replacement is atomic. Earlier successful files remain
        // committed; the complete job does not promise a filesystem transaction.
        if (name == "file-manifest")
        {
            Assert.Equal(new byte[] { 0, 127, 128, 255 }, host.ReadBytes("/outbox/a.bin"));
            Assert.Equal(Encoding.UTF8.GetBytes("café 😀\n"), host.ReadBytes("/outbox/é.txt"));
        }
    }

    [Theory]
    [InlineData("csv-summary", false)]
    [InlineData("csv-summary", true)]
    [InlineData("json-summary", false)]
    [InlineData("json-summary", true)]
    [InlineData("text-summary", false)]
    [InlineData("text-summary", true)]
    [InlineData("file-manifest", false)]
    [InlineData("file-manifest", true)]
    public async Task InvalidOrMissingInputFailsBeforeReportPublication(string name, bool asynchronous)
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "RepresentativeScripts", name + ".py"));
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
        var delayed = asynchronous ? new DelayedLythonHost() : null;
        var mock = asynchronous ? null : new MockLythonHost();
        ILythonHost host = (ILythonHost?)delayed ?? mock!;
        var inputPath = name == "csv-summary" ? "/input.csv" : "/input.jsonl";
        var input = name == "csv-summary" ? "kind,amount,note\nred,nope,invalid\n" : "{\"kind\":\"red\",\"amount\":invalid}\n";
        if (name is "csv-summary" or "json-summary")
        {
            if (delayed is not null) delayed.SeedFile(inputPath, input);
            else mock!.SeedFile(inputPath, input);
        }
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
        var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
        // Check actual run I/O before the asynchronous output-existence checks.
        if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
        Assert.False(result.Success);
        var expected = name switch
        {
            "csv-summary" => "ValueError",
            "json-summary" => "JSONDecodeError",
            // SPEC: generic host path/resource failures become RuntimeError.
            // CPython's concrete filesystem reports FileNotFoundError instead.
            _ => "RuntimeError"
        };
        Assert.Equal(expected, result.Failure?.ExceptionType);
        Assert.Empty(result.StandardOutput);
        Assert.Empty(result.StandardError);
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, 262144);
        foreach (var output in new[] { "/report.csv", "/report.json", "/report.txt", "/manifest.json" })
            Assert.False(await host.ExistsAsync(output, CancellationToken.None));
    }

    [Theory]
    [InlineData("csv-summary", false)]
    [InlineData("csv-summary", true)]
    [InlineData("json-summary", false)]
    [InlineData("json-summary", true)]
    public async Task RetainedRecordsRejectUnderTheSameBudgetAsCompletedStreamingJobs(string name, bool asynchronous)
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "RepresentativeScripts", name + ".py"))
            .Replace("totals = {}", "totals = {}\nretained = []");
        string input, path;
        if (name == "csv-summary")
        {
            source = source.Replace("        amount = int(row['amount'])", "        retained.append(row)\n        amount = int(row['amount'])");
            input = "kind,amount,note\n" + string.Concat(Enumerable.Repeat("red,3,\"café,\nquoted\"\nblue,2,😀\nred,-1,skip\nblue,5,ok\n", 1024));
            path = "/input.csv";
        }
        else
        {
            source = source.Replace("            record = json.loads(line)", "            record = json.loads(line)\n            retained.append(record)");
            input = string.Concat(Enumerable.Repeat("{\"kind\":\"red\",\"amount\":3}\n{\"kind\":\"blue\",\"amount\":2}\n{\"kind\":\"red\",\"amount\":-1}\n{\"kind\":\"blue\",\"amount\":5}\n\n", 1024));
            path = "/input.jsonl";
        }
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
        LythonExecutionResult result;
        if (asynchronous)
        {
            var host = new DelayedLythonHost();
            host.SeedFile(path, input);
            result = await script.RunAsync(host, options);
            Assert.True(host.CompletedAsynchronously > 0);
        }
        else
        {
            var host = new MockLythonHost();
            host.SeedFile(path, input);
            result = script.Run(host, options);
        }
        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        Assert.Empty(result.StandardOutput);
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, 262144);
    }

    [Theory]
    [InlineData("csv-summary", "/input.csv")]
    [InlineData("text-summary", "/input.txt")]
    [InlineData("json-summary", "/input.jsonl")]
    [InlineData("file-manifest", "/inbox/a.bin")]
    public async Task CancellationInterruptsActualHostReadBeforePublication(string name, string path)
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "RepresentativeScripts", name + ".py"));
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
        var host = new DelayedLythonHost();
        if (name == "file-manifest") host.SeedBytes(path, [0, 127, 128, 255]);
        else host.SeedFile(path, name == "csv-summary" ? "kind,amount,note\nred,1,ok\n" : "{}\n");
        var started = host.PauseReadUntilCancellation(path);
        using var cancellation = new CancellationTokenSource();
        var run = script.RunAsync(host, new LythonRunOptions
        {
            CancellationToken = cancellation.Token,
            MaxExecutionMemoryBytes = 262144
        });
        try
        {
            await started.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(run.IsCompleted);
        }
        finally { cancellation.Cancel(); }
        var result = await run.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("cancel", result.Failure?.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.StandardOutput);
    }

    [Theory]
    [InlineData("csv-summary", false)]
    [InlineData("csv-summary", true)]
    [InlineData("json-summary", false)]
    [InlineData("json-summary", true)]
    public async Task ConversionAndParseFailuresCloseTheirSourceBeforeGuestRecovery(string name, bool asynchronous)
    {
        var original = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "RepresentativeScripts", name + ".py")).Replace("\r\n", "\n");
        var source = "try:\n" + string.Join('\n', original.Split('\n').Select(line => " " + line)) +
            "\nexcept ValueError:\n return source.closed\n";
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
        var delayed = asynchronous ? new DelayedLythonHost() : null;
        var mock = asynchronous ? null : new MockLythonHost();
        ILythonHost host = (ILythonHost?)delayed ?? mock!;
        var path = name == "csv-summary" ? "/input.csv" : "/input.jsonl";
        var input = name == "csv-summary" ? "kind,amount,note\nred,nope,invalid\n" : "{\"kind\":\"red\",\"amount\":invalid}\n";
        if (delayed is not null) delayed.SeedFile(path, input); else mock!.SeedFile(path, input);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
        var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(true, result.ReturnValue);
        Assert.Empty(result.StandardOutput);
        if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
        Assert.False(await host.ExistsAsync(name == "csv-summary" ? "/report.csv" : "/report.json", CancellationToken.None));
    }
}
