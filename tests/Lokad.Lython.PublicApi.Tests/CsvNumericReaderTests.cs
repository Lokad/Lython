using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CsvNumericReaderTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "short decimal rounding and signed zero", """
            import csv
            import struct
            text = '+.5,-0.,0.1,1.23456789012345,9007199254740992,9007199254740993,0.0000000000000001'
            builtin = [struct.pack('>d', float(value)).hex() for value in text.split(',')]
            row = next(csv.reader([text], quoting=csv.QUOTE_NONNUMERIC))
            parsed = [struct.pack('>d', value).hex() for value in row]
            print(*builtin)
            print(*parsed)
            """, "3fe0000000000000 8000000000000000 3fb999999999999a 3ff3c0ca428c59dd 4340000000000000 4340000000000000 3c9cd2b297d889bc\n3fe0000000000000 8000000000000000 3fb999999999999a 3ff3c0ca428c59dd 4340000000000000 4340000000000000 3c9cd2b297d889bc\n" },
        { "mixed", "import csv\nprint(list(csv.reader(['1,\"2\",,3'],quoting=csv.QUOTE_NONNUMERIC)))", "[[1.0, '2', '', 3.0]]\n" },
        { "escaped", "import csv\nprint(list(csv.reader([r'\\3,4'],quoting=csv.QUOTE_NONNUMERIC,escapechar='\\\\')))", "[[3.0, 4.0]]\n" },
        { "unicode", "import csv\nprint(list(csv.reader(['٣_٤.٥,𝟙.𝟚,-٠'],quoting=csv.QUOTE_NONNUMERIC)))", "[[34.5, 1.2, -0.0]]\n" },
        { "long borrowed numeric field", """
            import csv
            row = '٠' * 2050 + '2_5.0,"25.0",-1e-999\n'
            print(next(csv.reader([row], quoting=csv.QUOTE_NONNUMERIC)))
            """, "[25.0, '25.0', -0.0]\n" },
        { "multiline", "import csv\nr=csv.reader(['\"é\\n','😀\",٢\\n'],quoting=csv.QUOTE_NONNUMERIC)\nprint(list(r),r.line_num)", "[['é\\n😀', 2.0]] 2\n" },
        { "recovery", "import csv\nr=csv.reader(['1,bad','2,\"three\"','٤,5'],quoting=csv.QUOTE_NONNUMERIC)\ntry:\n next(r)\nexcept ValueError as e:\n print(type(e).__name__,str(e))\nprint(next(r),next(r),next(r,'end'))", "ValueError could not convert string to float: 'bad'\n[2.0, 'three'] [4.0, 5.0] end\n" },
        { "dict", "import csv\nr=csv.DictReader(['\"a\",\"b\"','1,2,3','4'],restkey='extra',restval='missing',quoting=csv.QUOTE_NONNUMERIC)\nprint(list(r))", "[{'a': 1.0, 'b': 2.0, 'extra': [3.0]}, {'a': 4.0, 'b': 'missing'}]\n" },
        { "default", "import csv\nprint(list(csv.reader(['1,2'],quoting=csv.QUOTE_MINIMAL)))", "[['1', '2']]\n" },
        { "conversion before further input", """
            import csv
            events=[]
            def rows():
                events.append('first')
                yield 'bad,"open\n'
                events.append('second')
                yield 'close",1\n'
            r=csv.reader(rows(),quoting=csv.QUOTE_NONNUMERIC,strict=True)
            try:next(r)
            except ValueError as error:print(str(error))
            print(r.line_num,events)
            """, "could not convert string to float: 'bad'\n1 ['first']\n" },
        { "arithmetic", "import csv\nfor row in csv.reader(['1,2','٣,4'],quoting=csv.QUOTE_NONNUMERIC):\n print(row[0]+1)", "2.0\n4.0\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task NumericRowsMatchPythonAndReturnAllowance(string label, string source, string expected)
    {
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
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
    [InlineData(false)]
    [InlineData(true)]
    public async Task FixedUnpackPreservesErrorsFinalRecordAndRetainedFields(bool asynchronous)
    {
        const string source = """
            import csv
            with open('/input.csv', newline='') as stream:
                rows = csv.reader(stream, quoting=csv.QUOTE_NONNUMERIC)
                try:
                    for left, right in rows:
                        print("unexpected")
                except ValueError as error:
                    print(error)
                for first, *rest in rows:
                    print(first, rest)
                    break
                result = []
                for first, second in rows:
                    alias = first
                    result.append((first, second, first is alias))
                print(rows.line_num)
            print(result)
            """;
        const string input = "\"short\"\n\"middle\",1,2\n\"multi\nline\",3\n-0.0,\"tail";
        const string expected = "not enough values to unpack (expected 2, got 1)\nmiddle [1.0, 2.0]\n5\n" +
            "[('multi\\nline', 3.0, True), (-0.0, 'tail', True)]\n";
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        if (asynchronous)
        {
            var host = new DelayedLythonHost();
            host.SeedFile("/input.csv", input);
            using (var result = await script.RunAsync(host))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.True(host.CompletedAsynchronously > 0);
            Assert.Equal(1, host.ClosedFileCount);
        }
        else
        {
            var host = new MockLythonHost();
            host.SeedFile("/input.csv", input);
            using (var result = script.Run(host))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(1, host.ClosedFileCount);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeRowsKeepPriorFieldsWhenQuotedSyntaxNeedsDecoding(bool asynchronous)
    {
        const string source = """
            import csv
            with open('/input.csv', newline='') as stream:
                rows = list(csv.reader(stream, quoting=csv.QUOTE_NONNUMERIC, strict=True))
            print(rows)
            """;
        const string input = "\"a\",1,\"two,words\",٣_٤.٥\r\n\"b\",2,\"a\"\"b\",3\n" +
            "\"c\",4,\"line\nwrapped\",5\n\"d\",-٠,\"\",";
        const string golden = "[['a', 1.0, 'two,words', 34.5], ['b', 2.0, 'a\"b', 3.0], " +
            "['c', 4.0, 'line\\nwrapped', 5.0], ['d', -0.0, '', '']]\n";
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        var baseline = budget.ReservedBytes;
        if (asynchronous)
        {
            var host = new DelayedLythonHost();
            host.SeedFile("/input.csv", input);
            using (var result = await script.RunAsync(host)) Check(result);
            Assert.True(host.CompletedAsynchronously > 0);
            Assert.Equal(1, host.ClosedFileCount);
        }
        else
        {
            var host = new MockLythonHost();
            host.SeedFile("/input.csv", input);
            using (var result = script.Run(host)) Check(result);
            Assert.Equal(1, host.ClosedFileCount);
        }
        Assert.Equal(baseline, budget.ReservedBytes);

        static void Check(LythonExecutionResult result)
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(golden, result.StandardOutput);
        }
    }
}
