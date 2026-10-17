using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ExceptionArgumentTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "generic-construction-is-lazy", "events=[]\nclass Message:\n def __str__(self):events.append('str');return 'payload'\nmessage=Message()\nfor cls in [BaseException,Exception,ValueError,TypeError]:\n events.clear()\n error=cls(message)\n print(error.args[0] is message,events)", "True []\nTrue []\nTrue []\nTrue []\n" },
        { "mutable-single-argument", "items=[7]\nerror=ValueError(items)\nitems.append(8)\nprint(str(error),repr(error),error.args[0] is items)", "[7, 8] ValueError([7, 8]) True\n" },
        { "multiple-arguments-repr-is-lazy", "events=[]\nclass Item:\n def __repr__(self):events.append('repr');return 'item'\nitem=Item()\nerror=ValueError(item,7)\nprint(events)\nprint(str(error),repr(error),error.args[0] is item,events)", "[]\n(item, 7) ValueError(item, 7) True ['repr', 'repr']\n" },
        { "keyerror-repr-is-lazy", "events=[]\nclass Key:\n def __repr__(self):events.append('repr');return 'key'\n def __str__(self):events.append('str');return 'text'\nkey=Key()\nerror=KeyError(key)\nprint(events)\nprint(str(error),repr(error),error.args[0] is key,events)", "[]\nkey KeyError(key) True ['repr', 'repr']\n" },
        { "native-args", "for error in [ValueError(),ValueError(None),ValueError(7),ValueError('x'),ValueError(1,2)]:print(error.args,str(error),repr(error))", "()  ValueError()\n(None,) None ValueError(None)\n(7,) 7 ValueError(7)\n('x',) x ValueError('x')\n(1, 2) (1, 2) ValueError(1, 2)\n" },
        { "assigned-args", "error=ValueError('old')\nerror.args=([7],)\nerror.args[0].append(8)\nprint(str(error),repr(error))", "[7, 8] ValueError([7, 8])\n" },
        { "failing-render-hook", "class Message:\n def __str__(self):raise RuntimeError('render')\nmessage=Message()\nerror=ValueError(message)\nprint(error.args[0] is message)\ntry:str(error)\nexcept RuntimeError as failure:print(str(failure))", "True\nrender\n" },
        { "repeated-requested-rendering", "events=[]\nclass Message:\n def __str__(self):events.append('str');return str(len(events))\nerror=ValueError(Message())\nprint(events)\nfor _ in range(2):print(str(error))\nprint(events)", "[]\n1\n2\n['str', 'str']\n" },
        { "raise-keeps-args-without-rendering", "events=[]\nclass Message:\n def __str__(self):events.append('str');return 'payload'\nmessage=Message();original=ValueError(message)\ntry:raise original\nexcept ValueError as error:print(error is original,error.args[0] is message,events)", "True True []\n" },
        { "module-identity", "import csv\nevents=[]\nclass Message:\n def __str__(self):events.append('str');return 'payload'\nmessage=Message();error=csv.Error(message)\nprint(events,error.__class__ is csv.Error)\nprint(str(error),error.args[0] is message)", "[] True\npayload True\n" },
        { "args-identity-and-dict", "error=ValueError('payload')\nprint(error.args is error.args,error.__dict__)\nerror.note=7\nerror.args=('changed',)\nprint(error.note,error.__dict__,str(error))", "True {}\n7 {'note': 7} changed\n" },
        { "shadow-message-attribute", "events=[]\nclass Message:\n def __str__(self):events.append('str');return 'payload'\nerror=ValueError(Message());error.message='shadow'\nprint(error.message,events)", "shadow []\n" },
        { "keywords-rejected-before-render", "events=[]\nclass Message:\n def __str__(self):events.append('str');return 'payload'\ntry:ValueError(message=Message())\nexcept TypeError:print('rejected',events)", "rejected []\n" },
        { "systemexit-existing-branch", "for code in [7,'stop']:\n error=SystemExit(code)\n print(error.code,error.args,str(error))", "7 (7,) 7\nstop ('stop',) stop\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ConstructionPreservesArgumentsAndLazyRendering(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, name + ": " + string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
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

    [Fact]
    public async Task MessageCompatibilityMemberRendersCurrentArguments()
    {
        const string source = """
events=[]
class Message:
 def __str__(self):events.append('str');return str(len(events))
error=ValueError(Message())
print(events)
for _ in range(2):print(error.message)
print(events)
""";
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("[]\n1\n2\n['str', 'str']\n", result.StandardOutput);
        }
    }

}
