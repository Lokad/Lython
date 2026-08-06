using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class GzipModuleFunctionTests
{
    [Fact]
    public void CompressUsesDeterministicPythonCompatibleFraming()
    {
        var result = new LythonEngine().Run(
            """
import gzip

payload = gzip.compress(b"hello", mtime=0)
again = gzip.compress(b"hello", mtime=0)
level1 = gzip.compress(b"hello", 1, mtime=16909060)
defaulted = gzip.compress(b"hello")
checks = [
    payload == again,
    payload == b"\x1f\x8b\x08\x00\x00\x00\x00\x00\x02\xff\xcbH\xcd\xc9\xc9\x07\x00\x86\xa6\x106\x05\x00\x00\x00",
    payload[0:4] == b"\x1f\x8b\x08\x00",
    payload[4:8] == b"\x00\x00\x00\x00",
    payload[8] == 2,
    payload[9] == 255,
    level1[4:8] == b"\x04\x03\x02\x01",
    level1[8] == 4,
    defaulted[4:8] == b"\x00\x00\x00\x00",
    gzip.decompress(payload) == b"hello",
]
return str(checks)
""",
            new MockLythonHost());

        Assert.True(result.Success, Describe(result));
        Assert.Equal("[True, True, True, True, True, True, True, True, True, True]", result.ReturnValue);
    }

    [Fact]
    public void DecompressAcceptsPythonAndConcatenatedMembers()
    {
        var result = new LythonEngine().Run(
            """
import gzip

python_hello = b"\x1f\x8b\x08\x00\x00\x00\x00\x00\x02\xff\xcbH\xcd\xc9\xc9\x07\x00\x86\xa6\x106\x05\x00\x00\x00"
concatenated = b"\x1f\x8b\x08\x00\x01\x00\x00\x00\x02\xffK\xcc)\xc8H\x04\x00j9\xe0\xd0\x05\x00\x00\x00\x1f\x8b\x08\x00\x02\x00\x00\x00\x02\xffKJ-I\x04\x00c\x04\x91\x8f\x04\x00\x00\x00"
checks = [
    gzip.decompress(python_hello) == b"hello",
    gzip.decompress(concatenated) == b"alphabeta",
    gzip.decompress(b"") == b"",
]
return str(checks)
""",
            new MockLythonHost());

        Assert.True(result.Success, Describe(result));
        Assert.Equal("[True, True, True]", result.ReturnValue);
    }

    [Fact]
    public async Task AsyncExecutionHasPureGzipParity()
    {
        var result = await new LythonEngine().RunAsync(
            """
import gzip
return gzip.decompress(gzip.compress(b"alpha", -1, mtime=7))
""",
            new MockLythonHost());

        Assert.True(result.Success, Describe(result));
        Assert.Equal(new byte[] { 0x61, 0x6c, 0x70, 0x68, 0x61 }, Assert.IsType<byte[]>(result.ReturnValue));
    }

    [Fact]
    public void InvalidHeadersCrcAndTruncationRaiseCatchableBadGzipFile()
    {
        var result = new LythonEngine().Run(
            """
import gzip

valid = b"\x1f\x8b\x08\x00\x00\x00\x00\x00\x02\xff\xcbH\xcd\xc9\xc9\x07\x00\x86\xa6\x106\x05\x00\x00\x00"
bad_crc = b"\x1f\x8b\x08\x00\x00\x00\x00\x00\x02\xff\xcbH\xcd\xc9\xc9\x07\x00\x87\xa6\x106\x05\x00\x00\x00"
values = []
for payload in [b"not gzip", bad_crc, valid[:-1]]:
    try:
        gzip.decompress(payload)
    except gzip.BadGzipFile as ex:
        values.append(ex.type)

try:
    gzip.decompress(b"not gzip")
except OSError as ex:
    values.append("os:" + ex.type)

try:
    raise gzip.BadGzipFile("manual")
except OSError as ex:
    values.append(ex.type + ":" + ex.message)
return "|".join(values)
""",
            new MockLythonHost());

        Assert.True(result.Success, Describe(result));
        Assert.Equal("BadGzipFile|BadGzipFile|BadGzipFile|os:BadGzipFile|BadGzipFile:manual", result.ReturnValue);
    }

    [Fact]
    public void DecompressionExpansionCountsAgainstExecutionMemory()
    {
        var result = new LythonEngine().Run(
            """
import gzip
gzip.decompress(b"\x1f\x8b\x08\x00\x00\x00\x00\x00\x02\xffKL\x1c\x05\xa3`\x14\x8cT\x00\x00\xb9\x97U|\x00\x04\x00\x00")
""",
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 512 });

        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        Assert.Contains("execution memory budget exceeded (512)", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CompressionArgumentsValidateLikePython()
    {
        var result = new LythonEngine().Run(
            """
import gzip

values = []
try:
    gzip.compress("text")
except TypeError as ex:
    values.append(ex.type)
try:
    gzip.compress(b"x", 10)
except ValueError as ex:
    values.append(ex.type + ":" + ex.message)
try:
    gzip.compress(b"x", mtime=-1)
except OverflowError as ex:
    values.append(ex.type)
try:
    gzip.decompress("text")
except TypeError as ex:
    values.append(ex.type)
return "|".join(values)
""",
            new MockLythonHost());

        Assert.True(result.Success, Describe(result));
        Assert.Equal("TypeError|ValueError:Bad compression level|OverflowError|TypeError", result.ReturnValue);
    }

    [Fact]
    public void StaticContractsRecognizePureGzipCallShapes()
    {
        var valid = new LythonEngine().Compile(
            """
import gzip
from gzip import compress, decompress, BadGzipFile

payload = compress(b"abc", 6, mtime=0)
decompress(payload)
""");
        var invalid = new LythonEngine().Compile(
            """
import gzip
gzip.compress()
gzip.compress(b"a", 1, 0)
gzip.decompress()
gzip.decompress(b"a", b"b")
""");

        Assert.True(valid.IsValid, string.Join(" | ", valid.Diagnostics.Select(d => d.Message)));
        Assert.False(invalid.IsValid);
        Assert.Equal(4, invalid.Diagnostics.Count(d => d.Code == "LA3151"));
    }

    private static string Describe(LythonExecutionResult result)
        => result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message));
}
