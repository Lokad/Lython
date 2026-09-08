using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ScopeDirectiveCompatibilityTests
{
    [Fact]
    public void GlobalDirective_ReadsWritesAndDeletesModuleScope()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
value = 1

def bump():
    global value
    value += 2
    return value

first = bump()

def clear():
    global value
    del value

clear()
try:
    value
    status = "present"
except NameError:
    status = "missing"

__lython_file = open("/out.txt", "w")
__lython_file.write(str(first) + "|" + status)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Null(result.Failure);
        Assert.Equal("3|missing", host.ReadText("/out.txt"));
    }

    [Fact]
    public void GlobalDirective_UsesRunOptionGlobalsAndBypassesOuterScopes()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
value = "module"

def outer():
    value = "outer"
    def inner():
        global seed
        global value
        seed += 5
        return value
    return inner() + "|" + str(seed) + "|" + value

__lython_file = open("/out.txt", "w")
__lython_file.write(outer())
__lython_file.close()
""",
            host,
            new LythonRunOptions
            {
                Globals = new Dictionary<string, object?>
                {
                    ["seed"] = new System.Numerics.BigInteger(10)
                }
            });

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Null(result.Failure);
        Assert.Equal("module|15|outer", host.ReadText("/out.txt"));
    }

    [Fact]
    public void NonlocalDirective_MutatesNearestEnclosingFunctionScope()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
def outer():
    x = "outer"
    def middle():
        x = "middle"
        def inner():
            nonlocal x
            x = x + ":changed"
            return x
        first = inner()
        return first + "|" + x
    return middle() + "|" + x

__lython_file = open("/out.txt", "w")
__lython_file.write(outer())
__lython_file.close()
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Null(result.Failure);
        Assert.Equal("middle:changed|middle:changed|outer", host.ReadText("/out.txt"));
    }

    [Fact]
    public void NonlocalDirective_SharesSiblingClosureStateAndSupportsDelete()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
def outer():
    x = 0

    def inc():
        nonlocal x
        x += 1
        return x

    def read():
        nonlocal x
        return x

    def clear():
        nonlocal x
        del x

    first = inc()
    second = read()
    clear()
    try:
        x
        status = "present"
    except NameError:
        status = "missing"
    return str(first) + "|" + str(second) + "|" + status

__lython_file = open("/out.txt", "w")
__lython_file.write(outer())
__lython_file.close()
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Null(result.Failure);
        Assert.Equal("1|1|missing", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ScopeDirectives_TargetCommonBindingForms()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
def bind_globals():
    global GPath, g_func, GClass, g_loop, g_unpacked, g_exc, g_exc_text, g_match, g_walrus
    from pathlib import Path as GPath

    def g_func():
        return "gfunc"

    class GClass:
        pass

    for g_loop in [1]:
        pass
    g_unpacked, temp = [2, 99]
    try:
        raise ValueError("bad")
    except ValueError as g_exc:
        g_exc_text = str(g_exc)
    match {"x": 4}:
        case {"x": g_match}:
            pass
    if (g_walrus := 5):
        pass

def bind_nonlocals():
    n_loop = 0
    n_unpacked = 0
    n_exc = None
    n_exc_text = None
    n_match = 0
    n_walrus = 0
    n_func = None
    NClass = None
    NPath = None

    def inner():
        nonlocal NPath, n_func, NClass, n_loop, n_unpacked, n_exc, n_exc_text, n_match, n_walrus
        from pathlib import Path as NPath

        def n_func():
            return "nfunc"

        class NClass:
            pass

        for n_loop in [6]:
            pass
        n_unpacked, temp = [7, 99]
        try:
            raise ValueError("worse")
        except ValueError as n_exc:
            n_exc_text = str(n_exc)
        match {"x": 8}:
            case {"x": n_match}:
                pass
        if (n_walrus := 9):
            pass

    inner()
    return (
        NPath("/repo").as_posix()
        + "|" + n_func()
        + "|" + str(NClass is None)
        + "|" + str(n_loop)
        + "|" + str(n_unpacked)
        + "|" + n_exc_text
        + "|" + str(n_match)
        + "|" + str(n_walrus)
    )

bind_globals()
nonlocal_parts = bind_nonlocals()
global_parts = (
    GPath("/root").as_posix()
    + "|" + g_func()
    + "|" + str(GClass is None)
    + "|" + str(g_loop)
    + "|" + str(g_unpacked)
    + "|" + g_exc_text
    + "|" + str(g_match)
    + "|" + str(g_walrus)
)
__lython_file = open("/out.txt", "w")
__lython_file.write(global_parts + "\n" + nonlocal_parts)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Null(result.Failure);
        Assert.Equal(
            "/root|gfunc|False|1|2|bad|4|5\n/repo|nfunc|False|6|7|worse|8|9",
            host.ReadText("/out.txt"));
    }

    [Fact]
    public async Task ScopeDirectives_RunInAsyncLoweredExecution()
    {
        var host = new MockLythonHost();

        var result = await new LythonEngine().RunAsync(
            """
value = 1

def outer():
    x = 2
    def inner():
        global value
        nonlocal x
        value += 3
        x += 4
    inner()
    return x

__lython_file = open("/out.txt", "w")
__lython_file.write(str(outer()) + "|" + str(value))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Null(result.Failure);
        Assert.Equal("6|4", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData("nonlocal value\n", "LA3201")]
    [InlineData("def f():\n    global value\n    nonlocal value\n", "LA3203")]
    [InlineData("def f(value):\n    global value\n", "LA3204")]
    [InlineData("def f():\n    nonlocal value\n", "LA3205")]
    [InlineData("value = 1\ndef f():\n    nonlocal value\n", "LA3205")]
    [InlineData("def f():\n    value = 1\n    global value\n", "LA3206")]
    [InlineData("def f():\n    import pathlib as value\n    global value\n", "LA3206")]
    [InlineData("def f():\n    def value():\n        pass\n    global value\n", "LA3206")]
    [InlineData("def f():\n    class value:\n        pass\n    global value\n", "LA3206")]
    [InlineData("def f():\n    for value in [1]:\n        pass\n    global value\n", "LA3206")]
    [InlineData("def f():\n    with open(\"x\") as value:\n        pass\n    global value\n", "LA3206")]
    [InlineData("def f():\n    try:\n        raise ValueError(\"x\")\n    except ValueError as value:\n        pass\n    global value\n", "LA3206")]
    [InlineData("def f():\n    match {\"x\": 1}:\n        case {\"x\": value}:\n            pass\n    global value\n", "LA3206")]
    [InlineData("def f():\n    if (value := 1):\n        pass\n    global value\n", "LA3206")]
    [InlineData("class C:\n    global value\n", "LA3202")]
    public void InvalidScopeDirectives_ReportStaticDiagnostics(string source, string code)
    {
        var compiled = new LythonEngine().Compile(source);

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, diagnostic => diagnostic.Code == code);
    }

    [Theory]
    [InlineData("global value\nvalue = 1\n")]
    [InlineData("def f():\n    global match\n    match = 1\n")]
    public void ValidScopeDirectives_Compile(string source)
    {
        var compiled = new LythonEngine().Compile(source);

        Assert.True(compiled.IsValid, string.Join(" | ", compiled.Diagnostics.Select(diagnostic => diagnostic.Message)));
    }

    private static string DescribeFailure(LythonExecutionResult result)
        => result.Failure is { } failure
            ? failure.Message + " | " + string.Join(" > ", failure.StackTrace.Select(frame => $"{frame.FunctionName}@{frame.Span?.Line}:{frame.Span?.Column}"))
            : string.Join(" | ", result.Diagnostics.Select(diagnostic => $"{diagnostic.Code}@{diagnostic.Span?.Line}:{diagnostic.Span?.Column} {diagnostic.Message}"));
}

