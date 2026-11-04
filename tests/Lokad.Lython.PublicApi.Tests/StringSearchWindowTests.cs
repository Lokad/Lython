using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class StringSearchWindowTests
{
    // Independent CPython 3.13 goldens; covers discarded out-of-range facts
    // and rejecting an invalid start before invoking the end's index hook.
    public static TheoryData<string, string, string> Programs => new()
    {
        { "beyond-empty-search", "for value in ['abc','é😀','']:\n    for start in [len(value),len(value)+1,10**40]:\n        print(value.find('',start),value.rfind('',start),value.count('',start))\n    for name in ['index','rindex']:\n        try:getattr(value,name)('',len(value)+1)\n        except ValueError:print('ValueError')", "3 3 1\n-1 -1 0\n-1 -1 0\nValueError\nValueError\n2 2 1\n-1 -1 0\n-1 -1 0\nValueError\nValueError\n0 0 1\n-1 -1 0\n-1 -1 0\nValueError\nValueError\n" },
        { "invalid-start-before-end", "class Start:pass\nclass End:\n    def __index__(self):print('unexpected end');return 1\nfor name in ['find','rfind','count','index','rindex']:\n    try:getattr('é😀',name)('',Start(),End())\n    except TypeError:print('TypeError')", "TypeError\nTypeError\nTypeError\nTypeError\nTypeError\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task SearchPreservesBoundsAndIndexEffects(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, name + ": " + string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
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
}
