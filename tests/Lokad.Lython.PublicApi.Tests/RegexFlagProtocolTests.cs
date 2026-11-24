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

    [Theory]
    [InlineData("32", "32 x\nindex\n")]
    [InlineData("text", "TypeError\nindex\n")]
    [InlineData("1180591620717411303424", "OverflowError\nindex\n")]
    public async Task FlagsConvertedByPythonIndexCanSuspend(string input, string expected)
    {
        const string source = """
            import re
            operations = []
            class FlagBits:
                def __index__(self):
                    operations.append('index')
                    with open('/flags.txt') as stream:
                        value = stream.read()
                    return value if value == 'text' else int(value)
            class Flags:
                def __and__(self, other): return 0
                def __or__(self, other): return FlagBits()
                def __ior__(self, other): return other
                def __ror__(self, other): return other
            try:
                pattern = re.compile('x', Flags())
                print(pattern.flags, pattern.fullmatch('x').group())
            except (TypeError, OverflowError) as error:
                print(type(error).__name__)
            print(' '.join(operations))
            """;
        var allowance = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, allowance);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        var baseline = allowance.ReservedBytes;
        var syncHost = new MockLythonHost();
        syncHost.SeedFile("/flags.txt", input);
        using (var result = script.Run(syncHost))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
        Assert.Equal(baseline, allowance.ReservedBytes);
        var asyncHost = new DelayedLythonHost();
        asyncHost.SeedFile("/flags.txt", input);
        using (var result = await script.RunAsync(asyncHost))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
        Assert.Equal(1, asyncHost.ClosedFileCount);
        Assert.True(asyncHost.CompletedAsynchronously > 0);
        Assert.Equal(baseline, allowance.ReservedBytes);
    }

    [Theory]
    [InlineData("32", "'x', 'X', 34", "34 False")]
    [InlineData("34", "'x', 'X', 32", "32 True")]
    [InlineData("48", "'.', '\\n', 32", "32 True")]
    [InlineData("0", "r'\\w', 'é', 32", "32 False")]
    [InlineData("32", "r'\\w', 'é', 256", "256 True")]
    [InlineData("1099511627810", "'x', 'X', 32", "32 True")]
    public async Task CompileAndReportedFlagsUseIndependentUnions(string programFlags, string arguments, string expected)
    {
        const string source = """
            import re
            operands = []
            class Flags:
                def __init__(self, reported):
                    self.reported = reported
                def __and__(self, other): return 0
                def __ior__(self, other): return other
                def __ror__(self, state):
                    operands.append(('program', state))
                    with open('/flags.txt') as stream:
                        return int(stream.read())
                def __or__(self, state):
                    operands.append(('reported', state))
                    return self.reported

            def check(expression, subject, reported):
                pattern = re.compile(expression, Flags(reported))
                print(pattern.flags, pattern.fullmatch(subject) is not None)
                print(operands)
            """;
        expected += "\n[('program', 32), ('reported', 32)]\n";
        var allowance = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            source + "\ncheck(" + arguments + ")\n", allowance);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics));
        var baseline = allowance.ReservedBytes;

        var sync = new MockLythonHost();
        sync.SeedFile("/flags.txt", programFlags);
        using (var result = script.Run(sync))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
        Assert.Equal(baseline, allowance.ReservedBytes);

        var host = new DelayedLythonHost();
        host.SeedFile("/flags.txt", programFlags);
        using (var result = await script.RunAsync(host))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
        Assert.Equal(1, host.ClosedFileCount);
        Assert.True(host.CompletedAsynchronously > 0);
        Assert.Equal(baseline, allowance.ReservedBytes);
    }

    [Fact]
    public async Task WrapperCacheUsesRequestedFlagIdentityAndSuspendingEquality()
    {
        const string source = """
            import re
            operations = []
            class Flags:
                def __init__(self, label):
                    self.label = label
                def __hash__(self):
                    operations.append('hash')
                    with open('/flags.txt') as stream:
                        return int(stream.read())
                def __eq__(self, other):
                    operations.append('equal')
                    with open('/flags.txt') as stream:
                        zero = int(stream.read())
                    if isinstance(other, int):
                        return self.label == 'zero' and other == zero
                    return self.label == other.label
                def __and__(self, other): return 0
                def __ior__(self, other):
                    operations.append('normalize')
                    return other
                def __or__(self, other): return other
                def __ror__(self, other): return other

            flags = Flags('zero')
            original = re.compile('x', flags)
            operations.clear()
            print(re.compile('x', flags) is original, 'hash' in operations, 'normalize' in operations)
            operations.clear()
            print(re.compile('x', Flags('zero')) is original, 'equal' in operations, 'normalize' in operations)
            operations.clear()
            print(re.compile('x', 0) is original, 'equal' in operations, 'normalize' in operations)
            other = re.compile('x', Flags('other'))
            print(other is original, other == original)
            """;
        const string expected = "True True False\nTrue True False\nTrue True False\nFalse True\n";
        var allowance = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, allowance);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics));
        var baseline = allowance.ReservedBytes;

        var sync = new MockLythonHost();
        sync.SeedFile("/flags.txt", "0");
        using (var result = script.Run(sync))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
        Assert.Equal(baseline, allowance.ReservedBytes);

        var host = new DelayedLythonHost();
        host.SeedFile("/flags.txt", "0");
        using (var result = await script.RunAsync(host))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
        Assert.True(host.CompletedAsynchronously > 0);
        Assert.Equal(sync.ClosedFileCount, host.ClosedFileCount);
        Assert.Equal(baseline, allowance.ReservedBytes);
    }

    [Theory]
    [InlineData("purge", "False True\nx\n")]
    [InlineData("insert", "True True\nx\n")]
    public async Task SuspendedEqualityResolvesAgainstTheLiveCache(string operation, string expected)
    {
        const string source = """
            import re
            comparisons = []
            class Flags:
                def __hash__(self): return 0
                def __eq__(self, other):
                    with open('/flags.txt') as stream:
                        operation = stream.read()
                    if operation == 'purge':
                        re.purge()
                    else:
                        comparisons.append(other)
                        if len(comparisons) <= 2:
                            re.compile('other' + str(len(comparisons)))
                    return other == 0
                def __and__(self, other): return 0
                def __ior__(self, other): return other
                def __or__(self, other): return other
                def __ror__(self, other): return other

            before = re.compile('x', Flags())
            after = re.compile('x', 0)
            print(before is after, before == after)
            print(before.fullmatch('x').group())
            """;
        var allowance = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, allowance);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics));
        var baseline = allowance.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/flags.txt", operation);
        using (var result = await script.RunAsync(host))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
        Assert.Equal(1, host.ClosedFileCount);
        Assert.True(host.CompletedAsynchronously > 0);
        Assert.Equal(baseline, allowance.ReservedBytes);
    }

    [Theory]
    [InlineData("raise")]
    [InlineData("refuse")]
    [InlineData("cancel")]
    public async Task GuestFlagHashFailurePrecedesProviderWork(string mode)
    {
        const string source = """
            import re
            class Flags:
                def __hash__(self):
                    with open('/flags.txt') as stream:
                        if stream.read() == 'refuse':
                            return len('x' * 4000000)
                        raise RuntimeError('flag hash')
                def __and__(self, other):
                    print('unexpected normalization')
                    return 0
            try:
                re.search('x', 'x', Flags())
            except RuntimeError as error:
                print(str(error))
            """;
        var provider = new CountingProvider();
        var allowance = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine(provider).Compile(source, allowance);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics));
        var baseline = allowance.ReservedBytes;
        var compiled = provider.CompileCalls;
        var host = new DelayedLythonHost();
        host.SeedFile("/flags.txt", mode);
        using var cancellation = new CancellationTokenSource();
        var readStarted = mode == "cancel" ? host.PauseReadUntilCancellation("/flags.txt") : null;
        var pending = script.RunAsync(host, cancellation.Token);
        if (readStarted is not null)
        {
            try { await readStarted.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch
            {
                cancellation.Cancel();
                using var failed = await pending;
                throw;
            }
            cancellation.Cancel();
        }
        using (var result = await pending)
        {
            if (mode == "raise")
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("flag hash\n", result.StandardOutput);
            }
            else
            {
                Assert.False(result.Success);
                Assert.Equal(mode == "refuse" ? LythonFailureReason.SharedMemoryAllowance : LythonFailureReason.Cancelled,
                    result.FailureAttribution?.Reason);
                Assert.Empty(result.StandardOutput);
            }
        }
        Assert.Equal(compiled, provider.CompileCalls);
        Assert.Equal(1, host.ClosedFileCount);
        Assert.True(host.CompletedAsynchronously > 0);
        Assert.Equal(baseline, allowance.ReservedBytes);
    }

    private sealed class CountingProvider : ILythonRegexProvider
    {
        internal int CompileCalls { get; private set; }

        public ILythonRegexPattern Compile(ReadOnlyMemory<byte> pattern, LythonRegexOptions options,
            ILythonMemoryBudget memoryOwner, CancellationToken cancellationToken)
        {
            CompileCalls++;
            return Utf8RegexProvider.Instance.Compile(pattern, options, memoryOwner, cancellationToken);
        }
    }
}
