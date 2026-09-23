using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N16: negative-shift failures are recognized by their typed marker, never by
// matching English message text. Public identities and messages are unchanged;
// same-text guest failures propagate with their own identity.
public sealed class ShiftFailureWordingTests
{
    [Theory]
    [InlineData("return 1 << -1\n")]
    [InlineData("return 1 >> -1\n")]
    [InlineData("import operator\nreturn operator.lshift(1, -1)\n")]
    [InlineData("import operator\nreturn operator.rshift(1, -1)\n")]
    [InlineData("x = 1\nx <<= -1\nreturn x\n")]
    [InlineData("x = 1\nx >>= -1\nreturn x\n")]
    public void NegativeShifts_KeepValueErrorIdentityAndMessage(string source)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());
        Assert.False(result.Success);
        Assert.Equal("ValueError", result.Failure?.ExceptionType);
        Assert.Contains("negative shift count", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ValueError")]
    [InlineData("TypeError")]
    public async Task SameTextGuestFailure_PropagatesWithGuestIdentity(string errorType)
    {
        // The guest wording is identical to the internal marker message; only
        // the typed marker converts to ValueError, never message text.
        var source = "class S:\n    def __lshift__(self, other):\n        raise " + errorType + "(\"negative shift count\")\ntry:\n    S() << 1\n    return \"no-failure\"\nexcept " + errorType + " as e:\n    return str(e)\n";
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("negative shift count", sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(sync.ReturnValue, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task NegativeShiftAsyncMatchesSync()
    {
        const string source = "return 1 << -1\n";
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.False(sync.Success);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.False(asyncResult.Success);
        Assert.Equal(sync.Failure?.ExceptionType, asyncResult.Failure?.ExceptionType);
        Assert.Equal(sync.Failure?.Message, asyncResult.Failure?.Message);
    }
}
