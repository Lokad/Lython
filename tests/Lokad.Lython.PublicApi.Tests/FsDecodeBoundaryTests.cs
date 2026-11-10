using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FsDecodeBoundaryTests
{
    [Theory]
    [InlineData("ff", 0, 1)]
    [InlineData("61ff", 1, 2)]
    [InlineData("e282", 0, 2)]
    [InlineData("eda080", 0, 1)]
    [InlineData("f4908080", 0, 1)]
    public async Task MalformedFilesystemBytesDoNotBecomeGuestStrings(string hex, int start, int end)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile($$"""
            import os
            raw = bytes.fromhex('{{hex}}')
            try:
                os.fsdecode(raw)
            except UnicodeDecodeError as error:
                print(error.encoding, error.start, error.end, error.object == raw)
            else:
                print('accepted')
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal($"utf-8 {start} {end} True\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidFilesystemTextPreservesCharactersNewlinesBomAndIdentity(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import os
            text = '\ufeffA\r\ncafé😀\0tail'
            encoded = text.encode()
            decoded = os.fsdecode(encoded)
            print(decoded == text, os.fsencode(decoded) == encoded, os.fsdecode(text) is text)
            return decoded
            """);
        Assert.True(script.IsValid);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True True True\n", result.StandardOutput);
        Assert.Equal("\uFEFFA\r\ncafé😀\0tail", result.ReturnValue);
    }

    [Fact]
    public async Task DecodedStorageRemainsFundedAcrossSharedBudgetExecutions()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile("import os\nraw = b'x' * 8192\nreturn os.fsdecode(raw)", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using (var first = script.Run(new MockLythonHost()))
        {
            Assert.True(first.Success, first.Failure?.Message);
            var retained = budget.ReservedBytes;
            Assert.True(retained > baseline);
            using (var second = await script.RunAsync(new MockLythonHost()))
            {
                Assert.True(second.Success, second.Failure?.Message);
                Assert.Equal(new string('x', 8192), first.ReturnValue);
                Assert.Equal(first.ReturnValue, second.ReturnValue);
                Assert.True(budget.ReservedBytes > retained);
            }
            Assert.Equal(retained, budget.ReservedBytes);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
