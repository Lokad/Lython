using System.Text;
using BenchmarkDotNet.Attributes;

namespace Lokad.Lython.Benchmarks;

/// <summary>Separates ordinary line scans, retained lines, long records and XML graphs.</summary>
[MemoryDiagnoser]
public class TextScanBenchmarks
{
    [Params(1000, 10000, 100000)]
    public int RowCount { get; set; }

    private readonly BenchmarkHost _host = new();
    private readonly LythonCompiledScript _discardLines;
    private readonly LythonCompiledScript _retainLines;
    private readonly LythonCompiledScript _longRecord;
    private readonly LythonCompiledScript _xmlGraph;

    public TextScanBenchmarks()
    {
        var engine = new LythonEngine();
        _discardLines = BenchmarkScripts.Compile(engine, "return sum(len(line) for line in open('/lines'))");
        _retainLines = BenchmarkScripts.Compile(engine, "lines=list(open('/lines'))\nreturn len(lines)");
        _longRecord = BenchmarkScripts.Compile(engine, "return sum(len(line) for line in open('/xml'))");
        _xmlGraph = BenchmarkScripts.Compile(engine,
            "import xml.etree.ElementTree as ET\nroot=ET.fromstring(open('/xml').read())\nreturn len(root)");
    }

    [GlobalSetup]
    public void Seed()
    {
        _host.SeedUtf8("/lines", string.Concat(Enumerable.Repeat("1,2,3,4,5,6\n", RowCount)));
        var xml = new StringBuilder("<root>");
        for (var i = 0; i < RowCount; i++) xml.Append("<row a='1'>payload</row>");
        xml.Append("</root>");
        _host.SeedUtf8("/xml", xml.ToString());
    }

    [Benchmark(Description = "Text lines discard after summing lengths")]
    public Task<object?> DiscardLinesAsync() => BenchmarkScripts.RunAsync(_discardLines, _host);

    [Benchmark(Description = "Text lines retain all")]
    public Task<object?> RetainLinesAsync() => BenchmarkScripts.RunAsync(_retainLines, _host);

    [Benchmark(Description = "XML single record through text iteration")]
    public Task<object?> LongRecordAsync() => BenchmarkScripts.RunAsync(_longRecord, _host);

    [Benchmark(Description = "ElementTree retain all children")]
    public Task<object?> RetainXmlGraphAsync() => BenchmarkScripts.RunAsync(_xmlGraph, _host);
}
