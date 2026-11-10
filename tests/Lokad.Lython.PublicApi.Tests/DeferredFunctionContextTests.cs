using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DeferredFunctionContextTests
{
    public static TheoryData<string, string> MethodPrograms => new()
    {
        { "class Renderer:\n def render(self,row):\n  amount=int(row['amount'])\n  if amount<0:return None\n  return f\"{row['name']}: {amount:04d}\"\nrenderer=Renderer()\nprint(renderer.render({'name':'A','amount':'7'}),renderer.render({'name':'B','amount':'-1'}))", "A: 0007 None\n" },
        { "value=100\nclass Worker:\n def update(self,value,seen):\n  value+=3\n  seen['count']=seen['count']+1\n  if value<0:return None\n  snapshot=locals()\n  return value,snapshot['self'] is self,snapshot['value'],seen['count']\nprint(Worker().update(4,{'count':0}),value)", "(7, True, 7, 1) 100\n" },
        { "class Base:\n def value(self):return 3\nclass Child(Base):\n def value(self):\n  value=super().value()\n  if value<0:return None\n  return value+4,__class__.__name__\nprint(Child().value())", "(7, 'Child')\n" },
        { "class Worker:\n def update(self,value):\n  value+=2\n  if value<0:return None\n  def read():return value,self\n  return read\nworker=Worker()\nread=worker.update(5)\nprint(read()[0],read()[1] is worker)", "7 True\n" },
        { "class Worker:\n @classmethod\n def update(cls,value):\n  value+=3\n  if value<0:return None\n  return value,cls\nresult=Worker.update(4)\nprint(result[0],result[1] is Worker,Worker().update(5)[0])", "7 True 8\n" },
        { "class Worker:\n def parse(self,text):\n  value=int(text)\n  if value<0:return None\n  return value+missing\nworker=Worker()\ntry:\n raise RuntimeError('outer')\nexcept RuntimeError as outer:\n try:worker.parse('bad')\n except ValueError as error:print(error.__context__ is outer)\n try:worker.parse('3')\n except NameError as error:print(error.__context__ is outer)\nprint(worker.parse('-1'))", "True\nTrue\nNone\n" },
    };

    public static TheoryData<string, string> Programs => new()
    {
        { "class Bound:\n def __index__(self):return 1\ndef work(value,index):\n value+=1\n if value<0:return None\n return value,'abcd'[index:3]\nprint(work(4,Bound()))", "(5, 'bc')\n" },
        { "def add(row,totals):\n amount=int(row['amount'])\n if amount<0:return False\n kind=row['kind']\n totals[kind]=totals.get(kind,0)+amount\n return True\ntotals={}\nfor kind,amount in [('a','3'),('b','4'),('a','-1'),('a','5')]:\n print(add({'kind':kind,'amount':amount},totals))\nprint(totals)", "True\nTrue\nFalse\nTrue\n{'a': 8, 'b': 4}\n" },
        { "value=100\nview=globals()\ndef work(value,totals):\n value+=3\n totals['seen']=totals['seen']+1\n if value<0:return None\n snapshot=locals()\n return value,snapshot['value'],view['value'],totals['seen']\ntotals={'seen':0}\nprint(work(4,totals),value)", "(7, 7, 100, 1) 100\n" },
        { "class Number:\n def __radd__(self,value):\n  print('protocol',value)\n  return value+10\ndef work(row,totals):\n value=int(row['amount'])\n totals['seen']=totals['seen']+1\n if value<0:return None\n return value+row['other']\ntotals={'seen':0}\nprint(work({'amount':'7','other':Number()},totals),totals)", "protocol 7\n17 {'seen': 1}\n" },
        { "def work(text):\n value=int(text)\n if value<0:return None\n return value+missing\ntry:\n raise RuntimeError('outer')\nexcept RuntimeError as outer:\n try:work('bad')\n except ValueError as error:print(type(error).__name__,error.__context__ is outer)\n try:work('3')\n except NameError as error:print(type(error).__name__,error.__context__ is outer)\nprint(work('-1'))", "ValueError True\nNameError True\nNone\n" },
        { "def work(value,seen):\n value+=2\n seen['count']=seen['count']+1\n if value<0:return None\n return [value,seen]\nseen={'count':0}\nresult=work(3,seen)\nprint(result[0],result[1] is seen,seen)", "5 True {'count': 1}\n" },
        { "value=100\nview=globals()\ndef read():return view['value']\ndef work(value,callback=read):\n value+=3\n if value<0:return None\n return value,callback()\nprint(work(4))\nwork.__defaults__=(lambda:99,)\nprint(work(5),value)", "(7, 100)\n(8, 99) 100\n" },
        { "def bundle(value,*,extra):\n if extra is None:return None\n return [value,extra],(value,extra)\nitems=[]\nresult=bundle(items,extra=items)\nitems.append(5)\nprint(result[0][0] is items,result[0][1] is items,result[1][0] is items,result[1][1] is items,result)\nprint(bundle(items,extra=None))", "True True True True ([[5], [5]], ([5], [5]))\nNone\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    [MemberData(nameof(MethodPrograms))]
    public async Task NativeWorkAndLaterScopeWorkPreserveValuesAndEffects(string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
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

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task NativeErrorKeepsFunctionTraceAndAllowsBudgetReuse(bool asynchronous, bool method)
    {
        var source = method
            ? "class Worker:\n def work(self,text):\n  value=int(text)\n  if value<0:return None\n  return value+1\nreturn Worker().work(text)"
            : "def work(text):\n value=int(text)\n if value<0:return None\n return value+1\nreturn work(text)";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var text in new[] { "bad", "7" })
        {
            var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["text"] = text } };
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                if (text == "bad")
                {
                    Assert.False(result.Success);
                    var failure = Assert.IsType<LythonRuntimeFailure>(result.Failure);
                    Assert.Equal("ValueError", failure.ExceptionType);
                    Assert.Equal(method ? 3 : 2, failure.Span?.Line);
                    Assert.Contains(failure.StackTrace, frame => frame.FunctionName == "work" && frame.Span?.Line == (method ? 6 : 5));
                }
                else
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(new BigInteger(8), result.ReturnValue);
                }
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task LaterIoKeepsMutatedLocalsAndBorrowedAliasesFunded(bool cancel, bool method)
    {
        var source = """
            payload=['kept',1<<65536]
            totals={'seen':0}
            def pause(value,totals):
             global payload
             amount=int('7')
             totals['seen']=totals['seen']+1
             if amount<0:return None
             payload=[]
             source=open('/pause')
             source.read()
             source.close()
             assert amount==7 and totals['seen']==1 and payload==[]
             return value
            return pause(payload,totals)
            """;
        if (method)
        {
            var start = source.IndexOf("def pause(", StringComparison.Ordinal);
            var end = source.LastIndexOf("return pause(", StringComparison.Ordinal);
            var body = source[start..end].Replace("def pause(value,totals):", "def pause(self,value,totals):", StringComparison.Ordinal);
            source = source[..start] + "class Worker:\n" + string.Join('\n', body.TrimEnd().Split('\n').Select(line => " " + line)) +
                "\nreturn Worker().pause(payload,totals)\n";
        }
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost(); host.SeedFile("/pause", "ready");
            var entered = cancel ? host.PauseReadUntilCancellation("/pause") : null;
            using var cancellation = new CancellationTokenSource();
            var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
            try
            {
                if (entered is not null)
                {
                    Assert.Same(entered, await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5)));
                    Assert.False(pending.IsCompleted);
                    Assert.True(budget.ReservedBytes > baseline + 8192);
                    cancellation.Cancel();
                }
                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                if (cancel)
                {
                    Assert.False(result.Success);
                    Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                }
                else
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    var values = Assert.IsType<List<object?>>(result.ReturnValue);
                    Assert.Equal("kept", values[0]);
                    Assert.Equal(65537, Assert.IsType<BigInteger>(values[1]).GetBitLength());
                    Assert.True(budget.ReservedBytes > baseline + 8192);
                    Assert.True(host.CompletedAsynchronously > 0);
                }
            }
            finally
            {
                cancellation.Cancel();
                using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task ConstructedSequenceKeepsWithdrawnAliasesThroughPendingIo(bool tuple, bool cancel)
    {
        var source = """
payload=['kept',1<<65536]
def bundle(value,*,selector):
 if selector<0:return None
 return [value,value]
def consume(value):
 with open('/pause') as source: source.read()
 assert value[0] is value[1]
 return value
result=bundle(payload,selector=0)
payload=None
return consume(result)
""";
        if (tuple) source = source.Replace("return [value,value]", "return (value,value)", StringComparison.Ordinal);
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost(); host.SeedFile("/pause", "ready");
            var entered = cancel ? host.PauseReadUntilCancellation("/pause") : null;
            using var cancellation = new CancellationTokenSource();
            var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
            try
            {
                if (entered is not null)
                {
                    Assert.Same(entered, await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5)));
                    Assert.False(pending.IsCompleted);
                    Assert.True(budget.ReservedBytes > baseline + 8192);
                    cancellation.Cancel();
                }
                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                else
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    IReadOnlyList<object?> values = tuple
                        ? Assert.IsType<object?[]>(result.ReturnValue)
                        : Assert.IsType<List<object?>>(result.ReturnValue);
                    Assert.Equal(2, values.Count);
                    foreach (var item in values)
                    {
                        var value = Assert.IsType<List<object?>>(item);
                        Assert.Equal("kept", value[0]);
                        Assert.Equal(65537, Assert.IsType<BigInteger>(value[1]).GetBitLength());
                    }
                    Assert.True(budget.ReservedBytes > baseline + 8192);
                    Assert.True(host.CompletedAsynchronously > 0);
                }
            }
            finally
            {
                cancellation.Cancel();
                using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task RefusedSequenceStorageKeepsItsOriginAndStopsPublication(bool tuple, bool asynchronous)
    {
        var items = string.Join(',', Enumerable.Repeat("value", 256));
        var literal = tuple ? "(" + items + ")" : "[" + items + "]";
        var source = "import time\ndef bundle(value):\n if value is None:return None\n time.time()\n return " + literal + "\n" + """
try:
 result=bundle('kept')
 print('created')
except MemoryError:
 print('caught')
finally:
 return 0
""";
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var budget = new AdmissionGateBudget();
        foreach (var headroom in new[] { 1024L, 2L * 1024 * 1024 })
        {
            budget.Gate(64L * 1024 * 1024);
            var host = new AdmissionGateHost(budget, headroom);
            using (var result = asynchronous
                ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
                : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(budget.AtGate > 0);
                if (headroom == 1024)
                {
                    Assert.False(result.Success);
                    Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                    Assert.Equal(5, Assert.IsType<LythonRuntimeFailure>(result.Failure).Span?.Line);
                    Assert.Empty(result.StandardOutput);
                }
                else
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("created\n", result.StandardOutput);
                }
            }
            Assert.Equal(0, budget.Root.ReservedBytes);
        }
    }
}
