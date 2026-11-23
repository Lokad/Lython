using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeWordBindingTests
{
    public static TheoryData<string, string> Cases => new()
    {
        { "def f(text):\n counts={'a':0}\n original=counts\n try:\n  for counts in text.lower().split():\n   counts[counts]=counts.get(counts,0)+1\n except AttributeError:print(original,counts)\n else:print('wrong',original,counts)\nf('a a')\n", "{'a': 0} a\n" },
        { "def f(text):\n counts={'a':0}\n word=0\n try:\n  for word in text.lower().split():\n   counts[word]=counts.get(word,0)+1\n   word+=1\n except TypeError:print(counts,word)\n else:print('wrong',counts,word)\nf('a a')\n", "{'a': 1} a\n" },
        { "def run(text):\n counts={};total=0;word='before'\n for word in text.lower().split():counts[word]=counts.get(word,0)+1;total+=1\n return counts,total,word\nfor text in ('ALPHA','ALPHA ALPHA ALPHA',''):\n counts,total,word=run(text)\n print(counts,total,word,next(iter(counts)) is word if counts else False)\n", "{'alpha': 1} 1 alpha True\n{'alpha': 3} 3 alpha False\n{} 0 before False\n" },
        { "def run():\n counts={};total=0;word='before'\n for word in 'ALPHA ALPHA LONGER-THAN-INLINE ALPHA ALPHA'.lower().split():counts[word]=counts.get(word,0)+1;total+=1\n keys=list(counts)\n return counts,total,word,word is keys[0]\nprint(run())\n", "({'alpha': 4, 'longer-than-inline': 1}, 5, 'alpha', False)\n" },
        { "def run(text):\n counts={};total=0;word='before'\n for word in text.upper().split():counts[word]=counts.get(word,0)+1;total+=1\n key=next(iter(counts))\n print(len(key),counts[key],total,word==key,word is key)\nfor n in (4,2048,2049):\n token='ß'*n\n run(token)\n run(token+' '+token)\n", "8 1 1 True True\n8 2 2 True False\n4096 1 1 True True\n4096 2 2 True False\n4098 1 1 True True\n4098 2 2 True False\n" },
        { "def run():\n counts={'a':0,'é':None};total=0;word='before'\n try:\n  for word in 'A A É'.lower().split():counts[word]=counts.get(word,0)+1;total+=1\n finally:print(counts,total,word)\ntry:run()\nexcept TypeError:print('type')\n", "{'a': 2, 'é': None} 2 é\ntype\n" },
        { "def run():\n counts={'first':0};total=0;word='before'\n for word in 'FIRST NOUVEAU-CAFÉ'.lower().split():counts[word]=counts.get(word,36028797018963968)+1;total+=1\n print(counts,total,word is list(counts)[1])\nrun()\n", "{'first': 1, 'nouveau-café': 36028797018963969} 2 True\n" },
        { "def run():\n counts={'a':(1<<55)-2};total=0;word='before'\n for word in 'A A A A'.lower().split():counts[word]=counts.get(word,0)+1;total+=1\n return counts['a'],total,word\nprint(run())\n", "(36028797018963970, 4, 'a')\n" },
        { "def run():\n counts={};total=0;word='before'\n def read():return word\n for word in 'A A B B'.lower().split():counts[word]=counts.get(word,0)+1;total+=1\n return counts,total,read() is word,locals()['word'] is word\nprint(run())\n", "({'a': 2, 'b': 2}, 4, True, True)\n" },
        { "class Value:\n def __add__(self,other):print(word,total);return 5\ncounts={'alpha':0,'beta':Value()};total=0\nfor word in 'ALPHA ALPHA BETA BETA'.lower().split():counts[word]=counts.get(word,0)+1;total+=1\nprint(counts,total,word)\n", "beta 2\n{'alpha': 2, 'beta': 6} 4 beta\n" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task FinalBindingsAndKeyIdentityPreservePython(string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(';', script.Diagnostics));
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
    [MemberData(nameof(CasedWhitespacePipelineTests.UnicodeCases), MemberType = typeof(CasedWhitespacePipelineTests))]
    public async Task HistogramWordsPreserveUnicodeAndRetainedResults(string operation, string text, string[] expected)
    {
        var source = $$"""
            def run():
                counts={};total=0;word='before'
                for word in text.{{operation}}().split():counts[word]=counts.get(word,0)+1;total+=1
                assert counts==reference
                assert total==expected_count
                assert word==last
                return counts,word
            return run()
            """;
        var reference = expected.GroupBy(x => x).ToDictionary(g => g.Key, g => (object?)g.Count());
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(';', script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var options = new LythonRunOptions
            {
                MemoryBudget = budget,
                Globals = new Dictionary<string, object?>
                { ["text"] = text, ["reference"] = reference, ["expected_count"] = expected.Length, ["last"] = expected.LastOrDefault() ?? "before" },
            };
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options)
                    : script.Run(new MockLythonHost(), options))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.NotNull(result.ReturnValue);
                    Assert.True(budget.ReservedBytes > baseline);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
