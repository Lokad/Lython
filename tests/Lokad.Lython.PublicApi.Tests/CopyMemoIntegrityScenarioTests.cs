using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// R44: memo lookups keyed by lossy 32-bit identity hashes aliased distinct
/// live objects (6 wrong copies in 20000). Internal memos now consult only the
/// exact per-call map; user-memo hits verify against the original, so
/// resumption still shares while collisions re-copy safely.
/// </summary>
public sealed class CopyMemoIntegrityScenarioTests
{
    private const string CheckCopies =
        """
        import copy
        src = [[i] for i in range(20000)]
        dst = copy.deepcopy(src)
        bad = 0
        i = 0
        while i < 20000:
            if dst[i][0] != i:
                bad = bad + 1
            i = i + 1
        return [len(dst), bad]
        """;

    private const string CheckExternalCopies =
        """
        import copy
        src = [[i] for i in range(20000)]
        memo = {}
        a = copy.deepcopy(src, memo)
        b = copy.deepcopy(src, memo)
        bad = 0
        i = 0
        while i < 20000:
            if b[i][0] != i:
                bad = bad + 1
            i = i + 1
        return [len(b), bad, a is b]
        """;

    [Fact]
    public async Task InternalDeepCopyPreservesDistinctCopies()
    {
        var script = new LythonEngine().Compile(CheckCopies);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(20000), new BigInteger(0) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ExternalDeepCopyPreservesDistinctCopies()
    {
        var script = new LythonEngine().Compile(CheckExternalCopies);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(20000), new BigInteger(0), true };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
