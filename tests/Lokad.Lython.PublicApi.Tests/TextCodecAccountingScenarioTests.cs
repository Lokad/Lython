using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG07: codec paths must preserve decode values and error contracts while
/// their scratch stays bounded.
/// </summary>
public sealed class TextCodecAccountingScenarioTests
{
    [Fact]
    public async Task CodecContractsStayExact()
    {
        // MG07: bounding decode scratch must not change decoded values,
        // newline translation, or the strict/handler error contracts.
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            """
            results = []
            results.append(bytes([72, 105]).decode("utf-8"))
            results.append(bytes([255, 65]).decode("utf-8", errors="replace"))
            results.append(bytes([255]).decode("utf-8", errors="backslashreplace"))
            results.append(bytes([65, 13, 10, 66]).decode("utf-8"))
            try:
                bytes([255]).decode("utf-8")
            except UnicodeDecodeError:
                results.append("strict")
            try:
                bytes([65]).decode("utf-8", errors="bogus")
            except LookupError:
                results.append("badhandler")
            return results
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1048576) };
        var expected = new List<object?> { "Hi", "\uFFFDA", "\\xff", "A\r\nB", "strict", "badhandler" };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }
    [Fact]
    public async Task Utf8ErrorsPrecedeNewlineTranslationAndPreserveMalformedUnits()
    {
        // Frozen CPython 3.13 golden: ignore joins decoded CR/LF across invalid
        // bytes; replacement and backslash escapes separate that pair.
        using var script = new LythonEngine().Compile(
            """
            from pathlib import Path
            for errors in ['ignore', 'replace', 'backslashreplace']:
                data = Path('/input').read_bytes()
                with open('/input', errors=errors, newline='') as stream:
                    preserved = stream.read()
                print(errors, repr(data.decode('utf-8', errors)), repr(Path('/input').read_text(errors=errors)), repr(preserved))
            """);
        Assert.True(script.IsValid);
        const string expected = "ignore 'A\\r\\nBC\ud83d\ude00\\r\\nD' 'A\\nBC\ud83d\ude00\\nD' 'A\\r\\nBC\ud83d\ude00\\r\\nD'\nreplace 'A\\r\ufffd\\nB\ufffdC\ud83d\ude00\\r\\nD\ufffd' 'A\\n\ufffd\\nB\ufffdC\ud83d\ude00\\nD\ufffd' 'A\\r\ufffd\\nB\ufffdC\ud83d\ude00\\r\\nD\ufffd'\nbackslashreplace 'A\\r\\\\xff\\nB\\\\xe2\\\\x82C\ud83d\ude00\\r\\nD\\\\xf0\\\\x9f' 'A\\n\\\\xff\\nB\\\\xe2\\\\x82C\ud83d\ude00\\nD\\\\xf0\\\\x9f' 'A\\r\\\\xff\\nB\\\\xe2\\\\x82C\ud83d\ude00\\r\\nD\\\\xf0\\\\x9f'\n";
        foreach (var asynchronous in new[] { false, true })
        {
            var budget = new LythonMemoryBudget();
            var seed = Convert.FromHexString("410dff0a42e28243f09f98800d0a44f09f");
            var syncHost = new MockLythonHost();
            syncHost.SeedBytes("/input", seed);
            var delayedHost = new DelayedLythonHost();
            delayedHost.SeedBytes("/input", seed);
            ILythonHost host = asynchronous ? delayedHost : syncHost;
            var options = new LythonRunOptions { MemoryBudget = budget };
            using (var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
                Assert.True(budget.ReservedBytes > 0);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

}
