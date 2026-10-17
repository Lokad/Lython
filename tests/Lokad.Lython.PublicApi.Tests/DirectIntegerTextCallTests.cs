using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DirectIntegerTextCallTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "ordinary_text_bytes", "print(int('42'), int(b'-7'), int(' \\t+0012\\n'))", "42 -7 12\n" },
        { "aliases", "convert=int\nprint(convert('42'),convert(b'-7'))", "42 -7\n" },
        { "shadowed_builtin", "original=int\ndef int(value):return 99\nprint(int('4'),original('4'))", "99 4\n" },
        { "argument_rebind", "events=[]\ndef argument():\n global int\n events.append('argument')\n int=lambda value:99\n return '4'\nprint(int(argument()),int('5'),events)", "4 99 ['argument']\n" },
        { "property_callable", "events=[]\nclass Holder:\n @property\n def convert(self):\n  events.append('lookup')\n  return int\nprint(Holder().convert('7'),events)", "7 ['lookup']\n" },
        { "returned_callable", "def choose():return int\nprint(choose()(b'7'))", "7\n" },
        { "general_stars_bases", "print(int(*('7',)),int('ff',base=16),int('11',2),int('11',**{'base':2}))", "7 255 3 3\n" },
        { "argument_error_effects", "events=[]\ndef arg(label,value):events.append(label);return value\nfor action in [lambda:int(arg('bad','x')),lambda:int(arg('first','7'),arg('base',10),arg('extra',1)),lambda:int(x=arg('kw','7'))]:\n try:action()\n except (TypeError,ValueError) as error:events.append(type(error).__name__)\nprint(events)", "['bad', 'ValueError', 'first', 'base', 'extra', 'TypeError', 'kw', 'TypeError']\n" },
        { "dynamic_protocol", "events=[]\nclass Value:\n @property\n def __int__(self):\n  events.append('bind')\n  def convert():\n   events.append('call')\n   return 7\n  return convert\nprint(int(Value()),events)", "7 ['bind', 'call']\n" },
        { "dynamic_base", "events=[]\nclass Base:\n def __index__(self):events.append('base');return 16\nprint(int('ff',Base()),events)", "255 ['base']\n" },
        { "major_results", "source='9'*101\nprint(int(source).bit_length(),int(b'9'*101).bit_length())", "336 336\n" },
        { "integer_alias", "source=10**30\nprint(int(source) is source,int(True))", "True 1\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task OrdinaryAndGeneralConversionsPreservePythonEffects(string label, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, label);
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
    [InlineData(false)]
    [InlineData(true)]
    public async Task HostReadCompletesOrCancelsBeforeIntegerConversion(bool cancel)
    {
        const string source = "def receive():\n print('input')\n with open('/value') as stream:return stream.read()\nprint(int(receive()))\nprint('tail')";
        var payload = new string('9', 101);
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/value", payload);
        var entered = cancel ? host.PauseReadUntilCancellation("/value") : null;
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Same(entered, first);
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(!cancel, result.Success);
            Assert.Equal(cancel ? "input\n" : "input\n" + payload + "\ntail\n", result.StandardOutput);
            if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            else Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
