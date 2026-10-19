using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonRawIndexProtocolTests
{
    public static IEnumerable<object[]> Programs()
    {
        var programs = new (string Name, string Source, string Expected)[]
        {
            ("type-slot-over-instance", "import json\nevents=[]\nclass Index:\n def __index__(self):events.append('type');return 1\nindex=Index()\ndef spoof():events.append('instance');return 0\nindex.__index__=spoof\nprint(json.JSONDecoder().raw_decode('x7tail',index),events)", "(7, 2) ['type']\n"),
            ("type-slot-bypasses-getattribute", "import json\nevents=[]\nclass Index:\n def __index__(self):events.append('type');return 1\n def __getattribute__(self,name):\n  if name=='__index__':events.append('intercept');return lambda:0\n  return object.__getattribute__(self,name)\nprint(json.JSONDecoder().raw_decode('x7tail',Index()),events)", "(7, 2) ['type']\n"),
            ("instance-only-slot-is-ignored", "import json\nclass Index:pass\nindex=Index()\nindex.__index__=lambda:1\ntry:json.JSONDecoder().raw_decode('x7tail',index);print('accepted')\nexcept TypeError:print('type')", "type\n"),
            ("noncallable-type-slot", "import json\nclass Index:__index__=3\ntry:json.JSONDecoder().raw_decode('x7tail',Index())\nexcept TypeError as error:print(str(error))", "'int' object is not callable\n"),
            ("binding-errors-precede-index", "import json\nevents=[]\nclass Index:\n def __index__(self):events.append('index');return 1\nd=json.JSONDecoder()\ntry:d.raw_decode(None,Index())\nexcept TypeError:print('text')\ntry:d.raw_decode('x7',Index(),3)\nexcept TypeError:print('arity')\ntry:d.raw_decode('x7',idx=Index(),unknown=0)\nexcept TypeError:print('keyword')\nprint(events)", "text\narity\nkeyword\n['index']\n"),
            ("index-size-before-text-negative-after-text", "import json\nevents=[]\nclass Index:\n def __init__(self,n):self.n=n\n def __index__(self):events.append('index');return self.n\nd=json.JSONDecoder()\nfor text,index in [(None,Index(1)),(None,Index(-1)),(None,Index(1<<80)),(None,-1),(None,1<<80)]:\n try:d.raw_decode(text,index)\n except (TypeError,ValueError,OverflowError) as error:print(type(error).__name__)\nprint(events)", "TypeError\nTypeError\nOverflowError\nTypeError\nOverflowError\n['index', 'index', 'index']\n"),
            ("range-errors-and-recovery", "import json\nevents=[]\nclass Index:\n def __init__(self,n):self.n=n\n def __index__(self):events.append(self.n);return self.n\nd=json.JSONDecoder()\nfor n in [-1,1<<80,4]:\n try:d.raw_decode('x7',Index(n))\n except json.JSONDecodeError as error:print('J',error.pos)\n except OverflowError:print('O')\n except ValueError:print('V')\nprint(d.raw_decode('x7tail',idx=Index(1)),len(events))\nclass Bad:\n def __index__(self):return 1.0\ntry:d.raw_decode('x7',Bad())\nexcept TypeError as error:print(str(error))", "V\nO\nJ 4\n(7, 2) 4\n__index__ returned non-int (type float)\n"),
            ("guest-memory-error-is-catchable", "import json\nclass Index:\n def __index__(self):raise MemoryError('guest')\nd=json.JSONDecoder()\ntry:d.raw_decode('x7',Index())\nexcept MemoryError as error:print(str(error))\nprint(d.raw_decode('x7',1))", "guest\n(7, 2)\n"),
        };
        foreach (var program in programs)
        foreach (var asynchronous in new[] { false, true })
            yield return [program.Name, asynchronous, program.Source, program.Expected];
    }

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task RawIndexFollowsPythonSlotsAndValidation(string name, bool asynchronous, string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, name + ": " + result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    public static IEnumerable<object[]> HostCases()
    {
        foreach (var keyword in new[] { false, true })
        foreach (var descriptor in new[] { false, true })
        foreach (var flag in new[] { false, true })
            yield return [keyword, descriptor, flag];
    }

    private static string HostSource(bool keyword, bool descriptor)
    {
        var index = descriptor
            ? "class Slot:\n def __get__(self,instance,owner):touch();return lambda:2\nclass Index:__index__=Slot()\n"
            : "class Index:\n def __index__(self):touch();return 2\n";
        return "import json\ndef touch():\n with open('events.txt','a') as target:target.write('seen')\n" + index +
            "print(json.JSONDecoder().raw_decode('λ😀7tail'," + (keyword ? "idx=" : "") + "Index()))";
    }

    [Theory]
    [MemberData(nameof(HostCases))]
    public async Task CompletedHostsPreserveEffectsAndCharacterOffsets(bool keyword, bool descriptor, bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(HostSource(keyword, descriptor), budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new MockLythonHost(); host.SeedFile("/events.txt", "before");
        using (var result = asynchronous ? await script.RunAsync(host) : script.Run(host))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("(7, 3)\n", result.StandardOutput);
            Assert.Equal("beforeseen", host.ReadText("/events.txt"));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [MemberData(nameof(HostCases))]
    public async Task IndexAndDescriptorAwaitIOAndCancellation(bool keyword, bool descriptor, bool cancel)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(HostSource(keyword, descriptor), budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost(); host.SeedFile("/events.txt", "before");
        using var cancellation = new CancellationTokenSource();
        var entered = cancel ? host.PauseWriteUntilCancellation("/events.txt") : null;
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5));
                if (ReferenceEquals(first, pending))
                {
                    using var early = await pending;
                    Assert.Fail(early.Failure?.Message ?? "Index coercion did not reach its pending write.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(!cancel, result.Success);
            Assert.Equal(cancel ? "" : "(7, 3)\n", result.StandardOutput);
            Assert.Equal(cancel ? "before" : "beforeseen", host.ReadText("/events.txt"));
            if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [MemberData(nameof(HostCases))]
    public async Task RefusedIndexAdmissionStopsTheCommand(bool keyword, bool descriptor, bool asynchronous)
    {
        var body = HostSource(keyword, descriptor).Replace("with open('events.txt','a') as target:target.write('seen')", "print('entered');payload='x'*2000000");
        var source = "try:\n" + string.Join("\n", body.Split('\n').Select(line => " " + line)) +
            "\nexcept MemoryError:print('caught')\nprint('after')";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 64 * 1024 };
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
        {
            Assert.False(result.Success);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.Equal(LythonFailureReason.ExecutionMemoryLimit, result.FailureAttribution?.Reason);
            Assert.Equal("entered\n", result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
