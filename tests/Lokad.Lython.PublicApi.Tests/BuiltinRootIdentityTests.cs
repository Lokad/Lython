using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BuiltinRootIdentityTests
{
    // Independently checked with isolated CPython 3.13.2.
    public static TheoryData<string, string, string> Cases => new()
    {
        { "object-class", "class object:pass\nobject.marker=3\nprint(object.marker)\ndel object.marker\nprint(hasattr(object,'marker'))", "3\nFalse\n" },
        { "type-class", "class type:pass\ntype.marker=5\nprint(type.marker)\ndel type.marker\nprint(hasattr(type,'marker'))", "5\nFalse\n" },
        { "object-instance", "class object:pass\nvalue=object()\nvalue.marker=7\nprint(value.marker,vars(value))\ndel value.marker\nprint(hasattr(value,'marker'))", "7 {'marker': 7}\nFalse\n" },
        { "type-instance-error", "class type:pass\nvalue=type()\ntry:value.missing\nexcept AttributeError as error:print(str(error))", "'type' object has no attribute 'missing'\n" },
        { "native-controls", "for cls in [object,type,int,str,list,ValueError]:\n try:setattr(cls,'marker',1)\n except TypeError:print('immutable')\n else:print('WRITABLE')", "immutable\nimmutable\nimmutable\nimmutable\nimmutable\nimmutable\n" },
        { "original-aliases", "RootObject=object\nRootType=type\nclass object:pass\nclass type:pass\nobject.marker=3\ntype.marker=5\nprint(object.marker,type.marker)\nfor cls in [RootObject,RootType]:\n try:cls.marker=1\n except TypeError:print('immutable')", "3 5\nimmutable\nimmutable\n" },
        { "named-type-subclass", "RootType=type\nclass type(RootType):pass\ntype.marker=2\nprint(type.marker,type.__mro__[1] is RootType)\ndel type.marker", "2 True\n" },
        { "named-descriptor", "class object:\n @property\n def marker(self):return self.value\n @marker.setter\n def marker(self,value):self.value=value+1\nitem=object()\nitem.marker=6\nprint(item.marker,vars(item))", "7 {'value': 7}\n" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task NamesDoNotDetermineBuiltinIdentity(string name, string source, string expected)
    {
        _ = name;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var program = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(program.IsValid, string.Join(" | ", program.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await program.RunAsync(new MockLythonHost()) : program.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        program.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
