using System.Globalization;
using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class ExecutableAugmentedIntegerBoundaryTests(ITestOutputHelper output)
{
    private const string Source = "global left\nleft += right\n";

    [Theory]
    [InlineData("127", "1", "128")]
    [InlineData("-128", "-1", "-129")]
    [InlineData("9223372036854775807", "1", "9223372036854775808")]
    [InlineData("18446744073709551615", "1", "18446744073709551616")]
    [InlineData("-18446744073709551615", "-1", "-18446744073709551616")]
    [InlineData("340282366920938463463374607431768211455", "1", "340282366920938463463374607431768211456")]
    public void SignedPromotionKeepsTheActualResultAndItsOwnership(string left, string right, string expected)
    {
        var context = Create(left, right);
        var actualNamespace = context.Variables;
        var active = new PyException("ValueError", "outer", PyNone.Instance);
        context.Services.SetCurrentException(active);
        var before = context.MemoryGovernor.CurrentAccountedBytes;
        Run(context);
        var result = context.Variables["left"];
        Assert.Equal(BigInteger.Parse(expected, CultureInfo.InvariantCulture), Assert.IsType<BigInteger>(result));
        Assert.Same(actualNamespace, context.Variables);
        Assert.Same(active, context.Services.CurrentException);
        var integer = Assert.IsType<BigInteger>(result);
        var expectedFee = RuntimeMemoryEstimates.GetMagnitudeBitLength(integer) > 64
            ? RuntimeMemoryEstimates.EstimateBigIntegerBytes(integer) : 0;
        Assert.Equal(6, context.Limits.ExecutionStepCount);
        Assert.Equal(expectedFee, context.MemoryGovernor.CurrentAccountedBytes - before);
        output.WriteLine($"left={left}; steps={context.Limits.ExecutionStepCount}; fee={context.MemoryGovernor.CurrentAccountedBytes - before}");
        AssertUnwound(context);
        GC.KeepAlive(result);
    }

    [Fact]
    public void EveryOpcodeFuelCutoffKeepsTheOriginalSpanAndPublicationPoint()
    {
        var completed = Create("4", "7");
        Run(completed);
        Assert.Equal(6, completed.Limits.ExecutionStepCount);
        LythonSourceSpan?[] expectedSpans =
        [null, new(12, 13, 2, 1), new(20, 5, 2, 9), new(12, 13, 2, 1), new(12, 13, 2, 1), new(0, 0, 0, 0)];
        var spans = new List<LythonSourceSpan?>();
        for (var fuel = 0; fuel < completed.Limits.ExecutionStepCount; fuel++)
        {
            var context = Create("4", "7");
            var old = context.Variables["left"];
            context.Limits.ExecutionStepCount = 1000 - fuel;
            var error = Assert.Throws<LythonRuntimeException>(() => Run(context));
            Assert.Equal("maximum execution step count exceeded (1000)", error.Message);
            Assert.Null(error.SourcePath); // SourcePath is attached at the public result boundary.
            Assert.Equal(1001, context.Limits.ExecutionStepCount);
            if (fuel < 5) Assert.Same(old, context.Variables["left"]);
            else Assert.Equal(new BigInteger(11), context.Variables["left"]);
            output.WriteLine($"fuel={fuel}; left={context.Variables["left"]}; span={error.Span}");
            spans.Add(error.Span);
            AssertUnwound(context);
        }
        output.WriteLine($"steps={completed.Limits.ExecutionStepCount}; spans={string.Join(';', spans)}");
        Assert.Equal(expectedSpans, spans);
    }

    [Theory]
    [InlineData(40)]
    [InlineData(0)]
    public void DeniedIntegerPayloadKeepsTheOriginalValueAndFunding(long available)
    {
        var context = Create("18446744073709551615", "1");
        var governor = context.MemoryGovernor;
        var padding = 1024 * 1024 - governor.CurrentAccountedBytes - available;
        governor.Reserve(padding, null);
        governor.Commit(padding);
        var before = governor.CurrentCommittedBytes;
        var old = context.Variables["left"];
        var error = Assert.Throws<LythonRuntimeException>(() => Run(context));
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Same(old, context.Variables["left"]);
        Assert.Equal(before, governor.CurrentCommittedBytes);
        Assert.Equal(41, governor.LastDeniedReservationBytes);
        Assert.Equal(new LythonSourceSpan(12, 13, 2, 1), error.Span);
        Assert.Equal(4, context.Limits.ExecutionStepCount);
        output.WriteLine($"available={available}; denied={governor.LastDeniedReservationBytes}; span={error.Span}; steps={context.Limits.ExecutionStepCount}");
        AssertUnwound(context);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FuelFailureStillPrecedesCancellation(bool fuelDenied)
    {
        using var cancellation = new CancellationTokenSource();
        var context = Create("4", "7", cancellation.Token);
        context.Limits.ExecutionStepCount = fuelDenied ? 1000 : 0;
        cancellation.Cancel();
        var error = Assert.Throws<LythonRuntimeException>(() => Run(context));
        Assert.Equal(fuelDenied ? "maximum execution step count exceeded (1000)" : "execution canceled", error.Message);
        Assert.Null(error.Span);
        Assert.Equal(fuelDenied ? 1001 : 1, context.Limits.ExecutionStepCount);
        AssertUnwound(context);
    }

    private static Context Create(string left, string right, CancellationToken cancellation = default)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        {
            MaxExecutionSteps = 1000, MaxExecutionMemoryBytes = 1024 * 1024,
            SourcePath = "/integer-augmentation.py", CancellationToken = cancellation
        });
        context.Variables["left"] = BigInteger.Parse(left, CultureInfo.InvariantCulture);
        context.Variables["right"] = BigInteger.Parse(right, CultureInfo.InvariantCulture);
        return context;
    }

    private static void Run(Context context)
    {
        var frontend = LythonFrontend.Compile(Source);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script.RequireNotNull()));
        LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, [], null);
    }

    private static void AssertUnwound(Context context)
    {
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Null(context.CurrentExecutableFrame);
    }
}
