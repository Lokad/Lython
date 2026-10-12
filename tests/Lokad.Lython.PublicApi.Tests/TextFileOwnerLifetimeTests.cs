using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class TextFileOwnerLifetimeTests
{
    [Theory]
    [InlineData("r", false)]
    [InlineData("w", false)]
    [InlineData("a", false)]
    [InlineData("r", true)]
    [InlineData("w", true)]
    [InlineData("a", true)]
    public void DroppedClosedHandlesFinishWhileRetainedHandlesReject(string mode, bool retained)
    {
        var script = new LythonEngine().Compile("kept = []\nfor i in range(5000):\n with open('/file', '" + mode + "') as f:\n  pass\n" +
            (retained ? " kept.append(f)\n" : "") + " f = None\nreturn i\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
        var host = new MockLythonHost(); host.SeedFile("/file", "");
        var result = script.Run(host, new LythonRunOptions { MaxExecutionMemoryBytes = 262144 });
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, 262144);
        if (retained)
        {
            Assert.False(result.Success);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        }
        else
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(new BigInteger(4999), result.ReturnValue);
        }
    }

    [Fact]
    public void CapturedReadMethodsKeepClosedHandlesLive()
    {
        var script = new LythonEngine().Compile("kept = []\nfor i in range(5000):\n with open('/file') as f:\n  pass\n kept.append(f.read)\n f = None\nreturn i\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
        var host = new MockLythonHost(); host.SeedFile("/file", "");
        var result = script.Run(host, new LythonRunOptions { MaxExecutionMemoryBytes = 262144 });
        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, 262144);
    }

    [Fact]
    public void EscapedReadValueSurvivesClosedHandleChurn()
    {
        var script = new LythonEngine().Compile("with open('/line') as f:\n first = f.readline()\nf = None\nfor i in range(5000):\n with open('/file', 'w') as f:\n  pass\n f = None\nreturn i == 4999 and first == 'café 😀' * 2048 + '\\n'\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
        var host = new MockLythonHost(); host.SeedFile("/line", string.Concat(Enumerable.Repeat("café 😀", 2048)) + "\n");
        var result = script.Run(host, new LythonRunOptions { MaxExecutionMemoryBytes = 262144 });
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(true, result.ReturnValue);
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, 262144);
    }
}
