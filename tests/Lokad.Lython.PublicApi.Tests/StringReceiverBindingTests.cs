using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class StringReceiverBindingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImmediateAndExposedCallsPreserveReceiverBindingAndKeywords(bool asynchronous)
    {
        const string source = """
            text = 'a b c'
            method = text.rsplit
            def change():
                global text
                text = 'changed'
                return 1
            print(text.rsplit(None, change()), text)
            print(method(maxsplit=1), method.__name__, method.__self__)
            print(' x '.strip(), ' x '.lstrip(), ' x '.rstrip())
            print('a\tb'.expandtabs(tabsize=2), 'a\nb'.splitlines(keepends=True))
            for name in ['center', 'ljust', 'rjust']:
                print(getattr('é', name)(3, '.'))
            print('-3'.zfill(4))
            for name in ['find', 'index', 'rfind', 'rindex', 'count']:
                print(getattr('é😀é', name)('é', 1))
            events = []
            def argument():
                events.append('argument')
                return 1
            try:
                method(None, argument(), argument())
            except TypeError:
                events.append('error')
            print(events)
            """;
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.True(script.IsValid);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("['a b', 'c'] changed\n['a b', 'c'] rsplit a b c\nx x   x\na b ['a\\n', 'b']\n.é.\né..\n..é\n-003\n2\n2\n2\n2\n1\n['argument', 'argument', 'error']\n", result.StandardOutput);
    }

    public static IEnumerable<object[]> GuestCallbacks()
    {
        foreach (var method in new[] { "rsplit", "splitlines", "expandtabs", "center", "ljust", "rjust", "zfill", "find", "index", "rfind", "rindex", "count" })
        foreach (var exposed in new[] { false, true })
        foreach (var cancel in new[] { false, true })
            yield return [method, exposed, cancel];
    }

    [Theory]
    [MemberData(nameof(GuestCallbacks))]
    public async Task GuestProtocolsSuspendWithFundedOperandsAndAwaitedCancellation(string method, bool exposed, bool cancel)
    {
        var protocol = method == "splitlines" ? "__bool__" : "__index__";
        var value = method == "splitlines" ? "True" : method is "center" or "ljust" or "rjust" or "zfill" ? "8192" : "1";
        var arguments = method == "rsplit" ? "None,Hook()" : method is "find" or "index" or "rfind" or "rindex" or "count" ? "'λ',Hook()" : "Hook()";
        var source = "class Hook:\n def " + protocol + "(self):\n  global text\n  text=None\n  with open('/gate','a') as output:output.write('done')\n  return " + value
            + "\ntext=('λ a'+chr(9)+chr(10))*1024\n" + (exposed ? "method=text." + method + "\ntext=None\nresult=method(" : "result=text." + method + "(")
            + arguments + ")\nprint('done')";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/gate", "before:");
        var entered = cancel ? host.PauseWriteUntilCancellation("/gate") : null;
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(10));
                if (ReferenceEquals(first, pending))
                {
                    using var early = await pending;
                    Assert.Fail(early.Failure?.Message ?? "Guest protocol did not suspend.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(!cancel, result.Success);
            Assert.Equal(cancel ? "" : "done\n", result.StandardOutput);
            if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            Assert.Equal(cancel ? "before:" : "before:done", host.ReadText("/gate"));
            Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusedPaddingRemainsTerminalAndAllowanceIsReusable(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            method = ('x' * 4096).center
            def work():
                try:
                    method(32000000)
                except MemoryError:
                    print('caught')
                finally:
                    return 'hidden'
            print(work())
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Empty(result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        using (var result = new LythonEngine(Utf8RegexProvider.Instance).Run("return 'x'.center(20)", new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
            Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EscapingReceiverAndResultKeepTheirOwnerAfterScriptDisposal(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("text='λ a '*1024\nmethod=text.rsplit\ntext=None\nreturn [method.__self__,method()]", budget);
        Assert.True(script.IsValid);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        script.Dispose();
        Assert.True(budget.ReservedBytes > 0);
        result.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
