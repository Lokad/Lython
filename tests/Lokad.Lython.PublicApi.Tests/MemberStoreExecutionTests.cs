using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class MemberStoreExecutionTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "events=[]\nclass Box: pass\nbox=Box()\ndef rhs():\n events.append('rhs')\n return 7\ndef receiver():\n events.append('receiver')\n return box\nreceiver().value=rhs()\nprint(events,box.value)", "['rhs', 'receiver'] 7\n" },
        { "events=[]\nclass Box:\n @property\n def value(self): return self.saved\n @value.setter\n def value(self,value):\n  events.append(('set',value))\n  self.saved=value\nbox=Box()\nbox.value=8\nprint(events,box.value)", "[('set', 8)] 8\n" },
        { "events=[]\nclass Box: pass\nclass Bad:\n def __setattr__(self,name,value):\n  events.append((name,value))\n  raise ValueError('bad')\nbox=Box()\ntry: box.value=Bad().value=[]\nexcept ValueError: print(box.value,events)", "[] [('value', [])]\n" },
        { "def run():\n class Box: pass\n box=Box()\n value=9\n (lambda:box)().value=(lambda:value)()\n value=10\n return box.value\nprint(run())", "9\n" },
        { "events=[]\nclass Descriptor:\n def __set__(self,instance,value):\n  events.append(value)\n  instance.saved=value\nclass Box:\n value=Descriptor()\nbox=Box()\nbox.value=11\nprint(events,box.saved)", "[11] 11\n" },
        { "events=[]\nclass Box: pass\nbox=Box()\nbox.__setattr__=lambda name,value:events.append(name)\nbox.value=12\nprint(box.value,events)", "12 []\n" },
        { "events=[]\ndef rhs():\n events.append('rhs')\n return 1\ndef receiver():\n events.append('receiver')\n raise ValueError()\ntry: receiver().value=rhs()\nexcept ValueError: print(events)", "['rhs', 'receiver']\n" },
        { "class Box: pass\nleft=Box()\nright=Box()\nbox=left\ndef rhs():\n global box\n box=right\n return 13\nbox.value=rhs()\nprint(hasattr(left,'value'),right.value)", "False 13\n" },
        { "events=[]\nclass Box:\n @property\n def value(self): return 1\n @value.setter\n def value(self,value):\n  events.append(value)\n  raise ValueError()\nbox=Box()\ntry: box.value=14\nexcept ValueError: print(box.value,events)", "1 [14]\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task OrdinaryStoresMatchPython(string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
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

    [Fact]
    public async Task EscapingMemberValueRemainsFundedAcrossExecutions()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("class Box: pass\nbox=Box()\npayload=['x'*4096]\nbox.value=payload\nreturn box.value", budget);
        Assert.True(script.IsValid);
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
                Assert.True(held > baseline);
                Assert.Equal(held, budget.ReservedBytes);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("property", null)]
    [InlineData("property", "/rhs.txt")]
    [InlineData("property", "/receiver.txt")]
    [InlineData("property", "/set.txt")]
    [InlineData("setattr", null)]
    [InlineData("setattr", "/rhs.txt")]
    [InlineData("setattr", "/receiver.txt")]
    [InlineData("setattr", "/bind.txt")]
    [InlineData("setattr", "/set.txt")]
    [InlineData("descriptor", null)]
    [InlineData("descriptor", "/rhs.txt")]
    [InlineData("descriptor", "/receiver.txt")]
    [InlineData("descriptor", "/bind.txt")]
    [InlineData("descriptor", "/set.txt")]
    public async Task StorePhasesSuspendAndCancelWithoutReplay(string protocol, string? pausedPath)
    {
        var definition = protocol switch
        {
            "property" => "class Box:\n @property\n def value(self): return self.saved\n @value.setter\n def value(self,value):\n  mark('set.txt','S')\n  self.saved=value\n",
            "setattr" => "class Box:\n @property\n def __setattr__(self):\n  mark('bind.txt','B')\n  def assign(name,value):\n   mark('set.txt','S')\n   object.__setattr__(self,'saved',value)\n  return assign\n",
            _ => "class Descriptor:\n @property\n def __set__(self):\n  mark('bind.txt','B')\n  def assign(instance,value):\n   mark('set.txt','S')\n   instance.saved=value\n  return assign\nclass Box:\n value=Descriptor()\n",
        };
        var source = "def mark(path,label):\n with open(path,'a') as output: output.write(label)\n" + definition +
            "box=Box()\npayload=['value']\ndef rhs():\n mark('rhs.txt','R')\n return payload\ndef receiver():\n mark('receiver.txt','C')\n return box\nreceiver().value=rhs()\nprint(box.saved is payload)";
        var paths = protocol == "property" ? new[] { "/rhs.txt", "/receiver.txt", "/set.txt" }
            : new[] { "/rhs.txt", "/receiver.txt", "/bind.txt", "/set.txt" };
        var labels = protocol == "property" ? new[] { "R", "C", "S" } : new[] { "R", "C", "B", "S" };
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
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
            Assert.Equal(pausedPath is null ? "True\n" : "", result.StandardOutput);
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
}
