using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonTrailingDataTests
{
    public static TheoryData<string, int, int, int, int, int> Documents => new()
    {
        { "true false", 5, 1, 6, 0, 4 },
        { "nullx", 4, 1, 5, 0, 4 },
        { "01", 1, 1, 2, 0, 1 },
        { "[1] [2]", 4, 1, 5, 0, 3 },
        { "{\"a\":1} {}", 8, 1, 9, 0, 7 },
        { "\"café😀\" λ", 8, 1, 9, 0, 7 },
        { "\n{\"é\":\"😀\"}\n false", 12, 3, 2, 1, 10 },
        { "-Infinity!", 9, 1, 10, 0, 9 },
        { "NaNx", 3, 1, 4, 0, 3 },
        { "  null \n\t true", 10, 2, 3, 2, 6 },
    };

    [Theory]
    [MemberData(nameof(Documents))]
    public async Task ParserErrorsHavePythonMessagesArgumentsAndCoordinates(
        string document, int position, int line, int column, int rawStart, int rawEnd)
    {
        const string source = """
            import json
            try:
                if entry == 'loads':
                    json.loads(source)
                elif entry == 'decode':
                    json.JSONDecoder().decode(source)
                else:
                    with open('/input.json') as stream:
                        json.load(stream)
            except json.JSONDecodeError as error:
                print(error.msg)
                print(str(error))
                print(error.args)
                print(error.pos, error.lineno, error.colno, error.doc is source if entry != 'load' else error.doc == source)
            print(json.JSONDecoder().raw_decode(source, start)[1])
            """;
        var message = $"Extra data: line {line} column {column} (char {position})";
        var expected = $"Extra data\n{message}\n('{message}',)\n{position} {line} {column} True\n{rawEnd}\n";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        foreach (var entry in new[] { "loads", "decode", "load" })
        foreach (var asynchronous in new[] { false, true })
        {
            var host = new MockLythonHost();
            host.SeedFile("/input.json", document);
            var options = new LythonRunOptions
            {
                Globals = new Dictionary<string, object?> { ["source"] = document, ["entry"] = entry, ["start"] = rawStart }
            };
            using (var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
    public static TheoryData<string, int, string, int, int, int> OtherErrors => new()
    {
        { "{", -1, "Expecting property name enclosed in double quotes", 1, 1, 2 },
        { "[true,", -1, "Expecting value", 6, 1, 7 },
        { "\"a\u0001b\"", -1, "Invalid control character at", 2, 1, 3 },
        { "caf\u00e9\ud83d\ude00", 20, "Expecting value", 20, 1, 21 },
    };

    [Theory]
    [MemberData(nameof(OtherErrors))]
    public async Task SharedParserErrorFormattingKeepsPlainMessagesAndExplicitPositions(
        string document, int index, string message, int position, int line, int column)
    {
        const string source = """
            import json
            try:
                if index < 0: json.loads(source)
                else: json.JSONDecoder().raw_decode(source,index)
            except json.JSONDecodeError as error:
                print(error.msg)
                print(str(error))
                print(error.args)
                print(error.pos,error.lineno,error.colno,error.doc==source)
            """;
        var summary = $"{message}: line {line} column {column} (char {position})";
        var expected = $"{message}\n{summary}\n('{summary}',)\n{position} {line} {column} True\n";
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?>
            { ["source"] = document, ["index"] = index } };
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }

}
