using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// R44: memo lookups keyed by lossy 32-bit identity hashes aliased distinct
/// live objects (6 wrong copies in 20000). Internal memos consult only the
/// exact per-call map; all views are keyed by the shared stable identity
/// scheme, so resumption still shares, live objects never collide, and
/// pre-seeded user entries resolve like CPython.
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

    [Fact]
    public async Task UserMemoIdEntryIsHonored()
    {
        // MG03: a pre-seeded memo entry keyed by id() behaves like CPython:
        // the recorded replacement is returned as-is.
        var script = new LythonEngine().Compile(
            "import copy\n"
            + "a = [1]\n"
            + "replacement = [\"replacement\"]\n"
            + "memo = {id(a): replacement}\n"
            + "return [copy.deepcopy(a, memo) is replacement]\n");
        Assert.True(script.IsValid);
        var expected = new List<object?> { true };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
