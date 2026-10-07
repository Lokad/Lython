using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class TruthTypeSlotCompatibilityTests
{
    public static IEnumerable<object[]> PythonControls()
    {
        yield return new object[] { "truth-type-slots-and-instance-overrides", "class Boolean:\n    def __bool__(self):\n        return False\nclass Length:\n    def __len__(self):\n        return 0\nclass Plain:\n    pass\nboolean = Boolean(); boolean.__bool__ = lambda: True\nlength = Length(); length.__len__ = lambda: 1\nplain = Plain(); plain.__bool__ = lambda: False; plain.__len__ = lambda: 0\nfor value in [boolean, length, plain]:\n    print(bool(value), not value, 'yes' if value else 'no')\nprint(boolean.__bool__(), length.__len__())\nBoolean.__bool__ = lambda self: True\nLength.__len__ = lambda self: 2\nprint(bool(boolean), bool(length))\n", "False True no\nFalse True no\nTrue False yes\nTrue 1\nTrue True\n" };
        yield return new object[] { "truth-bypasses-getattribute", "class Boolean:\n    def __getattribute__(self, name):\n        raise RuntimeError('lookup')\n    def __bool__(self):\n        print('bool')\n        return False\nclass Length:\n    def __getattribute__(self, name):\n        raise RuntimeError('lookup')\n    def __len__(self):\n        print('len')\n        return 0\nprint(bool(Boolean()), not Length())\n", "bool\nlen\nFalse True\n" };
        yield return new object[] { "truth-noncallable-slots-and-length-bounds", "class Disabled:\n    __bool__ = None\n    def __len__(self):\n        print('unexpected len')\n        return 0\nclass Invalid:\n    __bool__ = 42\nclass InvalidLength:\n    __len__ = None\nclass Length:\n    def __init__(self, value):\n        self.value = value\n    def __len__(self):\n        return self.value\nfor value in [Disabled(), Invalid(), InvalidLength(), Length(0), Length(-1), Length(1.5), Length(9223372036854775808), Length(True)]:\n    try:\n        print(bool(value))\n    except Exception as e:\n        print(type(e).__name__)\n", "TypeError\nTypeError\nTypeError\nFalse\nValueError\nTypeError\nOverflowError\nTrue\n" };
        yield return new object[] { "truth-property-static-and-class-descriptors", "class Property:\n    @property\n    def __bool__(self):\n        print('property')\n        return lambda: False\nclass Static:\n    @staticmethod\n    def __bool__():\n        return False\nclass Class:\n    marker = False\n    @classmethod\n    def __bool__(cls):\n        return cls.marker\nclass Length:\n    @property\n    def __len__(self):\n        print('length property')\n        return lambda: 0\nfor value in [Property(), Static(), Class(), Length()]:\n    print(bool(value), not value)\n", "property\nproperty\nFalse True\nFalse True\nFalse True\nlength property\nlength property\nFalse True\n" };
    }

    [Theory]
    [MemberData(nameof(PythonControls))]
    public async Task MatchesPython(string name, string source, string expected)
    {
        _ = name;
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
