using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class MathNextAfterTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LargeCountsAndFloatingPointBoundariesMatchCpythonBits(bool asynchronous)
    {
        // Independent CPython 3.13.2 goldens, including zero signs and NaN payloads.
        var huge = BigInteger.Pow(10, 100);
        (ulong X, ulong Y, BigInteger Steps, ulong Expected)[] cases =
        [
            (0x3ff0000000000000, 0x4000000000000000, 2147483648UL, 0x3ff0000080000000),
            (0x4000000000000000, 0x3ff0000000000000, 2147483648UL, 0x3fffffff80000000),
            (0xbff0000000000000, 0xc000000000000000, 2147483648UL, 0xbff0000080000000),
            (0xc000000000000000, 0xbff0000000000000, 2147483648UL, 0xbfffffff80000000),
            (0xbff0000000000000, 0x3ff0000000000000, 4607182418800017408UL, 0x8000000000000000),
            (0xbff0000000000000, 0x3ff0000000000000, 4607182418800017409UL, 0x0000000000000001),
            (0x3ff0000000000000, 0xbff0000000000000, 4607182418800017408UL, 0x0000000000000000),
            (0x3ff0000000000000, 0xbff0000000000000, 4607182418800017409UL, 0x8000000000000001),
            (0x0000000000000000, 0x8000000000000000, 0, 0x0000000000000000),
            (0x0000000000000000, 0x8000000000000000, 1, 0x8000000000000000),
            (0x8000000000000000, 0x0000000000000000, 0, 0x8000000000000000),
            (0x8000000000000000, 0x0000000000000000, 1, 0x0000000000000000),
            (0x0000000000000001, 0x8000000000000002, 1, 0x0000000000000000),
            (0x0000000000000001, 0x8000000000000002, 2, 0x8000000000000001),
            (0x0000000000000001, 0x8000000000000002, 3, 0x8000000000000002),
            (0x8000000000000001, 0x0000000000000002, 1, 0x8000000000000000),
            (0x8000000000000001, 0x0000000000000002, 2, 0x0000000000000001),
            (0x7ff0000000000000, 0xfff0000000000000, 1, 0x7fefffffffffffff),
            (0xfff0000000000000, 0x7ff0000000000000, 1, 0xffefffffffffffff),
            (0xfff0000000000000, 0x7ff0000000000000, ulong.MaxValue, 0x7ff0000000000000),
            (0x7ff0000000000000, 0xfff0000000000000, huge, 0xfff0000000000000),
            (0x7ff8000000000042, 0x3ff0000000000000, 1, 0x7ff8000000000042),
            (0x3ff0000000000000, 0xfff8000000000043, 1, 0xfff8000000000043),
            (0x3ff0000000000000, 0xfff8000000000043, 0, 0x3ff0000000000000),
            (0x7ff8000000000042, 0xfff8000000000043, 1, 0x7ff8000000000042),
            (0xbff0000000000000, 0xbff0000000000000, huge, 0xbff0000000000000),
        ];
        using var script = new LythonEngine().Compile("import math\nreturn math.nextafter(x, y, steps=count)\n");
        Assert.True(script.IsValid);
        foreach (var (x, y, steps, expected) in cases)
        {
            var options = new LythonRunOptions
            {
                Globals = new Dictionary<string, object?>
                {
                    ["x"] = BitConverter.UInt64BitsToDouble(x),
                    ["y"] = BitConverter.UInt64BitsToDouble(y),
                    ["count"] = steps,
                },
            };
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options)
                : script.Run(new MockLythonHost(), options);
            Assert.True(result.Success, result.Failure?.Message);
            var actual = BitConverter.DoubleToUInt64Bits(Assert.IsType<double>(result.ReturnValue));
            Assert.True(expected == actual, $"{x:x16}->{y:x16}, {steps} steps: expected {expected:x16}, got {actual:x16}");
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task IndexProtocolRunsOnceAndHonorsCancellation(bool asynchronous, bool cancel)
    {
        using var script = new LythonEngine().Compile("""
            import math
            class Steps:
                def __index__(self):
                    print("index", end="")
                    return 2147483648
            return math.nextafter(1, 2, steps=Steps())
            """);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Message)));
        using var cancellation = new CancellationTokenSource();
        var host = new MockLythonHost();
        if (cancel) host.OnStandardOutputWrite = cancellation.Cancel;
        using var result = asynchronous
            ? await script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token })
            : script.Run(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        Assert.Equal("index", result.StandardOutput);
        if (cancel)
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        }
        else
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(0x3ff0000080000000UL,
                BitConverter.DoubleToUInt64Bits(Assert.IsType<double>(result.ReturnValue)));
        }
    }
}
