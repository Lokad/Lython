using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CsvBlankRecordTests
{
    // Complete stdout goldens from isolated CPython 3.13.2.
    public static TheoryData<string, string, string> Programs => new()
    {
        { "empty-fragment", "import csv\nlines=['']\nr=csv.reader(lines)\nprint(list(r),r.line_num)\nd=csv.DictReader(lines,fieldnames=['a','b'])\nprint(list(d),d.line_num)", "[[]] 1\n[] 1\n" },
        { "newline", "import csv\nlines=['\\n']\nr=csv.reader(lines)\nprint(list(r),r.line_num)\nd=csv.DictReader(lines,fieldnames=['a','b'])\nprint(list(d),d.line_num)", "[[]] 1\n[] 1\n" },
        { "crlf", "import csv\nlines=['\\r\\n']\nr=csv.reader(lines)\nprint(list(r),r.line_num)\nd=csv.DictReader(lines,fieldnames=['a','b'])\nprint(list(d),d.line_num)", "[[]] 1\n[] 1\n" },
        { "quoted-empty", "import csv\nlines=['\"\"\\n']\nr=csv.reader(lines)\nprint(list(r),r.line_num)\nd=csv.DictReader(lines,fieldnames=['a','b'])\nprint(list(d),d.line_num)", "[['']] 1\n[{'a': '', 'b': None}] 1\n" },
        { "delimiter", "import csv\nlines=[',\\n']\nr=csv.reader(lines)\nprint(list(r),r.line_num)\nd=csv.DictReader(lines,fieldnames=['a','b'])\nprint(list(d),d.line_num)", "[['', '']] 1\n[{'a': '', 'b': ''}] 1\n" },
        { "spaces", "import csv\nlines=[' \\n']\nr=csv.reader(lines)\nprint(list(r),r.line_num)\nd=csv.DictReader(lines,fieldnames=['a','b'])\nprint(list(d),d.line_num)", "[[' ']] 1\n[{'a': ' ', 'b': None}] 1\n" },
        { "quoted-continuation", "import csv\nlines=['\"one', '', 'two\"\\n']\nr=csv.reader(lines)\nprint(list(r),r.line_num)\nd=csv.DictReader(lines,fieldnames=['a','b'])\nprint(list(d),d.line_num)", "[['onetwo']] 3\n[{'a': 'onetwo', 'b': None}] 3\n" },
        { "blank-runs", "import csv\nlines=['\\n', '', '\\r\\n', 'one\\n', '\\n', 'two\\n', '']\nr=csv.reader(lines)\nprint(list(r),r.line_num)\nd=csv.DictReader(lines,fieldnames=['a','b'])\nprint(list(d),d.line_num)", "[[], [], [], ['one'], [], ['two'], []] 7\n[{'a': 'one', 'b': None}, {'a': 'two', 'b': None}] 7\n" },
        { "blank-before-numeric-error", "import csv\nr=csv.DictReader(['1\\n','\\n','bad\\n','3\\n'],fieldnames=['a'],quoting=csv.QUOTE_NONNUMERIC)\nprint(next(r),r.line_num)\ntry:\n next(r)\nexcept ValueError:\n print('bad',r.line_num)\nprint(next(r),r.line_num)", "{'a': 1.0} 1\nbad 2\n{'a': 3.0} 4\n" },
        { "empty-names", "import csv\nr=csv.DictReader(['\\n','1,2\\n','\\n'],fieldnames=[])\nprint(next(r),r.line_num)\nprint(next(r,'end'),r.line_num)", "{None: ['1', '2']} 2\nend 3\n" },
        { "inferred-blank-data", "import csv\nr=csv.DictReader(['a,b\\n','\\n','1,2\\n','\\n'])\nprint(r.fieldnames,r.line_num)\nprint(next(r),r.line_num)\nprint(next(r,'end'),r.line_num)", "['a', 'b'] 1\n{'a': '1', 'b': '2'} 3\nend 4\n" },
        { "two-blanks-before-error", "import csv\nr=csv.DictReader(['1\\n','\\n','\\n','bad\\n','3\\n'],fieldnames=['a'],quoting=csv.QUOTE_NONNUMERIC)\nprint(next(r),r.line_num)\ntry:\n next(r)\nexcept ValueError:\n print('bad',r.line_num)\nprint(next(r),r.line_num)", "{'a': 1.0} 1\nbad 2\n{'a': 3.0} 5\n" },
        { "source-effects", "import csv\nclass Source:\n def __init__(self): self.i=0\n def __iter__(self): return self\n def __next__(self):\n  self.i+=1\n  print('pull',self.i)\n  if self.i==1: return '\\n'\n  if self.i==2: return '1\\n'\n  raise StopIteration\nr=csv.DictReader(Source(),fieldnames=['a'])\nprint('made',r.line_num)\nfirst=next(r)\nprint(first,r.line_num)\nprint(next(r,'end'),r.line_num)\nprint(first)", "made 0\npull 1\npull 2\n{'a': '1'} 2\npull 3\nend 2\n{'a': '1'}\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task BlankRecordsFollowPythonIterationAndFieldSemantics(string label, string source, string expected)
    {
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, label + ":" + string.Join(";", script.Diagnostics.Select(d => d.Message)));
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

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task EndlessBlankSourceHonorsExecutionLimitOrCancellation(bool asynchronous, bool cancel)
    {
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine().Compile("""
            import csv
            class Source:
                def __iter__(self): return self
                def __next__(self):
                    print('pull')
                    return '\n'
            r = csv.DictReader(Source(), fieldnames=['a'])
            next(r)
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using var cancellation = new CancellationTokenSource();
        var callbacks = 0;
        var host = new MockLythonHost();
        if (cancel) host.OnStandardOutputWrite = () => { if (++callbacks == 3) cancellation.Cancel(); };
        var options = new LythonRunOptions { MaxExecutionSteps = 256, CancellationToken = cancellation.Token };
        using var result = asynchronous
            ? await script.RunAsync(host, options)
            : script.Run(host, options);
        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains(cancel ? "execution canceled" : "maximum execution step count exceeded", result.Failure?.Message ?? "", StringComparison.Ordinal);
        if (cancel) Assert.True(callbacks >= 3);
        result.Dispose();
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BlankBatchesKeepAliasesAndReturnSharedAllowance(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine().Compile("""
            import csv
            with open('/r.csv') as f:
                r = csv.DictReader(f)
                n = 0
                kept = None
                for row in r:
                    if n == 0:
                        kept = row
                    n += 1
                return [n, r.line_num, kept['a']]
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 131072 };
        var fixture = "a\n" + string.Concat(Enumerable.Repeat("\n1\n", 32));
        for (var batch = 0; batch < 8; batch++)
        {
            ILythonHost host;
            if (asynchronous)
            {
                var delayed = new DelayedLythonHost();
                delayed.SeedFile("/r.csv", fixture);
                host = delayed;
            }
            else
            {
                var synchronous = new MockLythonHost();
                synchronous.SeedFile("/r.csv", fixture);
                host = synchronous;
            }
            using var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(new List<object?> { new System.Numerics.BigInteger(32), new System.Numerics.BigInteger(65), "1" }, result.ReturnValue);
            Assert.InRange(result.PeakExecutionMemoryBytes, 1, 131072);
            if (host is DelayedLythonHost delayedHost) Assert.True(delayedHost.CompletedAsynchronously > 0);
            result.Dispose();
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
