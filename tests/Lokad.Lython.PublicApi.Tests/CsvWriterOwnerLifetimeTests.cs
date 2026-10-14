using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CsvWriterOwnerLifetimeTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var kind in new[] { "plain", "dictionary", "history", "retained-writers", "retained-methods", "escaped-history", "callback-failure", "retained-output" })
            foreach (var asynchronous in new[] { false, true }) yield return [kind, asynchronous];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task WriterBatchesReleaseTheirOwnersAndPreserveEscapedAliases(string kind, bool asynchronous)
    {
        var body = kind switch
        {
            "dictionary" => " w = csv.DictWriter(s, ['a','b'])\n w = None\n",
            "history" => " w = csv.writer()\n w.writerow([1,2])\n w = None\n",
            "retained-writers" => " w = csv.writer(s)\n kept.append(w)\n w = None\n",
            "retained-methods" => " w = csv.writer(s)\n kept.append(w.writerow)\n w = None\n",
            "escaped-history" => " w = csv.writer()\n w.writerow([1,2])\n if i == 0:\n  first = w.getvalue()\n w = None\n",
            "callback-failure" => " w = csv.writer(s)\n try:\n  w.writerow([1,2])\n except ValueError:\n  pass\n w = None\n",
            "retained-output" => " w = csv.writer(s)\n w.writerow([1,2])\n w = None\n",
            _ => " w = csv.writer(s)\n w = None\n"
        };
        var callback = kind == "callback-failure" ? "  raise ValueError('stop')\n" :
            kind == "retained-output" ? "  kept.append(text)\n  return len(text)\n" : "  return len(text)\n";
        var source = "with open('/prefix.txt') as f:\n f.read()\nimport csv\nkept = []\nfirst = None\nclass Sink:\n def write(self,text):\n" + callback +
            "s = Sink()\nfor i in range(5000):\n" + body +
            (kind == "escaped-history" ? "return i == 4999 and first == '1,2'\n" : "return i\n");
        var retained = kind.StartsWith("retained-", StringComparison.Ordinal);
        var count = retained ? 5000 : 16;
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine().Compile(source.Replace("5000", count.ToString()).Replace("4999", (count - 1).ToString()), budget);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
        for (var batch = 0; batch < (retained ? 1 : 8); batch++)
        {
            LythonExecutionResult result;
            if (asynchronous)
            {
                var host = new DelayedLythonHost();
                host.SeedFile("/prefix.txt", "ready");
                result = await script.RunAsync(host, options);
                Assert.True(host.CompletedAsynchronously > 0);
            }
            else
            {
                var host = new MockLythonHost();
                host.SeedFile("/prefix.txt", "ready");
                result = script.Run(host, options);
            }
            Assert.InRange(result.PeakExecutionMemoryBytes, 1, 262144);
            if (kind.StartsWith("retained-", StringComparison.Ordinal))
            {
                Assert.False(result.Success);
                Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            }
            else
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(kind == "escaped-history" ? true : (object)new BigInteger(count - 1), result.ReturnValue);
            }
            result.Dispose();
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DelayedHostOutputIsExactAndWriteCancellationCompletes(bool dictionary)
    {
        var make = dictionary ? "csv.DictWriter(f, ['a','b'], lineterminator='\\n')" : "csv.writer(f, lineterminator='\\n')";
        var row = dictionary ? "{'a':1,'b':2}" : "[1,2]";
        var source = "import csv\nwith open('/out.csv', 'w') as f:\n w = " + make + "\n w.writerow(" + row + ")\n";
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
        var host = new DelayedLythonHost();
        var result = await script.RunAsync(host, new LythonRunOptions { MaxExecutionMemoryBytes = 262144 });
        Assert.True(result.Success, result.Failure?.Message);
        Assert.True(host.CompletedAsynchronously > 0);
        Assert.Equal("1,2\n", host.ReadText("/out.csv"));

        var cancelledHost = new DelayedLythonHost();
        var started = cancelledHost.PauseWriteUntilCancellation("/out.csv");
        using var cancellation = new CancellationTokenSource();
        var run = script.RunAsync(cancelledHost, new LythonRunOptions
        { MaxExecutionMemoryBytes = 262144, CancellationToken = cancellation.Token });
        try { await started.WaitAsync(TimeSpan.FromSeconds(5)); Assert.False(run.IsCompleted); }
        finally { cancellation.Cancel(); }
        var failed = await run.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(failed.Success);
        Assert.Equal("RuntimeError", failed.Failure?.ExceptionType);
        Assert.Contains("cancel", failed.Failure?.Message, StringComparison.OrdinalIgnoreCase);
    }
}
