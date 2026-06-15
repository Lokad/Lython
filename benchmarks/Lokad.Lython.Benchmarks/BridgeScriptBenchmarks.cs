using BenchmarkDotNet.Attributes;

namespace Lokad.Lython.Benchmarks;

[MemoryDiagnoser]
public class BridgeScriptBenchmarks
{
    private readonly BenchmarkHost _jsonHost = new();
    private readonly BenchmarkHost _csvHost = new();
    private readonly BenchmarkHost _regexHost = new();
    private readonly LythonCompiledScript _jsonScript;
    private readonly LythonCompiledScript _csvScript;
    private readonly LythonCompiledScript _regexScript;

    public BridgeScriptBenchmarks()
    {
        var engine = new LythonEngine();

        _jsonHost.SeedUtf8("/input.json", "{\"ok\":true,\"items\":[1,2,3],\"name\":\"alpha\"}");
        _csvHost.SeedUtf8("/input.csv", "name,value\nalpha,1\nbeta,2");
        _regexHost.SeedUtf8("/input.txt", "alpha beta gamma alpha");

        _jsonScript = engine.Compile("""
import json
data = json.loads(open("/input.json").read())
data["items"] = [data["items"][0], data["items"][2]]
__lython_file = open("/output.json", "w")
__lython_file.write(json.dumps(data))
__lython_file.close()
""");

        _csvScript = engine.Compile("""
import csv
rows = csv.reader(open("/input.csv").read().splitlines())
writer = csv.writer()
for row in rows:
    writer.writerow(row)
__lython_file = open("/output.csv", "w")
__lython_file.write(writer.getvalue())
__lython_file.close()
""");

        _regexScript = engine.Compile("""
import re
text = open("/input.txt").read()
parts = re.split(" +", text)
__lython_file = open("/output.txt", "w")
__lython_file.write(re.sub("alpha", "omega", parts[0] + " " + parts[1] + " " + parts[2] + " " + parts[3]))
__lython_file.close()
""");
    }

    [Benchmark]
    public int JsonRoundTripScript()
        => RunAndReadLength(_jsonScript, _jsonHost, "/output.json");

    [Benchmark]
    public int CsvReaderWriterScript()
        => RunAndReadLength(_csvScript, _csvHost, "/output.csv");

    [Benchmark]
    public int RegexRewriteScript()
        => RunAndReadLength(_regexScript, _regexHost, "/output.txt");

    private static int RunAndReadLength(LythonCompiledScript script, BenchmarkHost host, string outputPath)
    {
        var result = script.Run(host);
        if (!result.Success)
        {
            throw new InvalidOperationException(result.Failure?.Message ?? "Benchmark script failed.");
        }

        return host.ReadTextUtf8(outputPath).Length;
    }
}
