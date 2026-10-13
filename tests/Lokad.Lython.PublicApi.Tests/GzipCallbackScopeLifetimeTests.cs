using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GzipCallbackScopeLifetimeTests
{
    private const string Member = "1f8b08007b00000002ff4bcc29c848e44a4a2d49e402006e50306e0b000000";

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task EscapingReaderDropsCreationLocalsAndFundsRetainedAliases(bool asynchronous, bool close, bool retain)
    {
        var script = new LythonEngine().Compile($$"""
import gzip, io
def acquire():
    unrelated = 'x' * 16000
    assert len(locals()['unrelated']) == 16000
    reader = gzip.GzipFile(fileobj=io.BytesIO(bytes.fromhex('{{Member}}')))
    if {{(close ? "True" : "False")}}: reader.close()
    return reader, unrelated if {{(retain ? "True" : "False")}} else None
reader, kept = acquire()
with open('/checkpoint', 'rb') as checkpoint:
    assert checkpoint.read(1) == b'g'
print('gate')
candidate = 'y' * 16000
reader.close()
return len(candidate)
""");
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        var delayed = asynchronous ? new DelayedLythonHost() : null;
        var mock = asynchronous ? null : new MockLythonHost();
        ILythonHost host = (ILythonHost?)delayed ?? mock!;
        if (delayed is not null) delayed.SeedBytes("/checkpoint", [(byte)'g']);
        else mock!.SeedBytes("/checkpoint", [(byte)'g']);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 28000 };
        var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
        if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
        Assert.Equal("gate\n", result.StandardOutput);
        Assert.InRange(result.PeakExecutionMemoryBytes, 16000, 28000);
        if (retain)
        {
            Assert.False(result.Success);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.Equal(LythonFailureReason.ExecutionMemoryLimit, result.Failure?.Attribution?.Reason);
        }
        else
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(new BigInteger(16000), result.ReturnValue);
        }
    }

    [Theory]
    [InlineData("next", false)]
    [InlineData("next", true)]
    [InlineData("for", false)]
    [InlineData("for", true)]
    [InlineData("list", false)]
    [InlineData("list", true)]
    [InlineData("generator", false)]
    [InlineData("generator", true)]
    [InlineData("returned", false)]
    [InlineData("returned", true)]
    [InlineData("with", false)]
    [InlineData("with", true)]
    [InlineData("bound", false)]
    [InlineData("bound", true)]
    [InlineData("writelines", false)]
    [InlineData("writelines", true)]
    [InlineData("reentrant", false)]
    [InlineData("reentrant", true)]
    [InlineData("closed", false)]
    [InlineData("closed", true)]
    public async Task ProtocolEntriesPreserveIdentityContentAndYieldedCallbacks(string entry, bool asynchronous)
    {
        var body = entry switch
        {
            "next" => "assert iter(reader) is reader\nassert next(reader) == b'alpha\\n'\nassert next(reader) == b'beta\\n'\nassert next(reader, None) is None",
            "for" => "lines=[]\nfor line in reader: lines.append(line)\nassert b''.join(lines) == b'alpha\\nbeta\\n'",
            "list" => "assert list(reader) == [b'alpha\\n', b'beta\\n']",
            "generator" => "assert sum(len(line) for line in reader) == 11",
            "returned" => "class Lines:\n    def __iter__(self): return reader\nassert list(Lines()) == [b'alpha\\n', b'beta\\n']",
            "with" => "with reader as entered:\n    assert entered is reader\n    assert entered.readlines() == [b'alpha\\n', b'beta\\n']\nassert reader.closed",
            "bound" => "pull=reader.__next__\nassert pull.__self__ is reader\nassert pull() == b'alpha\\n'\nassert pull() == b'beta\\n'",
            "reentrant" => "assert reader.read() == b'alpha\\nbeta\\n'",
            "closed" => "reader.close()\ntry: iter(reader)\nexcept ValueError: pass\nelse: raise AssertionError('closed iteration accepted')",
            _ => "sink=io.BytesIO()\nwriter=gzip.GzipFile(fileobj=sink, mode='wb', mtime=0)\nwriter.writelines(reader)\nwriter.close()\nassert gzip.decompress(sink.getvalue()) == b'alpha\\nbeta\\n'",
        };
        var reentrant = entry == "reentrant" ? """
        for action in [lambda: reader.read(0), lambda: reader.__exit__(None, None, None)]:
            try: action()
            except RuntimeError as error: assert 'reentrant' in str(error)
            else: raise AssertionError('reentrant operation accepted')
""" : "";
        var script = new LythonEngine().Compile($$"""
import gzip, io
class Source:
    def __init__(self): self.raw=io.BytesIO(bytes.fromhex('{{Member}}'))
    def read(self, size):
        with open('/checkpoint', 'rb') as checkpoint:
            assert checkpoint.read(1) == b'g'
{{reentrant}}
        return self.raw.read(size)
source=Source()
reader=gzip.GzipFile(fileobj=source)
{{body}}
reader.close()
assert not source.raw.closed
print('ok')
""");
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        var delayed = asynchronous ? new DelayedLythonHost() : null;
        var mock = asynchronous ? null : new MockLythonHost();
        ILythonHost host = (ILythonHost?)delayed ?? mock!;
        if (delayed is not null) delayed.SeedBytes("/checkpoint", [(byte)'g']);
        else mock!.SeedBytes("/checkpoint", [(byte)'g']);
        var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("ok\n", result.StandardOutput);
        if (delayed is not null && entry != "closed") Assert.True(delayed.CompletedAsynchronously > 0);
    }
}
