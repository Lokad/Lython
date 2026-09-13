using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// MG11: invocation contexts retained through closures own their storage while
// their closures are alive. The lowered path retains whole scopes (frame plus
// variable tables); the executable path keeps locals in slots that vanish on
// return and retains only captured cells. T1 pins that engine-appropriate
// split; T1c and T2 are the flips (captured cells on the executable path,
// intermediate chain levels on the lowered path).
public sealed class ClosureContextAccountingScenarioTests
{
    private const string MakerFew =
        "fs = []\n" +
        "def maker(a0):\n" +
        "    def inner():\n" +
        "        return a0\n" +
        "    return inner\n" +
        "i = 0\n" +
        "while i < 3000:\n" +
        "    fs.append(maker(i))\n" +
        "    i = i + 1\n" +
        "return 0\n";

    private const string Temps40 =
        "    t01 = 0\n" +
        "    t02 = 0\n" +
        "    t03 = 0\n" +
        "    t04 = 0\n" +
        "    t05 = 0\n" +
        "    t06 = 0\n" +
        "    t07 = 0\n" +
        "    t08 = 0\n" +
        "    t09 = 0\n" +
        "    t10 = 0\n" +
        "    t11 = 0\n" +
        "    t12 = 0\n" +
        "    t13 = 0\n" +
        "    t14 = 0\n" +
        "    t15 = 0\n" +
        "    t16 = 0\n" +
        "    t17 = 0\n" +
        "    t18 = 0\n" +
        "    t19 = 0\n" +
        "    t20 = 0\n" +
        "    t21 = 0\n" +
        "    t22 = 0\n" +
        "    t23 = 0\n" +
        "    t24 = 0\n" +
        "    t25 = 0\n" +
        "    t26 = 0\n" +
        "    t27 = 0\n" +
        "    t28 = 0\n" +
        "    t29 = 0\n" +
        "    t30 = 0\n" +
        "    t31 = 0\n" +
        "    t32 = 0\n" +
        "    t33 = 0\n" +
        "    t34 = 0\n" +
        "    t35 = 0\n" +
        "    t36 = 0\n" +
        "    t37 = 0\n" +
        "    t38 = 0\n" +
        "    t39 = 0\n" +
        "    t40 = 0\n";

    private const string Sum40 =
        "        return a0 + t01 + t02 + t03 + t04 + t05 + t06 + t07 + t08 + t09 + t10" +
        " + t11 + t12 + t13 + t14 + t15 + t16 + t17 + t18 + t19 + t20" +
        " + t21 + t22 + t23 + t24 + t25 + t26 + t27 + t28 + t29 + t30" +
        " + t31 + t32 + t33 + t34 + t35 + t36 + t37 + t38 + t39 + t40\n";

    private const string Temps40Inner =
        "        t01 = 0\n" +
        "        t02 = 0\n" +
        "        t03 = 0\n" +
        "        t04 = 0\n" +
        "        t05 = 0\n" +
        "        t06 = 0\n" +
        "        t07 = 0\n" +
        "        t08 = 0\n" +
        "        t09 = 0\n" +
        "        t10 = 0\n" +
        "        t11 = 0\n" +
        "        t12 = 0\n" +
        "        t13 = 0\n" +
        "        t14 = 0\n" +
        "        t15 = 0\n" +
        "        t16 = 0\n" +
        "        t17 = 0\n" +
        "        t18 = 0\n" +
        "        t19 = 0\n" +
        "        t20 = 0\n" +
        "        t21 = 0\n" +
        "        t22 = 0\n" +
        "        t23 = 0\n" +
        "        t24 = 0\n" +
        "        t25 = 0\n" +
        "        t26 = 0\n" +
        "        t27 = 0\n" +
        "        t28 = 0\n" +
        "        t29 = 0\n" +
        "        t30 = 0\n" +
        "        t31 = 0\n" +
        "        t32 = 0\n" +
        "        t33 = 0\n" +
        "        t34 = 0\n" +
        "        t35 = 0\n" +
        "        t36 = 0\n" +
        "        t37 = 0\n" +
        "        t38 = 0\n" +
        "        t39 = 0\n" +
        "        t40 = 0\n";

    private const string MakerManyUncaptured =
        "fs = []\n" +
        "def maker(a0):\n" +
        Temps40 +
        "    def inner():\n" +
        "        return a0\n" +
        "    return inner\n" +
        "i = 0\n" +
        "while i < 3000:\n" +
        "    fs.append(maker(i))\n" +
        "    i = i + 1\n" +
        "return 0\n";

    private const string MakerManyCaptured =
        "fs = []\n" +
        "def maker(a0):\n" +
        Temps40 +
        "    def inner():\n" +
        Sum40 +
        "    return inner\n" +
        "i = 0\n" +
        "while i < 3000:\n" +
        "    fs.append(maker(i))\n" +
        "    i = i + 1\n" +
        "return 0\n";

    private const string DeepUncaptured =
        "fs = []\n" +
        "def l0(a):\n" +
        Temps40 +
        "    def l1(b):\n" +
        Temps40Inner +
        "        def l2(c):\n" +
        "            return a\n" +
        "        return l2\n" +
        "    return l1(a)\n" +
        "i = 0\n" +
        "while i < 3000:\n" +
        "    fs.append(l0(i))\n" +
        "    i = i + 1\n" +
        "return 0\n";

    private const string DroppedManyUncaptured =
        "def maker(a0):\n" +
        Temps40 +
        "    def inner():\n" +
        "        return a0\n" +
        "    return inner()\n" +
        "i = 0\n" +
        "total = 0\n" +
        "while i < 3000:\n" +
        "    total = total + maker(i)\n" +
        "    i = i + 1\n" +
        "return total > 0\n";

    [Fact]
    public async Task FewLocalsRetainedFit()
    {
        var script = new LythonEngine().Compile(MakerFew);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 6291456 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
    }

    [Fact]
    public async Task UncapturedTempsScopeVersusSlots()
    {
        var script = new LythonEngine().Compile(MakerManyUncaptured);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 6291456 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task CapturedTempsTripBothPaths()
    {
        var script = new LythonEngine().Compile(MakerManyCaptured);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 6291456 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task DeepChainTripsLoweredRetainsIntermediateLevels()
    {
        var script = new LythonEngine().Compile(DeepUncaptured);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 12582912 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task DroppedClosuresFitRoomyBudget()
    {
        var script = new LythonEngine().Compile(DroppedManyUncaptured);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 16777216 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
    }

    [Fact]
    public async Task RetainedClosuresStillCapture()
    {
        var script = new LythonEngine().Compile(
            "fs = []\n" +
            "def maker(a0):\n" +
            "    t1 = a0 + 1\n" +
            "    def inner():\n" +
            "        return t1\n" +
            "    return inner\n" +
            "i = 0\n" +
            "while i < 10:\n" +
            "    fs.append(maker(i))\n" +
            "    i = i + 1\n" +
            "return [fs[0](), fs[9]()]\n");
        Assert.True(script.IsValid);
        var expected = new List<object?> { new System.Numerics.BigInteger(1), new System.Numerics.BigInteger(10) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task RetainedGenexprOverBigStringsTrips()
    {
        var script = new LythonEngine().Compile(
            "gs = []\n" +
            "i = 0\n" +
            "while i < 5:\n" +
            "    big = \"x\" * 200000\n" +
            "    gs.append(x for x in big)\n" +
            "    i = i + 1\n" +
            "return 0\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 524288 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }
}
