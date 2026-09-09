using BenchmarkDotNet.Attributes;

namespace Lokad.Lython.Benchmarks;

/// <summary>
/// Integer magnitude-sizing benchmarks: million-bit shift and power operands
/// exercise the allocation-free magnitude bit-length path behind the shift and
/// power memory guards, contrasted with a thousand-bit shift. Baselines are
/// recorded on release runs; see <c>benchmarks/Baselines.md</c>.
/// </summary>
[MemoryDiagnoser]
public class IntegerSizingBenchmarks
{
    private readonly LythonCompiledScript _shiftLarge;
    private readonly LythonCompiledScript _powerLarge;
    private readonly LythonCompiledScript _shiftSmall;

    public IntegerSizingBenchmarks()
    {
        var engine = new LythonEngine();
        _shiftLarge = Compile(engine, "return (1 << 1000000) >> 999999\n");
        _powerLarge = Compile(engine, "return pow(2, 1000000) >> 999999\n");
        _shiftSmall = Compile(engine, "return (1 << 1000) >> 999\n");
    }

    [Benchmark(Description = "Shift a million-bit integer")]
    public object? ShiftLarge() => Run(_shiftLarge);

    [Benchmark(Description = "Power to a million-bit integer")]
    public object? PowerLarge() => Run(_powerLarge);

    [Benchmark(Description = "Shift a thousand-bit integer")]
    public object? ShiftSmall() => Run(_shiftSmall);

    private static LythonCompiledScript Compile(LythonEngine engine, string source)
    {
        var script = engine.Compile(source);
        if (!script.IsValid)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, script.Diagnostics.Select(diagnostic => diagnostic.Message)));
        }

        return script;
    }

    private static object? Run(LythonCompiledScript script)
    {
        var result = script.Run(new BenchmarkHost());
        return result.Success
            ? result.ReturnValue
            : throw new InvalidOperationException(result.Failure?.Message ?? "Benchmark script failed.");
    }
}
