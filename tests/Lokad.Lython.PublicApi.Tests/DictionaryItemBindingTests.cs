using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DictionaryItemBindingTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "keyword-forms", "d={'x':7}\nmethod=d.__getitem__\nevents=[]\ndef key():events.append('key');return 'x'\nfor call in [lambda:d.__getitem__(index=key()),lambda:method(index=key()),lambda:dict.__getitem__(d,index=key()),lambda:method(**{'index':key()})]:\n try:print(call())\n except TypeError:print('keyword')\nprint(events)", "keyword\nkeyword\nkeyword\nkeyword\n['key', 'key', 'key', 'key']\n" },
        { "arity-controls", "d={'x':7}\nmethod=d.__getitem__\nfor call in [lambda:method(),lambda:method('x','extra'),lambda:method(key='x'),lambda:method('x')]:\n try:print(call())\n except TypeError:print('arity')", "arity\narity\narity\n7\n" },
        { "protocol-keyword-effects", "events=[]\nclass Key:\n def __hash__(self):return hash('x')\n def __eq__(self,other):events.append('lookup');return other=='x'\nd={Key():7}\nmethod=d.__getitem__\ndef key():events.append('argument');return 'x'\nfor call in [lambda:d.__getitem__(index=key()),lambda:method(index=key()),lambda:dict.__getitem__(d,index=key()),lambda:method(**{'index':key()})]:\n try:print(call())\n except TypeError:print('keyword')\nprint(events)", "keyword\nkeyword\nkeyword\nkeyword\n['argument', 'argument', 'argument', 'argument']\n" },
        { "guest-keywords-and-constructor", "class Mapping:\n def __getitem__(self,index):return index\nprint(Mapping().__getitem__(index='guest'))\nprint(dict(index='constructor')['index'])\nprint({'x':7}.__getitem__('x'))", "guest\nconstructor\n7\n" },
        { "setitem-keyword-effects", "for form in range(4):\n d={'x':7}\n method=d.__setitem__\n try:\n  if form==0:d.__setitem__(index='x',value=9)\n  elif form==1:method('x',value=9)\n  elif form==2:dict.__setitem__(d,index='x',value=9)\n  else:method(**{'index':'x','value':9})\n  print('accepted',d)\n except TypeError:print('keyword',d)", "keyword {'x': 7}\nkeyword {'x': 7}\nkeyword {'x': 7}\nkeyword {'x': 7}\n" },
        { "delitem-keyword-effects", "for form in range(4):\n d={'x':7}\n method=d.__delitem__\n try:\n  if form==0:d.__delitem__(index='x')\n  elif form==1:method(index='x')\n  elif form==2:dict.__delitem__(d,index='x')\n  else:method(**{'index':'x'})\n  print('accepted',d)\n except TypeError:print('keyword',d)", "keyword {'x': 7}\nkeyword {'x': 7}\nkeyword {'x': 7}\nkeyword {'x': 7}\n" },
        { "positional-and-arity", "d={'x':7}\nprint(d.__getitem__('x'),dict.__getitem__(d,'x'))\nfor call in [lambda:d.__getitem__(),lambda:d.__getitem__('x',9),lambda:d.__setitem__('x'),lambda:d.__setitem__('x',9,10),lambda:d.__delitem__(),lambda:d.__delitem__('x',9)]:\n try:call()\n except TypeError:print('arity')\nprint(d.__setitem__('x',9),d)\nprint(d.__delitem__('x'),d)", "7 7\narity\narity\narity\narity\narity\narity\nNone {'x': 9}\nNone {}\n" },
        { "all-argument-effects", "events=[]\ndef mark(name,value):events.append(name);return value\nd={'x':7}\nfor call in [lambda:d.__setitem__(value=mark('value',9),index=mark('index','x')),lambda:d.__delitem__(index=mark('delete','x')),lambda:d.__getitem__(index=mark('read','x'))]:\n try:call()\n except TypeError:events.append('type')\n except KeyError:events.append('missing')\nprint(events,d)", "['value', 'index', 'type', 'delete', 'type', 'read', 'type'] {'x': 7}\n" },
        { "guest-item-keywords", "class Mapping:\n def __init__(self):self.data={}\n def __getitem__(self,index):return self.data[index]\n def __setitem__(self,index,value):self.data[index]=value\n def __delitem__(self,index):del self.data[index]\nvalue=Mapping()\nvalue.__setitem__(index='x',value=7)\nprint(value.__getitem__(index='x'))\nvalue.__delitem__(index='x')\nprint(value.data,dict(index='constructor'))", "7\n{} {'index': 'constructor'}\n" },
        { "protected-facts", "d={'x':7}\ntry:d.__setitem__(index='y',value=9)\nexcept TypeError:pass\ntry:d.__delitem__(index='x')\nexcept TypeError:pass\nprint(list(d.items()))\ntry:print(d['x'])\nexcept KeyError:print('missing')", "[('x', 7)]\n7\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ItemMethodsFollowPythonBindingAndArgumentOrder(string name, string source, string expected)
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
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("__getitem__", "d.__getitem__(index=argument('key','x'))", "beforekey")]
    [InlineData("__getitem__", "method(index=argument('key','x'))", "beforekey")]
    [InlineData("__getitem__", "dict.__getitem__(d,index=argument('key','x'))", "beforekey")]
    [InlineData("__getitem__", "method(**{'index':argument('key','x')})", "beforekey")]
    [InlineData("__setitem__", "d.__setitem__(index=argument('key','x'),value=argument('value',9))", "beforekeyvalue")]
    [InlineData("__setitem__", "method(argument('key','x'),value=argument('value',9))", "beforekeyvalue")]
    [InlineData("__setitem__", "dict.__setitem__(d,index=argument('key','x'),value=argument('value',9))", "beforekeyvalue")]
    [InlineData("__setitem__", "method(*[argument('key','x')],**{'value':argument('value',9)})", "beforekeyvalue")]
    [InlineData("__delitem__", "d.__delitem__(index=argument('key','x'))", "beforekey")]
    [InlineData("__delitem__", "method(index=argument('key','x'))", "beforekey")]
    [InlineData("__delitem__", "dict.__delitem__(d,index=argument('key','x'))", "beforekey")]
    [InlineData("__delitem__", "method(**{'index':argument('key','x')})", "beforekey")]
    public async Task ArgumentsAwaitButRejectedKeywordsNeverInvokeEqualityOrMutation(string member, string call, string arguments)
    {
        const string template = """
class Key:
 def __hash__(self):return hash('x')
 def __eq__(self,other):
  with open('lookup.txt','a') as target:target.write('lookup')
  return other=='x'
d={Key():7}
method=d.MEMBER
def argument(label,value):
 with open('arguments.txt','a') as target:target.write(label)
 return value
try:CALL
except TypeError:print('keyword')
print(len(d),list(d.values()))
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(template.Replace("MEMBER", member).Replace("CALL", call), budget);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            var delayed = asynchronous ? new DelayedLythonHost() : null;
            var mock = asynchronous ? null : new MockLythonHost();
            if (delayed is not null)
            {
                delayed.SeedFile("/lookup.txt", "untouched");
                delayed.SeedFile("/arguments.txt", "before");
            }
            else
            {
                mock!.SeedFile("/lookup.txt", "untouched");
                mock.SeedFile("/arguments.txt", "before");
            }
            using (var result = asynchronous ? await script.RunAsync(delayed!) : script.Run(mock!))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("keyword\n1 [7]\n", result.StandardOutput);
                Assert.Equal("untouched", delayed?.ReadText("/lookup.txt") ?? mock!.ReadText("/lookup.txt"));
                Assert.Equal(arguments, delayed?.ReadText("/arguments.txt") ?? mock!.ReadText("/arguments.txt"));
                if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task CancellationDuringArgumentPrecedesKeywordError()
    {
        const string source = """
d={'x':7}
def key():
 with open('arguments.txt','a') as target:target.write('argument')
 return 'x'
try:d.__getitem__(index=key())
except TypeError:print('keyword')
""";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/arguments.txt", "before");
        var entered = host.PauseWriteUntilCancellation("/arguments.txt");
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            await entered.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            cancellation.Cancel();
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            Assert.Empty(result.StandardOutput);
            Assert.Equal("before", host.ReadText("/arguments.txt"));
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusedArgumentCannotBecomeAKeywordErrorOrContinue(bool asynchronous)
    {
        const string source = """
d={'x':7}
def key():
 print('argument-before')
 try:unused=[0]*8192
 except MemoryError:pass
 return 'x'
try:d.__getitem__(index=key())
except TypeError:print('keyword')
print('after')
""";
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(64 * 1024) };
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
        {
            Assert.False(result.Success);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal("argument-before\n", result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Fact]
    public async Task RejectedCallKeepsEscapingArgumentsFundedAcrossExecutions()
    {
        const string source = """
stored=None
def argument():
 global stored
 stored=['a'*4096]
 return stored
try:{}.__getitem__(index=argument())
except TypeError:pass
return stored[0]
""";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using (var first = script.Run(new MockLythonHost()))
        {
            Assert.True(first.Success, first.Failure?.Message);
            Assert.Equal(new string('a', 4096), Assert.IsType<string>(first.ReturnValue));
            var held = budget.ReservedBytes;
            Assert.True(held > baseline);
            using (var second = await script.RunAsync(new MockLythonHost()))
            {
                Assert.True(second.Success, second.Failure?.Message);
                Assert.Equal(first.ReturnValue, second.ReturnValue);
                Assert.True(budget.ReservedBytes > held);
            }
            Assert.Equal(held, budget.ReservedBytes);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
