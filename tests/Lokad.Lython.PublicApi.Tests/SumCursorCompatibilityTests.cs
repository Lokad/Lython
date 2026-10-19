using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SumCursorCompatibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExactRangePreservesStartAliasesTypesAndLargeBounds(bool asynchronous)
    {
        const string source = """
start=1<<256
print(sum(range(0),start) is start,sum(range(0),True) is True,
      sum(range(1),True),type(sum(range(1),True)).__name__)
print(sum(range(5,-3,-2)),sum(range(3),start=start)-start,
      sum(range(start,start+3),start=-start*3))
for invalid in ('bad',b'bad'):
 try:sum(range(0),invalid)
 except TypeError:print('start')
""";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("True True 1 int\n8 3 3\nstart\nstart\n", result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    public static TheoryData<bool, string, string> Programs => new()
    {
        { false, "marker=[]\nprint(sum([],marker) is marker,sum([],True) is True,sum([True,False,True]),sum([1,2],start=4))", "True True 2 7\n" },
        { true, "marker=[]\nprint(sum([],marker) is marker,sum([],True) is True,sum([True,False,True]),sum([1,2],start=4))", "True True 2 7\n" },
        { false, "events=[]\nclass Source:\n def __iter__(self):\n  events.append('iter')\n  return iter([1])\nfor start in ('x',b'x'):\n try:sum(Source(),start)\n except TypeError:print('start')\nprint(events)", "start\nstart\n['iter', 'iter']\n" },
        { true, "events=[]\nclass Source:\n def __iter__(self):\n  events.append('iter')\n  return iter([1])\nfor start in ('x',b'x'):\n try:sum(Source(),start)\n except TypeError:print('start')\nprint(events)", "start\nstart\n['iter', 'iter']\n" },
        { false, "def values():\n yield 1\n yield 'bad'\n yield 3\nit=values()\ntry:sum(it)\nexcept TypeError:print('item')\nprint(next(it),list(it))", "item\n3 []\n" },
        { true, "def values():\n yield 1\n yield 'bad'\n yield 3\nit=values()\ntry:sum(it)\nexcept TypeError:print('item')\nprint(next(it),list(it))", "item\n3 []\n" },
        { false, "values=[1,2]\nevents=[]\nclass Total:\n def __add__(self,item):\n  events.append(item)\n  if item==1:values.append(3)\n  return self\nstart=Total()\nprint(sum(values,start) is start,events,values)", "True [1, 2, 3] [1, 2, 3]\n" },
        { true, "values=[1,2]\nevents=[]\nclass Total:\n def __add__(self,item):\n  events.append(item)\n  if item==1:values.append(3)\n  return self\nstart=Total()\nprint(sum(values,start) is start,events,values)", "True [1, 2, 3] [1, 2, 3]\n" },
        { false, "events=[]\nclass Value:\n def __radd__(self,left):\n  events.append(left)\n  return 7\nprint(sum([Value(),2]),events)\nprint(sum(iter((1,2,3))),sum(range(65)),sum([0.5,1.5,2.0]))", "9 [0]\n6 2080 4.0\n" },
        { true, "events=[]\nclass Value:\n def __radd__(self,left):\n  events.append(left)\n  return 7\nprint(sum([Value(),2]),events)\nprint(sum(iter((1,2,3))),sum(range(65)),sum([0.5,1.5,2.0]))", "9 [0]\n6 2080 4.0\n" },
        { false, "mapping={'a':1,'b':2}\nclass Total:\n def __add__(self,key):\n  mapping['c']=3\n  return self\ntry:sum(mapping,Total())\nexcept RuntimeError:print('changed')\nprint(mapping)", "changed\n{'a': 1, 'b': 2, 'c': 3}\n" },
        { true, "mapping={'a':1,'b':2}\nclass Total:\n def __add__(self,key):\n  mapping['c']=3\n  return self\ntry:sum(mapping,Total())\nexcept RuntimeError:print('changed')\nprint(mapping)", "changed\n{'a': 1, 'b': 2, 'c': 3}\n" },
        { false, "class Source:\n def __iter__(self):raise ValueError('iterator')\nfor start in ('x',b'x'):\n try:sum(Source(),start)\n except Exception as error:print(type(error).__name__,str(error))", "ValueError iterator\nValueError iterator\n" },
        { true, "class Source:\n def __iter__(self):raise ValueError('iterator')\nfor start in ('x',b'x'):\n try:sum(Source(),start)\n except Exception as error:print(type(error).__name__,str(error))", "ValueError iterator\nValueError iterator\n" },
        { false, "events=[]\nclass Source:\n def __iter__(self):\n  events.append('iter')\n  return self\n def __next__(self):\n  events.append('next')\n  raise StopIteration\ntry:sum(Source(),'bad')\nexcept TypeError:print(events)", "['iter']\n" },
        { true, "events=[]\nclass Source:\n def __iter__(self):\n  events.append('iter')\n  return self\n def __next__(self):\n  events.append('next')\n  raise StopIteration\ntry:sum(Source(),'bad')\nexcept TypeError:print(events)", "['iter']\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task SumPreservesAcquisitionAliasesAndIteratorState(bool asynchronous, string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }
}
