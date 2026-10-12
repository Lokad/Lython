using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CsvFieldNameLifetimeTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var kind in new[] { "inferred", "explicit", "empty", "writer-failure", "retained-readers", "retained-names", "first-name" })
            foreach (var asynchronous in new[] { false, true })
                yield return [kind, asynchronous];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task TablesFollowActualLifetimeAndEscapedNamesStayFunded(string kind, bool asynchronous)
    {
        var body = kind switch
        {
            "explicit" => " r = csv.DictReader(['1,2\\n'], fieldnames=['a','b'])\n next(r)\n r = None\n",
            "empty" => " r = csv.DictReader([], fieldnames=[])\n r = None\n",
            "writer-failure" => " try:\n  csv.DictWriter(1, ['a','b'])\n except (TypeError, AttributeError):\n  pass\n",
            "retained-readers" => " r = csv.DictReader(['a,b\\n','1,2\\n'])\n next(r)\n kept.append(r)\n r = None\n",
            "retained-names" => " r = csv.DictReader(['a,b\\n','1,2\\n'])\n next(r)\n kept.append(r.fieldnames[0])\n r = None\n",
            "first-name" => " r = csv.DictReader(['a,b\\n','1,2\\n'])\n next(r)\n if i == 0:\n  first = r.fieldnames[0]\n r = None\n",
            _ => " r = csv.DictReader(['a,b\\n','1,2\\n'])\n next(r)\n r = None\n"
        };
        // A real initial host yield keeps async execution genuine without
        // inserting 5,000 artificial I/O waits into a bounded churn control.
        var source = "with open('/prefix.txt') as f:\n f.read()\nimport csv\nkept = []\nfirst = None\nfor i in range(5000):\n" + body +
            (kind == "first-name" ? "return i == 4999 and first == 'a'\n" : "return i\n");
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
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
            Assert.Equal(kind == "first-name" ? true : (object)new BigInteger(4999), result.ReturnValue);
        }
    }
}
