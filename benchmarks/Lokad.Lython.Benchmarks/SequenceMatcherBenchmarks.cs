using BenchmarkDotNet.Attributes;

namespace Lokad.Lython.Benchmarks;

/// <summary>
/// SequenceMatcher scaling benchmarks: similarity ratios over small and
/// large near-identical inputs, plus opcode generation over the large pair.
/// </summary>
[MemoryDiagnoser]
public class SequenceMatcherBenchmarks
{
    private readonly LythonCompiledScript _ratioSmall;
    private readonly LythonCompiledScript _ratioLarge;
    private readonly LythonCompiledScript _opcodesLarge;

    public SequenceMatcherBenchmarks()
    {
        var engine = new LythonEngine(Utf8RegexProvider.Instance);
        _ratioSmall = Compile(engine, RatioSource(500));
        _ratioLarge = Compile(engine, RatioSource(4000));
        _opcodesLarge = Compile(engine, OpcodesSource(4000));
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _ratioSmall?.Dispose();
        _ratioLarge?.Dispose();
        _opcodesLarge?.Dispose();
    }

    [Benchmark(Description = "SequenceMatcher ratio over 1K inputs")]
    public void RatioSmall() => Run(_ratioSmall);

    [Benchmark(Description = "SequenceMatcher ratio over 8K inputs")]
    public void RatioLarge() => Run(_ratioLarge);

    [Benchmark(Description = "SequenceMatcher opcodes over 8K inputs")]
    public void OpcodesLarge() => Run(_opcodesLarge);

    private static string RatioSource(int half)
    {
        return "import difflib\n"
            + "left = \"a\" * " + half + " + \"X\" + \"b\" * " + half + "\n"
            + "right = \"a\" * " + half + " + \"Y\" + \"b\" * " + half + "\n"
            + "return difflib.SequenceMatcher(None, left, right).ratio()\n";
    }

    private static string OpcodesSource(int half)
    {
        return "import difflib\n"
            + "left = \"a\" * " + half + " + \"X\" + \"b\" * " + half + "\n"
            + "right = \"a\" * " + half + " + \"Y\" + \"b\" * " + half + "\n"
            + "return difflib.SequenceMatcher(None, left, right).get_opcodes()\n";
    }

    private static LythonCompiledScript Compile(LythonEngine engine, string source)
        => BenchmarkScripts.Compile(engine, source);

    private static void Run(LythonCompiledScript script)
        => BenchmarkScripts.Run(script, new BenchmarkHost());
}
