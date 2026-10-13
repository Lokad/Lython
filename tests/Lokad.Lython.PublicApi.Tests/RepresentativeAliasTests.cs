using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

[Collection("RuntimeStress")]
public sealed class RepresentativeAliasTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BodyConversionErrorDoesNotExhaustACallerRetainedCsvReader(bool asynchronous)
    {
        var source = """
import csv
reader = csv.DictReader(['kind,amount', 'bad,nope', 'good,2'])
try:
    for row in reader:
        int(row['amount'])
except ValueError:
    pass
row = next(reader)
return [row['kind'], int(row['amount']), next(reader, None)]
""";
        var script = new LythonEngine().Compile(source);
        var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { "good", new System.Numerics.BigInteger(2), null }, result.ReturnValue);
    }

    public static IEnumerable<object[]> Cases()
    {
        foreach (var name in new[] { "csv-summary", "json-summary", "text-summary" })
        foreach (var retainAll in new[] { false, true })
        foreach (var asynchronous in new[] { false, true }) yield return [name, retainAll, asynchronous];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task EscapedAliasSurvivesClosureWhileRetainingAllValuesRejects(string name, bool retainAll, bool asynchronous)
    {
        const int repetitions = 1024;
        string Repeat(string value) => string.Concat(Enumerable.Repeat(value, repetitions));
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "RepresentativeScripts", name + ".py"));
        string input, inputPath, reportPath, report, stdout, alias, needle, capture;
        if (name == "csv-summary")
        {
            input = "kind,amount,note\n" + Repeat("red,3,\"café,\nquoted\"\nblue,2,😀\nred,-1,skip\nblue,5,ok\n");
            inputPath = "/input.csv"; reportPath = "/report.csv";
            report = "kind,total\nblue,7168\nred,3072\n"; stdout = "3072\n";
            alias = "café,\nquoted";
            needle = "        amount = int(row['amount'])";
            capture = "row['note']";
        }
        else if (name == "json-summary")
        {
            input = Repeat("{\"kind\":\"red\",\"amount\":3}\n{\"kind\":\"blue\",\"amount\":2}\n{\"kind\":\"red\",\"amount\":-1}\n{\"kind\":\"blue\",\"amount\":5}\n\n");
            inputPath = "/input.jsonl"; reportPath = "/report.json";
            report = "{\"blue\":7168,\"red\":3072}\n"; stdout = "3072\n";
            alias = "red";
            needle = "            amount = record['amount']";
            capture = "record['kind']";
        }
        else
        {
            input = Repeat("Alpha café 😀\nalpha BETA café\n\n");
            inputPath = "/input.txt"; reportPath = "/report.txt";
            report = "alpha=2048\nbeta=1024\ncafé=2048\n😀=1024\n"; stdout = "6144\n";
            alias = "Alpha café 😀\n";
            needle = "        for word in line.lower().split():";
            capture = "line";
        }
        Assert.Contains(needle, source, StringComparison.Ordinal);
        var indent = needle[..(needle.Length - needle.TrimStart().Length)];
        var insertion = retainAll ? indent + "held.append(" + capture + ")\n"
            : indent + "if not held:\n" + indent + "    held.append(" + capture + ")\n";
        source = "held = []\n" + source.Replace(needle, insertion + needle) + "\nassert source.closed\nreturn held[0]\n";
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        var delayed = asynchronous ? new DelayedLythonHost() : null;
        var mock = asynchronous ? null : new MockLythonHost();
        ILythonHost host = (ILythonHost?)delayed ?? mock!;
        var collectedBeforeProjection = false;
        void CollectAfterClosedScan()
        {
            GC.Collect();
            collectedBeforeProjection = true;
        }
        if (!retainAll)
        {
            if (delayed is not null) delayed.OnStandardOutputWrite = CollectAfterClosedScan;
            else mock!.OnStandardOutputWrite = CollectAfterClosedScan;
        }
        if (delayed is not null) delayed.SeedFile(inputPath, input); else mock!.SeedFile(inputPath, input);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
        var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
        if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, 262144);
        if (retainAll)
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.ExecutionMemoryLimit, result.FailureAttribution?.Reason);
            Assert.Empty(result.StandardOutput);
            Assert.False(await host.ExistsAsync(reportPath, CancellationToken.None));
        }
        else
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(alias, result.ReturnValue);
            Assert.True(collectedBeforeProjection);
            Assert.Equal(stdout, result.StandardOutput);
            var actual = delayed is not null ? delayed.ReadText(reportPath) : mock!.ReadText(reportPath);
            Assert.Equal(Encoding.UTF8.GetBytes(report), Encoding.UTF8.GetBytes(actual));
        }
    }
}
