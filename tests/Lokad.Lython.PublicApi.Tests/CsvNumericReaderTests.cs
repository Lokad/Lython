using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CsvNumericReaderTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "mixed", "import csv\nprint(list(csv.reader(['1,\"2\",,3'],quoting=csv.QUOTE_NONNUMERIC)))", "[[1.0, '2', '', 3.0]]\n" },
        { "escaped", "import csv\nprint(list(csv.reader([r'\\3,4'],quoting=csv.QUOTE_NONNUMERIC,escapechar='\\\\')))", "[[3.0, 4.0]]\n" },
        { "unicode", "import csv\nprint(list(csv.reader(['٣_٤.٥,𝟙.𝟚,-٠'],quoting=csv.QUOTE_NONNUMERIC)))", "[[34.5, 1.2, -0.0]]\n" },
        { "multiline", "import csv\nr=csv.reader(['\"é\\n','😀\",٢\\n'],quoting=csv.QUOTE_NONNUMERIC)\nprint(list(r),r.line_num)", "[['é\\n😀', 2.0]] 2\n" },
        { "recovery", "import csv\nr=csv.reader(['1,bad','2,\"three\"','٤,5'],quoting=csv.QUOTE_NONNUMERIC)\ntry:\n next(r)\nexcept ValueError as e:\n print(type(e).__name__,str(e))\nprint(next(r),next(r),next(r,'end'))", "ValueError could not convert string to float: 'bad'\n[2.0, 'three'] [4.0, 5.0] end\n" },
        { "dict", "import csv\nr=csv.DictReader(['\"a\",\"b\"','1,2,3','4'],restkey='extra',restval='missing',quoting=csv.QUOTE_NONNUMERIC)\nprint(list(r))", "[{'a': 1.0, 'b': 2.0, 'extra': [3.0]}, {'a': 4.0, 'b': 'missing'}]\n" },
        { "default", "import csv\nprint(list(csv.reader(['1,2'],quoting=csv.QUOTE_MINIMAL)))", "[['1', '2']]\n" },
        { "arithmetic", "import csv\nfor row in csv.reader(['1,2','٣,4'],quoting=csv.QUOTE_NONNUMERIC):\n print(row[0]+1)", "2.0\n4.0\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task NumericRowsMatchPythonAndReturnAllowance(string label, string source, string expected)
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
