using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DictionaryViewCallTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "d={'a':1}\nk=d.keys();v=d.values();i=d.items()\nd['a']=2\nd['b']=3\nprint(list(k),list(v),list(i),len(k),len(v),len(i))", "['a', 'b'] [2, 3] [('a', 2), ('b', 3)] 2 2 2\n" },
        { "d={'a':1}\nold=d\nk=d.keys();v=d.values();i=d.items()\nd={'new':9}\nold['a']=7\nprint(list(k),list(v),list(i),d)\nold.clear()\nprint(list(k),list(v),list(i))", "['a'] [7] [('a', 7)] {'new': 9}\n[] [] []\n" },
        { "d={'a':1}\nk=d.keys;v=d.values;i=d.items\nprint(k.__self__ is d,v.__self__ is d,i.__self__ is d,k is d.keys)\nprint(list(k()),list(v()),list(i()))\nprint(list(d.keys(*())),list(d.values(*[],**{})),list(d.items(**{})))", "True True True False\n['a'] [1] [('a', 1)]\n['a'] [1] [('a', 1)]\n" },
        { "d={}\ndef argument():\n print('argument')\n return 1\ntry:d.keys(argument())\nexcept TypeError:print('keys')\ntry:d.values(value=argument())\nexcept TypeError:print('values')\ntry:d.items(*[argument()])\nexcept TypeError:print('items')\nprint(d)", "argument\nkeys\nargument\nvalues\nargument\nitems\n{}\n" },
        { "class C:\n def __getattribute__(self,name):\n  print('lookup',name)\n  return lambda:[name]\nc=C()\nprint(c.keys(),c.values(),c.items())", "lookup keys\nlookup values\nlookup items\n['keys'] ['values'] ['items']\n" },
        { "from collections import Counter,defaultdict\nfor d in (Counter({'a':2}),defaultdict(int,{'a':2})):\n print(list(d.keys()),list(d.values()),list(d.items()))", "['a'] [2] [('a', 2)]\n['a'] [2] [('a', 2)]\n" },
        { "for name in ('keys','values','items'):\n d={'a':1,'b':2}\n view=getattr(d,name)()\n iterator=iter(view)\n next(iterator)\n d['c']=3\n try:next(iterator)\n except RuntimeError:print(name,'changed')", "keys changed\nvalues changed\nitems changed\n" },
        { "d={'a':1}\nv=iter(d.values());i=iter(d.items())\nd['a']=9\nprint(next(v),next(i))\nprint(list(dict.keys(d)),list(dict.values(d)),list(dict.items(d)))", "9 ('a', 9)\n['a'] [9] [('a', 9)]\n" },
        { "def views(d):\n yield list(d.keys())\n yield list(d.values())\n yield list(d.items())\nprint(list(views({'a':1})))\nprint([list(d.items()) for d in ({'a':1},{'b':2})])", "[['a'], [1], [('a', 1)]]\n[[('a', 1)], [('b', 2)]]\n" },
        { "events=[]\ndef receiver():\n events.append('receiver')\n return {'a':1}\nprint(list(receiver().keys()),list(receiver().values()),list(receiver().items()),events)", "['a'] [1] [('a', 1)] ['receiver', 'receiver', 'receiver']\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ViewsPreservePythonBehavior(string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
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

    [Theory]
    [InlineData("keys")] [InlineData("values")] [InlineData("items")]
    public async Task ViewKeepsItsDictionaryFundedAfterRebinding(string method)
    {
        var source = (method == "keys" ? "d={'a'*4096:1}" : "d={'a':'a'*4096}")
            + $"\nview=d.{method}()\nd=None\nreturn list(view)[0]" + (method == "items" ? "[1]" : "");
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var held = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(held.Success, held.Failure?.Message);
                Assert.Equal(new string('a', 4096), held.ReturnValue);
                var retained = budget.ReservedBytes;
                Assert.True(retained > baseline);
                using (var next = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(next.Success, next.Failure?.Message);
                    Assert.True(budget.ReservedBytes > retained);
                    Assert.Equal(new string('a', 4096), held.ReturnValue);
                }
                Assert.Equal(retained, budget.ReservedBytes);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("keys", false)] [InlineData("keys", true)]
    [InlineData("values", false)] [InlineData("values", true)]
    [InlineData("items", false)] [InlineData("items", true)]
    public async Task ReceiverIoRemainsFundedUntilCompletionOrCancellation(string method, bool cancel)
    {
        var source = "def receiver():\n d=" + (method == "keys" ? "{'a'*4096:1}" : "{'a':'a'*4096}")
            + "\n with open('receiver.txt','a') as target:target.write('value')\n return d\n"
            + $"view=receiver().{method}()\nreturn list(view)[0]" + (method == "items" ? "[1]" : "");
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost(); host.SeedFile("/receiver.txt", "before");
        using var cancellation = new CancellationTokenSource();
        var entered = cancel ? host.PauseWriteUntilCancellation("/receiver.txt") : null;
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5));
                if (ReferenceEquals(first, pending))
                {
                    using var early = await pending;
                    Assert.Fail(early.Failure?.Message ?? "Receiver did not suspend.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(!cancel, result.Success);
            Assert.Equal(cancel ? "before" : "beforevalue", host.ReadText("/receiver.txt"));
            if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            else
            {
                Assert.Equal(new string('a', 4096), result.ReturnValue);
                Assert.True(host.CompletedAsynchronously > 0);
            }
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
