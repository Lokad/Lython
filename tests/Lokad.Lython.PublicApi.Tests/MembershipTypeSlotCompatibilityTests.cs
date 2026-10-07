using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class MembershipTypeSlotCompatibilityTests
{
    public static IEnumerable<object[]> PythonControls()
    {
        yield return new object[] { "membership-type-slot-and-class-mutation", "class Base:\n    def __contains__(self, needle):\n        print('base', needle)\n        return needle == 'yes'\nclass Child(Base):\n    pass\nvalue = Child()\nvalue.__contains__ = lambda needle: False\nprint('yes' in value, 'no' not in value, value.__contains__('yes'))\nBase.__contains__ = lambda self, needle: needle == 'new'\nprint('yes' in value, 'new' in value)\n", "base yes\nbase no\nTrue True False\nFalse True\n" };
        yield return new object[] { "membership-bypasses-instance-getattribute", "class Text:\n    def __getattribute__(self, name):\n        print('lookup', name)\n        raise RuntimeError('lookup')\n    def __contains__(self, needle):\n        print('contains', needle)\n        return True\nprint('x' in Text(), 'x' not in Text())\n", "contains x\ncontains x\nTrue False\n" };
        yield return new object[] { "membership-slot-descriptors", "class Property:\n    @property\n    def __contains__(self):\n        print('property')\n        return lambda needle: needle == 42\nclass Static:\n    @staticmethod\n    def __contains__(needle):\n        return needle == 42\nclass Class:\n    marker = 42\n    @classmethod\n    def __contains__(cls, needle):\n        return needle == cls.marker\nfor value in [Property(), Static(), Class()]:\n    print(42 in value, 0 not in value)\n", "property\nproperty\nTrue True\nTrue True\nTrue True\n" };
        yield return new object[] { "membership-disabled-and-noncallable-slots", "class Disabled:\n    __contains__ = None\nclass Invalid:\n    __contains__ = 42\nclass InvalidProperty:\n    @property\n    def __contains__(self):\n        return None\nclass Missing:\n    pass\nmissing = Missing()\nmissing.__contains__ = lambda needle: True\nfor value in [Disabled(), Invalid(), InvalidProperty(), missing]:\n    try:\n        print('x' in value)\n    except Exception as e:\n        print(type(e).__name__)\n", "TypeError\nTypeError\nTypeError\nTypeError\n" };
        yield return new object[] { "membership-truth-and-native-controls", "class Answer:\n    def __bool__(self):\n        print('truth')\n        return False\nclass Container:\n    def __contains__(self, needle):\n        print('contains', needle)\n        return Answer()\nprint(1 in Container(), 1 not in Container())\nprint('\u00e9' in '\u00e9\ud83d\ude00', 2 in [1, 2], 3 not in (1, 2), 'x' in {'x': 1}, 1 in {1})\n", "contains 1\ntruth\ncontains 1\ntruth\nFalse True\nTrue True True True True\n" };
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
