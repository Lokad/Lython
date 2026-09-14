namespace Lokad.Lython.Benchmarks;

// Shared compile/run/failure helpers for the benchmark classes. Hosts stay
// per benchmark (seeds and capabilities differ intentionally); only the
// invalid-script guard and the failure surfacing are consolidated here so
// every benchmark fails the same loud way on a broken script.
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

    public static async Task<object?> RunAsync(LythonCompiledScript script, ILythonHost host, LythonRunOptions options)
    {
        var result = await script.RunAsync(host, options).ConfigureAwait(false);
        return result.Success
            ? result.ReturnValue
            : throw new InvalidOperationException(result.Failure?.Message ?? "Benchmark script failed.");
    }
}