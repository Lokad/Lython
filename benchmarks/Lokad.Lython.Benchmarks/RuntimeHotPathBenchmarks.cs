using BenchmarkDotNet.Attributes;

namespace Lokad.Lython.Benchmarks;

[MemoryDiagnoser]
public class RuntimeHotPathBenchmarks
{
    private readonly BenchmarkHost _host = new();
    private readonly LythonCompiledScript _textReplace;
    private readonly LythonCompiledScript _listClone;
    private readonly LythonCompiledScript _dictionaryClone;
    private readonly LythonCompiledScript _namedBuiltinCall;

    public RuntimeHotPathBenchmarks()
    {
        var engine = new LythonEngine();
        _textReplace = Compile(engine, "return 'alpha beta gamma delta epsilon zeta eta theta'.replace('ta', 'XX')");
        _listClone = Compile(engine, "items = list(range(32))\nreturn list(items)");
        _dictionaryClone = Compile(engine, "items = {'a': 1, 'b': 2, 'c': 3, 'd': 4}\nreturn dict(items)");
        _namedBuiltinCall = Compile(engine, "return len(sorted([3, 1, 2], reverse=True))");
    }

    [Benchmark]
    public object? PyStringReplace() => Run(_textReplace);

    [Benchmark]
    public object? PyListClone() => Run(_listClone);

    [Benchmark]
    public object? PyDictClone() => Run(_dictionaryClone);

    [Benchmark]
    public object? CallBinderNamedBuiltin() => Run(_namedBuiltinCall);

    private static LythonCompiledScript Compile(LythonEngine engine, string source)
    {
        var script = engine.Compile(source);
        if (!script.IsValid)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, script.Diagnostics.Select(diagnostic => diagnostic.Message)));
        }

        return script;
    }

    private object? Run(LythonCompiledScript script)
    {
        var result = script.Run(_host);
        return result.Success
            ? result.ReturnValue
            : throw new InvalidOperationException(result.Failure?.Message ?? "Benchmark script failed.");
    }
}
