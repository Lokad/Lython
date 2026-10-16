using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeItemMutationTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "list-set", "values=[0,1,2]\nevents=[]\nclass Index:\n def __index__(self): events.append('type'); return 1\nkey=Index();key.__index__=lambda:0\nvalues[key]=7\nprint(values,events)", "[0, 7, 2] ['type']\n" },
        { "list-delete", "values=[0,1,2]\nevents=[]\nclass Index:\n def __index__(self): events.append('type'); return 1\nkey=Index();key.__index__=lambda:0\ndel values[key]\nprint(values,events)", "[0, 2] ['type']\n" },
        { "list-recoercion", "values=[0,1,2]\nevents=[]\nclass Index:\n def __init__(self): self.value=0\n def __index__(self): events.append(self.value); return self.value\nkey=Index()\ndef rhs(): key.value=1; return 10\nvalues[key]+=rhs()\nprint(values,events)", "[0, 10, 2] [0, 1]\n" },
        { "deque-set", "from collections import deque\nvalues=deque([0,1,2])\nevents=[]\nclass Index:\n def __index__(self): events.append('type'); return 1\nkey=Index();key.__index__=lambda:0\nvalues[key]=7\nprint(list(values),events)", "[0, 7, 2] ['type']\n" },
        { "deque-delete", "from collections import deque\nvalues=deque([0,1,2])\nevents=[]\nclass Index:\n def __index__(self): events.append('type'); return 1\nkey=Index();key.__index__=lambda:0\ndel values[key]\nprint(list(values),events)", "[0, 2] ['type']\n" },
        { "deque-recoercion", "from collections import deque\nvalues=deque([0,1,2])\nevents=[]\nclass Index:\n def __init__(self): self.value=0\n def __index__(self): events.append(self.value); return self.value\nkey=Index()\ndef rhs(): key.value=1; return 10\nvalues[key]+=rhs()\nprint(list(values),events)", "[0, 10, 2] [0, 1]\n" },
        { "missing-set", "events=[]\nclass Index: pass\nkey=Index();key.__index__=lambda:1\nvalues=[0,1,2]\ntry: values[key]=7\nexcept (TypeError,ValueError) as e: print(type(e).__name__,str(e))\nprint(values,events)", "TypeError list indices must be integers or slices, not Index\n[0, 1, 2] []\n" },
        { "missing-delete", "events=[]\nclass Index: pass\nkey=Index();key.__index__=lambda:1\nvalues=[0,1,2]\ntry: del values[key]\nexcept (TypeError,ValueError) as e: print(type(e).__name__,str(e))\nprint(values,events)", "TypeError list indices must be integers or slices, not Index\n[0, 1, 2] []\n" },
        { "getattr-set", "events=[]\nclass Index:\n def __getattr__(self,name): events.append(name);return lambda:1\nkey=Index()\nvalues=[0,1,2]\ntry: values[key]=7\nexcept (TypeError,ValueError) as e: print(type(e).__name__,str(e))\nprint(values,events)", "TypeError list indices must be integers or slices, not Index\n[0, 1, 2] []\n" },
        { "getattr-delete", "events=[]\nclass Index:\n def __getattr__(self,name): events.append(name);return lambda:1\nkey=Index()\nvalues=[0,1,2]\ntry: del values[key]\nexcept (TypeError,ValueError) as e: print(type(e).__name__,str(e))\nprint(values,events)", "TypeError list indices must be integers or slices, not Index\n[0, 1, 2] []\n" },
        { "bad-result-set", "events=[]\nclass Index:\n def __index__(self): events.append('index');return 'bad'\nkey=Index()\nvalues=[0,1,2]\ntry: values[key]=7\nexcept (TypeError,ValueError) as e: print(type(e).__name__,str(e))\nprint(values,events)", "TypeError __index__ returned non-int (type str)\n[0, 1, 2] ['index']\n" },
        { "bad-result-delete", "events=[]\nclass Index:\n def __index__(self): events.append('index');return 'bad'\nkey=Index()\nvalues=[0,1,2]\ntry: del values[key]\nexcept (TypeError,ValueError) as e: print(type(e).__name__,str(e))\nprint(values,events)", "TypeError __index__ returned non-int (type str)\n[0, 1, 2] ['index']\n" },
        { "raising-set", "events=[]\nclass Index:\n def __index__(self): events.append('index');raise ValueError('bad')\nkey=Index()\nvalues=[0,1,2]\ntry: values[key]=7\nexcept (TypeError,ValueError) as e: print(type(e).__name__,str(e))\nprint(values,events)", "ValueError bad\n[0, 1, 2] ['index']\n" },
        { "raising-delete", "events=[]\nclass Index:\n def __index__(self): events.append('index');raise ValueError('bad')\nkey=Index()\nvalues=[0,1,2]\ntry: del values[key]\nexcept (TypeError,ValueError) as e: print(type(e).__name__,str(e))\nprint(values,events)", "ValueError bad\n[0, 1, 2] ['index']\n" },
        { "descriptor-set", "events=[]\nclass Index:\n @property\n def __index__(self):\n  events.append('bind')\n  return lambda:1\nkey=Index();values=[0,1,2]\nvalues[key]=7\nprint(values,events)", "[0, 7, 2] ['bind']\n" },
        { "descriptor-delete", "events=[]\nclass Index:\n @property\n def __index__(self):\n  events.append('bind')\n  return lambda:1\nkey=Index();values=[0,1,2]\ndel values[key]\nprint(values,events)", "[0, 2] ['bind']\n" },
        { "raw-keys", "class Key:\n def __index__(self): raise AssertionError('raw')\nkey=Key();mapping={key:1}\nmapping[key]=7\nprint(mapping[key]);del mapping[key];print(mapping)\nclass Box:\n def __setitem__(self,k,v): print(k is key,v)\n def __delitem__(self,k): print(k is key)\nbox=Box();box[key]=7;del box[key]", "7\n{}\nTrue 7\nTrue\n" },
        { "immutable-index-before-rejection", "events=[]\nclass Key:\n def __index__(self): events.append('index');return 1\nkey=Key();values=(0,1,2)\ntry: values[key]=7\nexcept TypeError: print('set',events)\ntry: del values[key]\nexcept TypeError: print('delete',events)", "set ['index']\ndelete ['index', 'index']\n" },
        { "bounds-after-conversion", "values=[0,1,2]\nclass Key:\n def __index__(self): values.clear(); return 0\ntry: values[Key()]=7\nexcept IndexError: print(values)", "[]\n" },
        { "bytes-index-before-rejection", "events=[]\nclass Key:\n def __index__(self): events.append('index');return 1\nkey=Key()\nvalues=b'abc'\ntry: values[key]=7\nexcept TypeError: print('set',events)\ntry: del values[key]\nexcept TypeError: print('delete',events)", "set ['index']\ndelete ['index', 'index']\n" },
        { "bytes-missing", "events=[]\nclass Key: pass\nkey=Key();key.__index__=lambda:1\nvalues=b'abc'\ntry: values[key]=7\nexcept TypeError: print('set',events)\ntry: del values[key]\nexcept TypeError: print('delete',events)", "set []\ndelete []\n" },
        { "text-index-before-rejection", "events=[]\nclass Key:\n def __index__(self): events.append('index');return 1\nkey=Key()\nvalues='abc'\ntry: values[key]=7\nexcept TypeError: print('set',events)\ntry: del values[key]\nexcept TypeError: print('delete',events)", "set ['index']\ndelete ['index', 'index']\n" },
        { "text-missing", "events=[]\nclass Key: pass\nkey=Key();key.__index__=lambda:1\nvalues='abc'\ntry: values[key]=7\nexcept TypeError: print('set',events)\ntry: del values[key]\nexcept TypeError: print('delete',events)", "set []\ndelete []\n" },
        { "range-index-before-rejection", "events=[]\nclass Key:\n def __index__(self): events.append('index');return 1\nkey=Key()\nvalues=range(3)\ntry: values[key]=7\nexcept TypeError: print('set',events)\ntry: del values[key]\nexcept TypeError: print('delete',events)", "set ['index']\ndelete ['index', 'index']\n" },
        { "range-missing", "events=[]\nclass Key: pass\nkey=Key();key.__index__=lambda:1\nvalues=range(3)\ntry: values[key]=7\nexcept TypeError: print('set',events)\ntry: del values[key]\nexcept TypeError: print('delete',events)", "set []\ndelete []\n" },
        { "tuple-missing", "events=[]\nclass Key: pass\nkey=Key();key.__index__=lambda:1\nvalues=(0,1,2)\ntry: values[key]=7\nexcept TypeError: print('set',events)\ntry: del values[key]\nexcept TypeError: print('delete',events)", "set []\ndelete []\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task NativeMutationUsesTypeIndexBeforeItsBounds(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, name);
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

    public static IEnumerable<object?[]> SuspensionCases()
    {
        foreach (var deque in new[] { false, true })
        foreach (var delete in new[] { false, true })
        foreach (var pause in new[] { null, "/bind.txt", "/call.txt" })
            yield return [deque, delete, pause];
    }

    [Theory]
    [MemberData(nameof(SuspensionCases))]
    public async Task IndexHooksSuspendAndCancelBeforeMutation(bool deque, bool delete, string? pause)
    {
        const string definition = """
            def mark(path):
             with open(path,'a') as output: output.write('x')
            class Key:
             @property
             def __index__(self):
              mark('bind.txt')
              def convert():
               mark('call.txt')
               return 1
              return convert
            key=Key()
            """;
        var source = definition + "\n" + (deque ? "from collections import deque\nvalues=deque([0,1,2])\n" : "values=[0,1,2]\n")
            + (delete ? "del values[key]\n" : "values[key]=7\n")
            + "with open('result.txt','w') as output: output.write(str(list(values)))";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/bind.txt", "before");
        host.SeedFile("/call.txt", "before");
        host.SeedFile("/result.txt", "original");
        var entered = pause is null ? null : host.PauseWriteUntilCancellation(pause);
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
                    Assert.Fail(early.Failure?.Message ?? "Mutation finished before the index hook suspended.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(pause is null, result.Success);
            Assert.Equal(pause is null ? delete ? "[0, 2]" : "[0, 7, 2]" : "original", host.ReadText("/result.txt"));
            Assert.Equal("before" + (pause != "/bind.txt" ? "x" : ""), host.ReadText("/bind.txt"));
            Assert.Equal("before" + (pause is null ? "x" : ""), host.ReadText("/call.txt"));
            if (entered is null) Assert.True(host.CompletedAsynchronously > 0);
            else Assert.True(entered.IsCompletedSuccessfully);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
