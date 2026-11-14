using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CsvOwnedRangeTests
{
    // Independent CPython 3.13.2 csv.reader goldens; native and borrowed inputs.
    public static TheoryData<string, string, string, bool> Cases => new()
    {
        { "alpha,,\"a,b\",\"a\"\"b\",😀,tail\nlast,\"line\nnext\",back\\slash,é,,end\n", "delimiter=','", "[[\"alpha\", \"\", \"a,b\", \"a\\\"b\", \"😀\", \"tail\"], [\"last\", \"line\\nnext\", \"back\\\\slash\", \"é\", \"\", \"end\"]]", false },
        { "alpha,,\"a,b\",\"a\"\"b\",😀,tail\nlast,\"line\nnext\",back\\slash,é,,end\n", "delimiter=','", "[[\"alpha\", \"\", \"a,b\", \"a\\\"b\", \"😀\", \"tail\"], [\"last\", \"line\\nnext\", \"back\\\\slash\", \"é\", \"\", \"end\"]]", true },
        { "alpha,,\"a,b\",a\\\"b,😀,tail\nlast,\"line\nnext\",back\\\\slash,é,,end\n", "doublequote=False, escapechar='\\\\'", "[[\"alpha\", \"\", \"a,b\", \"a\\\"b\", \"😀\", \"tail\"], [\"last\", \"line\\nnext\", \"back\\\\slash\", \"é\", \"\", \"end\"]]", false },
        { "alpha,,\"a,b\",a\\\"b,😀,tail\nlast,\"line\nnext\",back\\\\slash,é,,end\n", "doublequote=False, escapechar='\\\\'", "[[\"alpha\", \"\", \"a,b\", \"a\\\"b\", \"😀\", \"tail\"], [\"last\", \"line\\nnext\", \"back\\\\slash\", \"é\", \"\", \"end\"]]", true },
        { "alpha🦊🦊a,b🦊a\"b🦊😀😀😀😀🦊tail\nlast🦊😀line\nnext😀🦊back\\slash🦊é🦊🦊end\n", "delimiter='🦊', quotechar='😀', escapechar='💥'", "[[\"alpha\", \"\", \"a,b\", \"a\\\"b\", \"😀\", \"tail\"], [\"last\", \"line\\nnext\", \"back\\\\slash\", \"é\", \"\", \"end\"]]", false },
        { "alpha🦊🦊a,b🦊a\"b🦊😀😀😀😀🦊tail\nlast🦊😀line\nnext😀🦊back\\slash🦊é🦊🦊end\n", "delimiter='🦊', quotechar='😀', escapechar='💥'", "[[\"alpha\", \"\", \"a,b\", \"a\\\"b\", \"😀\", \"tail\"], [\"last\", \"line\\nnext\", \"back\\\\slash\", \"é\", \"\", \"end\"]]", true },
        { "  alpha,  ,\"a,b\",\"a\"\"b\",😀, tail \n", "skipinitialspace=True", "[[\"alpha\", \"\", \"a,b\", \"a\\\"b\", \"😀\", \"tail \"]]", false },
        { "  alpha,  ,\"a,b\",\"a\"\"b\",😀, tail \n", "skipinitialspace=True", "[[\"alpha\", \"\", \"a,b\", \"a\\\"b\", \"😀\", \"tail \"]]", true },
        { "alpha,,a\\,b,a\"b,😀,tail\nlast,line\\\nnext,back\\\\slash,é,,end\n", "quotechar=None, quoting=3, escapechar='\\\\'", "[[\"alpha\", \"\", \"a,b\", \"a\\\"b\", \"😀\", \"tail\"], [\"last\", \"line\\nnext\", \"back\\\\slash\", \"é\", \"\", \"end\"]]", false },
        { "alpha,,a\\,b,a\"b,😀,tail\nlast,line\\\nnext,back\\\\slash,é,,end\n", "quotechar=None, quoting=3, escapechar='\\\\'", "[[\"alpha\", \"\", \"a,b\", \"a\\\"b\", \"😀\", \"tail\"], [\"last\", \"line\\nnext\", \"back\\\\slash\", \"é\", \"\", \"end\"]]", true },
        { "\"alpha\",\"\",\"a,b\",\"a\"\"b\",\"😀\",\"tail\"\r\n\"last\",\"line\nnext\",\"back\\slash\",\"é\",\"\",\"end\"\r\n", "quoting=1", "[[\"alpha\", \"\", \"a,b\", \"a\\\"b\", \"😀\", \"tail\"], [\"last\", \"line\\nnext\", \"back\\\\slash\", \"é\", \"\", \"end\"]]", false },
        { "\"alpha\",\"\",\"a,b\",\"a\"\"b\",\"😀\",\"tail\"\r\n\"last\",\"line\nnext\",\"back\\slash\",\"é\",\"\",\"end\"\r\n", "quoting=1", "[[\"alpha\", \"\", \"a,b\", \"a\\\"b\", \"😀\", \"tail\"], [\"last\", \"line\\nnext\", \"back\\\\slash\", \"é\", \"\", \"end\"]]", true },
        { "alpha\t\ta,b\ta\"b\t😀\ttail\nlast\t'line\nnext'\tback\\\\slash\té\t\tend\n", "delimiter='\\t', quotechar=\"'\", escapechar='\\\\'", "[[\"alpha\", \"\", \"a,b\", \"a\\\"b\", \"😀\", \"tail\"], [\"last\", \"line\\nnext\", \"back\\\\slash\", \"é\", \"\", \"end\"]]", false },
        { "alpha\t\ta,b\ta\"b\t😀\ttail\nlast\t'line\nnext'\tback\\\\slash\té\t\tend\n", "delimiter='\\t', quotechar=\"'\", escapechar='\\\\'", "[[\"alpha\", \"\", \"a,b\", \"a\\\"b\", \"😀\", \"tail\"], [\"last\", \"line\\nnext\", \"back\\\\slash\", \"é\", \"\", \"end\"]]", true },
        { "\n,,\n\"\",\"\"\n", "delimiter=','", "[[], [\"\", \"\", \"\"], [\"\", \"\"]]", false },
        { "\n,,\n\"\",\"\"\n", "delimiter=','", "[[], [\"\", \"\", \"\"], [\"\", \"\"]]", true },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task NativeAndBorrowedRowsPreserveValuesAndInput(string input, string arguments,
        string expectedJson, bool borrowed)
    {
        var source = "import csv\nwith open('/input.csv', newline='') as stream:\n    "
            + (borrowed ? "lines=stream.readlines()\n    " : "")
            + $"rows=list(csv.reader({(borrowed ? "lines" : "stream")}, {arguments}))\n"
            + (borrowed ? "return rows, ''.join(lines)" : "return rows, None");
        var expected = JsonSerializer.Deserialize<string[][]>(expectedJson)!;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var host = new MockLythonHost();
            host.SeedFile("/input.csv", input);
            using (var result = script.Run(host)) Check(result);
            Assert.Equal(baseline, budget.ReservedBytes);
            var delayed = new DelayedLythonHost();
            delayed.SeedFile("/input.csv", input);
            using (var result = await script.RunAsync(delayed)) Check(result);
            Assert.True(delayed.CompletedAsynchronously > 0);
            Assert.Equal(1, delayed.ClosedFileCount);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);

        void Check(LythonExecutionResult result)
        {
            Assert.True(result.Success, result.Failure?.Message);
            var returned = Assert.IsAssignableFrom<IReadOnlyList<object?>>(result.ReturnValue);
            var rows = Assert.IsAssignableFrom<IReadOnlyList<object?>>(returned[0]);
            Assert.Equal(expected.Length, rows.Count);
            for (var i = 0; i < expected.Length; i++)
            {
                var row = Assert.IsAssignableFrom<IReadOnlyList<object?>>(rows[i]);
                Assert.Equal(expected[i], row.Select(value => Assert.IsType<string>(value)));
            }
            if (borrowed) Assert.Equal(input, returned[1]);
            else Assert.Null(returned[1]);
        }
    }

    [Theory]
    [InlineData(15)]
    [InlineData(4095)]
    public async Task EscapedMultilineFieldSurvivesBufferReuseAndActualIo(int prefixLength)
    {
        var field = new string('x', prefixLength) + "\"😀\ny";
        var input = "\"" + field.Replace("\"", "\"\"") + "\",tail\nend,last\n";
        const string source = """
            import csv
            with open('/input.csv', newline='') as stream:
                reader=csv.reader(stream)
                first=next(reader)
                second=next(reader)
            return first,second
            """;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost();
            host.SeedFile("/input.csv", input);
            using (var result = await script.RunAsync(host))
            {
                Assert.True(result.Success, result.Failure?.Message);
                var rows = Assert.IsAssignableFrom<IReadOnlyList<object?>>(result.ReturnValue);
                Assert.Equal(new object?[] { field, "tail" }, Assert.IsAssignableFrom<IReadOnlyList<object?>>(rows[0]));
                Assert.Equal(new object?[] { "end", "last" }, Assert.IsAssignableFrom<IReadOnlyList<object?>>(rows[1]));
                Assert.True(budget.ReservedBytes > baseline);
            }
            Assert.True(host.CompletedAsynchronously > 0);
            Assert.Equal(1, host.ClosedFileCount);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
