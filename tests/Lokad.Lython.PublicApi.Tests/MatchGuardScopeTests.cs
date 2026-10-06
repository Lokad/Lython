using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class MatchGuardScopeTests
{
    [Fact]
    public async Task GuardAssignmentsAndCapturesRemainVisible()
    {
        await AssertOutput("""
            match 1:
                case x if y := x + 1:
                    print(x, y)
            print(x, y)
            """, "1 2\n1 2\n");
    }

    [Fact]
    public async Task FalseGuardsKeepAssignmentsAndCaptures()
    {
        await AssertOutput("""
            y = "initial"
            match 1:
                case x if (y := 0):
                    print("incorrect")
                case _:
                    print(x, y)
            print(x, y)
            """, "1 0\n1 0\n");
    }

    [Fact]
    public async Task GuardCanRebindItsOwnCapture()
    {
        await AssertOutput("""
            match 5:
                case x if (x := x + 1):
                    print(x)
            print(x)
            """, "6\n6\n");
    }

    [Fact]
    public async Task GuardShortCircuitLeavesSkippedAssignmentsUntouched()
    {
        await AssertOutput("""
            y = 0
            match 1:
                case x if False and (y := 99):
                    print("incorrect")
                case _:
                    print(y)
            print(x, y)
            """, "0\n1 0\n");
    }

    [Fact]
    public async Task GuardsUpdateEnclosingFunctionCells()
    {
        await AssertOutput("""
            def f():
                y = 0
                def get():
                    return y
                match 2:
                    case x if y := x + 1:
                        print(get(), x, y)
                return get
            print(f()())
            """, "3 2 3\n3\n");
    }

    [Fact]
    public async Task GuardsHonorGlobalAndNonlocalDeclarations()
    {
        await AssertOutput("""
            y = 0
            def f():
                global y
                match 2:
                    case x if y := x + 1:
                        print(x, y)
            f()
            print(y)
            def outer():
                z = 0
                def inner():
                    nonlocal z
                    match 4:
                        case x if z := x + 1:
                            print(x, z)
                inner()
                return z
            print(outer())
            """, "2 3\n3\n4 5\n5\n");
    }

    [Fact]
    public async Task GuardFailureKeepsEarlierBindings()
    {
        await AssertOutput("""
            try:
                match 1:
                    case x if ((y := 2) and 1 / 0):
                        print("incorrect")
            except ZeroDivisionError:
                print(x, y)
            """, "1 2\n");
    }

    [Fact]
    public async Task GuardsInClassBodiesBindClassAttributes()
    {
        await AssertOutput("""
            class C:
                match 1:
                    case x if y := x + 1:
                        print(x, y)
            print(C.x, C.y)
            """, "1 2\n1 2\n");
    }

    [Fact]
    public async Task GuardCallbacksObserveAlreadyBoundCaptures()
    {
        await AssertOutput("""
            def read():
                return x
            match 1:
                case x if y := read():
                    print(x, y)
            """, "1 1\n");
    }

    [Fact]
    public async Task GuardTruthUsesThePythonBooleanProtocol()
    {
        await AssertOutput("""
            class Truth:
                def __bool__(self):
                    print("bool")
                    return True
            match 3:
                case x if (y := Truth()):
                    print(x, type(y).__name__)
            """, "bool\n3 Truth\n");
    }

    [Theory]
    [InlineData("module")]
    [InlineData("function")]
    [InlineData("class")]
    [InlineData("truth")]
    public async Task GuardsAwaitDelayedHosts(string scope)
    {
        var source = scope switch
        {
            "module" => """
                match 1:
                    case x if (text := Path("/guard.txt").read_text()):
                        print(x, text)
                """,
            "function" => """
                def f():
                    match 1:
                        case x if (text := Path("/guard.txt").read_text()):
                            print(x, text)
                f()
                """,
            "class" => """
                class C:
                    match 1:
                        case x if (text := Path("/guard.txt").read_text()):
                            print(x, text)
                """,
            _ => """
                class Flag:
                    def __bool__(self):
                        return Path("/guard.txt").read_text() == "go"
                match 1:
                    case x if (flag := Flag()):
                        print(x, "go")
                """,
        };
        var compiled = new LythonEngine().Compile("from pathlib import Path\n" + source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var immediate = new MockLythonHost();
        immediate.SeedFile("/guard.txt", "go");
        var sync = compiled.Run(immediate);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("1 go\n", sync.StandardOutput);
        var delayed = new DelayedLythonHost();
        delayed.SeedFile("/guard.txt", "go");
        var result = await compiled.RunAsync(delayed);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.True(delayed.CompletedAsynchronously > 0);
    }

    private static async Task AssertOutput(string source, string expected)
    {
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
