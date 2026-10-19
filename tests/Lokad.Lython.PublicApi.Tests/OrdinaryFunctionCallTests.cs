using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class OrdinaryFunctionCallTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "def f(a=1,*,b=2):return a,b\nprint(f(b=7,a=3),f())\nf.__defaults__=(4,)\nf.__kwdefaults__={'b':5}\nprint(f(),f(b=9))", "(3, 7) (1, 2)\n(4, 5) (4, 9)\n" },
        { "def f(a,/,*args,b=2,**kw):return a,args,b,kw\nprint(f(1,3,4,a=9,b=5,z=8))", "(1, (3, 4), 5, {'a': 9, 'z': 8})\n" },
        { "def f(value,*,offset=1):return value+offset\nsaved=f\ndef change():\n global f\n f=lambda value:99\n return 3\nprint(f(offset=change(),value=4),f(4),saved(value=2,offset=5))", "7 99 7\n" },
        { "events=[]\ndef arg(label,value):events.append(label);return value\ndef f(a,/,b,*,c):events.append('body')\ndef exercise(call):\n for action in [lambda:call(arg('a',1),b=arg('b',2),c=arg('c',3),extra=arg('extra',4)),lambda:call(arg('a',1),arg('b',2),b=arg('dup',3),c=arg('c',4)),lambda:call(arg('a',1),b=arg('b',2)),lambda:call(arg('a',1),b=arg('b',2),c=arg('c',3))]:\n  try:action()\n  except TypeError:events.append('error')\nexercise(f)\nprint(events)", "['a', 'b', 'c', 'extra', 'error', 'a', 'b', 'dup', 'c', 'error', 'a', 'b', 'error', 'a', 'b', 'c', 'body']\n" },
        { "class Box:\n def f(self,a,*,b=3,**kw):return a,b,kw\nbox=Box()\nmethod=box.f\nprint(box.f(b=4,a=2),method(5,z=6))\nprint(method.__self__ is box,method.__func__ is Box.f)", "(2, 4, {}) (5, 3, {'z': 6})\nTrue True\n" },
        { "events=[]\ndef f(value,*,offset):return value+offset\nclass Access:\n @property\n def get(self):events.append('get');return f\nprint(Access().get(offset=7,value=2),events)", "9 ['get']\n" },
        { "def pack(value,*,offset):\n snapshot=locals()\n def read():return value\n value.append(offset)\n return read,snapshot\nvalue=[]\nread,snapshot=pack(offset=3,value=value)\nprint(read() is value,snapshot['value'] is value,value)", "True True [3]\n" },
        { "def f(value,*,offset=1):return value+offset\nprint(f(*[3],**{'offset':4}))\nclass Callable:\n def __call__(self,value,*,offset=2):return value+offset\nprint(Callable()(offset=5,value=6))", "7\n11\n" },
        { "def method(self,value,*,offset=1):return value+offset\nclass Box:pass\nBox.method=method\nbox=Box()\nsaved=box.method\nprint(box.method(offset=5,value=2),saved(value=3))\nprint(saved.__self__ is box,saved.__func__ is method)", "7 4\nTrue True\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task OrdinaryAndExpandedCallsPreservePythonBinding(string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Message)));
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
