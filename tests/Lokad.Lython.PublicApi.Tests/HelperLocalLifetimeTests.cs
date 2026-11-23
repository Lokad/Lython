using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class HelperLocalLifetimeTests
{
    public static IEnumerable<object[]> Programs()
    {
        yield return ["""
            def choose(flag, base):
                if flag: value = base + 7
                return value
            assert choose(True, 10) == 17
            try: choose(False, 40)
            except UnboundLocalError: pass
            else: raise AssertionError('an earlier invocation supplied an unassigned local')
            assert choose(True, 20) == 27
            print('ok')
            """];
        yield return ["""
            def leaf(value): return value + 1
            def inner(value):
                keep = value + 100
                return leaf(value) + keep
            def middle(value):
                before = value * 3
                after = inner(value)
                return before + after + value
            def recurse(depth, value):
                saved = value + 1
                if depth: return recurse(depth - 1, saved) + saved
                return middle(saved)
            assert middle(5) == 131
            assert recurse(12, 5) == 347
            print('ok')
            """];
        yield return ["""
            def pair(left, right): return left + right, left - right
            def check(value):
                saved = value + 1
                left, right = pair(saved, 3)
                return left, right, saved
            subject = [10, 20]
            match subject:
                case [left, right] if check(left) == (14, 8, 11):
                    assert (left, right) == (10, 20)
                case _: raise AssertionError('helper changed pattern state')
            assert check(20) == (24, 18, 21)
            print('ok')
            """];
        yield return ["""
            def factory(shared):
                def child():
                    saved = shared
                    return lambda: saved
                return child(), shared
            obj = []
            read, alias = factory(obj)
            assert read() is obj and alias is obj
            result = factory(obj)
            assert type(result) is tuple and result[0]() is result[1]
            print('ok')
            """];
        yield return ["""
            def private_factory(value):
                def child(arg=value): return arg
                return child
            def recursive_factory(value):
                def child(depth=1):
                    if depth: return child(depth - 1)
                    return value
                return child
            def protected_factory(value, observed):
                try:
                    def child(arg=value): return arg
                    return child
                finally:
                    observed.append(locals()['child'])
            value = []
            private = private_factory(value)
            recursive = recursive_factory(value)
            observed = []
            protected = protected_factory(value, observed)
            assert private() is value and recursive() is value
            assert protected() is value and observed[0] is protected
            print('ok')
            """];
    }

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task RepeatedAndNestedHelpersPreserveLocalLifetimes(string source)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("ok\n", result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NestedLexicalChildPromotesOnceAcrossHostSuspension(bool cancel)
    {
        const string source = """
            class Source:
                def __getitem__(self, index):
                    with open('effects.txt', 'a') as output:
                        output.write('write')
                    return index
            def factory(source):
                count = 0
                def child():
                    nonlocal count
                    count += 1
                    selected = source[7]
                    return selected
                return child(), lambda: count
            value, read = factory(Source())
            print(value, read())
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/effects.txt", "before");
        var blockedWrite = cancel ? host.PauseWriteUntilCancellation("/effects.txt") : null;
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (blockedWrite is not null)
            {
                var first = await Task.WhenAny(blockedWrite, pending).WaitAsync(TimeSpan.FromSeconds(10));
                if (ReferenceEquals(first, pending))
                {
                    using var early = await pending;
                    Assert.Fail(early.Failure?.Message ?? "The child completed before the host write suspended.");
                }
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }

            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(!cancel, result.Success);
            Assert.Equal(cancel ? "" : "7 1\n", result.StandardOutput);
            Assert.Equal(cancel ? "before" : "beforewrite", host.ReadText("/effects.txt"));
            if (!cancel) Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
