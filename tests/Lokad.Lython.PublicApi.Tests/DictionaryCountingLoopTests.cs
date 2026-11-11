using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DictionaryCountingLoopTests
{
    public static TheoryData<string, string, string> Cases => new()
    {
        { "histogram-and-total", "counts={};total=0\nfor word in ('a','b','a'): counts[word]=counts.get(word,0)+1;total+=1\nprint(counts,total,word)\n", "{'a': 2, 'b': 1} 3 a\n" },
        { "histogram-only-and-else", "counts={}\nfor word in ['a','a','b']: counts[word]=counts.get(word,0)+1\nelse: print('done')\nprint(counts,word)\n", "done\n{'a': 2, 'b': 1} b\n" },
        { "empty-keeps-binding", "counts={};total=0;word='before'\nfor word in (): counts[word]=counts.get(word,0)+1;total+=1\nprint(counts,total,word)\n", "{} 0 before\n" },
        { "casing-and-native-fragments", "counts={};total=0\nfor word in 'A Café A Σ'.lower().split(): counts[word]=counts.get(word,0)+1;total+=1\nprint(counts,total,word)\n", "{'a': 2, 'café': 1, 'σ': 1} 4 σ\n" },
        { "dynamic-value-once", "events=[]\nclass Value:\n def __add__(self,other): events.append('add'); return 5\ncounts={'a':Value()};total=0\nfor word in ('a','a'): counts[word]=counts.get(word,0)+1;total+=1\nprint(counts,total,events)\n", "{'a': 6} 2 ['add']\n" },
        { "dynamic-total-after-store", "events=[]\nclass Value:\n def __iadd__(self,other): events.append(counts['a']); return 1\ncounts={'a':1};total=Value()\nfor word in ('a','a'): counts[word]=counts.get(word,0)+1;total+=1\nprint(counts,total,events)\n", "{'a': 3} 2 [2]\n" },
        { "error-keeps-original-order", "counts={'a':None};total=0;word='before'\ntry:\n for word in ('a','b'): counts[word]=counts.get(word,0)+1;total+=1\nexcept TypeError: print(counts,total,word)\n", "{'a': None} 0 a\n" },
        { "promotion-and-old-aliases", "counts={'a':(1<<55)-1};total=(1<<55)-1;old=(counts['a'],total)\nfor word in ('a','a'): counts[word]=counts.get(word,0)+1;total+=1\nprint(counts['a'].bit_length(),total.bit_length(),old[0].bit_length(),old[1].bit_length())\n", "56 56 55 55\n" },
        { "custom-mapping-effects", "events=[]\nclass Mapping:\n def __init__(self): self.data={}\n def get(self,key,default): events.append('get');return self.data.get(key,default)\n def __setitem__(self,key,value): events.append('set');self.data[key]=value\ncounts=Mapping();total=0\nfor word in ('a','a'): counts[word]=counts.get(word,0)+1;total+=1\nprint(counts.data,total,events)\n", "{'a': 2} 2 ['get', 'set', 'get', 'set']\n" },
        { "protocol-keys-keep-equality-effects", "events=[]\nclass Key:\n def __hash__(self): return hash('a')\n def __eq__(self,other): events.append('eq');return other=='a'\ncounts={Key():1};total=0\nfor word in ('a','a'): counts[word]=counts.get(word,0)+1;total+=1\nprint(list(counts.values()),total,events)\n", "[3] 2 ['eq', 'eq', 'eq', 'eq']\n" },
        { "list-mutation-during-continuation", "words=['a','a'];events=[]\nclass Value:\n def __add__(self,other): events.append('add');words.append('a');return 1\ncounts={'a':Value()};total=0\nfor word in words: counts[word]=counts.get(word,0)+1;total+=1\nprint(counts,total,len(words),events)\n", "{'a': 3} 3 3 ['add']\n" },
        { "namespace-keeps-last-alias", "words=('alpha-value','beta-value');counts={};total=0\nfor word in words: counts[word]=counts.get(word,0)+1;total+=1\nprint(locals()['word'] is words[-1],total)\n", "True 2\n" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task CountingPreservesPythonEffectsAndBudgetReuse(string name, string source, string expected)
    {
        _ = name;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(';', script.Diagnostics.Select(d => d.Message)));
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProtocolContinuationSuspendsOnceAndJoinsCancellation(bool cancel)
    {
        const string source = """
            class Value:
                def __add__(self,other):
                    with open('/gate','a') as output: output.write('add')
                    return 2
            counts={'a':Value()};total=0
            for word in ('a','a'): counts[word]=counts.get(word,0)+1;total+=1
            print(counts,total)
            return counts
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
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
                Assert.Same(entered, await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(10)));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using (var result = await pending.WaitAsync(TimeSpan.FromSeconds(10)))
            {
                if (cancel) Assert.False(result.Success);
                else
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.True(budget.ReservedBytes > baseline);
                }
                Assert.Equal(cancel ? "" : "{'a': 3} 2\n", result.StandardOutput);
                Assert.Equal(cancel ? "before:" : "before:add", host.ReadText("/gate"));
                Assert.True(host.CompletedAsynchronously > 0);
            }
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
