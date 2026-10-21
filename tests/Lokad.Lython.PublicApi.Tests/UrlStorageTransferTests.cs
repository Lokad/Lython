using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class UrlStorageTransferTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RefusedEscapingCopyReturnsAStructuredFailure(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("import urllib.parse\nreturn urllib.parse.unquote_to_bytes('" + new string('x', 20000) + "')");
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(32768);
        for (var pass = 0; pass < 2; pass++)
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                                            : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Equal("ProjectionError", result.Failure?.ExceptionType);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("quote", 32768, 20000)]
    [InlineData("quote-bytes", 32768, 20000)]
    [InlineData("unquote", 32768, 20000)]
    [InlineData("ascii-result", 32768, 20000)]
    [InlineData("utf16-result", 65536, 40000)]
    public async Task PrivateBackingIsAdmittedOnceAndTransfersToItsValue(string operation, long allowance, int expectedLength)
    {
        var text = new string('x', 20000);
        var expression = operation switch
        {
            "quote" => "urllib.parse.quote('" + text + "')",
            "quote-bytes" => "urllib.parse.quote_from_bytes(b'" + text + "')",
            "unquote" => "urllib.parse.unquote_to_bytes('" + text + "')",
            _ => "urllib.parse.SplitResult('','','" + text + "','','').encode('" + (operation == "ascii-result" ? "ascii" : "utf-16-le") + "').path"
        };
        using var script = new LythonEngine().Compile("import urllib.parse\nreturn len(" + expression + ")");
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            var budget = new LythonMemoryBudget(allowance);
            for (var pass = 0; pass < 2; pass++)
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                                                : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(new BigInteger(expectedLength), result.ReturnValue);
                }
                Assert.Equal(0, budget.ReservedBytes);
            }
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EscapingQuotedResultsKeepTheirSharedAllowance(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("import urllib.parse\nreturn urllib.parse.unquote_to_bytes('" + new string('x', 20000) + "')");
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(65536);
        var options = new LythonRunOptions { MemoryBudget = budget };
        using var first = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
        using var second = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
        Assert.True(first.Success, first.Failure?.Message);
        Assert.True(second.Success, second.Failure?.Message);
        Assert.Equal(Enumerable.Repeat((byte)'x', 20000), Assert.IsType<byte[]>(first.ReturnValue));
        first.Dispose();
        Assert.True(budget.ReservedBytes > 0);
        second.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
