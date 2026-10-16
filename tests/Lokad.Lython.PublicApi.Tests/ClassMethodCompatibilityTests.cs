using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ClassMethodCompatibilityTests
{
    public static TheoryData<string, string, string> Cases => new()
    {
        { "accumulator", "class Accumulator:\n def __init__(self):self.value=0\n def add(self,x):\n  self.value+=x\n  return self.value\na=Accumulator()\nprint([a.add(i) for i in range(5)],a.value)", "[0, 1, 3, 6, 10] 10\n" },
        { "skip-class-namespace", "value=10\nclass Box:\n value=20\n def read(self,default=value):return value,default\nprint(Box().read())", "(10, 20)\n" },
        { "enclosing-late-value", "def make():\n value=1\n class Box:\n  def read(self):return value\n value=7\n return Box\nprint(make()().read())", "7\n" },
        { "unbound-enclosing", "def make():\n class Box:\n  def read(self):return value\n return Box\n value=7\ntry:make()().read()\nexcept NameError:print('unbound')", "unbound\n" },
        { "nonlocal-writes", "def make():\n value=2\n class Box:\n  def add(self,n):\n   nonlocal value\n   value+=n\n   return value\n return Box(),lambda:value\nbox,read=make()\nprint(box.add(1),box.add(4),read())", "3 7 7\n" },
        { "nonlocal-nested-lambda", "def make():\n value=2\n class Box:\n  def read(self):\n   nonlocal value\n   return lambda:value\n  def write(self,n):\n   nonlocal value\n   value=n\n return Box()\nbox=make()\nread=box.read()\nbox.write(9)\nprint(read())", "9\n" },
        { "method-local-capture", "class Box:\n def make(self):\n  return [lambda:i for i in range(3)]\nprint([f() for f in Box().make()])", "[2, 2, 2]\n" },
        { "lexical-class-super", "class Base:\n value=4\nclass Box(Base):\n def read(self,n):return super().value+n,__class__.__name__\nprint(Box().read(3))", "(7, 'Box')\n" },
        { "method-wrappers", "class Box:\n def __init__(self,n):self._value=n\n @classmethod\n def make(cls):return cls(7)\n @staticmethod\n def twice(n):return 2*n\n @property\n def value(self):return self._value\n @value.setter\n def value(self,n):self._value=n\na=Box.make()\na.value=8\nprint(a.value,Box.twice(3))", "8 6\n" },
        { "annotation-order", "events=[]\ndef mark(name):\n events.append(name)\n return name\ndef decorate(fn):\n events.append(('decorate',fn.__annotations__))\n return fn\nclass Box:\n @decorate\n def run(self,a:mark('annotation')=mark('default'))->mark('return'):return a\nprint(Box().run(),events)", "default ['default', 'annotation', 'return', ('decorate', {'a': 'annotation', 'return': 'return'})]\n" },
        { "live-defaults", "class Box:\n def run(self,x=[]):\n  x.append(1)\n  return x\nbox=Box()\nprint(box.run(),box.run())\nmethod=box.run\nBox.run.__defaults__=([9],)\nprint(method())", "[1, 1] [1, 1]\n[9, 1]\n" },
        { "method-replacement", "class Box:\n def run(self):return 2\nbox=Box()\nold=box.run\ndef replacement(self):return 9\nBox.run=replacement\nprint(old(),box.run())\ndel Box.run\ntry:box.run()\nexcept AttributeError:print('missing')", "2 9\nmissing\n" },
        { "locals-and-class-cell", "class Box:\n def run(self,a):\n  owner=__class__\n  b=7\n  return sorted(k for k in locals() if k!='self')\nprint(Box().run(2))", "['__class__', 'a', 'b', 'owner']\n" },
        { "method-error-cleanup", "events=[]\nclass Box:\n def run(self):\n  try:raise ValueError('leaf')\n  finally:events.append('finally')\ntry:Box().run()\nexcept ValueError as error:print(str(error),events)", "leaf ['finally']\n" },
        { "per-definition-defaults", "def make(n):\n class Box:\n  def read(self,value=n):return value\n return Box\nfirst=make(2)\nsecond=make(9)\nfirst.read.__defaults__=(7,)\nprint(first().read(),second().read())", "7 9\n" },
        { "unbound-declared-nonlocal", "def make():\n class Box:\n  def read(self):\n   nonlocal value\n   return value\n return Box\n value=7\ntry:make()().read()\nexcept NameError:print('unbound')", "unbound\n" },
        { "nonlocal-active-parent", "def make():\n value=1\n class Box:\n  def write(self):\n   nonlocal value\n   value=9\n Box().write()\n return value\nprint(make())", "9\n" },
        { "transport-empty-formals", "class Box:\n def bad():return 'bad'\ntry:Box().bad()\nexcept TypeError:print('arity')", "arity\n" },
        { "transport-default-self", "class Box:\n def method(self='fallback',value=7):return self.__class__.__name__,value\nbox=Box()\nmethod=box.method\nprint(method())\nBox.method.__defaults__=('ignored',11)\nprint(method())", "('Box', 7)\n('Box', 11)\n" },
        { "transport-arity-effects", "events=[]\ndef mark(n):\n events.append(n)\n return n\nclass Box:\n def method(self,x):\n  events.append('body')\n  return x\nbox=Box()\ntry:box.method(mark(1),mark(2))\nexcept TypeError:print('many',events)\ntry:box.method()\nexcept TypeError:print('missing',events)\nprint(box.method(3),events)", "many [1, 2]\nmissing [1, 2]\n3 [1, 2, 'body']\n" },
        { "transport-wrappers", "class Box:\n @classmethod\n def who(cls,value=3):return cls.__name__,value\n @staticmethod\n def twice(value=4):return 2*value\nprint(Box.who(7),Box().who(),Box.twice(),Box().twice(5))", "('Box', 7) ('Box', 3) 8 10\n" },
        { "transport-general", "class Box:\n def method(self,x,*,y=4):return x+y\n def other(self,*items):return items\nbox=Box()\nprint(box.method(1,y=2),box.other(*[3,4]))", "3 (3, 4)\n" },
        { "transport-stored-alias-patch", "class Box:\n def method(self,x=1):return 'old',x\nbox=Box()\nold=box.method\noriginal=Box.method\ndef replacement(self,x=2):return 'new',x\nBox.method=replacement\nprint(old.__self__ is box,old.__func__ is original,old(7),box.method())\ndel Box.method\nprint(old())", "True True ('old', 7) ('new', 2)\n('old', 1)\n" },
        { "transport-target-before-arguments","events=[]\nclass Box:\n def run(self,x):\n  events.append(('old',x))\n  return x\nbox=Box()\nold=box.run\ndef replacement(self,x):\n events.append(('new',x))\n return x+10\ndef change():\n Box.run=replacement\n events.append('arg')\n return 3\nprint(box.run(change()),box.run(4),old.__self__ is box,events)","3 14 True ['arg', ('old', 3), ('new', 4)]\n" },
        { "transport-defaults-during-arguments","class Box:\n def run(self,x,y=2):return x,y\ndef change():\n Box.run.__defaults__=(9,)\n return 3\nprint(Box().run(change()))","(3, 9)\n" },
        { "transport-positional-only-negative-keyword","class Box:\n def run(self,/,a=2):return a\nmethod=Box().run\nprint(method(),method(4))\ntry:method(self=2)\nexcept TypeError:print('keyword')","2 4\nkeyword\n" },
        { "transport-receiver-result-aliases","class Box:\n def run(self,a):return self,a\nbox=Box()\nmethod=box.run\nanswer,argument=method(box)\nprint(answer is box,argument is box,method.__func__ is Box.run,method.__self__ is box)","True True True True\n" },
        { "method-returned-generator-iteration", "class Box:\n def run(self):return (i for i in range(3))\ng=Box().run()\nprint(next(g),next(g),list(g))", "0 1 [2]\n" },
        { "class-cell-static-locals", "class Box:\n @staticmethod\n def read():\n  owner=__class__\n  return locals()['__class__'] is Box,vars()['__class__'] is Box\nprint(Box.read())", "(True, True)\n" },
        { "class-cell-local-shadow", "class Box:\n def read(self):\n  __class__='local'\n  return locals()['__class__']\nprint(Box().read())", "local\n" },
        { "class-cell-global-shadow", "__class__='global'\nclass Box:\n def read(self):\n  global __class__\n  value=__class__\n  return '__class__' in locals(),value\nprint(Box().read())", "(False, 'global')\n" },
        { "class-cell-not-captured", "class Box:\n def read(self):return '__class__' in locals()\nprint(Box().read())", "False\n" },
        { "class-cell-deleted-local", "class Box:\n def read(self):\n  __class__='local'\n  del __class__\n  return '__class__' in locals()\nprint(Box().read())", "False\n" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task MethodsFollowPython(string name, string source, string expected)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.True(result.Success, name + ": " + result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }

    private const string ReadSource = "class Worker:\n def __init__(self,prefix):self.prefix=prefix\n def read(self):\n  with open('/input.txt') as source:\n   return self.prefix+source.read()\nprint(Worker('>').read())";

    [Fact]
    public async Task MethodBodyAwaitsHostIoAndReturnsAllowanceAfterCleanup()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(ReadSource, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost();
            host.SeedFile("/input.txt", "ready");
            using (var result = await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(">ready\n", result.StandardOutput);
                Assert.True(host.CompletedAsynchronously > 0);
                Assert.True(budget.ReservedBytes > baseline);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task CancellationDuringMethodIoKeepsOwnershipUntilTheRunEnds()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(ReadSource, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost();
            host.SeedFile("/input.txt", "ready");
            var started = host.PauseReadUntilCancellation("/input.txt");
            using var cancellation = new CancellationTokenSource();
            var pending = script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget, CancellationToken = cancellation.Token });
            try
            {
                await started.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
                var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            }
            finally
            {
                cancellation.Cancel();
                using var cleanup = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            }
            Assert.Equal(baseline, budget.ReservedBytes);
            var fresh = new MockLythonHost();
            fresh.SeedFile("/input.txt", "ready");
            using (var result = script.Run(fresh, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(">ready\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
