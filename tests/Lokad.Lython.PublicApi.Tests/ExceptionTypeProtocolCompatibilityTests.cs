using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ExceptionTypeProtocolCompatibilityTests
{
    public static IEnumerable<object[]> PythonControls()
    {
        yield return new object[] { "print(isinstance(ValueError,type),isinstance(ValueError,object),isinstance(ValueError,Exception),issubclass(ValueError,object))\n", "True True False True\n" };
        yield return new object[] { "for cls in [ValueError,KeyError,UnicodeDecodeError,OSError]:\n    print(issubclass(cls,Exception),issubclass(cls,ValueError),issubclass(cls,object))\nprint(issubclass(IOError,OSError),issubclass(ValueError,ValueError),issubclass(ValueError,int))\n", "True True True\nTrue False True\nTrue True True\nTrue False True\nTrue True False\n" };
        yield return new object[] { "for value in [ValueError('bad'),KeyError('key'),FileNotFoundError('file'),IndexError('index')]:\n    print(isinstance(value,Exception),isinstance(value,(LookupError,OSError)),isinstance(value,ValueError),isinstance(value,int))\nprint(isinstance(1,ValueError))\n", "True False True False\nTrue True False False\nTrue True False False\nTrue True False False\nFalse\n" };
        yield return new object[] { "import json\ntry: json.loads('!')\nexcept ValueError as error:\n    print(isinstance(error,json.JSONDecodeError),isinstance(error,Exception),issubclass(json.JSONDecodeError,ValueError))\n    print(issubclass(json.JSONDecodeError,KeyError),isinstance(error,KeyError))\n", "True True True\nFalse False\n" };
    }

    [Theory]
    [MemberData(nameof(PythonControls))]
    public async Task ExceptionTypeChecksFollowPython(string source, string expected)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
