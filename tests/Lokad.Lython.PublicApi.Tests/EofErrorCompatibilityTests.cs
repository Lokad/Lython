using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class EofErrorCompatibilityTests
{
    [Theory]
    [InlineData("import builtins\nprint(EOFError is builtins.EOFError,EOFError.__module__,EOFError.__name__)\nprint(EOFError.__bases__[0] is Exception,issubclass(EOFError,Exception),issubclass(EOFError,OSError))\n", "True builtins EOFError\nTrue True False\n")]
    [InlineData("try: raise EOFError('short')\nexcept Exception as error: print(type(error) is EOFError,str(error),error.args,isinstance(error,BaseException))\n", "True short ('short',) True\n")]
    [InlineData("try: raise EOFError\nexcept (ValueError,EOFError) as error: print(type(error).__name__,error.args)\n", "EOFError ()\n")]
    [InlineData("try: raise EOFError('first')\nexcept EOFError:\n    try: raise\n    except BaseException as error: print(type(error).__name__,str(error))\n", "EOFError first\n")]
    public async Task EofErrorUsesTheBuiltinExceptionHierarchy(string source, string expected)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new DelayedLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
