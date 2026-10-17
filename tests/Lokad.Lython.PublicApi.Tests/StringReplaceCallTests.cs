using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class StringReplaceCallTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetainedMethodsAndResultsKeepTheirOwners(bool exposed)
    {
        var source = "text='a'*4096\n" + (exposed
            ? "method=text.replace\ntext='changed'\nreturn method('a','b')"
            : "return text.replace('a','b')");
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var retained = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(retained.Success, retained.Failure?.Message);
                Assert.Equal(new string('b', 4096), Assert.IsType<string>(retained.ReturnValue));
                var held = budget.ReservedBytes;
                using (var next = script.Run(new MockLythonHost())) Assert.True(next.Success, next.Failure?.Message);
                Assert.Equal(held, budget.ReservedBytes);
                Assert.True(held > baseline);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeniedReplaceDoesNotPublishTheResult(bool exposed)
    {
        var source = "value='before'\ntext='a'*32768\n" + (exposed ? "method=text.replace\n" : "") +
            "print('ready')\ntry: value=" + (exposed ? "method('a','b')" : "text.replace('a','b')") +
            "\nexcept MemoryError: print(value,len(text))";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 80 * 1024 };
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.False(result.Success);
                Assert.Equal("MemoryError", result.Failure?.ExceptionType);
                Assert.Equal("ready\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("/bind.txt")]
    [InlineData("/call.txt")]
    public async Task CustomBindingAndCallsStillSuspendAndCancel(string? pausedPath)
    {
        const string source = "class Text:\n @property\n def replace(self):\n  with open('bind.txt','a') as output: output.write('bind')\n  def convert(old,new):\n   with open('call.txt','a') as output: output.write('call')\n   return 'custom'\n  return convert\nprint(Text().replace('a','b'))";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/bind.txt", "before");
        host.SeedFile("/call.txt", "before");
        var entered = pausedPath is null ? null : host.PauseWriteUntilCancellation(pausedPath);
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
                    Assert.Fail(early.Failure?.Message ?? "Execution completed before its custom replace operation suspended.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            if (pausedPath is null) Assert.True(result.Success, result.Failure?.Message);
            else Assert.False(result.Success);
            Assert.Equal(pausedPath is null ? "custom\n" : "", result.StandardOutput);
            Assert.Equal(pausedPath == "/bind.txt" ? "before" : "beforebind", host.ReadText("/bind.txt"));
            Assert.Equal(pausedPath is null ? "beforecall" : "before", host.ReadText("/call.txt"));
            Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    public static TheoryData<string, string, string> Programs => new()
    {
        { "plain", "print('patch a path'.replace('a','b'))", "pbtch b pbth\n" },
        { "unicode", "print('é😀é'.replace('é','λ'))", "λ😀λ\n" },
        { "empty_old", "print('é😀'.replace('','-'))", "-é-😀-\n" },
        { "overlap", "print('aaa'.replace('aa','X'))", "Xa\n" },
        { "missing", "print('report'.replace('z','x'))", "report\n" },
        { "remove", "print('é😀é'.replace('é',''))", "😀\n" },
        { "empty_source", "print(''.replace('','λ'))", "λ\n" },
        { "custom_method", "class R:\n def replace(self,old,new):return old+\":\"+new\nprint(R().replace(\"old\",\"new\"))", "old:new\n" },
        { "custom_property", "events=[]\nclass R:\n @property\n def replace(self):\n  events.append(\"bind\")\n  def call(old,new):\n   events.append(\"call\")\n   return old+\":\"+new\n  return call\nprint(R().replace(\"old\",\"new\"))\nprint(events)", "old:new\n['bind', 'call']\n" },
        { "receiver_rebind", "text=\"aa\"\nevents=[]\ndef old():\n global text\n events.append(\"old\")\n text=\"changed\"\n return \"a\"\ndef new():\n events.append(\"new\")\n return \"b\"\nprint(text.replace(old(),new()))\nprint(text,events)", "bb\nchanged ['old', 'new']\n" },
        { "retained_method", "text=\"aa\"\nmethod=text.replace\ntext=\"changed\"\nprint(method(\"a\",\"b\"))", "bb\n" },
        { "fresh_methods", "text=\"aa\"\nprint(text.replace is text.replace)", "False\n" },
        { "third_count", "print(\"aaaa\".replace(\"a\",\"b\",count=2))", "bbaa\n" },
        { "expanded", "args=(\"a\",\"b\")\nprint(\"aa\".replace(*args))", "bb\n" },
        { "unbound", "print(str.replace(\"aa\",\"a\",\"b\"))", "bb\n" },
        { "bad_old", "def call(text,old,new):return text.replace(old,new)\ntry:call(\"text\",None,\"x\")\nexcept Exception as error:print(type(error).__name__,str(error))", "TypeError replace() argument 1 must be str, not None\n" },
        { "bad_new", "def call(text,old,new):return text.replace(old,new)\ntry:call(\"text\",\"x\",[])\nexcept Exception as error:print(type(error).__name__,str(error))", "TypeError replace() argument 2 must be str, not list\n" },
        { "error_order", "def call(text,old,new):return text.replace(old,new)\ntry:call(\"text\",None,7)\nexcept Exception as error:print(type(error).__name__,str(error))", "TypeError replace() argument 1 must be str, not None\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ImmediateExposedAndDynamicCallsMatchPython(string label, string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, label);
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
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ArgumentSuspensionPreservesCapturedReceiverAndCancellation(bool cancel)
    {
        const string source = "text='aa'\ndef old():\n global text\n text='changed'\n with open('argument.txt','a') as output:output.write('old')\n return 'a'\nprint(text.replace(old(),'b'),text)";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/argument.txt", "before");
        var entered = cancel ? host.PauseWriteUntilCancellation("/argument.txt") : null;
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Same(entered, first);
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(!cancel, result.Success);
            if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            Assert.Equal(cancel ? "" : "bb changed\n", result.StandardOutput);
            Assert.Equal(cancel ? "before" : "beforeold", host.ReadText("/argument.txt"));
            Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
