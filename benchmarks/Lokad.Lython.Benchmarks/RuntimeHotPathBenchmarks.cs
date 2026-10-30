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
        var engine = new LythonEngine(Utf8RegexProvider.Instance);
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

    [GlobalCleanup]
    public void Cleanup()
    {
        _textReplace?.Dispose();
        _listClone?.Dispose();
        _dictionaryClone?.Dispose();
        _namedBuiltinCall?.Dispose();
        _unicodeIndexing?.Dispose();
        _steppedListDeletion?.Dispose();
        _counterEquality?.Dispose();
        _dequeEquality?.Dispose();
        _chainMapLookup?.Dispose();
        _tupleKeyBuild?.Dispose();
        _nestedTupleKeyLookup?.Dispose();
        _openPyxlAppend?.Dispose();
    }

    [Benchmark]
    public void PyStringReplace() => Run(_textReplace);

    [Benchmark]
    public void PyListClone() => Run(_listClone);

    [Benchmark]
    public void PyDictClone() => Run(_dictionaryClone);

    [Benchmark]
    public void CallBinderNamedBuiltin() => Run(_namedBuiltinCall);

    [Benchmark]
    public void UnicodeRandomIndexing() => Run(_unicodeIndexing);

    [Benchmark]
    public void LargeSteppedListDeletion() => Run(_steppedListDeletion);

    [Benchmark]
    public void LargeCounterEquality() => Run(_counterEquality);

    [Benchmark]
    public void LargeDequeEquality() => Run(_dequeEquality);

    [Benchmark]
    public void ChainMapLookupAcrossManyMaps() => Run(_chainMapLookup);

    [Benchmark]
    public void TupleKeyDictBuild() => Run(_tupleKeyBuild);

    [Benchmark]
    public void NestedTupleKeyRepeatedLookup() => Run(_nestedTupleKeyLookup);

    [Benchmark]
    public void OpenPyxlIncrementalAppend() => Run(_openPyxlAppend);

    [Benchmark]
    public bool CompileRepeatedConstantPool()
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(_repeatedConstantSource);
        return script.IsValid;
    }

    [Benchmark(Description = "Reject an invalid repeated-constant pool")]
    public bool CompileInvalidConstantPool()
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(_invalidConstantSource);
        return !script.IsValid;
    }

    private static LythonCompiledScript Compile(LythonEngine engine, string source)
        => BenchmarkScripts.Compile(engine, source);

    private void Run(LythonCompiledScript script)
        => BenchmarkScripts.Run(script, _host);
}
