using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class AnyAllCursorCompatibilityTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return [false, "retained-iterator", "def values(a,b):\n yield a\n yield b\n yield 2\ng=values(0,1)\nprint(any(g),list(g))\ng=values(1,0)\nprint(all(g),list(g))", "True [2]\nFalse [2]\n"];
        yield return [true, "retained-iterator", "def values(a,b):\n yield a\n yield b\n yield 2\ng=values(0,1)\nprint(any(g),list(g))\ng=values(1,0)\nprint(all(g),list(g))", "True [2]\nFalse [2]\n"];
        yield return [false, "list-mutation", "class Value:\n def __bool__(self):\n  values.clear()\n  return False\nvalues=[Value(),0,1]\nprint(any(values),values)\nclass More:\n def __bool__(self):\n  values.append(0)\n  return True\nvalues=[More()]\nprint(all(values),len(values))", "False []\nFalse 2\n"];
        yield return [true, "list-mutation", "class Value:\n def __bool__(self):\n  values.clear()\n  return False\nvalues=[Value(),0,1]\nprint(any(values),values)\nclass More:\n def __bool__(self):\n  values.append(0)\n  return True\nvalues=[More()]\nprint(all(values),len(values))", "False []\nFalse 2\n"];
        yield return [false, "dict-mutation", "class Value:\n def __bool__(self):\n  values['b']=1\n  return True\nvalues={'a':Value()}\ntry:print(all(values.values()))\nexcept RuntimeError:print('changed')", "changed\n"];
        yield return [true, "dict-mutation", "class Value:\n def __bool__(self):\n  values['b']=1\n  return True\nvalues={'a':Value()}\ntry:print(all(values.values()))\nexcept RuntimeError:print('changed')", "changed\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ShortCircuitAndMutationMatchPython(bool asynchronous, string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
