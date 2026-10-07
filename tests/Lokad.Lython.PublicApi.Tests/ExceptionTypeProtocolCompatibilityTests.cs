using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ExceptionTypeProtocolCompatibilityTests
{
    public static IEnumerable<object[]> PythonControls()
    {
        yield return new object[] { "real_object = object\nclass object:\n    pass\nprint(issubclass(ValueError, real_object), issubclass(ValueError, object))\nprint(isinstance(ValueError('x'), real_object), isinstance(ValueError('x'), object))\n", "True False\nTrue False\n" };
        yield return new object[] { "real_type=type\nclass type: pass\nprint(isinstance(ValueError,real_type),isinstance(ValueError,type))\nprint(ValueError.__class__ is real_type,ValueError('x').__class__ is ValueError)\n", "True False\nTrue True\n" };
        yield return new object[] { "real_object=object\nreal_type=type\nreal_int=int\nreal_tuple=tuple\nclass object: pass\nclass type: pass\nclass int: pass\nclass tuple: pass\nprint(isinstance(1,real_object),isinstance(1,object),issubclass(real_int,real_object),issubclass(real_int,object))\nprint(real_type(1) is real_int,(1).__class__ is real_int,real_type(()) is real_tuple)\n", "True False True False\nTrue True True\n" };
        yield return new object[] { "from urllib.parse import urlsplit,SplitResult\nreal_object=object\nreal_type=type\nclass object: pass\nclass type: pass\nvalue=urlsplit('https://example.com/a')\nprint(isinstance(value,real_object),isinstance(value,object),issubclass(SplitResult,real_object),issubclass(SplitResult,object))\nprint(isinstance(SplitResult,real_type),isinstance(SplitResult,type),SplitResult.__class__ is real_type)\n", "True False True False\nTrue False True\n" };
        yield return new object[] { "from functools import singledispatch\nreal_object=object\nclass object: pass\n@singledispatch\ndef render(value): return 'default'\n@render.register(object)\ndef render_guest(value): return 'guest'\nprint(render(ValueError('x')),render.dispatch(ValueError)(ValueError('x')))\nprint(render(object()),render.dispatch(object)(object()))\n", "default default\nguest guest\n" };
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
