using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class LenTypeSlotTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "class V:\n def __getattribute__(self,name): raise RuntimeError('ordinary lookup')\n def __len__(self): return 4\nprint(len(V()))", "4\n" },
        { "class V:\n def __len__(self): return 5\nv=V()\nv.__len__=lambda:9\nprint(len(v))", "5\n" },
        { "class V:\n @property\n def __len__(self):\n  def size(): return 6\n  return size\nprint(len(V()))", "6\n" },
        { "class Base:\n def __len__(self):return 4\nclass Child(Base):pass\nv=Child()\nv.__len__=lambda:99\nprint(len(v))\nBase.__len__=lambda self:7\nprint(len(v))", "4\n7\n" },
        { "class V:\n def __getattr__(self,name):return lambda:8\nclass Empty:pass\nv=Empty();v.__len__=lambda:9\nfor item in [V(),v]:\n try:print(len(item))\n except TypeError:print('missing')", "missing\nmissing\n" },
        { "class A:__len__=None\nclass B:__len__=42\nfor item in [A(),B()]:\n try:len(item)\n except TypeError as error:print(str(error))", "'NoneType' object is not callable\n'int' object is not callable\n" },
        { "class A:\n @staticmethod\n def __len__():return 4\nclass B:\n marker=5\n @classmethod\n def __len__(cls):return cls.marker\nprint(len(A()),len(B()))", "4 5\n" },
        { "class V:\n def __init__(self,value):self.value=value\n def __len__(self):return self.value\nfor value in [0,True,-1,1.5,None]:\n try:print(len(V(value)))\n except Exception as error:print(type(error).__name__)", "0\n1\nValueError\nTypeError\nTypeError\n" },
        { "class V:\n def __len__(self):return 5\nv=V();v.__len__=lambda:9\nheld=len\nlen=lambda value:1\nprint(held(v),len(v),v.__len__(),held.__name__)", "5 1 9 len\n" },
        { "class V:\n @property\n def __len__(self):raise AttributeError('descriptor')\n def __getattr__(self,name):return lambda:8\ntry:print(len(V()))\nexcept Exception as error:print(type(error).__name__,str(error))", "AttributeError descriptor\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ImplicitLengthMatchesPythonWhileExplicitLookupRemainsOrdinary(string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
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
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
