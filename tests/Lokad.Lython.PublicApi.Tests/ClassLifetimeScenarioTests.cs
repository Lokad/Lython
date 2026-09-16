using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// M05: class definitions own lifetime like function definitions do: the type
// record, member slots and governed construction shares ride one pool coupon.
public sealed class ClassLifetimeScenarioTests
{
    private const long ThreeMib = 3145728;
    private const long OneMib = 1048576;

    private static LythonRunOptions Budgeted() => new() { MaxExecutionMemoryBytes = ThreeMib };

    private static async Task AssertCompletes(string source, string expected)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(new MockLythonHost(), Budgeted());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue?.ToString());
        var asyncResult = await script.RunAsync(new MockLythonHost(), Budgeted());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue?.ToString());
    }

    [Fact]
    public async Task NestedClassDiscardCompletes()
        => await AssertCompletes(
            "def outer():\n    class C:\n        pass\n    return 0\nfor i in range(50000):\n    y = outer()\nreturn 0\n", "0");

    [Fact]
    public async Task ModuleLoopClassDiscardCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    class C:\n        pass\nreturn 0\n", "0");

    [Fact]
    public async Task AnnotatedClassDiscardCompletes()
        => await AssertCompletes(
            "for i in range(20000):\n    class C:\n        x: int = 1\n        y: str = 'a'\nreturn 0\n", "0");

    [Fact]
    public async Task MakeDataclassDiscardCompletes()
        => await AssertCompletes(
            "from dataclasses import make_dataclass\nfor i in range(20000):\n    y = make_dataclass('C', [])\nreturn 0\n", "0");

    [Fact]
    public async Task ClassBehaves()
        => await AssertCompletes(
            "def outer():\n    class C:\n        x = 1\n        def m(self):\n            return self.x + 1\n    return C\nreturn str(outer()().m())\n", "2");

    [Fact]
    public async Task RetainedClassDenied()
    {
        var script = new LythonEngine().Compile(
            "def outer():\n    class C:\n        pass\n    return C\nobjs = []\ni = 0\nwhile i < 20000:\n    objs.append(outer())\n    i = i + 1\nreturn len(objs)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = OneMib };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= OneMib);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= OneMib);
    }
    [Fact]
    public async Task MakeDataclassFieldsDiscardCompletes()
        => await AssertCompletes(
            "from dataclasses import make_dataclass\nfor i in range(20000):\n    y = make_dataclass('C', [('x', int)])\nreturn 0\n", "0");

    [Fact]
    public async Task DataclassDecoratorDiscardCompletes()
        => await AssertCompletes(
            "import dataclasses\nfor i in range(20000):\n    @dataclasses.dataclass\n    class D:\n        x: int = 1\nreturn 0\n", "0");

    [Fact]
    public async Task DataclassFrozenDiscardCompletes()
        => await AssertCompletes(
            "import dataclasses\nfor i in range(20000):\n    @dataclasses.dataclass(frozen=True)\n    class D:\n        x: int = 1\nreturn 0\n", "0");

    [Fact]
    public async Task FieldFactoryDiscardCompletes()
        => await AssertCompletes(
            "from dataclasses import field\nfor i in range(20000):\n    y = field(default=1)\nreturn 0\n", "0");

    [Fact]
    public async Task DataclassBehaves()
        => await AssertCompletes(
            "import dataclasses\n@dataclasses.dataclass\nclass D:\n    x: int = 1\nreturn str(D(2).x) + '|' + str(D().x)\n", "2|1");
}
