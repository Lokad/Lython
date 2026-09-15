namespace Lokad.Lython.Benchmarks;

// Shared compile/run/failure helpers for the benchmark classes. Hosts stay
// per benchmark (seeds and capabilities differ intentionally); only the
// invalid-script guard, the failure surfacing, and the expected-result check
// are consolidated here so every benchmark fails the same loud way on a
// broken script.
internal static class BenchmarkScripts
{
    public static LythonCompiledScript Compile(LythonEngine engine, string source)
    {
        var script = engine.Compile(source);
        if (!script.IsValid)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, script.Diagnostics.Select(diagnostic => diagnostic.Message)));
        }

        return script;
    }

    public static object? Run(LythonCompiledScript script, ILythonHost host)
    {
        var result = script.Run(host);
        return result.Success
            ? result.ReturnValue
            : throw new InvalidOperationException(result.Failure?.Message ?? "Benchmark script failed.");
    }

    public static object? Run(LythonCompiledScript script, ILythonHost host, LythonRunOptions options)
    {
        var result = script.Run(host, options);
        return result.Success
            ? result.ReturnValue
            : throw new InvalidOperationException(result.Failure?.Message ?? "Benchmark script failed.");
    }

    public static async Task<object?> RunAsync(LythonCompiledScript script, ILythonHost host)
    {
        var result = await script.RunAsync(host).ConfigureAwait(false);
        return result.Success
            ? result.ReturnValue
            : throw new InvalidOperationException(result.Failure?.Message ?? "Benchmark script failed.");
    }

    // Validates a benchmark script result once at construction (outside timed
    // sections): success alone is not enough, the projected value must match
    // exactly, including its CLR representation. Throws loudly on mismatch so
    // a broken script can never silently benchmark the wrong work.
    public static void CheckResult(object? actual, object? expected, string name)
    {
        if (!Equals(actual, expected))
        {
            throw new InvalidOperationException($"Benchmark {name} produced {FormatValue(actual)} instead of {FormatValue(expected)}.");
        }
    }

    private static string FormatValue(object? value)
        => value is null ? "null" : value.ToString() + " (" + value.GetType().Name + ")";

    public static async Task<object?> RunAsync(LythonCompiledScript script, ILythonHost host, LythonRunOptions options)
    {
        var result = await script.RunAsync(host, options).ConfigureAwait(false);
        return result.Success
            ? result.ReturnValue
            : throw new InvalidOperationException(result.Failure?.Message ?? "Benchmark script failed.");
    }
}