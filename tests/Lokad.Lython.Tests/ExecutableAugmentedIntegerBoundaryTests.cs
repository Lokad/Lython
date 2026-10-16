using System.Globalization;
using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class ExecutableAugmentedIntegerBoundaryTests(ITestOutputHelper output)
{
    private const string Source = "global left\nleft += right\n";

    [Theory]
    [InlineData("-")]
    [InlineData("*")]
    [InlineData("/")]
    [InlineData("//")]
    [InlineData("%")]
    [InlineData("**")]
    [InlineData("|")]
    [InlineData("^")]
    [InlineData("&")]
    [InlineData("<<")]
    [InlineData(">>")]
    public void OtherIntegerOperatorsPreserveEveryFuelBoundary(string token)
    {
        var source = $"global left\nleft {token}= right\n";
        var completed = Create("13", "4");
        Run(completed, false, source);
        Assert.Equal(6, completed.Limits.ExecutionStepCount);
        for (var fuel = 0; fuel <= 6; fuel++)
        {
            (object Value, long Bytes, LythonSourceSpan? Span)? synchronous = null;
            foreach (var asynchronous in new[] { false, true })
            {
                var context = Create("13", "4");
                var original = context.Variables["left"];
                context.Limits.ExecutionStepCount = 1000 - fuel;
                var failure = Record.Exception(() => Run(context, asynchronous, source));
                if (fuel < 6)
                {
                    var error = Assert.IsType<LythonRuntimeException>(failure);
                    Assert.Equal("maximum execution step count exceeded (1000)", error.Message);
                    Assert.Equal(1001, context.Limits.ExecutionStepCount);
                    if (fuel < 5) Assert.Same(original, context.Variables["left"]);
                }
                else
                {
                    Assert.Null(failure);
                    Assert.Equal(completed.Variables["left"], context.Variables["left"]);
                }
                var observation = (context.Variables["left"], context.MemoryGovernor.CurrentAccountedBytes,
                    (failure as LythonRuntimeException)?.Span);
                if (asynchronous) Assert.Equal(synchronous!.Value, observation);
                else synchronous = observation;
                AssertUnwound(context);
            }
        }
    }

    [Theory]
    [InlineData("127", "1", "128")]
    [InlineData("-128", "-1", "-129")]
    [InlineData("9223372036854775807", "1", "9223372036854775808")]
    [InlineData("18446744073709551615", "1", "18446744073709551616")]
    [InlineData("-18446744073709551615", "-1", "-18446744073709551616")]
    [InlineData("340282366920938463463374607431768211455", "1", "340282366920938463463374607431768211456")]
    public void SignedPromotionKeepsTheActualResultAndItsOwnership(string left, string right, string expected)
    {
        foreach (var asynchronous in new[] { false, true })
        {
            var context = Create(left, right);
            var actualNamespace = context.Variables;
            var active = new PyException("ValueError", "outer", PyNone.Instance);
            context.Services.SetCurrentException(active);
            var before = context.MemoryGovernor.CurrentAccountedBytes;
            Run(context, asynchronous);
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
    }

    [Fact]
    public void EveryOpcodeFuelCutoffKeepsTheOriginalSpanAndPublicationPoint()
    {
        foreach (var asynchronous in new[] { false, true })
        {
            var completed = Create("4", "7");
            Run(completed, asynchronous);
            Assert.Equal(6, completed.Limits.ExecutionStepCount);
            LythonSourceSpan?[] expectedSpans =
            [null, new(12, 13, 2, 1), new(20, 5, 2, 9), new(12, 13, 2, 1), new(12, 13, 2, 1), new(0, 0, 0, 0)];
            var spans = new List<LythonSourceSpan?>();
            for (var fuel = 0; fuel < completed.Limits.ExecutionStepCount; fuel++)
            {
                var context = Create("4", "7");
                var old = context.Variables["left"];
                context.Limits.ExecutionStepCount = 1000 - fuel;
                var error = Assert.Throws<LythonRuntimeException>(() => Run(context, asynchronous));
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
    }

    [Theory]
    [InlineData(40)]
    [InlineData(0)]
    public void DeniedIntegerPayloadKeepsTheOriginalValueAndFunding(long available)
    {
        foreach (var asynchronous in new[] { false, true })
        {
            var context = Create("18446744073709551615", "1");
            var governor = context.MemoryGovernor;
            var padding = new BudgetPad(available, PyString.FromString("funded", governor));
            context.Variables["pad"] = padding;
            var before = governor.CurrentCommittedBytes;
            var old = context.Variables["left"];
            var error = Assert.Throws<LythonRuntimeException>(() => Run(context, asynchronous, "pad()\n" + Source));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Same(old, context.Variables["left"]);
            Assert.Equal(before + padding.Bytes, governor.CurrentCommittedBytes);
            Assert.Equal(41, governor.LastDeniedReservationBytes);
            Assert.Equal(new LythonSourceSpan(18, 13, 3, 1), error.Span);
            Assert.Equal(padding.Steps + 4, context.Limits.ExecutionStepCount);
            output.WriteLine($"available={available}; denied={governor.LastDeniedReservationBytes}; span={error.Span}; steps={context.Limits.ExecutionStepCount}");
            AssertUnwound(context);
        }
    }

    [Theory]
    [InlineData("*")]
    [InlineData("<<")]
    [InlineData("**")]
    public void OtherIntegerResultPreflightsRefuseBeforeAssignment(string token)
    {
        foreach (var asynchronous in new[] { false, true })
        {
            var context = Create("18446744073709551615", "2");
            var governor = context.MemoryGovernor;
            var padding = new BudgetPad(0, PyString.FromString("funded", governor));
            context.Variables["pad"] = padding;
            var before = governor.CurrentCommittedBytes;
            var original = context.Variables["left"];
            var error = Assert.Throws<LythonRuntimeException>(() => Run(context, asynchronous,
                $"pad()\nglobal left\nleft {token}= right\n"));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Same(original, context.Variables["left"]);
            Assert.Equal(before + padding.Bytes, governor.CurrentCommittedBytes);
            Assert.True(governor.LastDeniedReservationBytes > 0);
            Assert.Equal(padding.Steps + 4, context.Limits.ExecutionStepCount);
            AssertUnwound(context);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FuelFailureStillPrecedesCancellation(bool fuelDenied)
    {
        foreach (var asynchronous in new[] { false, true })
        {
            using var cancellation = new CancellationTokenSource();
            var context = Create("4", "7", cancellation.Token);
            context.Limits.ExecutionStepCount = fuelDenied ? 1000 : 0;
            cancellation.Cancel();
            var error = Assert.Throws<LythonRuntimeException>(() => Run(context, asynchronous));
            Assert.Equal(fuelDenied ? "maximum execution step count exceeded (1000)" : "execution canceled", error.Message);
            Assert.Null(error.Span);
            Assert.Equal(fuelDenied ? 1001 : 1, context.Limits.ExecutionStepCount);
            AssertUnwound(context);
        }
    }

    private sealed class BudgetPad(long available, PyString result) : LythonRuntime.ICallable
    {
        internal long Bytes;
        internal long Steps;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            var governor = context.MemoryGovernor;
            Bytes = governor.MaxAccountedBytes!.Value - governor.CurrentAccountedBytes - available;
            governor.Reserve(Bytes, span);
            governor.Commit(Bytes);
            Steps = context.Limits.ExecutionStepCount;
            return result;
        }
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

    private static void Run(Context context, bool asynchronous, string source = Source)
    {
        var frontend = LythonFrontend.Compile(source);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script.RequireNotNull()));
        if (asynchronous) LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context).GetAwaiter().GetResult();
        else LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null);
    }

    private static void AssertUnwound(Context context)
    {
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Null(context.CurrentExecutableFrame);
    }
}
