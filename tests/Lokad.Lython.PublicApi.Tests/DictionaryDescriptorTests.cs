using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DictionaryDescriptorTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "base-keywords-derived-receivers", "from collections import Counter,defaultdict\nfor factory in [lambda:defaultdict(int,{'x':7}),lambda:Counter({'x':7})]:\n for name in ['__getitem__','__setitem__','__delitem__']:\n  d=factory()\n  method=getattr(dict,name)\n  try:\n   if name=='__setitem__':method(d,index='x',value=9)\n   else:method(d,index='x')\n   print('accepted',dict(d))\n  except TypeError:print('keyword',dict(d))", "keyword {'x': 7}\nkeyword {'x': 7}\nkeyword {'x': 7}\nkeyword {'x': 7}\nkeyword {'x': 7}\nkeyword {'x': 7}\n" },
        { "declared-owner-control", "from collections import Counter,defaultdict\nd=defaultdict(lambda:9)\nprint(dict.__getitem__(d,'x'),dict(d))\nc=Counter()\nprint(dict.__getitem__(c,'x'),dict(c))\ntry:dict.__delitem__(c,'missing');print('accepted')\nexcept KeyError:print('missing')\ntry:c.__delitem__(elem='missing');print('counter-keyword')\nexcept TypeError:print('counter-typeerror')", "9 {'x': 9}\n0 {}\nmissing\ncounter-keyword\n" },
        { "inherited-item-keywords", "from collections import Counter,defaultdict\nfor factory in [lambda:defaultdict(int,{'x':7}),lambda:Counter({'x':7})]:\n for name in ['__getitem__','__setitem__','__delitem__']:\n  if name=='__delitem__' and factory().__class__ is Counter:continue\n  d=factory()\n  method=getattr(d,name)\n  for form in range(3):\n   try:\n    if name=='__setitem__':\n     if form==0:d.__setitem__(index='x',value=9)\n     elif form==1:method('x',value=9)\n     else:method(**{'index':'x','value':9})\n    elif form==0:getattr(d,name)(index='x')\n    elif form==1:method(index='x')\n    else:method(**{'index':'x'})\n    print('accepted',dict(d))\n   except TypeError:print('keyword',dict(d))", "keyword {'x': 7}\nkeyword {'x': 7}\nkeyword {'x': 7}\nkeyword {'x': 7}\nkeyword {'x': 7}\nkeyword {'x': 7}\nkeyword {'x': 7}\nkeyword {'x': 7}\nkeyword {'x': 7}\nkeyword {'x': 7}\nkeyword {'x': 7}\nkeyword {'x': 7}\nkeyword {'x': 7}\nkeyword {'x': 7}\nkeyword {'x': 7}\n" },
        { "bound-base-descriptors", "from collections import Counter,defaultdict\nfor factory in [lambda:defaultdict(int,{'x':7}),lambda:Counter({'x':7})]:\n for name in ['__getitem__','__setitem__','__delitem__']:\n  d=factory()\n  method=getattr(dict,name).__get__(d,type(d))\n  try:\n   if name=='__setitem__':method(index='x',value=9)\n   else:method(index='x')\n   print('accepted')\n  except TypeError:print('keyword',dict(d))\n  if name=='__setitem__':print(method('x',9),dict(d))\n  else:print(method('x'),dict(d))", "keyword {'x': 7}\n7 {'x': 7}\nkeyword {'x': 7}\nNone {'x': 9}\nkeyword {'x': 7}\nNone {}\nkeyword {'x': 7}\n7 {'x': 7}\nkeyword {'x': 7}\nNone {'x': 9}\nkeyword {'x': 7}\nNone {}\n" },
        { "base-delete-missing", "from collections import Counter,defaultdict\nfor d in [{},defaultdict(int),Counter()]:\n for call in [lambda:dict.__delitem__(d,'missing'),lambda:dict.__delitem__.__get__(d,type(d))('missing'),lambda:dict.__delitem__(*[d,'missing'])]:\n  try:call();print('accepted')\n  except KeyError as error:print('missing',error.args,dict(d))", "missing ('missing',) {}\nmissing ('missing',) {}\nmissing ('missing',) {}\nmissing ('missing',) {}\nmissing ('missing',) {}\nmissing ('missing',) {}\nmissing ('missing',) {}\nmissing ('missing',) {}\nmissing ('missing',) {}\n" },
        { "counter-delete-override", "from collections import Counter\nc=Counter({'x':7})\nmethod=c.__delitem__\nprint(method(elem='x'),dict(c))\nprint(method(elem='missing'),dict(c))\nprint(method(**{'elem':'missing'}),dict(c))\ntry:method(index='missing');print('accepted')\nexcept TypeError:print('index-rejected')\ntry:method('missing',elem='other');print('accepted')\nexcept TypeError:print('duplicate-rejected')", "None {}\nNone {}\nNone {}\nindex-rejected\nduplicate-rejected\n" },
        { "missing-get-hooks", "from collections import Counter,defaultdict\nevents=[]\ndef make():events.append('factory');return 9\nd=defaultdict(make)\nfor key,call in [('a',lambda:dict.__getitem__(d,'a')),('b',lambda:d.__getitem__('b')),('c',lambda:dict.__getitem__.__get__(d,type(d))('c'))]:print(call(),dict(d))\nprint(events)\nc=Counter()\nprint(dict.__getitem__(c,'a'),c.__getitem__('b'),dict.__getitem__.__get__(c,type(c))('c'),dict(c))\ntry:dict.__getitem__(defaultdict(None),'x')\nexcept KeyError:print('missing-no-factory')", "9 {'a': 9}\n9 {'a': 9, 'b': 9}\n9 {'a': 9, 'b': 9, 'c': 9}\n['factory', 'factory', 'factory']\n0 0 0 {}\nmissing-no-factory\n" },
        { "valid-positional-mutations", "from collections import Counter,defaultdict\nfor d in [{},defaultdict(int),Counter()]:\n print(dict.__setitem__(d,'x',7),dict(d))\n print(d.__getitem__('x'),dict.__getitem__(d,'x'))\n print(d.__setitem__('x',9),dict(d))\n print(dict.__delitem__(d,'x'),dict(d))\n print(dict.__setitem__.__get__(d,type(d))('x',11),dict(d))\n print(dict.__delitem__.__get__(d,type(d))('x'),dict(d))", "None {'x': 7}\n7 7\nNone {'x': 9}\nNone {}\nNone {'x': 11}\nNone {}\nNone {'x': 7}\n7 7\nNone {'x': 9}\nNone {}\nNone {'x': 11}\nNone {}\nNone {'x': 7}\n7 7\nNone {'x': 9}\nNone {}\nNone {'x': 11}\nNone {}\n" },
        { "argument-effects-before-keyword-rejection", "from collections import Counter,defaultdict\nevents=[]\nclass Key:\n def __hash__(self):return hash('x')\n def __eq__(self,other):events.append('lookup');return other=='x'\ndef mark(label,value):events.append(label);return value\nfor d in [defaultdict(int,{Key():7}),Counter({Key():7})]:\n for call in [lambda:dict.__getitem__(d,index=mark('key','x')),lambda:dict.__setitem__(d,index=mark('key','x'),value=mark('value',9)),lambda:dict.__delitem__(d,index=mark('key','x'))]:\n  try:call();print('accepted')\n  except TypeError:print('keyword',list(d.values()))\nprint(events)", "keyword [7]\nkeyword [7]\nkeyword [7]\nkeyword [7]\nkeyword [7]\nkeyword [7]\n['key', 'key', 'value', 'key', 'key', 'key', 'value', 'key']\n" },
        { "derived-positional-arity", "from collections import Counter,defaultdict\nfor d in [defaultdict(int,{'x':7}),Counter({'x':7})]:\n for call in [lambda:d.__getitem__(),lambda:d.__getitem__('x',9),lambda:d.__setitem__('x'),lambda:d.__setitem__('x',9,10),lambda:dict.__delitem__(d),lambda:dict.__delitem__(d,'x',9)]:\n  try:call();print('accepted')\n  except TypeError:print('arity',dict(d))", "arity {'x': 7}\narity {'x': 7}\narity {'x': 7}\narity {'x': 7}\narity {'x': 7}\narity {'x': 7}\narity {'x': 7}\narity {'x': 7}\narity {'x': 7}\narity {'x': 7}\narity {'x': 7}\narity {'x': 7}\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task DeclaredOwnerAndInheritedMethodsFollowPython(string name, string source, string expected)
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
    [InlineData("defaultdict(int,{Key():7})")]
    [InlineData("Counter({Key():7})")]
    public async Task RejectedKeywordsAwaitArgumentsButNeverLookupOrMutate(string constructor)
    {
        const string template = """
from collections import Counter,defaultdict
class Key:
 def __hash__(self):return hash('x')
 def __eq__(self,other):
  with open('lookup.txt','a') as target:target.write('lookup')
  return other=='x'
d=CONSTRUCTOR
def argument(label,value):
 with open('arguments.txt','a') as target:target.write(label)
 return value
for name in ['__getitem__','__setitem__','__delitem__']:
 method=getattr(d,name)
 base=getattr(dict,name)
 bound=base.__get__(d,type(d))
 if name=='__setitem__':
  calls=[lambda:method(index=argument('key','x'),value=argument('value',9)),lambda:base(d,index=argument('key','x'),value=argument('value',9)),lambda:bound(index=argument('key','x'),value=argument('value',9)),lambda:base(*[d],**{'index':argument('key','x'),'value':argument('value',9)})]
 else:
  calls=[lambda:method(index=argument('key','x')),lambda:base(d,index=argument('key','x')),lambda:bound(index=argument('key','x')),lambda:base(*[d],**{'index':argument('key','x')})]
 for call in calls:
  try:call()
  except TypeError:print('keyword')
print(len(d),list(d.values()))
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(template.Replace("CONSTRUCTOR", constructor), budget);
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
                Assert.Equal(string.Concat(Enumerable.Repeat("keyword\n", 12)) + "1 [7]\n", result.StandardOutput);
                Assert.Equal("untouched", delayed?.ReadText("/lookup.txt") ?? mock!.ReadText("/lookup.txt"));
                Assert.Equal("beforekeykeykeykeykeyvaluekeyvaluekeyvaluekeyvaluekeykeykeykey",
                    delayed?.ReadText("/arguments.txt") ?? mock!.ReadText("/arguments.txt"));
                if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("defaultdict(int,{'x':7})")]
    [InlineData("Counter({'x':7})")]
    public async Task CancellationDuringDescriptorArgumentDrainsBeforeRefund(string constructor)
    {
        const string template = """
from collections import Counter,defaultdict
d=CONSTRUCTOR
method=dict.__getitem__.__get__(d,type(d))
def key():
 with open('arguments.txt','a') as target:target.write('argument')
 return 'x'
try:method(index=key())
except TypeError:print('keyword')
""";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(template.Replace("CONSTRUCTOR", constructor), budget);
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
    [InlineData("defaultdict(int,{'x':7})")]
    [InlineData("Counter({'x':7})")]
    public async Task RefusedDescriptorArgumentFailsTheCommand(string constructor)
    {
        const string template = """
from collections import Counter,defaultdict
d=CONSTRUCTOR
method=dict.__getitem__.__get__(d,type(d))
def key():
 print('argument-before')
 try:unused=[0]*8192
 except MemoryError:pass
 return 'x'
try:method(index=key())
except TypeError:print('keyword')
print('after')
""";
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile(template.Replace("CONSTRUCTOR", constructor), budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(64 * 1024) };
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.False(result.Success);
                Assert.Equal("MemoryError", result.Failure?.ExceptionType);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Equal("argument-before\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("defaultdict(None)")]
    [InlineData("Counter()")]
    public async Task EscapingBaseDescriptorResultStaysFunded(string constructor)
    {
        var source = "from collections import Counter,defaultdict\nd=" + constructor + "\nd['x']=['a'*4096]\nreturn dict.__getitem__.__get__(d,type(d))('x')";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using (var first = script.Run(new MockLythonHost()))
        {
            Assert.True(first.Success, first.Failure?.Message);
            var values = Assert.IsAssignableFrom<System.Collections.IEnumerable>(first.ReturnValue).Cast<object?>().ToArray();
            Assert.Equal(new string('a', 4096), Assert.Single(values));
            var held = budget.ReservedBytes;
            Assert.True(held > baseline);
            using (var second = await script.RunAsync(new MockLythonHost()))
            {
                Assert.True(second.Success, second.Failure?.Message);
                Assert.True(budget.ReservedBytes > held);
            }
            Assert.Equal(held, budget.ReservedBytes);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
