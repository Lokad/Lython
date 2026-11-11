using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class PreparedCallArgumentTests
{
    // Expected output is from CPython 3.13. Binding and descriptor selection
    // precede consumption of the borrowed arguments in both hosting APIs.
    public static TheoryData<string, string, string> Cases => new()
    {
        { "descriptor-returned-function", "events=[]\ndef initialize(value): events.append(value)\nclass Descriptor:\n def __get__(self,instance,owner): return initialize\nclass C: __init__=Descriptor()\nC(7)\nprint(events)\n", "[7]\n" },
        { "variance-order", "from typing import TypeVar\nevents=[]\nclass Flag:\n def __init__(self,name): self.name=name\n def __bool__(self): events.append(self.name); return False\nT=TypeVar('T',contravariant=Flag('contra'),infer_variance=Flag('infer'),covariant=Flag('co'))\nprint(events)\n", "['co', 'contra', 'infer']\n" },
        { "typevar-invalid-keyword", "from typing import TypeVar\nevents=[]\nclass Flag:\n def __bool__(self): events.append('truth'); return True\ndef call(factory):\n try: factory('T',covariant=Flag(),garbage=True)\n except TypeError: print('TypeError')\ncall(TypeVar)\nprint(events)\n", "TypeError\n[]\n" },
        { "dataclass-invalid-keyword", "from dataclasses import make_dataclass\nevents=[]\nclass Flag:\n def __bool__(self): events.append('truth'); return True\ndef call(factory):\n try: factory('C',[],init=Flag(),garbage=True)\n except TypeError: print('TypeError')\ncall(make_dataclass)\nprint(events)\n", "TypeError\n[]\n" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task PreparationPreservesPythonBindingAndCleanup(string name, string source, string expected)
    {
        _ = name;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(';', script.Diagnostics.Select(d => d.Message)));
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
    [InlineData(false)]
    [InlineData(true)]
    public async Task InitializerDescriptorKeepsInputsFundedThroughSuspensionAndCancellation(bool cancel)
    {
        const string source = """
            def initialize(value): pass
            class Descriptor:
                def __get__(self,instance,owner):
                    with open('/gate','a') as output: output.write('selected')
                    return initialize
            class C: __init__=Descriptor()
            value=[7]
            C(value)
            print(value)
            return value
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/gate", "before:");
        var entered = cancel ? host.PauseWriteUntilCancellation("/gate") : null;
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                Assert.Same(entered, await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(10)));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using (var result = await pending.WaitAsync(TimeSpan.FromSeconds(10)))
            {
                if (cancel) Assert.False(result.Success);
                else
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(new BigInteger(7), Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<object?>>(result.ReturnValue)));
                    Assert.True(budget.ReservedBytes > baseline);
                }
                Assert.Equal(cancel ? "" : "[7]\n", result.StandardOutput);
                Assert.Equal(cancel ? "before:" : "before:selected", host.ReadText("/gate"));
                Assert.True(host.CompletedAsynchronously > 0);
            }
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
