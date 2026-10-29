using BenchmarkDotNet.Attributes;

namespace Lokad.Lython.Benchmarks;

/// <summary>Measures streaming, retained captures and compiled-pattern reuse separately.</summary>
[MemoryDiagnoser]
public class RegexScanBenchmarks
{
    // Keep input scales finite; compare streaming with retained capture results.
    [Params(1000, 5000, 10000)]
    public int MatchCount { get; set; }

    private readonly BenchmarkHost _host = new();
    private readonly Dictionary<string, LythonCompiledScript> _scripts = new();

    [GlobalSetup]
    public void Compile()
    {
        var engine = new LythonEngine(Utf8RegexProvider.Instance);
        _scripts["iterator"] = BenchmarkScripts.Compile(engine,
            $"import re\ntext='ab '*{MatchCount}\nreturn sum(1 for match in re.finditer('(a)(b)',text))");
        _scripts["all"] = BenchmarkScripts.Compile(engine,
            $"import re\ntext='ab '*{MatchCount}\nrows=re.findall('(a)(b)',text)\nreturn len(rows)");
        _scripts["reuse"] = BenchmarkScripts.Compile(engine,
            $"import re\np=re.compile('(a)(b)')\ntotal=0\nfor i in range({MatchCount}):\n    total+=len(p.search('ab').group(1))\nreturn total");
        _scripts["cache"] = BenchmarkScripts.Compile(engine,
            $"import re\ntotal=0\nfor i in range({MatchCount}):\n    total+=len(re.compile('(a)(b)').search('ab').group(1))\nreturn total");
        _scripts["fresh"] = BenchmarkScripts.Compile(engine,
            $"import re\ntotal=0\nfor i in range({MatchCount}):\n    re.purge()\n    total+=len(re.compile('(a)(b)').search('ab').group(1))\nreturn total");
    }

    [Benchmark(Description = "Regex finditer discard matches")]
    public Task<object?> DiscardMatchesAsync() => BenchmarkScripts.RunAsync(_scripts["iterator"], _host);

    [Benchmark(Description = "Regex findall retain tuples")]
    public Task<object?> RetainMatchesAsync() => BenchmarkScripts.RunAsync(_scripts["all"], _host);

    [Benchmark(Description = "Regex compiled pattern reuse")]
    public Task<object?> ReusePatternAsync() => BenchmarkScripts.RunAsync(_scripts["reuse"], _host);

    [Benchmark(Description = "Regex compile calls with cache hits")]
    public Task<object?> CachedCompileAsync() => BenchmarkScripts.RunAsync(_scripts["cache"], _host);

    [Benchmark(Description = "Regex purge and compile every iteration")]
    public Task<object?> FreshCompileAsync() => BenchmarkScripts.RunAsync(_scripts["fresh"], _host);
}
