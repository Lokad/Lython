using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ReceiverFunctionEntryTests
{
    public static TheoryData<string, string, string> Cases => new()
    {
        { "direct-alias-and-receiver", "class Reader:\n    def identity(self): return self\n    def words(self,line): return line.lower().split()\nvalue=Reader()\nsaved=value.words\nprint(value.identity() is value,saved.__self__ is value,saved is value.words)\nprint(value.words('A Café'),saved('B TEST'))\n", "True True False\n['a', 'café'] ['b', 'test']\n" },
        { "live-defaults-and-keywords", "class Reader:\n    def wanted(self,value,/,prefix='A',*,suffix='!'):\n        return value.startswith(prefix) and value.endswith(suffix)\nvalue=Reader();saved=value.wanted\nprint(saved('ABC!'),value.wanted('ABC!',suffix='?'))\nReader.wanted.__defaults__=('B',)\nReader.wanted.__kwdefaults__['suffix']='?'\nprint(saved('BCD?'),value.wanted('AX!',prefix='A',suffix='!'))\nfor bad in [lambda:saved(value='x'),lambda:saved('x',prefix='A',self=value),lambda:saved()]:\n    try:bad()\n    except TypeError:print('TypeError')\n", "True False\nTrue True\nTypeError\nTypeError\nTypeError\n" },
        { "evaluated-target-survives-rebinding", "class Reader:\n    def old(self,value): return value.lower()\n    def new(self,value): return value.upper()\n    read=old\nvalue=Reader();saved=value.read\ndef argument():\n    Reader.read=Reader.new\n    return 'aB'\nprint(value.read(argument()),saved('Cd'),value.read('eF'))\n", "ab cd EF\n" },
        { "class-static-and-explicit-receiver", "class Reader:\n    @classmethod\n    def wanted(cls,value,*,prefix='A'): return value.startswith(prefix)\n    @staticmethod\n    def trim(value): return value.strip()\n    def identity(self,value): return value\nvalue=Reader();saved=Reader.wanted\nprint(Reader.wanted('ABC'),value.wanted('ABC',prefix='B'),saved('BCD',prefix='B'))\nprint(Reader.trim(' x '),value.trim(' y '),Reader.identity(value,'z'))\n", "True False True\nx y z\n" },
        { "bound-dunder-and-descriptor", "class Reader:\n    def words(self,line): return line.lower().split()\nclass Descriptor:\n    def __get__(self,instance,owner):return Reader().words\nclass Box:read=Descriptor()\nprint(Box().read('A B'))\nclass More:\n    def __call__(self,value,*,prefix='A'):return value.startswith(prefix)\ntarget=More();saved=target.__call__\nprint(target('AB'),saved('CD',prefix='C'))\n", "['a', 'b']\nTrue True\n" },
        { "dynamic-body-preserves-order", "events=[]\nclass Value:\n    def __add__(self,other):events.append(other);return 'custom'\nclass Reader:\n    def add(self,value):return value+'!'\ntarget=Reader();saved=target.add\nprint(saved(Value()),target.add('text'),events)\n", "custom text! ['!']\n" },
        { "closure-class-cell-and-late-names", "prefix='A'\nclass Parent:\n    def read(self,value):return value.lower()\nclass Child(Parent):\n    def read(self,value):return super().read(value)\n    def wanted(self,value):return value.startswith(prefix)\nchild=Child();saved=child.wanted\nprint(child.read('AB'),saved('AB'))\nprefix='B'\nprint(saved('AB'),saved('BC'))\n", "ab True\nFalse True\n" },
        { "wide-and-variadic-binding", "class Reader:\n    def wide(self,a,b,c,d,e,f,g,h,i,j,k,l,m,n,o,p):return p\n    def variadic(self,*args,**kwargs):return args,kwargs\nvalue=Reader()\nprint(value.wide(1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16))\nprint(value.variadic(2,3,key=4))\n", "16\n((2, 3), {'key': 4})\n" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task MethodEntryPreservesPythonBehaviorAndOwnership(string name, string source, string expected)
    {
        _ = name;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(';', script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(expected, result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
