using System.Numerics;

namespace Lokad.Lython.Tests;

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
}
