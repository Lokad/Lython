using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SubscriptStoreExecutionTests
{
    [Fact]
    public async Task StoredAliasesRemainFundedAcrossExecutions()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile("box={}\nvalue=['x'*4096]\nbox['key']=value\nreturn box['key']", budget);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var retained = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(retained.Success, retained.Failure?.Message);
                var values = Assert.IsAssignableFrom<System.Collections.IEnumerable>(retained.ReturnValue).Cast<object?>();
                Assert.Equal(new string('x', 4096), Assert.IsType<string>(Assert.Single(values)));
                var held = budget.ReservedBytes;
                using (var next = script.Run(new MockLythonHost())) Assert.True(next.Success, next.Failure?.Message);
                Assert.Equal(held, budget.ReservedBytes);
                Assert.True(held > baseline);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("/rhs.txt")]
    [InlineData("/receiver.txt")]
    [InlineData("/key.txt")]
    [InlineData("/bind.txt")]
    [InlineData("/set.txt")]
    public async Task EveryStorePhaseCanSuspendAndCancelWithoutReplay(string? pausedPath)
    {
        const string source = "def mark(path,label):\n with open(path,'a') as output: output.write(label)\nclass Box:\n @property\n def __setitem__(self):\n  mark('bind.txt','B')\n  def assign(key,value):\n   mark('set.txt','S')\n   self.saved=(key,value)\n  return assign\nbox=Box()\npayload=['value']\ndef rhs():\n mark('rhs.txt','R')\n return payload\ndef receiver():\n mark('receiver.txt','C')\n return box\ndef key():\n mark('key.txt','K')\n return 'x'\nreceiver()[key()]=rhs()\nprint(box.saved[0],box.saved[1] is payload)";
        var paths = new[] { "/rhs.txt", "/receiver.txt", "/key.txt", "/bind.txt", "/set.txt" };
        var labels = new[] { "R", "C", "K", "B", "S" };
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        foreach (var path in paths) host.SeedFile(path, "before");
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
                    Assert.Fail(early.Failure?.Message ?? "Store completed before its controlled host operation suspended.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            if (pausedPath is null) Assert.True(result.Success, result.Failure?.Message);
            else Assert.False(result.Success);
            Assert.Equal(pausedPath is null ? "x True\n" : "", result.StandardOutput);
            var stop = pausedPath is null ? paths.Length : Array.IndexOf(paths, pausedPath);
            for (var i = 0; i < paths.Length; i++)
                Assert.Equal("before" + (i < stop ? labels[i] : ""), host.ReadText(paths[i]));
            Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    public static TheoryData<string, string> Programs => new()
    {
        { "def run():\n box={}\n key='before'\n (lambda:box)()[(lambda:key)()]=3\n key='after'\n return box\nprint(run())", "{'before': 3}\n" },
        { "box={}\nclass Bad:\n def __setitem__(self,key,value): raise ValueError('bad')\ntry: box['ok']=Bad()['bad']=[]\nexcept ValueError: print(box)", "{'ok': []}\n" },
        { "left={}\nright={}\nbox=left\nkey='old'\ndef rhs():\n global box,key\n box=right\n key='new'\n return 1\nbox[key]=rhs()\nprint(left,right)", "{} {'new': 1}\n" },
        { "left={}\nright={}\nbox=left\ndef receiver(): return box\ndef key():\n global box\n box=right\n return 'kept'\nreceiver()[key()]=7\nprint(left,right)", "{'kept': 7} {}\n" },
        { "events=[]\nbox={}\ndef rhs():\n events.append('rhs')\n return 7\ndef receiver():\n events.append('receiver')\n return box\ndef key():\n events.append('key')\n return 'x'\nreceiver()[key()]=rhs()\nprint(events,box)", "['rhs', 'receiver', 'key'] {'x': 7}\n" },
        { "events=[]\nbox={}\ndef rhs():\n events.append('rhs')\n return 7\ndef receiver(name):\n events.append(name)\n return box\ndef key(name):\n events.append(name)\n return name\nreceiver('left')[key('a')]=receiver('right')[key('b')]=rhs()\nprint(events,box)", "['rhs', 'left', 'a', 'right', 'b'] {'a': 7, 'b': 7}\n" },
        { "box={}\ndef key(): raise ValueError('bad')\ntry: box[key()]=1\nexcept ValueError: print(box)", "{}\n" },
        { "def run():\n box={}\n name='x'\n value=3\n box[name]=value\n return box\nprint(run())", "{'x': 3}\n" },
        { "events=[]\nclass Key:\n def __index__(self):\n  events.append('bad-index')\n  return 0\nkey=Key()\nvalue=[]\nclass Box:\n def __setitem__(self,received,item): events.append((received is key,item is value))\nBox()[key]=value\nprint(events)", "[(True, True)]\n" },
        { "events=[]\nclass Box: pass\nbox=Box()\nbox.__setitem__=lambda key,value:events.append('instance')\ntry: box[0]=1\nexcept TypeError: print('type-only',events)", "type-only []\n" },
        { "events=[]\nclass Box:\n def __setitem__(self,key,value): events.append('type')\nbox=Box()\nbox.__setitem__=lambda key,value:events.append('instance')\nbox[0]=1\nprint(events)", "['type']\n" },
        { "events=[]\nclass Box:\n def __getattr__(self,name):\n  events.append(name)\n  return lambda key,value:None\ntry: Box()[0]=1\nexcept TypeError: print(events)", "[]\n" },
        { "events=[]\nclass Box:\n @property\n def __setitem__(self):\n  events.append('bind')\n  return lambda key,value:events.append((key,value))\nBox()[0]=1\nprint(events)", "['bind', (0, 1)]\n" },
        { "events=[]\nclass Box:\n @property\n def __setitem__(self):\n  events.append('bind')\n  raise ValueError('bad')\ntry: Box()[0]=1\nexcept ValueError: print(events)", "['bind']\n" },
        { "events=[]\nclass Box:\n def __delitem__(self,key): events.append('type')\nbox=Box()\nbox.__delitem__=lambda key:events.append('instance')\ndel box[0]\nprint(events)", "['type']\n" },
        { "events=[]\nclass Box: pass\nbox=Box()\nbox.__delitem__=lambda key:events.append('instance')\ntry: del box[0]\nexcept TypeError: print('type-only',events)", "type-only []\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task TargetOrderAndImplicitSettersMatchPython(string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
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
}
