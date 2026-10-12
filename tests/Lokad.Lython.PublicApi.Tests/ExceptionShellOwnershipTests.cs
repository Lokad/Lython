using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ExceptionShellOwnershipTests
{
    public static IEnumerable<object[]> Cases()
    {
        var cases = new[]
        {
            ("for i in range(5000):\n    try:\n        raise ValueError()\n    except ValueError as e:\n        pass\nprint('done')", true),
            ("e = ValueError()\nfor i in range(5000):\n    try:\n        raise e\n    except ValueError as alias:\n        pass\nprint('done')", true),
            ("import sys\nfor i in range(5000):\n    try:\n        1 / 0\n    except ZeroDivisionError:\n        e = sys.exc_info()[1]\nprint('done')", true),
            ("a = ValueError()\nb = ValueError()\na.__cause__ = b\nb.__context__ = a\nfor i in range(5000):\n    try:\n        raise a\n    except ValueError as e:\n        pass\nprint('done')", true),
            ("es = []\nfor i in range(400):\n    es.append(ValueError())\nprint('done')", false),
            ("es = []\nfor i in range(400):\n    try:\n        raise ValueError()\n    except ValueError as e:\n        es.append(e)\nprint('done')", false),
            ("previous = None\nfor i in range(400):\n    e = ValueError()\n    e.__cause__ = previous\n    previous = e\nprint('done')", false),
            ("import sys\nes = []\nfor i in range(400):\n    try:\n        1 / 0\n    except ZeroDivisionError:\n        es.append(sys.exc_info()[1])\nprint('done')", false),
        };
        foreach (var (source, success) in cases)
        foreach (var asynchronous in new[] { false, true }) yield return [source, success, asynchronous];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task DroppedReboundAndRetainedValuesObeyOneFixedLiveBudget(string source, bool success, bool asynchronous)
    {
        var host = new MockLythonHost();
        var timing = new YieldTiming();
        host.SetTiming(timing);
        if (asynchronous) source = "import time\ntime.sleep(0)\n" + source;
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
        Assert.Equal(success, result.Success);
        Assert.True(result.PeakExecutionMemoryBytes <= 65536);
        if (success)
        {
            Assert.Equal("done\n", result.StandardOutput);
            Assert.Equal(0, result.DeniedReservationBytes);
        }
        else
        {
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.Equal(LythonFailureReason.ExecutionMemoryLimit, result.FailureAttribution?.Reason);
        }
        if (asynchronous) Assert.Equal(1, timing.CompletedAsynchronously);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GuestExitCanDropOrRetainAnUnboundControlException(bool retain)
    {
        var source = """
saved = []
class CM:
    def __enter__(self):
        return self
    def __exit__(self, kind, value, trace):
        if retain:
            saved.append(value)
        return True
for i in range(400):
    with CM():
        1 / 0
print('done')
""";
        var script = new LythonEngine().Compile(source);
        foreach (var asynchronous in new[] { false, true })
        {
            var options = new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 65536,
                Globals = new Dictionary<string, object?> { ["retain"] = retain },
            };
            var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
            Assert.Equal(!retain, result.Success);
            if (retain) Assert.Equal(LythonFailureReason.ExecutionMemoryLimit, result.FailureAttribution?.Reason);
            else Assert.Equal("done\n", result.StandardOutput);
        }
    }

    private sealed class YieldTiming : ILythonTiming
    {
        public long MonotonicNanoseconds => 0;
        public int CompletedAsynchronously { get; private set; }
        public async ValueTask DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            CompletedAsynchronously++;
        }
    }
}
