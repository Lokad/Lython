using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class BulkTextConstructionTests
{
    public static IEnumerable<object[]> TabCases()
    {
        foreach (var prefix in new[] { 4093, 4094, 4095, 4096 })
            foreach (var width in new[] { -4, 0, 1, 4, 8 })
                yield return [prefix, width];
    }

    [Theory]
    [MemberData(nameof(TabCases))]
    public void TabsCountScalarsAcrossChunksAndOnlyCrLfResetColumns(int prefixLength, int width)
    {
        var prefix = new string('a', prefixLength);
        var input = prefix + "😀\tq\r\né\tz\nA\u0301\tZ\r\tend\v\t.";
        var suffix = width switch
        {
            1 => "q\r\né z\nA\u0301 Z\r end\v .",
            4 => "q\r\né   z\nA\u0301  Z\r    end\v    .",
            8 => "q\r\né       z\nA\u0301      Z\r        end\v    .",
            _ => "q\r\néz\nA\u0301Z\rend\v.",
        };
        var expected = prefix + "😀" + new string(' ', width <= 0 ? 0 : width - (prefixLength + 1) % width) + suffix;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var span = new LythonSourceSpan(1, 1, 1, 1);
            var source = PyString.FromString(input, context.MemoryGovernor, span);
            var result = PyStringOps.ExpandTabs(source, width, context, span);
            Assert.Equal(expected, result.AsString());
            Assert.Equal(input, source.AsString());
            Assert.Same(context.MemoryGovernor, result.OwnerMemoryGovernor);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("", int.MaxValue, "")]
    [InlineData("😀é", int.MaxValue, "😀é")]
    [InlineData("\t\t", 0, "")]
    [InlineData("\t\t", -4, "")]
    [InlineData("é\t😀\t", 1, "é 😀 ")]
    public void TabEdgesPreserveTextAndReturnSharedAllowanceAfterCleanup(string input, int width, string expected)
    {
        var budget = new LythonMemoryBudget(65536);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                new LythonRunOptions { MemoryBudget = owner });
            var source = PyString.FromString(input, context.MemoryGovernor, null);
            var result = PyStringOps.ExpandTabs(source, width, context);
            Assert.Equal(expected, result.AsString());
            Assert.Equal(input, source.AsString());
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            if (result.OwnerMemoryGovernor is not null)
                Assert.Same(context.MemoryGovernor, result.OwnerMemoryGovernor);
            GC.KeepAlive(source);
            GC.KeepAlive(result);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
