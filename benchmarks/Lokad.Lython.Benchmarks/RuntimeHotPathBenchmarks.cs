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
    private readonly LythonCompiledScript _unicodeIndexing;
    private readonly LythonCompiledScript _steppedListDeletion;
    private readonly LythonCompiledScript _counterEquality;
    private readonly LythonCompiledScript _dequeEquality;
    private readonly LythonCompiledScript _chainMapLookup;
    private readonly LythonCompiledScript _tupleKeyBuild;
    private readonly LythonCompiledScript _nestedTupleKeyLookup;
    private readonly LythonCompiledScript _openPyxlAppend;
    private readonly string _repeatedConstantSource;
    private readonly string _invalidConstantSource;

    public RuntimeHotPathBenchmarks()
    {
        var engine = new LythonEngine();
        _textReplace = Compile(engine, "return 'alpha beta gamma delta epsilon zeta eta theta'.replace('ta', 'XX')");
        _listClone = Compile(engine, "items = list(range(32))\nreturn list(items)");
        _dictionaryClone = Compile(engine, "items = {'a': 1, 'b': 2, 'c': 3, 'd': 4}\nreturn dict(items)");
        _namedBuiltinCall = Compile(engine, "return len(sorted([3, 1, 2], reverse=True))");
        _unicodeIndexing = Compile(
            engine,
            "text = 'aé漢🙂' * 4096\ntotal = 0\nfor index in range(0, len(text), 17):\n    total += ord(text[index])\nreturn total");
        _steppedListDeletion = Compile(
            engine,
            "items = list(range(20000))\ndel items[1::2]\nreturn len(items)");
        _counterEquality = Compile(
            engine,
            "from collections import Counter\nleft = Counter(range(10000))\nright = Counter(range(10000))\nreturn left == right");
        _dequeEquality = Compile(
            engine,
            "from collections import deque\nleft = deque(range(10000))\nright = deque(range(10000))\nreturn left == right");
        _chainMapLookup = Compile(
            engine,
            "from collections import ChainMap\nmaps = [{str(value): value} for value in range(256)]\ncombined = ChainMap(*maps)\nreturn sum(combined[str(value)] for value in range(256))");
        _tupleKeyBuild = Compile(engine, "d = {}\nfor i in range(2000):\n    d[(i, i + 1)] = i\nreturn len(d)");
        _nestedTupleKeyLookup = Compile(
            engine,
            "d = {((i,), i): i for i in range(2000)}\ntotal = 0\nfor i in range(2000):\n    total += d[((i,), i)]\nreturn total");
        _openPyxlAppend = Compile(
            engine,
            "from openpyxl import Workbook\nworksheet = Workbook().active\nfor value in range(2000):\n    worksheet.append([value, value + 1, value + 2])\nreturn worksheet.max_row");
        _repeatedConstantSource = string.Join(
            '\n',
            Enumerable.Range(0, 5_000).Select(static index => $"value_{index} = 'shared constant'")) +
            "\nreturn value_4999";
        _invalidConstantSource = _repeatedConstantSource + "\nvalue_5000 = (1 +\n";
    }

    [Benchmark]
    public object? PyStringReplace() => Run(_textReplace);

    [Benchmark]
    public object? PyListClone() => Run(_listClone);

    [Benchmark]
    public object? PyDictClone() => Run(_dictionaryClone);

    [Benchmark]
    public object? CallBinderNamedBuiltin() => Run(_namedBuiltinCall);

    [Benchmark]
    public object? UnicodeRandomIndexing() => Run(_unicodeIndexing);

    [Benchmark]
    public object? LargeSteppedListDeletion() => Run(_steppedListDeletion);

    [Benchmark]
    public object? LargeCounterEquality() => Run(_counterEquality);

    [Benchmark]
    public object? LargeDequeEquality() => Run(_dequeEquality);

    [Benchmark]
    public object? ChainMapLookupAcrossManyMaps() => Run(_chainMapLookup);

    [Benchmark]
    public object? TupleKeyDictBuild() => Run(_tupleKeyBuild);

    [Benchmark]
    public object? NestedTupleKeyRepeatedLookup() => Run(_nestedTupleKeyLookup);

    [Benchmark]
    public object? OpenPyxlIncrementalAppend() => Run(_openPyxlAppend);

    [Benchmark]
    public bool CompileRepeatedConstantPool() => new LythonEngine().Compile(_repeatedConstantSource).IsValid;

    [Benchmark(Description = "Reject an invalid repeated-constant pool")]
    public bool CompileInvalidConstantPool() => !new LythonEngine().Compile(_invalidConstantSource).IsValid;

    private static LythonCompiledScript Compile(LythonEngine engine, string source)
        => BenchmarkScripts.Compile(engine, source);

    private object? Run(LythonCompiledScript script)
        => BenchmarkScripts.Run(script, _host);
}
