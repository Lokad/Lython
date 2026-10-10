using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RecursiveExceptionPropagationTests
{
    [Fact]
    public void DeepMethodFailurePreservesEveryGuestFrameInIsolatedProcess()
    {
        const string source = """
            class Chain:
                def descend(self, n):
                    if n == 0:
                        raise ValueError('leaf')
                    return self.descend(n - 1)
            Chain().descend(300)
            """;

        using var response = RunProbe(source, asynchronous: false);
        var result = response.RootElement.GetProperty("Lython");
        Assert.False(result.GetProperty("Success").GetBoolean());
        var failure = result.GetProperty("Failure");
        Assert.Equal("ValueError", failure.GetProperty("ExceptionType").GetString());
        Assert.Equal("leaf", failure.GetProperty("Message").GetString());
        var frames = failure.GetProperty("Frames").EnumerateArray().ToArray();
        Assert.Equal(301, frames.Length);
        Assert.All(frames, frame => Assert.StartsWith("descend ", frame.GetString()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CaughtMethodFailurePreservesIdentityFinallyOrderAndSubsequentCalls(bool asynchronous)
    {
        const string source = """
            events = []
            original = ValueError('leaf')
            class Chain:
                def descend(self, n):
                    try:
                        if n == 0:
                            raise original
                        return self.descend(n - 1)
                    finally:
                        events.append(n)
            chain = Chain()
            try:
                chain.descend(100)
            except ValueError as error:
                print(error is original, len(events), events[0], events[-1], sum(events))
            try:
                chain.descend(0)
            except ValueError as error:
                print(error is original, len(events), events[-1])
            """;

        using var response = RunProbe(source, asynchronous);
        var result = response.RootElement.GetProperty("Lython");
        Assert.True(result.GetProperty("Success").GetBoolean(), result.ToString());
        Assert.Equal("True 101 0 100 5050\nTrue 102 0\n", result.GetProperty("StandardOutput").GetString());
    }

    private static JsonDocument RunProbe(string source, bool asynchronous)
    {
        var arguments = new List<string> { SubprocessProbeRunner.FindProbeDll(), "--batch-json" };
        if (asynchronous) arguments.Add("--async");
        var run = SubprocessProbeRunner.Run("dotnet", arguments, JsonSerializer.Serialize(new[] { source }), TimeSpan.FromSeconds(30));
        Assert.True(run.ExitCode is 0 or 1, "probe exited with " + run.ExitCode + ": " + run.StandardError);
        var lines = run.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
        return JsonDocument.Parse(lines[0]);
    }
}
