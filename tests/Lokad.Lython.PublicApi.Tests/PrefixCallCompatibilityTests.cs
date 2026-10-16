using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class PrefixCallCompatibilityTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "custom-method", "class C:\n def startswith(self,prefix): return prefix\nprint(C().startswith(7),C().startswith(prefix=8))", "7 8\n" },
        { "arity", "def run(text,args): return text.startswith(*args)\nfor args in [(),('a',),('b',1),('b',1,2),('a',0,3,4)]:\n try: print(run('abc',args))\n except TypeError: print('rejected')", "rejected\nTrue\nTrue\nTrue\nrejected\n" },
        { "unknown-first", "def run(prefix): return 'abc'.startswith((prefix,None))\nprint(run('a'))\ntry: run('x')\nexcept TypeError: print('rejected')", "True\nrejected\n" },
        { "bound-keywords", "for options in [{'start':0},{'end':3}]:\n try: print('abc'.startswith('a',**options))\n except TypeError: print('rejected')", "rejected\nrejected\n" },
        { "ordinary", "for text in ['NEEDLE:a','other','café','😀x']: print(text.startswith(('NEEDLE:','😀')),text.endswith(('a','x')))", "True True\nFalse False\nFalse False\nTrue True\n" },
        { "tuple-shortcircuit", "print('abc'.startswith(('a',None)))\ndef match(value):return 'abc'.startswith(value)\ntry:match((None,'a'))\nexcept TypeError:print('rejected')", "True\nrejected\n" },
        { "receiver-capture", "text=['abc']\ndef prefix():text[0]='xyz';return 'a'\nprint(text[0].startswith(prefix()),text[0])", "True xyz\n" },
        { "exposed-expanded", "method='abc'.startswith\nprint(method('a'),str.startswith('abc','a'),'abc'.startswith(*['a']))", "True True True\n" },
        { "keywords", "try:'abc'.startswith(prefix='a')\nexcept TypeError:print('rejected')\nelse:print('accepted')\ntry:'abc'.endswith(suffix='c')\nexcept TypeError:print('rejected')\nelse:print('accepted')", "rejected\nrejected\n" },
        { "invalid-type", "for value in [None,1,[],b'a']:\n try:'abc'.startswith(value)\n except TypeError:print('rejected')", "rejected\nrejected\nrejected\nrejected\n" },
        { "bounds", "events=[]\nclass Index:\n def __init__(self,value):self.value=value\n def __index__(self):events.append(self.value);return self.value\nprint('😀abc'.startswith('a',Index(1),Index(3)),events)", "True [1, 3]\n" },
        { "empty-bounds", "for text in ['','abc','😀']:\n print(text.startswith(''),text.startswith('',10**100),text.endswith('',-10**100))", "True False True\nTrue False True\nTrue False True\n" },
        { "endswith-shortcircuit", "print('abc'.endswith(('c',None)))\ndef match(value):return 'abc'.endswith(value)\ntry:match((None,'c'))\nexcept TypeError:print('rejected')", "True\nrejected\n" },
        { "exposed-keyword", "method='abc'.startswith\ntry:method(prefix='a')\nexcept TypeError:print('rejected')\nelse:print('accepted')", "rejected\n" },
        { "unbound-keyword", "try:str.startswith('abc',prefix='a')\nexcept TypeError:print('rejected')\nelse:print('accepted')", "rejected\n" },
        { "expanded-keyword", "for name in ['startswith','endswith']:\n method=getattr('abc',name)\n try:method(**{'prefix' if name=='startswith' else 'suffix':'a'})\n except TypeError:print('rejected')\n else:print('accepted')", "rejected\nrejected\n" },
        { "unknown-earlier", "def run(text): return text.startswith(('x',None))\nprint(run('xyz'))\ntry:run('abc')\nexcept TypeError:print('rejected')", "True\nrejected\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task PrefixCallsMatchPython(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
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

    public static IEnumerable<object[]> KeywordCases()
    {
        foreach (var method in new[] { "startswith", "endswith" })
        foreach (var form in new[] { "ordinary", "exposed", "unbound", "expanded" })
        foreach (var cancel in new[] { false, true })
            yield return [method, form, cancel];
    }

    [Theory]
    [MemberData(nameof(KeywordCases))]
    public async Task KeywordArgumentsEvaluateOnceBeforeRejection(string method, string form, bool cancel)
    {
        var keyword = method == "startswith" ? "prefix" : "suffix";
        var call = form switch
        {
            "ordinary" => $"text.{method}({keyword}=argument())",
            "exposed" => $"callback({keyword}=argument())",
            "unbound" => $"str.{method}(text,{keyword}=argument())",
            _ => $"callback(**{{'{keyword}':argument()}})"
        };
        var source = "text='abc'\ncallback=text." + method + "\ndef argument():\n with open('argument.txt','a') as output: output.write('x')\n return 'a'\ntry: "
            + call + "\nexcept TypeError: print('rejected')\nelse: raise AssertionError('keyword accepted')";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
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
                if (ReferenceEquals(first, pending))
                {
                    using var early = await pending;
                    Assert.Fail(early.Failure?.Message ?? "Keyword call completed before argument suspension.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(!cancel, result.Success);
            Assert.Equal(cancel ? "" : "rejected\n", result.StandardOutput);
            Assert.Equal(cancel ? "before" : "beforex", host.ReadText("/argument.txt"));
            if (!cancel) Assert.True(host.CompletedAsynchronously > 0);
            else Assert.True(entered!.IsCompletedSuccessfully);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
