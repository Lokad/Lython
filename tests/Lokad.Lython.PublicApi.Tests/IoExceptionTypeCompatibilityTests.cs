using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class IoExceptionTypeCompatibilityTests
{
    [Fact]
    public async Task UnsupportedOperationPreservesPythonTypeProtocols()
    {
        var script = new LythonEngine().Compile("from io import UnsupportedOperation as U\nprint(U.__name__,U.__module__,isinstance(U,type),issubclass(U,OSError),issubclass(U,ValueError))\nerror=U('unavailable')\nprint(type(error) is U,isinstance(error,U),isinstance(error,(KeyError,ValueError)),str(error),error.args)\ntry: raise error\nexcept ValueError as caught: print(caught is error)\n");
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("UnsupportedOperation io True True True\nTrue True True unavailable ('unavailable',)\nTrue\n", result.StandardOutput);
        }
    }
}
