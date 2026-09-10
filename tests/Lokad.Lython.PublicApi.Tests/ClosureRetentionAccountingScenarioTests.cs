using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: a function value retains its defining context for the run. Nested
/// definitions own the context scaffolding plus one slot per captured entry,
/// while module-level definitions share the run-rooted frame and stay free.
/// </summary>
public sealed class ClosureRetentionAccountingScenarioTests
{
    // 5000 closures own 128B (value) + 512B (context) + 32B (cell) + 16B
    // (list slot) each, so they fit 1.5MB pre-fix and trip post-fix.
    private const long ClosureBudgetBytes = 1572864;

    // 3000 closures over 21 captured entries own an extra 640B each.
    private const long WideClosureBudgetBytes = 1572864;

    [Fact]
    public async Task ManyRetainedClosuresStayCharged()
    {
        var script = new LythonEngine().Compile("""
            def make(n):
                x = n
                def inner():
                    return x
                return inner
            funcs = []
            i = 0
            while i < 5000:
                funcs.append(make(i))
                i = i + 1
            return len(funcs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ClosureBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= ClosureBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= ClosureBudgetBytes);
    }

    [Fact]
    public async Task ManyWideClosuresStayCharged()
    {
        var body = new System.Text.StringBuilder("def make(n):\n");
        var total = "n";
        for (var k = 0; k < 20; k++)
        {
            body.Append("    v").Append(k).Append(" = n\n");
            total += " + v" + k;
        }

        body.Append("    def inner():\n        return ").Append(total).Append("\n    return inner\nfuncs = []\ni = 0\nwhile i < 3000:\n    funcs.append(make(i))\n    i = i + 1\nreturn len(funcs)\n");
        var script = new LythonEngine().Compile(body.ToString());
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = WideClosureBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task ClosureIsolationAndNonlocalBehave()
    {
        var script = new LythonEngine().Compile("""
            def make(n):
                x = n
                def get():
                    return x
                def bump(d):
                    nonlocal x
                    x = x + d
                    return x
                return [get, bump]
            pair = make(10)
            get = pair[0]
            bump = pair[1]
            first = get()
            after = bump(5)
            return [first, after, get()]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(10), new BigInteger(15), new BigInteger(15) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}