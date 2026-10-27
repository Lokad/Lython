using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeCallbackUnwindTests
{
    public static TheoryData<string, string> Boundaries => new()
    {
        { "namedtuple-fields", "from typing import NamedTuple\ndef fields():\n read()\n yield ('x',int)\nNamedTuple('P',fields())" },
        { "typevar-variance", "from typing import TypeVar\nclass Flag:\n def __bool__(self):read();return True\nTypeVar('T',covariant=Flag())" },
        { "paramspec-variance", "from typing import ParamSpec\nclass Flag:\n def __bool__(self):read();return True\nParamSpec('P',contravariant=Flag())" },
        { "decimal-localcontext", "import decimal\nwith decimal.localcontext():read()" },
        { "repeat-index", "class Count:\n def __index__(self):read();return 2\nvalue='x'*Count()" },
        { "repeat-index-descriptor", "class Slot:\n def __get__(self,instance,owner):read();return lambda:2\nclass Count:__index__=Slot()\nvalue=[1].__mul__(Count())" },
        { "repeat-inplace", "class Count:\n def __index__(self):read();return 2\nvalue=[1];value*=Count()" },
        { "divmod-left", "class Value:\n def __divmod__(self,other):read();return (1,2)\ndivmod(Value(),1)" },
        { "divmod-reflected", "class Base:pass\nclass Child(Base):\n def __rdivmod__(self,other):read();return (1,2)\ndivmod(Base(),Child())" },
        { "binary-descriptor", "class Slot:\n def __get__(self,instance,owner):read();return lambda other:42\nclass Value:__add__=Slot()\nValue()+1" },
        { "argparse-add-help", "import argparse\nclass Flag:\n def __bool__(self):read();return False\nargparse.ArgumentParser(add_help=Flag())" },
        { "argparse-required", "import argparse\nclass Flag:\n def __bool__(self):read();return False\np=argparse.ArgumentParser(add_help=False);p.add_argument('--x',required=Flag());p.parse_args([])" },
        { "argparse-format-required", "import argparse\nclass Flag:\n def __bool__(self):read();return True\np=argparse.ArgumentParser(add_help=False);p.add_argument('--x',required=Flag());p.print_help()" },
        { "argparse-count", "import argparse\nclass Number:\n def __add__(self,other):read();return 42\np=argparse.ArgumentParser(add_help=False);p.add_argument('-v',action='count',default=Number());p.parse_args(['-v'])" },
        { "argparse-writer", "import argparse\nclass Writer:\n def write(self,text):read();return len(text)\nargparse.ArgumentParser(prog='tool',add_help=False).print_help(file=Writer())" },
        { "argparse-equality", "import argparse\nclass Value:\n def __eq__(self,other):read();return True\nargparse.Namespace(x=Value())==argparse.Namespace(x=1)" },
        { "bytes-bound", "class Bound:\n def __index__(self):read();return 0\nb'abc'.find(b'a',Bound())" },
        { "bytes-needle", "class Bound:\n def __index__(self):read();return 97\nb'abc'.find(Bound())" },
        { "bytes-contains", "class Bound:\n def __index__(self):read();return 97\nvalue=Bound() in b'abc'" },
        { "bytes-pad", "class Bound:\n def __index__(self):read();return 4\nb'ab'.zfill(Bound())" },
        { "bytes-split-count", "class Bound:\n def __index__(self):read();return 1\nb'a b'.rsplit(None,Bound())" },
        { "bytes-hex-group", "class Bound:\n def __index__(self):read();return 2\nb'ab'.hex('-',Bound())" },
        { "bytes-lines-truth", "class Flag:\n def __bool__(self):read();return False\nb'a\\nb'.splitlines(Flag())" },
        { "struct-float", "import struct\nclass Number:\n def __float__(self):read();return 1.0\nstruct.pack('<d',Number())" },
        { "codec-reason", "class Reason:\n def __str__(self):read();return 'changed'\ntry:b'\\xff'.decode('ascii')\nexcept UnicodeDecodeError as error:\n error.reason=Reason()\n str(error)" },
        { "search-equality", "class K:\n def __eq__(self,other):read();return True\n[K()].index(1)" },
        { "remove-equality", "class K:\n def __eq__(self,other):read();return True\n[K()].remove(1)" },
        { "list-ordering", "class K:\n def __eq__(self,other):read();return True\n[K()]<[1]" },
        { "slice-equality", "class K:\n def __eq__(self,other):read();return True\nslice(K())==slice(1)" },
        { "sorted-truth", "class Flag:\n def __bool__(self):read();return False\nsorted([2,1],reverse=Flag())" },
        { "sort-truth", "class Flag:\n def __bool__(self):read();return False\nitems=[2,1];items.sort(reverse=Flag())" },
        { "list-bound", "class Bound:\n def __index__(self):read();return 0\n[1,2].index(1,Bound())" },
        { "tuple-bound", "class Bound:\n def __index__(self):read();return 0\n(1,2).index(1,Bound())" },
        { "deque-bound", "class Bound:\n def __index__(self):read();return 0\ndeque([1,2]).index(1,Bound())" },
        { "bound-descriptor", "class Bound:\n @property\n def __index__(self):read();return lambda:0\n[1,2].index(1,Bound())" },
        { "list-insert", "class Bound:\n def __index__(self):read();return 0\nitems=[1,2];items.insert(Bound(),9)" },
        { "deque-insert", "class Bound:\n def __index__(self):read();return 0\nitems=deque([1,2]);items.insert(Bound(),9)" },
        { "deque-remove", "class K:\n def __eq__(self,other):read();return True\ndeque([K()]).remove(1)" },
    };

    private const string Prelude = """
        from collections import deque
        def read():
            with open('/cleanup','w') as target:
                target.write('done')
                target.flush()
                with open('/value') as source: source.read()
                try:
                    if fail: storage='x'*8388608
                finally: return 1
        """;

    [Theory, MemberData(nameof(Boundaries))]
    public async Task InFlightCancellationUnwindsBeforeReuse(string name, string boundary)
    {
        var budget=new LythonMemoryBudget(2*1024*1024);
        using var script=new LythonEngine().Compile(Prelude+"\n"+boundary,budget);
        Assert.True(script.IsValid,name);
        var baseline=budget.ReservedBytes;
        var host=new DelayedLythonHost();host.SeedFile("/value","1");
        var started=host.PauseReadUntilCancellation("/value");
        using var cancellation=new CancellationTokenSource();
        var run=script.RunAsync(host,new LythonRunOptions { CancellationToken=cancellation.Token,Globals=new Dictionary<string,object?> { ["fail"]=false } });
        try
        {
            await started.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(run.IsCompleted);
            Assert.True(budget.ReservedBytes>baseline);
            cancellation.Cancel();
            using var result=await run.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled,result.FailureAttribution?.Reason);
        }
        finally
        {
            cancellation.Cancel();
            using var drained=await run.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal("done",host.ReadText("/cleanup"));
        Assert.Equal(baseline,budget.ReservedBytes);
        var fresh=new DelayedLythonHost();fresh.SeedFile("/value","1");
        using(var result=await script.RunAsync(fresh,new LythonRunOptions { Globals=new Dictionary<string,object?> { ["fail"]=false } }))
            Assert.True(result.Success,result.Failure?.Message);
        Assert.Equal(baseline,budget.ReservedBytes);
    }

    [Theory, MemberData(nameof(Boundaries))]
    public async Task RefusalThroughFinallyRemainsTerminal(string name,string boundary)
    {
        var budget=new LythonMemoryBudget(2*1024*1024);
        using var script=new LythonEngine().Compile(Prelude+"\ntry:\n "+boundary.Replace("\n","\n ")+"\nexcept MemoryError:pass\nprint('later')",budget);
        Assert.True(script.IsValid,name);
        var baseline=budget.ReservedBytes;
        foreach(var asynchronous in new[] {false,true})
        {
            var immediate=new MockLythonHost();immediate.SeedFile("/value","1");
            var delayed=new DelayedLythonHost();delayed.SeedFile("/value","1");
            var options=new LythonRunOptions { Globals=new Dictionary<string,object?> { ["fail"]=true } };
            using(var result=asynchronous?await script.RunAsync(delayed,options):script.Run(immediate,options))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance,result.FailureAttribution?.Reason);
                Assert.Empty(result.StandardOutput);
            }
            Assert.Equal("done",asynchronous?delayed.ReadText("/cleanup"):immediate.ReadText("/cleanup"));
            Assert.Equal(baseline,budget.ReservedBytes);
        }
    }
}
