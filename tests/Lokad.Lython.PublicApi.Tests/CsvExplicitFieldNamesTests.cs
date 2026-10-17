using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CsvExplicitFieldNamesTests
{
    // Complete stdout goldens from isolated CPython 3.13.2.
    public static TheoryData<string, string, string> Programs => new()
    {
        { "source-mutation", "import csv\nrows=['1,2','3,4']\nr=csv.DictReader(rows,fieldnames=['a','b'])\nprint(r.line_num)\nrows[0]='5,6'\nprint(next(r))", "0\n{'a': '5', 'b': '6'}\n" },
        { "numeric-error", "import csv\ntry:\n r=csv.DictReader(['bad','2'],fieldnames=['a'],quoting=csv.QUOTE_NONNUMERIC)\n print('made',r.line_num)\nexcept ValueError:\n print('constructor')\nelse:\n try:\n  next(r)\n except ValueError:\n  print('pull')\n print(next(r))", "made 0\npull\n{'a': 2.0}\n" },
        { "custom-iterator", "import csv\nclass Source:\n def __iter__(self): return self\n def __next__(self):\n  print('pull')\n  raise StopIteration\nr=csv.DictReader(Source(),fieldnames=['a'])\nprint('made')\nprint(next(r,'end'))", "made\npull\nend\n" },
        { "empty-fieldnames", "import csv\nr=csv.DictReader(['1,2'],fieldnames=[])\nprint(r.fieldnames,r.line_num)\nprint(next(r),next(r,'end'))", "[] 0\n{None: ['1', '2']} end\n" },
        { "mixed-consumption", "import csv\nr=csv.DictReader(['1','2','3'],fieldnames=['a'])\nit=iter(r)\nprint(r.line_num,it is r)\nfirst=next(r)\nprint(first,next(it),list(r),next(it,'done'))\nprint(first)", "0 True\n{'a': '1'} {'a': '2'} [{'a': '3'}] done\n{'a': '1'}\n" },
        { "parse-error", "import csv\nr=csv.DictReader(['\"broken','fixed'],fieldnames=['a'],strict=True)\nprint('made',r.line_num)\ntry:\n next(r)\nexcept csv.Error:\n print('parse')\nprint(next(r,'end'))", "made 0\nparse\nend\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ExplicitNamesDeferDataUntilFirstPull(string label, string source, string expected)
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
}
