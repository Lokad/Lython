using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RootLifetimeContainmentTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var root in new[] { "call-argument", "caller-local", "closure", "generator", "active-exception", "native-accumulator", "bound-buffer" })
        foreach (var constrained in new[] { true, false })
        foreach (var asynchronous in new[] { false, true })
            yield return [root, constrained, asynchronous];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task LiveStorageRemainsFundedAcrossImplicitExecutionRoots(string root, bool constrained, bool asynchronous)
    {
        // Each first payload has lost a direct namespace binding by the time
        // allocating_rhs runs, or lives in a caller/suspended frame. It must
        // remain funded even while genuine host I/O suspends the current call.
        var payloadBytes = root == "bound-buffer" ? 32000 : 16000;
        var prefix = """
def allocating_rhs():
    with open('/gate', 'rb') as gate:
        assert gate.read(1) == b'g'
    print('gate')
    return 'y' * 16000

""".Replace("'y' * 16000", "'y' * " + payloadBytes);
        var body = root switch
        {
            "call-argument" => """
owners = ['x' * 16000]
def consume(left, right):
    return len(left) + len(right)
return consume(owners.pop(), allocating_rhs())
""",
            "caller-local" => """
def outer():
    saved = 'x' * 16000
    candidate = allocating_rhs()
    return len(saved) + len(candidate)
return outer()
""",
            "closure" => """
def factory():
    saved = 'x' * 16000
    return lambda: len(saved)
reader = factory()
candidate = allocating_rhs()
return reader() + len(candidate)
""",
            "generator" => """
def chunks():
    saved = 'x' * 16000
    yield None
    yield saved
cursor = chunks()
next(cursor)
candidate = allocating_rhs()
return len(next(cursor)) + len(candidate)
""",
            "active-exception" => """
try:
    raise ValueError('x' * 16000)
except ValueError:
    return len(allocating_rhs())
""",
            "native-accumulator" => """
class Total:
    def __init__(self, payload):
        self.payload = payload
    def __add__(self, ignored):
        return self
class First:
    def __radd__(self, ignored):
        return Total('x' * 16000)
def values():
    yield First()
    yield allocating_rhs()
return len(sum(values()).payload)
""",
            "bound-buffer" => """
import io
read = io.BytesIO(b'x' * 16000).getvalue
candidate = allocating_rhs()
return len(read()) + len(candidate)
""",
            _ => throw new ArgumentOutOfRangeException(nameof(root))
        };
        var script = new LythonEngine().Compile(prefix + body);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        var delayed = asynchronous ? new DelayedLythonHost() : null;
        var mock = asynchronous ? null : new MockLythonHost();
        ILythonHost host = (ILythonHost?)delayed ?? mock!;
        if (delayed is not null) delayed.SeedBytes("/gate", [(byte)'g']);
        else mock!.SeedBytes("/gate", [(byte)'g']);
        var cap = constrained ? (root == "bound-buffer" ? 44000L : 28000L) : 120000L;
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = cap };
        LythonExecutionResult result;
        if (delayed is not null)
        {
            var pending = script.RunAsync(host, options);
            Assert.False(pending.IsCompleted);
            result = await pending;
            Assert.True(delayed.CompletedAsynchronously > 0);
        }
        else result = script.Run(host, options);

        // The read and its close completed before the second payload preflight.
        Assert.Equal("gate\n", result.StandardOutput);
        Assert.Empty(result.StandardError);
        Assert.InRange(result.PeakExecutionMemoryBytes, 16000, cap);
        if (constrained)
        {
            Assert.False(result.Success);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            var attribution = result.FailureAttribution!.Value;
            Assert.Equal(LythonFailureReason.ExecutionMemoryLimit, attribution.Reason);
            Assert.Equal(cap, attribution.Limit);
            Assert.True(attribution.Observed >= 16000);
            Assert.True(attribution.Requested >= payloadBytes);
        }
        else
        {
            Assert.True(result.Success, result.Failure?.Message);
            var expected = root is "active-exception" or "native-accumulator" ? 16000 : 16000 + payloadBytes;
            Assert.Equal(new BigInteger(expected), result.ReturnValue);
        }
    }
}
