using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CsvLineNumberTests
{
    // Complete stdout goldens from isolated CPython 3.13.2.
    public static TheoryData<string, string, string> Programs => new()
    {
        { "first-numeric-error", "import csv\nr=csv.DictReader(['bad','2'],fieldnames=['a'],quoting=csv.QUOTE_NONNUMERIC)\nprint(r.line_num)\ntry:\n next(r)\nexcept ValueError:\n print('bad',r.line_num)\nprint(next(r),r.line_num)\nprint(next(r,'end'),r.line_num)", "0\nbad 0\n{'a': 2.0} 2\nend 2\n" },
        { "later-numeric-error", "import csv\nr=csv.DictReader(['1','bad','2'],fieldnames=['a'],quoting=csv.QUOTE_NONNUMERIC)\nprint(next(r),r.line_num)\ntry:\n next(r)\nexcept ValueError:\n print('bad',r.line_num)\nprint(next(r),r.line_num)", "{'a': 1.0} 1\nbad 1\n{'a': 2.0} 3\n" },
        { "strict-error", "import csv\nr=csv.DictReader(['first','\"broken','fixed'],fieldnames=['a'],strict=True)\nprint(next(r),r.line_num)\ntry:\n next(r)\nexcept csv.Error:\n print('bad',r.line_num)\nprint(next(r,'end'),r.line_num)", "{'a': 'first'} 1\nbad 1\nend 1\n" },
        { "multiline", "import csv\nr=csv.DictReader(['\"one\\n','two\"','three'],fieldnames=['a'])\nprint(next(r),r.line_num)\nprint(next(r),r.line_num)\nprint(next(r,'end'),r.line_num)", "{'a': 'one\\ntwo'} 2\n{'a': 'three'} 3\nend 3\n" },
        { "inferred-header-error", "import csv\nr=csv.DictReader(['\"a\"','bad','2'],quoting=csv.QUOTE_NONNUMERIC)\nprint(r.fieldnames,r.line_num)\ntry:\n next(r)\nexcept ValueError:\n print('bad',r.line_num)\nprint(next(r),r.line_num)", "['a'] 1\nbad 1\n{'a': 2.0} 3\n" },
        { "first-strict-error", "import csv\nr=csv.DictReader(['\"broken'],fieldnames=['a'],strict=True)\nprint(r.line_num)\ntry:\n next(r)\nexcept csv.Error:\n print('bad',r.line_num)\nprint(next(r,'end'),r.line_num)", "0\nbad 0\nend 1\n" },
        { "conversion-error", "import csv\nr=csv.DictReader(['1,2'],fieldnames=['a'],restkey=[])\ntry:\n next(r)\nexcept TypeError:\n print('mapping',r.line_num)\nprint(next(r,'end'),r.line_num)", "mapping 1\nend 1\n" },
        { "fieldnames-refresh", "import csv\nr=csv.DictReader(['bad','2'],fieldnames=['a'],quoting=csv.QUOTE_NONNUMERIC)\ntry:\n next(r)\nexcept ValueError:\n print('bad',r.line_num)\nprint(r.fieldnames,r.line_num)\nprint(next(r),r.line_num)", "bad 0\n['a'] 1\n{'a': 2.0} 2\n" },
        { "repeated-first-errors", "import csv\nr=csv.DictReader(['bad','bad','3'],fieldnames=['a'],quoting=csv.QUOTE_NONNUMERIC)\nfor i in range(2):\n try:\n  next(r)\n except ValueError:\n  print('bad',r.line_num)\nprint(next(r),r.line_num)", "bad 0\nbad 1\n{'a': 3.0} 3\n" },
        { "plain-reader-error", "import csv\nr=csv.reader(['1','bad','2'],quoting=csv.QUOTE_NONNUMERIC)\nprint(next(r),r.line_num)\ntry:\n next(r)\nexcept ValueError:\n print('bad',r.line_num)\nprint(next(r),r.line_num)\nprint(next(r,'end'),r.line_num)", "[1.0] 1\nbad 2\n[2.0] 3\nend 3\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ErrorsPreserveTheAppropriatePublishedLineCount(string label, string source, string expected)
    {
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, label + ": " + string.Join(";", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
            result.Dispose();
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task DelayedDataErrorKeepsThePriorCountAndReturnedRowFunded()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile("""
            import csv
            with open('/r.csv') as f:
                r = csv.DictReader(f, quoting=csv.QUOTE_NONNUMERIC)
                kept = next(r)
                try:
                    next(r)
                except ValueError:
                    print('bad', r.line_num)
                last = next(r)
                return [kept['a'], last['a'], r.line_num]
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var reads = 0;
        var host = new DelayedLythonHost { OnTextReadCompleted = () => reads++ };
        // The failed record crosses the primed window, forcing suspended I/O
        // after the first returned row has escaped the parser.
        host.SeedFile("/r.csv", "\"a\"\n1\n" + new string('x', 20 * 1024) + "\n3\n");
        using var result = await script.RunAsync(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("bad 2\n", result.StandardOutput);
        Assert.Equal(new List<object?> { 1.0, 3.0, new System.Numerics.BigInteger(4) }, result.ReturnValue);
        Assert.True(reads >= 2);
        Assert.True(host.CompletedAsynchronously > 0);
        result.Dispose();
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
