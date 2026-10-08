using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class TextCodecCheckpointTests
{
    [Theory]
    [InlineData(false, "latin1")]
    [InlineData(true, "latin1")]
    [InlineData(false, "replace")]
    [InlineData(true, "replace")]
    [InlineData(false, "newline")]
    [InlineData(true, "newline")]
    public async Task WholeBufferManagedCodecLoopsObserveExecutionCheckpoints(bool asynchronous, string operation)
    {
        object input;
        string source;
        if (operation == "newline")
        {
            input = string.Concat(Enumerable.Repeat("a\n", 131072));
            source = "f = open('/out.txt', 'w', newline='\\r\\n')\nprint('work')\nreturn f.write(data)";
        }
        else
        {
            var bytes = new byte[262144];
            Array.Fill(bytes, operation == "latin1" ? (byte)0xE9 : (byte)0xFF);
            input = bytes;
            source = operation == "latin1"
                ? "print('work')\nvalue = data.decode('latin-1')\nreturn len(value)"
                : "print('work')\nvalue = data.decode('utf-8', 'replace')\nreturn len(value)";
        }
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Message)));
        const long memoryLimit = 16L * 1024 * 1024;
        // Deterministic opt-in fuel proves the shared cancellation checkpoint
        // path is reached during the codec, without a wall-clock race.
        var options = new LythonRunOptions
        {
            Globals = new Dictionary<string, object?> { ["data"] = input },
            MaxExecutionSteps = 100,
            MaxExecutionMemoryBytes = memoryLimit,
        };
        var pending = asynchronous ? script.RunAsync(new MockLythonHost(), options)
            : Task.Run(() => script.Run(new MockLythonHost(), options));
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(result.Success);
        Assert.Equal("work\n", result.StandardOutput);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("maximum execution step count exceeded", result.Failure?.Message ?? "");
        Assert.True(result.PeakExecutionMemoryBytes <= memoryLimit);
    }
}
