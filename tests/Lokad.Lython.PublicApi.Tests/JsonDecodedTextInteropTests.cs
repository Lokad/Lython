using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonDecodedTextInteropTests
{
    [Theory, InlineData(false), InlineData(true)]
    public async Task DecodedTextKeepsPythonOperationsAndOffsets(bool asynchronous)
    {
        const string source = """
            import json, re
            p = json.loads(document)
            v = p['text']
            print(repr(v), len(v), len(v.encode('utf-8')))
            print(repr(v[1]), repr(v[-1]), repr(v[::-1]))
            print(repr(v.strip().lower()), repr(v.replace(p['piece'], 'X')), repr(v * 2))
            print(hash(v) == hash('é😀Ab c  '), {v: 7}['é😀Ab c  '])
            m = re.search(p['pattern'], v)
            print(m.span(), m.groupdict())
            print(repr(re.sub(p['pattern'], r'<\g<g>>', v)))
            print(json.dumps(p, ensure_ascii=False))
            """;
        const string document = """{"text":"é😀Ab c  ","piece":"😀","pattern":"(?P<g>😀A)"}""";
        const string expected = """
            'é😀Ab c  ' 8 12
            '😀' ' ' '  c bA😀é'
            'é😀ab c' 'éXAb c  ' 'é😀Ab c  é😀Ab c  '
            True 7
            (1, 3) {'g': '😀A'}
            'é<😀A>b c  '
            {"text": "é😀Ab c  ", "piece": "😀", "pattern": "(?P<g>😀A)"}

            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var held = budget.ReservedBytes;
            var options = new LythonRunOptions
            {
                Globals = new Dictionary<string, object?> { ["document"] = document },
                MemoryBudget = budget, ResultMemoryBudget = budget,
            };
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options)
                : script.Run(new MockLythonHost(), options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected.ReplaceLineEndings("\n"), result.StandardOutput);
            }
            Assert.Equal(held, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
