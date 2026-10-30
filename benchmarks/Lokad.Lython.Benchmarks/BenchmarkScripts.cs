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
            var message = string.Join(Environment.NewLine, script.Diagnostics.Select(diagnostic => diagnostic.Message));
            script.Dispose();
            throw new InvalidOperationException(message);
        }

        return script;
    }

    // Borrow projected payloads only inside inspect. Every invocation includes
    // deterministic result cleanup; no result aliases escape into BenchmarkDotNet.
    public static void Run(LythonCompiledScript script, ILythonHost host, LythonRunOptions? options = null, Action<object?>? inspect = null)
    {
        using var result = options is null ? script.Run(host) : script.Run(host, options);
        if (!result.Success) throw new InvalidOperationException(result.Failure?.Message ?? "Benchmark script failed.");
        inspect?.Invoke(result.ReturnValue);
    }

    public static async Task RunAsync(LythonCompiledScript script, ILythonHost host, LythonRunOptions? options = null, Action<object?>? inspect = null)
    {
        using var result = await (options is null ? script.RunAsync(host) : script.RunAsync(host, options)).ConfigureAwait(false);
        if (!result.Success) throw new InvalidOperationException(result.Failure?.Message ?? "Benchmark script failed.");
        inspect?.Invoke(result.ReturnValue);
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

}
