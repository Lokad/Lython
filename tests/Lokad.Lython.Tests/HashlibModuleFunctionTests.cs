using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class HashlibModuleFunctionTests
{
    private const string VectorSource = """
import hashlib

return "|".join([
    hashlib.md5(b"abc").hexdigest(),
    hashlib.sha1(b"abc").hexdigest(),
    hashlib.sha256(b"abc").hexdigest(),
    hashlib.sha384(b"abc").hexdigest(),
    hashlib.sha512(b"abc").hexdigest(),
])
""";

    private const string VectorResult = "900150983cd24fb0d6963f7d28e17f72|a9993e364706816aba3e25717850c26c9cd0d89d|ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad|cb00753f45a35e8bb5a03d699ac65007272c32ab0eded1631a8b605a43ff5bed8086072ba1e7cc2358baeca134c825a7|ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f";

    [Fact]
    public void ManagedAlgorithmsMatchStandardVectors()
    {
        var result = new LythonEngine().Run(VectorSource, new MockLythonHost());

        Assert.True(result.Success, Describe(result));
        Assert.Equal(VectorResult, result.ReturnValue);
    }

    [Fact]
    public async Task AsyncExecutionHasTheSamePureHashResults()
    {
        var result = await new LythonEngine().RunAsync(VectorSource, new MockLythonHost());

        Assert.True(result.Success, Describe(result));
        Assert.Equal(VectorResult, result.ReturnValue);
    }

    [Fact]
    public void HashObjectsSupportChunkingCopyRawDigestAndMetadata()
    {
        var result = new LythonEngine().Run(
            """
import hashlib

base = hashlib.sha256(b"a")
copy = base.copy()
updated = base.update(b"b")
copy.update(b"c")
raw = hashlib.md5(b"abc").digest()
return "|".join([
    str(updated is None),
    base.hexdigest(),
    copy.hexdigest(),
    str(raw == b"\x90\x01P\x98<\xd2O\xb0\xd6\x96?}(\xe1\x7fr"),
    base.name,
    str(base.digest_size),
    str(base.block_size),
    hashlib.sha512().name,
    str(hashlib.sha512().digest_size),
    str(hashlib.sha512().block_size),
])
""",
            new MockLythonHost());

        Assert.True(result.Success, Describe(result));
        Assert.Equal(
            "True|fb8e20fc2e4c3f248c60c39bd652f3c1347298bb977b8b4d5903b85055620603|f45de51cdef30991551e41e882dd7b5404799648a0a00753f44fc966e6153fc1|True|sha256|32|64|sha512|64|128",
            result.ReturnValue);
    }

    [Fact]
    public void NewAliasesSecurityKeywordAndAlgorithmInventoriesArePythonShaped()
    {
        var result = new LythonEngine().Run(
            """
import hashlib

checks = [
    hashlib.new("SHA-256", b"abc", usedforsecurity=False).hexdigest() == hashlib.sha256(string=b"abc", usedforsecurity=True).hexdigest(),
    hashlib.md5(usedforsecurity=False).hexdigest() == hashlib.md5(b"").hexdigest(),
    hashlib.algorithms_available == hashlib.algorithms_guaranteed,
    sorted(hashlib.algorithms_guaranteed) == ["md5", "sha1", "sha256", "sha384", "sha512"],
]
return str(checks)
""",
            new MockLythonHost());

        Assert.True(result.Success, Describe(result));
        Assert.Equal("[True, True, True, True]", result.ReturnValue);
    }

    [Fact]
    public void UnsupportedAlgorithmsAndNonBytesInputsFailPrecisely()
    {
        var result = new LythonEngine().Run(
            """
import hashlib

values = []
try:
    hashlib.new("sha3_256")
except ValueError as ex:
    values.append(ex.type + ":" + ex.message)
try:
    hashlib.sha256("abc")
except TypeError as ex:
    values.append(ex.type)
try:
    hashlib.md5(None)
except TypeError as ex:
    values.append(ex.type)
h = hashlib.sha1()
try:
    h.update([1, 2, 3])
except TypeError as ex:
    values.append(ex.type)
return "|".join(values)
""",
            new MockLythonHost());

        Assert.True(result.Success, Describe(result));
        Assert.Equal("ValueError:unsupported hash type sha3_256|TypeError|TypeError|TypeError", result.ReturnValue);
    }

    [Fact]
    public void FileDigestReportsTheGenericBinaryHandleBoundary()
    {
        var result = new LythonEngine().Run(
            """
import hashlib
hashlib.file_digest(None, "sha256")
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.Equal("NotImplementedError", result.Failure?.ExceptionType);
        Assert.Contains("generic binary file handles", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RetainedHashInputAndCopiesCountAgainstExecutionMemory()
    {
        var result = new LythonEngine().Run(
            """
import hashlib
h = hashlib.sha256(b"abc")
h.copy()
""",
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 200 });

        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        Assert.Contains("execution memory budget exceeded (200)", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StaticContractsRecognizeHashlibCallShapes()
    {
        var valid = new LythonEngine().Compile(
            """
import hashlib
from hashlib import sha256

hashlib.md5(usedforsecurity=False)
sha256(b"abc", usedforsecurity=True)
hashlib.new("sha1", data=b"abc", usedforsecurity=False)
""");
        var invalid = new LythonEngine().Compile(
            """
import hashlib
hashlib.md5(b"a", False)
hashlib.new()
hashlib.new("sha1", b"a", False)
hashlib.file_digest(None)
""");

        Assert.True(valid.IsValid, string.Join(" | ", valid.Diagnostics.Select(d => d.Message)));
        Assert.False(invalid.IsValid);
        Assert.Equal(4, invalid.Diagnostics.Count(d => d.Code == "LA3151"));
    }

    private static string Describe(LythonExecutionResult result)
        => result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message));
}
