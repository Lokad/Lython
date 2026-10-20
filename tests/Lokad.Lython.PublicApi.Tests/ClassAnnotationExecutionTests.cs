using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ClassAnnotationExecutionTests
{
    public static TheoryData<string, string, string> Cases => new()
    {
        { "identity", "class Box:item:int=7\nprint(Box.__annotations__['item'] is int,Box.item)", "True 7\n" },
        { "eager-order", "events=[]\ndef value():events.append('value');return 7\ndef annotation():events.append('annotation');return int\nclass Box:item:annotation()=value()\nprint(events,Box.__annotations__['item'] is int)", "['value', 'annotation'] True\n" },
        { "class-local", "class Box:\n Kind=int\n item:Kind\n Kind=str\nprint(Box.__annotations__['item'] is int)", "True\n" },
        { "conditional-setup", "outside:int\nclass Box:\n print(list(__annotations__))\n if False:item:int\nprint(list(Box.__annotations__))", "[]\n[]\n" },
        { "mapping-protocol", "events=[]\nclass Registry:\n def __setitem__(self,key,value):events.append((key,value is int))\nregistry=Registry()\ndef annotation():events.append('annotation');return int\nclass Box:\n __annotations__=registry\n item:annotation()=7\nprint(events,Box.__annotations__ is registry)", "['annotation', ('item', True)] True\n" },
        { "attribute-annotation", "events=[]\nclass Holder:pass\nholder=Holder()\ndef receiver():events.append('base');return holder\ndef annotation():events.append('annotation');return int\nclass Box:receiver().item:annotation()\nprint(events,Box.__annotations__)", "['base', 'annotation'] {}\n" },
        { "missing-name", "try:\n class Box:item:Missing\nexcept NameError:print('missing')\nelse:print('unexpected')", "missing\n" },
        { "deleted-mapping", "outside:int\nclass Box:\n del __annotations__\n item:int\nprint(list(__annotations__))", "['outside', 'item']\n" },
        { "global-mapping", "outside:int\nclass Box:\n global __annotations__\n item:int\nprint(list(__annotations__),list(Box.__annotations__))", "['outside'] ['item']\n" },
        { "deleted-mapping-closure", "outside:int\ndef outer():\n __annotations__={'outer':int}\n class Box:\n  del __annotations__\n  item:int\n return __annotations__\nprint(list(outer()),list(__annotations__))", "['outer'] ['outside', 'item']\n" },
        { "postponed", "from __future__ import annotations\nclass Box:item:Missing\nprint(Box.__annotations__['item'])", "Missing\n" },
        { "quoted-forward", "class Node:child:'Node'\nprint(Node.__annotations__['child']=='Node')", "True\n" },
        { "function-local", "events=[]\ndef annotation():events.append('annotation');return int\ndef work():\n local:annotation()\n return 7\nprint(work(),events)", "7 []\n" },
        { "dataclass-kinds", "from dataclasses import dataclass,fields,InitVar,KW_ONLY\nfrom typing import ClassVar\n@dataclass\nclass Box:\n a:int\n tag:ClassVar[str]='x'\n _:KW_ONLY\n b:int=2\n seed:InitVar[int]=0\n def __post_init__(self,seed):self.a+=seed\nprint(Box(1,b=3,seed=4),[f.name for f in fields(Box)],fields(Box)[0].type is int,Box.__annotations__['a'] is int)", "Box(a=5, b=3) ['a', 'b'] True True\n" },
        { "dataclass-conditional", "from dataclasses import dataclass,fields\n@dataclass\nclass Box:\n if True:item:int=7\nprint(Box(),[f.name for f in fields(Box)],fields(Box)[0].type is int)", "Box(item=7) ['item'] True\n" },
        { "dataclass-live-mapping", "from dataclasses import dataclass,fields\n@dataclass\nclass Box:\n x:int=1\n __annotations__={'y':str}\n y='value'\nprint(Box(),[f.name for f in fields(Box)],fields(Box)[0].type is str)", "Box(y='value') ['y'] True\n" },
        { "initvar-value", "from dataclasses import InitVar\nkind=InitVar[int]\nprint(kind.type is int,kind,InitVar)\ntry:kind[str]\nexcept TypeError:print('not subscriptable')", "True dataclasses.InitVar[int] <class 'dataclasses.InitVar'>\nnot subscriptable\n" },
        { "initvar-unicode", "from dataclasses import InitVar\nprint(InitVar['é雪'])", "dataclasses.InitVar['é雪']\n" },
        { "initvar-large-render", "from dataclasses import InitVar\ntext=repr(InitVar['é'*8192])\nprint(len(text),text.startswith(\"dataclasses.InitVar['\"),text.endswith(\"']\"))", "8215 True True\n" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ClassAnnotationsUseLiveValuesAndNamespace(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(";", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, $"{name}: {result.Failure?.Message}");
                    Assert.Equal(expected, result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LargeInitVarRenderingRefusalIsTerminalAndReturnsItsOwnership(bool asynchronous)
    {
        const string source = "from dataclasses import InitVar\nvalue=InitVar['é'*32768]\nprint('ready')\ntry:print(value)\nexcept BaseException:print('caught')\nprint('after')";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var invocation = new LythonMemoryBudget(192 * 1024);
            var options = new LythonRunOptions { MemoryBudget = invocation };
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Equal("ready\n", result.StandardOutput);
            }
            Assert.Equal(0, invocation.ReservedBytes);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
