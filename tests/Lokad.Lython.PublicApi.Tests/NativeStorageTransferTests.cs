using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeStorageTransferTests
{
    [Theory]
    [InlineData("latin-1", false)] [InlineData("latin-1", true)]
    [InlineData("ascii", false)] [InlineData("ascii", true)]
    [InlineData("cp1252", false)] [InlineData("cp1252", true)]
    [InlineData("utf-16-le", false)] [InlineData("utf-16-le", true)]
    [InlineData("utf-16-be", false)] [InlineData("utf-16-be", true)]
    [InlineData("pack", false)] [InlineData("pack", true)]
    [InlineData("unpack", false)] [InlineData("unpack", true)]
    public async Task AdmittedBackingTransfersToAnEscapingResult(string operation, bool asynchronous)
    {
        const int length = 20000;
        var input = operation switch
        {
            "utf-16-le" => string.Concat(Enumerable.Repeat("x\\x00", length)),
            "utf-16-be" => string.Concat(Enumerable.Repeat("\\x00x", length)),
            _ => new string('x', length)
        };
        var expression = operation switch
        {
            "pack" => "struct.pack('>20000s',b'" + input + "')",
            "unpack" => "struct.unpack('>20000s',b'" + input + "')[0]",
            _ => "b'" + input + "'.decode('" + operation + "')"
        };
        var compilation = new LythonMemoryBudget();
        using var script = new LythonEngine().Compile("import struct\nreturn " + expression, compilation);
        Assert.True(script.IsValid);
        var baseline = compilation.ReservedBytes;
        var budget = new LythonMemoryBudget(65536);
        for (var pass = 0; pass < 2; pass++)
        {
            using (var result = asynchronous
                ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                if (operation is "pack" or "unpack") Assert.Equal(Enumerable.Repeat((byte)'x', length), Assert.IsType<byte[]>(result.ReturnValue));
                else Assert.Equal(new string('x', length), result.ReturnValue);
                Assert.True(budget.ReservedBytes > 0);
            }
            Assert.Equal(0, budget.ReservedBytes);
            Assert.Equal(baseline, compilation.ReservedBytes);
        }
    }
}
