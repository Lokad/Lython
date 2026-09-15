using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// P02: ordinary executable returns no longer throw when no cleanup handles
// them in-frame. These scenarios pin the preserved return/cleanup/exception
// semantics in both sync and async modes.
public sealed class ReturnDeliveryScenarioTests
{
    private static async Task AssertBothModes(string script, Action<LythonExecutionResult, MockLythonHost> assert)
    {
        var compiled = new LythonEngine().Compile(script);
        Assert.True(compiled.IsValid);
        var syncHost = new MockLythonHost();
        assert(compiled.Run(syncHost, new LythonRunOptions()), syncHost);
        var asyncHost = new MockLythonHost();
        assert(await compiled.RunAsync(asyncHost, new LythonRunOptions()), asyncHost);
    }

    [Fact]
    public async Task HandlerFreeReturnDeliversValue()
    {
        await AssertBothModes(
            """
            def f(x):
                return x
            total = 0
            for i in range(10000):
                total = total + f(i)
            return total
            """,
            (result, _) =>
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new BigInteger(49995000), result.ReturnValue);
            });
    }

    [Fact]
    public async Task FinallyCleanupRunsBeforeReturn()
    {
        await AssertBothModes(
            """
            def f():
                try:
                    return 1
                finally:
                    log.append("cleanup")
            log = []
            r = f()
            __lython_file = open("/out.txt", "w")
            __lython_file.write("|".join(log) + "=" + str(r))
            __lython_file.close()
            return r
            """,
            (result, host) =>
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new BigInteger(1), result.ReturnValue);
                Assert.Equal("cleanup=1", host.ReadText("/out.txt"));
            });
    }

    [Fact]
    public async Task FinallyReturnOverridesTryReturn()
    {
        await AssertBothModes(
            """
            def f():
                try:
                    return 1
                finally:
                    return 2
            return f()
            """,
            (result, _) =>
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new BigInteger(2), result.ReturnValue);
            });
    }

    [Fact]
    public async Task WithExitRunsOnReturn()
    {
        await AssertBothModes(
            """
            log = []
            class Ctx:
                def __enter__(self):
                    log.append("enter")
                    return self
                def __exit__(self, exc_type, exc, tb):
                    log.append("exit")
                    return False
            def f():
                with Ctx():
                    return 41
            r = f()
            __lython_file = open("/out.txt", "w")
            __lython_file.write("|".join(log) + "=" + str(r))
            __lython_file.close()
            return r
            """,
            (result, host) =>
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new BigInteger(41), result.ReturnValue);
                Assert.Equal("enter|exit=41", host.ReadText("/out.txt"));
            });
    }

    [Fact]
    public async Task ActiveExceptionPreservedAcrossReturnFromExcept()
    {
        await AssertBothModes(
            """
            def f():
                try:
                    raise ValueError("inner")
                except ValueError:
                    return 1
            r = f()
            try:
                raise RuntimeError("outer")
            except RuntimeError as e:
                r = str(e)
            __lython_file = open("/out.txt", "w")
            __lython_file.write(str(r))
            __lython_file.close()
            return r
            """,
            (result, host) =>
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("outer", result.ReturnValue);
                Assert.Equal("outer", host.ReadText("/out.txt"));
            });
    }

    [Fact]
    public async Task ExceptionInFinallyDuringReturnPropagates()
    {
        await AssertBothModes(
            """
            def f():
                try:
                    return 1
                finally:
                    raise RuntimeError("cleanup-boom")
            f()
            """,
            (result, _) =>
            {
                Assert.False(result.Success);
                Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
                Assert.Contains("cleanup-boom", result.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
            });
    }
}