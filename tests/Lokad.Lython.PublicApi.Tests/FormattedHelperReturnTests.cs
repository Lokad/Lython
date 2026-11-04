using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FormattedHelperReturnTests
{
    public static TheoryData<string, string> Cases => new()
    {
        { "def render(record):\n    return f\"[{record['name']:*^7.1s}] {record['amount']:>9,d} {record['active']!s} {None}\"\nprint(render({'name':'é😀', 'amount':-12345, 'active':True}))\n", "[***é***]   -12,345 True None\n" },
        { "def same(value):\n    return f'{value}'\ndef nested(value):\n    return f'{f\"{value}\"}'\ndef convert(value):\n    return f'P{value!r}Q{value!a}R'\nvalue=''.join(['é','😀'])\nprint(same(value) is value, nested(value) is value)\nprint(convert(value))\n", "True True\nP'é😀'Q'\\xe9\\U0001f600'R\n" },
        { "def render(record):\n    return f\"{record['first']:bad}{record['missing']}\"\ntry:\n    render({'first':7})\nexcept Exception as error:\n    print(type(error).__name__)\n", "ValueError\n" },
        { "events=[]\nrecord={'later':'before'}\nclass Value:\n    def __format__(self,spec):\n        events.append('format:'+spec)\n        record['later']='after'\n        return 'formatted'\nrecord['first']=Value()\ndef render(value):\n    return f\"A{value['first']:custom}|{value['later']}Z\"\nprint(render(record),events)\n", "Aformatted|afterZ ['format:custom']\n" },
        { "events=[]\nclass Key:\n    def __hash__(self):return hash('missing')\n    def __eq__(self,other):\n        events.append('equal')\n        return False\nrecord={'first':7}\nrecord[Key()]=9\ndef render(record):\n    return f\"{record['first']:bad}{record['missing']}\"\ntry:render(record)\nexcept ValueError:print(events)\n", "[]\n" },
        { "def render(value):\n    return f\"a{f'b{value:04d}c'}d\"\nprint(render(7))\n", "ab0007cd\n" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task HelperFormattingPreservesValuesOrderAndSharedAllowance(string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(expected, result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task CustomFieldFormattingRetainsItsOrdinaryAsyncCallback()
    {
        const string source = """
            class Value:
                def __format__(self, spec):
                    with open('/value.txt') as source:
                        return source.read() + spec
            def render(record):
                return f"prefix:{record['value']:!}:suffix"
            print(render({'value': Value()}))
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost();
            host.SeedFile("/value.txt", "observed");
            using (var result = await script.RunAsync(host))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("prefix:observed!:suffix\n", result.StandardOutput);
                Assert.True(host.CompletedAsynchronously > 0);
            }
            Assert.Equal(1, host.ClosedFileCount);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
