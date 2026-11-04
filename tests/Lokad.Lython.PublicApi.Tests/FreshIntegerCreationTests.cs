using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FreshIntegerCreationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreationPreservesExactValuesAliasesAndOwnership(bool asynchronous)
    {
        const string source = """
            import json
            values = [-257, -6, -5, -1, 0, 1, 255, 256, 257,
                      2147483648, 9223372036854775808, 1 << 130,
                      (1 << 130) + 42, -(1 << 130) - 5, (1 << 130) + 256]
            for original in values:
                text = str(original)
                parsed = int(text)
                alias = parsed
                copies = [parsed + 0, parsed - 0, parsed * 1, parsed // 1,
                          json.loads(text), range(parsed, parsed + 1)[0],
                          next(iter(range(parsed, parsed + 1))),
                          next(enumerate(['x'], start=parsed))[0]]
                assert all(type(value) is int and value == original and
                           hash(value) == hash(original) for value in copies)
                assert parsed is alias and int(parsed) is parsed and +parsed is parsed
                if parsed >= 0:
                    assert abs(parsed) is parsed
                assert (-parsed) // 7 == -(parsed // 7) - (parsed % 7 != 0)
                assert (parsed & 255) == parsed % 256
            assert first is not second
            assert int(first) is first and int(second) is second
            assert +first is first and abs(second) is second
            assert type(True + False) is int and True + False == 1
            assert type(1 + 0.5) is float and 1 + 0.5 == 1.5
            assert len({True: 'old', 1: 'replacement'}) == 1
            print('exact aliases and values')
            """;
        var budget = new LythonMemoryBudget(2_000_000);
        object first = new BigInteger(42);
        object second = new BigInteger(42);
        using (var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var options = new LythonRunOptions
            {
                MemoryBudget = budget,
                Globals = new Dictionary<string, object?> { ["first"] = first, ["second"] = second }
            };
            using (var result = asynchronous
                ? await script.RunAsync(new MockLythonHost(), options)
                : script.Run(new MockLythonHost(), options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("exact aliases and values\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
