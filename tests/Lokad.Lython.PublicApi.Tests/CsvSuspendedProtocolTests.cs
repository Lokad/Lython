using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CsvSuspendedProtocolTests
{
    public static IEnumerable<object[]> Callbacks()
    {
        yield return new object[] { "reader-iterator", "class Source:\n def __iter__(self):\n  read()\n  return iter(['a'])\nr=csv.reader(Source())\nprint('made')\nprint(next(r))", "read\nmade\n['a']\n" };
        yield return new object[] { "field-iterator", "def fields():\n yield read()\nr=csv.DictReader(['1'],fields())\nprint(next(r))", "read\n{'a': '1'}\n" };
        yield return new object[] { "lazy-header", "def rows():\n yield read()\n yield '1'\nr=csv.DictReader(rows())\nprint('made',r.line_num)\nprint(r.fieldnames,r.line_num)\nprint(next(r))", "made 0\nread\n['a'] 1\n{'a': '1'}\n" };
        yield return new object[] { "option-truth", "class Flag:\n def __bool__(self):\n  read()\n  return True\nprint(list(csv.reader(['a, b'],skipinitialspace=Flag())))", "read\n[['a', 'b']]\n" };
        yield return new object[] { "cell-str", "class Cell:\n def __str__(self):return read()\ns=io.StringIO()\ncsv.writer(s,lineterminator=';').writerow([Cell()])\nprint(s.getvalue())", "read\na;\n" };
        yield return new object[] { "mapping-keys", "class Row:\n def keys(self):\n  read()\n  return {'a':0}.keys()\n def get(self,key,default):return 'value'\ns=io.StringIO()\ncsv.DictWriter(s,['a'],lineterminator=';').writerow(Row())\nprint(s.getvalue())", "read\nvalue;\n" };
        yield return new object[] { "mapping-get", "class Row:\n def keys(self):return {'a':0}.keys()\n def get(self,key,default):return read()\ns=io.StringIO()\ncsv.DictWriter(s,['a'],lineterminator=';').writerow(Row())\nprint(s.getvalue())", "read\na;\n" };
    }

    private const string Prefix = "import csv,io\ndef read():\n with open('/value') as f:value=f.read()\n print('read')\n return value\n";

    [Theory]
    [MemberData(nameof(Callbacks))]
    public async Task ActualSuspensionPreservesProtocolEffectsAndOwnership(string name, string body, string expected)
    {
        using var script = new LythonEngine().Compile(Prefix+body);
        Assert.True(script.IsValid, name+": "+string.Join("; ",script.Diagnostics.Select(d=>d.Message)));
        var budget = new LythonMemoryBudget(262144);
        var host = new DelayedLythonHost();
        host.SeedFile("/value","a");
        using(var result = await script.RunAsync(host,new LythonRunOptions {MemoryBudget=budget}))
        {
            Assert.True(result.Success,result.Failure?.Message);
            Assert.Equal(expected,result.StandardOutput);
            Assert.True(host.CompletedAsynchronously>0);
        }
        Assert.Equal(0,budget.ReservedBytes);
    }

    [Theory]
    [MemberData(nameof(Callbacks))]
    public async Task InFlightProtocolCancellationUnwindsBeforeLaterEffects(string name, string body, string expected)
    {
        _ = expected;
        using var script = new LythonEngine().Compile(Prefix+body+"\nprint('finished')");
        Assert.True(script.IsValid,name);
        var budget = new LythonMemoryBudget(262144);
        var host = new DelayedLythonHost();host.SeedFile("/value","a");
        var entered = host.PauseReadUntilCancellation("/value");
        using var cancellation = new CancellationTokenSource();
        var run = script.RunAsync(host,new LythonRunOptions {MemoryBudget=budget},cancellation.Token);
        try { await entered.WaitAsync(TimeSpan.FromSeconds(10));Assert.False(run.IsCompleted);Assert.True(budget.ReservedBytes>0); }
        finally { cancellation.Cancel(); }
        using(var result = await run.WaitAsync(TimeSpan.FromSeconds(10)))
        {
            Assert.Equal(LythonFailureReason.Cancelled,result.FailureAttribution?.Reason);
            Assert.DoesNotContain("read\n",result.StandardOutput,StringComparison.Ordinal);
            Assert.DoesNotContain("finished",result.StandardOutput,StringComparison.Ordinal);
        }
        Assert.Equal(0,budget.ReservedBytes);
    }
}
