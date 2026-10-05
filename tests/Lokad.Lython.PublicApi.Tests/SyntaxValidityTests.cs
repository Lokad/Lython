using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SyntaxValidityTests
{
    [Theory]
    [InlineData("def f(x, x):\n    return x")]
    [InlineData("f = lambda x, x: x")]
    [InlineData("[x := x for x in range(3)]")]
    [InlineData("[x for x in (y := range(3))]")]
    [InlineData("[[x := y for y in range(2)] for x in range(2)]")]
    [InlineData("match [1, 2]:\n    case [x, x]:\n        pass")]
    [InlineData("match [1]:\n    case [x] | [y]:\n        pass")]
    [InlineData("break")]
    [InlineData("if False:\n    continue")]
    [InlineData("for x in range(1):\n    def f():\n        break")]
    [InlineData("for x in range(1):\n    pass\nelse:\n    break")]
    [InlineData("def f():\n    print(end='', end='')")]
    public async Task InvalidProgramsFailBeforeAnyGuestEffects(string source)
    {
        var compiled = new LythonEngine().Compile("print('side effect')\n" + source);
        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA1100" && d.Span is not null);
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.IsType<LythonExecutionResult.CompilationFailedState>(result.State);
            Assert.Equal("", result.StandardOutput);
        }
    }

    [Fact]
    public async Task ValidContextsAndRuntimeKeywordCollisionsRemainSupported()
    {
        var compiled = new LythonEngine().Compile("""
            for x in range(2):
                for y in range(2):
                    break
                else:
                    continue
            functions = [(lambda: (x := 1)) for x in range(2)]
            match [3]:
                case [x] | (x,):
                    print(x)
            def f(x):
                return x
            try:
                f(x=1, **{'x': 2})
            except TypeError:
                print('collision')
            return 42
            """);
        Assert.True(compiled.IsValid);
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("3\ncollision\n", result.StandardOutput);
            Assert.Equal("42", result.ReturnValue?.ToString());
        }
    }
}
