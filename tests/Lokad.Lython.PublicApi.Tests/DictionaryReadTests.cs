using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DictionaryReadTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "rows", "def amount(record:dict,key:str):return record[key]\nrows=[{'kind':'red','amount':3},{'kind':'blue','amount':2}]\nprint([(amount(row,'kind'),amount(row,'amount')) for row in rows])", "[('red', 3), ('blue', 2)]\n" },
        { "values", "d={'zero':0,'false':False,'none':None,'list':[]}\nprint(d['zero'],d['false'],d['none'],d['list'] is d['list'])", "0 False None True\n" },
        { "unicode-key", "d={'caf\u00e9\ud83d\ude00':7}\nkey=''.join(['caf\u00e9','\ud83d\ude00'])\nprint(d[key])", "7\n" },
        { "missing", "def take(record,key):return record[key]\nfor key in ['missing','caf\u00e9\ud83d\ude00']:\n try:take({'x':7},key)\n except KeyError as error:print(error.args,repr(str(error)))", "('missing',) \"'missing'\"\n('caf\u00e9\ud83d\ude00',) \"'caf\u00e9\ud83d\ude00'\"\n" },
        { "rebind-receiver", "d={'x':7}\nold=d\ndef key():\n global d\n d={'x':99}\n return 'x'\nprint(d[key()],d['x'],old is d)", "7 99 False\n" },
        { "mutate-value", "d={'x':7}\ndef key():\n d['x']=9\n return 'x'\nprint(d[key()])", "9\n" },
        { "insert-protocol-before-read", "class Key:\n def __hash__(self):return hash('x')\n def __eq__(self,other):print('eq');return other=='x'\nd={}\ndef key():\n d[Key()]=7\n return 'x'\nprint(d[key()])", "eq\n7\n" },
        { "remove-protocol-before-read", "class Key:\n def __hash__(self):return hash('x')\n def __eq__(self,other):raise AssertionError('withdrawn')\nd={Key():7}\ndef key():\n d.clear()\n d['x']=9\n return 'x'\nprint(d[key()])", "9\n" },
        { "dynamic-key", "class Key:\n def __hash__(self):return hash('x')\n def __eq__(self,other):print('eq');return other=='x'\nd={'x':7}\nprint(d[Key()])", "eq\n7\n" },
        { "stored-protocol", "class Key:\n def __hash__(self):return hash('x')\n def __eq__(self,other):print('eq');return other=='x'\nd={Key():7}\nprint(d['x'])", "eq\n7\n" },
        { "custom-receiver", "events=[]\nclass Mapping:\n def __getitem__(self,key):events.append(key);return 7\nprint(Mapping()['x'],events)", "7 ['x']\n" },
        { "other-mappings", "from collections import Counter,defaultdict,ChainMap\nprint(Counter()['x'],defaultdict(lambda:7)['x'],ChainMap({'x':9})['x'])", "0 7 9\n" },
        { "key-failure", "events=[]\nd={'x':7}\nvalue='before'\ndef key():events.append('key');raise ValueError('bad')\ntry:value=d[key()]\nexcept ValueError:print(value,events,d)", "before ['key'] {'x': 7}\n" },
        { "alias", "value=[]\nd={'x':value}\nfound=d['x']\nd.clear()\nvalue.append(7)\nprint(found is value,found)", "True [7]\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ReadsPreservePythonValuesEffectsAndDynamicKeys(string name, string source, string expected)
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
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task KeyAndReceiverCallbacksAwaitWithoutReplayAndKeepStorageFunded(bool customReceiver, bool cancel)
    {
        const string native = """
d={'x':'a'*4096}
def key():
 global d
 d=None
 with open('events.txt','a') as target:target.write('once')
 return 'x'
return d[key()]
""";
        const string custom = """
class Mapping:
 def __init__(self):self.value='a'*4096
 def __getitem__(self,key):
  with open('events.txt','a') as target:target.write('once')
  return self.value
return Mapping()['x']
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(customReceiver ? custom : native, budget);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/events.txt", "before");
        var entered = cancel ? host.PauseWriteUntilCancellation("/events.txt") : null;
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5));
                if (ReferenceEquals(first, pending))
                {
                    using var early = await pending;
                    Assert.Fail(early.Failure?.Message ?? "Lookup completed before controlled host suspension.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                Assert.Equal("before", host.ReadText("/events.txt"));
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(!cancel, result.Success);
            if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            else
            {
                Assert.Equal(new string('a', 4096), Assert.IsType<string>(result.ReturnValue));
                Assert.True(host.CompletedAsynchronously > 0);
                Assert.True(budget.ReservedBytes > baseline);
            }
            Assert.Equal(cancel ? "before" : "beforeonce", host.ReadText("/events.txt"));
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Fact]
    public async Task RetainedLookupResultsRemainChargedAcrossSharedBudgetExecutions()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile("d={'x':'a'*4096}\nvalue=d['x']\nd=None\nreturn value", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using var first = script.Run(new MockLythonHost());
        Assert.True(first.Success, first.Failure?.Message);
        Assert.Equal(new string('a', 4096), Assert.IsType<string>(first.ReturnValue));
        var held = budget.ReservedBytes;
        Assert.True(held > baseline);
        using var second = await script.RunAsync(new MockLythonHost());
        Assert.True(second.Success, second.Failure?.Message);
        Assert.Equal(first.ReturnValue, second.ReturnValue);
        Assert.True(budget.ReservedBytes > held);
        first.Dispose();
        Assert.True(budget.ReservedBytes > baseline);
        second.Dispose();
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, "d['x']")]
    [InlineData(true, "d['x']")]
    [InlineData(false, "d.__getitem__('x')")]
    [InlineData(true, "d.__getitem__('x')")]
    public async Task SuspendedMissOrWithdrawnEntryRaisesTheOriginalKey(bool withdraw, string call)
    {
        const string source = """
class Key:
 def __hash__(self):return hash('x')
 def __eq__(self,other):
  global d
  d=None
  with open('events.txt','a') as target:target.write('eq')
  if withdraw:old.clear()
  return withdraw
key=Key()
d={key:7}
old=d
try:value=CALL
except KeyError as error:print(error.args)
print(old is d)
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source.Replace("CALL", call), budget);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/events.txt", "before");
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["withdraw"] = withdraw } };
        using (var result = await script.RunAsync(host, options))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("('x',)\nFalse\n", result.StandardOutput);
            Assert.Equal("beforeeq", host.ReadText("/events.txt"));
            Assert.True(host.CompletedAsynchronously > 0);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusedEqualityWorkCannotPublishOrRecover(bool asynchronous)
    {
        const string source = """
class Key:
 def __hash__(self):return hash('x')
 def __eq__(self,other):
  print('eq-before')
  try:unused=[0]*8192
  except MemoryError:pass
  return True
d={Key():7}
try:print(d['x'])
except BaseException:print('caught')
print('after')
""";
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 64 * 1024 };
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.ExecutionMemoryLimit, result.FailureAttribution?.Reason);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.Equal("eq-before\n", result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Fact]
    public async Task SuspendedProtocolLookupKeepsEscapingResultCharged()
    {
        const string source = """
class Key:
 def __hash__(self):return hash('x')
 def __eq__(self,other):
  with open('events.txt','a') as target:target.write('eq')
  return other=='x'
def produce():
 d={Key():['a'*4096]}
 return d['x']
return produce()
""";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/events.txt", "before");
        using (var first = await script.RunAsync(host))
        {
            Assert.True(first.Success, first.Failure?.Message);
            var values = Assert.IsAssignableFrom<System.Collections.IEnumerable>(first.ReturnValue).Cast<object?>().ToArray();
            Assert.Equal(new string('a', 4096), Assert.Single(values));
            Assert.Equal("beforeeq", host.ReadText("/events.txt"));
            Assert.True(host.CompletedAsynchronously > 0);
            var held = budget.ReservedBytes;
            Assert.True(held > baseline);
            var other = new MockLythonHost();
            other.SeedFile("/events.txt", "");
            using (var second = script.Run(other))
            {
                Assert.True(second.Success, second.Failure?.Message);
                Assert.True(budget.ReservedBytes > held);
            }
            Assert.Equal(held, budget.ReservedBytes);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusedKeyWorkCannotContinueToLookupOrPublication(bool asynchronous)
    {
        const string source = """
d={'x':7}
def key():
 try:unused=[0]*4096
 except MemoryError:pass
 return 'x'
try:print(d[key()])
except BaseException:print('caught')
print('after')
""";
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 32 * 1024 };
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.ExecutionMemoryLimit, result.FailureAttribution?.Reason);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.Empty(result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
