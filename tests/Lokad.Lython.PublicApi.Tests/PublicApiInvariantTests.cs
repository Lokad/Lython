using System.Numerics;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class PublicApiInvariantTests
{
    [Fact]
    public void PathMetadataUsesOnePreciseKindAndTypedTimestamp()
    {
        var timestamp = DateTimeOffset.UnixEpoch;
        var file = new LythonPathStat(LythonPathKind.File, new BigInteger(12), timestamp);

        Assert.Equal(LythonPathKind.File, file.Kind);
        Assert.True(file.Exists);
        Assert.True(file.IsFile);
        Assert.False(file.IsDir);
        Assert.Equal(timestamp, file.ModifiedAtTimestamp);
        Assert.Throws<ArgumentException>(() => new LythonPathStat(LythonPathKind.Missing, BigInteger.One, null));
    }

    [Fact]
    public void ExecutionResultsRejectContradictoryOutcomeState()
    {
        Assert.Throws<ArgumentException>(() => new LythonExecutionResult(
            LythonExecutionOutcome.Succeeded,
            returnValue: null,
            standardOutput: string.Empty,
            standardError: string.Empty,
            exitCode: 1,
            diagnostics: [],
            failure: null));

        Assert.Throws<ArgumentException>(() => new LythonExecutionResult(
            LythonExecutionOutcome.RuntimeFailed,
            returnValue: "unexpected",
            standardOutput: string.Empty,
            standardError: string.Empty,
            exitCode: 1,
            diagnostics: [],
            failure: null));
    }

    [Fact]
    public async Task SubprocessContractsUseValidatedDomainValues()
    {
        var request = new LythonSubprocessRequest(
            Args: ["tool", "--flag"],
            Cwd: "/work",
            Environment: new Dictionary<string, string> { ["MODE"] = "test" },
            StandardInputUtf8: ReadOnlyMemory<byte>.Empty,
            StandardInput: LythonSubprocessStreamMode.Inherit,
            StandardOutput: LythonSubprocessStreamMode.Pipe,
            StandardError: LythonSubprocessStreamMode.Pipe,
            InvocationMode: LythonSubprocessInvocationMode.Direct,
            ContentMode: LythonSubprocessContentMode.Text,
            TextEncoding: LythonSubprocessTextEncoding.Utf8,
            TextErrorMode: LythonSubprocessTextErrorMode.Strict,
            Timeout: TimeSpan.FromSeconds(2),
            OutputLimit: new LythonSubprocessOutputLimit(16));

        var result = await LythonSubprocessCompletion.CompleteBufferedAsync(
            request,
            returnCode: 3,
            standardOutputUtf8: "out"u8.ToArray(),
            standardErrorUtf8: "err"u8.ToArray(),
            (_, _) => throw new InvalidOperationException("Captured output must not be inherited."),
            (_, _) => throw new InvalidOperationException("Captured errors must not be inherited."),
            CancellationToken.None);

        Assert.Equal(TimeSpan.FromSeconds(2), request.Timeout);
        Assert.Equal(16, request.OutputLimit?.Bytes);
        Assert.Equal(3, result.ReturnCode);
        Assert.Equal("out"u8.ToArray(), result.StandardOutputUtf8.ToArray());
        Assert.Equal("err"u8.ToArray(), result.StandardErrorUtf8.ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => new LythonSubprocessOutputLimit(-1));
    }
}
