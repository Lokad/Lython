using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RegexFlagProtocolTests
{
    [Theory]
    [InlineData("re.compile('x', flags).flags", "32\n", false)]
    [InlineData("re.search('x', 'x', flags).span()", "(0, 1)\n", false)]
    [InlineData("re.split('x', 'axb', flags=flags)", "['a', 'b']\n", false)]
    [InlineData("re.sub('x', 'y', 'x', flags=flags)", "y\n", false)]
    [InlineData("re.compile(re.compile('x'), flags).flags", "32\n", true)]
    [InlineData("re.search(re.compile('x'), 'x', flags).span()", "(0, 1)\n", true)]
    public async Task RunAsyncAwaitsGuestFlags(string expression, string expected, bool compiledPattern)
    {
        var source = """
            import re
            operations = []
            class Flags:
                def __and__(self, other):
                    operations.append('and' + str(int(other)))
                    with open('/flags.txt') as stream:
                        return int(stream.read()) & other
                def __bool__(self):
                    operations.append('bool')
                    with open('/flags.txt') as stream:
                        return bool(int(stream.read()))
                def __ior__(self, other):
                    operations.append('ior')
                    return other
                def __or__(self, other):
                    operations.append('or')
                    return other
                def __ror__(self, other):
                    operations.append('ror')
                    return other
            flags = Flags()
            """ + "\nprint(" + expression + ")\nprint(' '.join(operations))\n";
        expected += compiledPattern ? "bool\n" : "and64 and4 and256 ior and128 ror and128 or and128\n";
        var allowance = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, allowance))
        {
            Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
            var baseline = allowance.ReservedBytes;
            var syncHost = new MockLythonHost();
            syncHost.SeedFile("/flags.txt", "0");
            using (var result = script.Run(syncHost))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, allowance.ReservedBytes);

            var asyncHost = new DelayedLythonHost();
            asyncHost.SeedFile("/flags.txt", "0");
            using (var result = await script.RunAsync(asyncHost))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.True(asyncHost.CompletedAsynchronously > 0);
            Assert.Equal(baseline, allowance.ReservedBytes);
        }
        Assert.Equal(0, allowance.ReservedBytes);
    }

    [Fact]
    public async Task FlagRefusalAfterSuspensionIsTerminalAndReturnsTheAllowance()
    {
        var allowance = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import re
            class Flags:
                def __bool__(self):
                    with open('/flags.txt') as stream:
                        if stream.read() == 'large':
                            return bool('x' * 2000000)
                        return False
            try:
                pattern = re.compile(re.compile('x'), Flags())
            except MemoryError:
                print('caught')
            print(pattern.pattern)
            """, allowance);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        var baseline = allowance.ReservedBytes;
        var refusedHost = new DelayedLythonHost();
        refusedHost.SeedFile("/flags.txt", "large");
        using (var result = await script.RunAsync(refusedHost))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Empty(result.StandardOutput);
        }
        Assert.Equal(1, refusedHost.ClosedFileCount);
        Assert.Equal(baseline, allowance.ReservedBytes);

        var nextHost = new DelayedLythonHost();
        nextHost.SeedFile("/flags.txt", "small");
        using (var result = await script.RunAsync(nextHost))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("x\n", result.StandardOutput);
        }
        Assert.Equal(1, nextHost.ClosedFileCount);
        Assert.Equal(baseline, allowance.ReservedBytes);
    }
}
