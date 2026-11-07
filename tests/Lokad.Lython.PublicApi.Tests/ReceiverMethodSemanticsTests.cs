using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ReceiverMethodSemanticsTests
{
    [Fact]
    public async Task RegexCallsSnapshotReceiversAndPreserveExposedMethodsAndDefaults()
    {
        const string source = """
            import re
            match = re.search('(a)', '-a')
            original = match
            method = match.start
            def change():
                global match
                match = re.search('(a)', '--a')
                return 1
            print(match.start(change()), match.start(1))
            print(method.__self__ is original, method.__name__, method is original.start)
            print(hash(method) == hash(original.start), method(1))
            optional = re.search('(?P<a>a)(?P<b>b)?', 'a')
            marker = []
            groups = optional.groups
            named = optional.groupdict
            print(groups(default=marker)[1] is marker, named(default=marker)['b'] is marker)
            print(optional.expand(template=r'<\g<a>>'), optional.__getitem__('a'))
            print(optional.group(0, 1, 2))
            """;
        // Independent CPython 3.13 output; includes attribute values as well as
        // immediate calls, positional selectors and keyword default presence.
        await AssertBoth(source, "1 2\nTrue start False\nTrue 1\nTrue True\n<a> a\n('a', 'a', None)\n");
    }

    [Fact]
    public async Task InvalidCallsEvaluateArgumentsBeforeBindingAndDoNotMutateReceivers()
    {
        await AssertBoth("""
            import re
            events = []
            items = []
            mapping = {'a': 1}
            match = re.search('(a)', 'a')
            def value():
                events.append('argument')
                return 0
            def regex_calls(match):
                return [lambda: match.start(group=value()), lambda: match.end(value(), value()),
                        lambda: match.groups(value(), value())]
            for operation in [lambda: items.append(), lambda: items.append(value(), value()),
                              lambda: mapping.get(), lambda: mapping.get(key=value()),
                              lambda: mapping.setdefault('x', value(), value())] + regex_calls(match):
                try:
                    operation()
                except TypeError:
                    events.append('error')
            print(events)
            print(items, mapping)
            """, "['error', 'argument', 'argument', 'error', 'error', 'argument', 'error', 'argument', 'argument', 'error', 'argument', 'error', 'argument', 'argument', 'error', 'argument', 'argument', 'error']\n[] {'a': 1}\n");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuspendedRegexSelectorKeepsReceiverFundingAndIndependentCurrentTokens(bool exposed)
    {
        var source = """
            import re
            class Index:
                def __index__(self):
                    global match, source
                    match = None
                    source = None
                    with open('/index') as handle:
                        return int(handle.read())
            source = 'α' * 4096 + 'b'
            match = re.search('(α+)(b)', source)
            """ + (exposed ? "\nmethod = match.span\nprint(method(Index()))" : "\nprint(match.span(Index()))");
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            var paused = new DelayedLythonHost();
            paused.SeedFile("/index", "2");
            var started = paused.PauseReadUntilCancellation("/index");
            using var cancellation = new CancellationTokenSource();
            var pending = script.RunAsync(paused, new LythonRunOptions { CancellationToken = cancellation.Token });
            try
            {
                await started.WaitAsync(TimeSpan.FromSeconds(10));
                var held = budget.ReservedBytes;
                Assert.True(held > baseline + 8192);
                var other = new DelayedLythonHost();
                other.SeedFile("/index", "2");
                using (var result = await script.RunAsync(other))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("(4096, 4097)\n", result.StandardOutput);
                }
                Assert.Equal(held, budget.ReservedBytes);
                Assert.False(pending.IsCompleted);
            }
            finally
            {
                cancellation.Cancel();
                using var stopped = await pending.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(stopped.Success);
                Assert.Equal(LythonFailureReason.Cancelled, stopped.FailureAttribution?.Reason);
                Assert.Empty(stopped.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
            var fresh = new MockLythonHost();
            fresh.SeedFile("/index", "2");
            using (var result = script.Run(fresh)) Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static async Task AssertBoth(string source, string expected)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget))
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
}
