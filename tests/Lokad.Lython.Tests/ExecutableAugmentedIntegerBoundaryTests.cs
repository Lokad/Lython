using System.Globalization;
using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class ExecutableAugmentedIntegerBoundaryTests
{
    private const long HostAllowanceBytes = 1024 * 1024;
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
    public void OtherIntegerOperatorsPreserveResultsAndCleanup(string token)
    {
        var source = $"global left\nleft {token}= right\n";
        var expected = Create("13", "4");
        Run(expected, false, source);
        (object Value, long Bytes)? synchronous = null;
        foreach (var asynchronous in new[] { false, true })
        {
            var context = Create("13", "4");
            Run(context, asynchronous, source);
            Assert.Equal(expected.Variables["left"], context.Variables["left"]);
            var observation = (context.Variables["left"], context.MemoryGovernor.CurrentAccountedBytes);
            if (asynchronous) Assert.Equal(synchronous!.Value, observation);
            else synchronous = observation;
            AssertUnwound(context);
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
            Assert.Equal(expectedFee, context.MemoryGovernor.CurrentAccountedBytes - before);
            AssertUnwound(context);
            GC.KeepAlive(result);
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
            Assert.True(governor.LastDeniedReservationBytes >= 41);
            Assert.True(governor.LastDeniedReservationBytes > available);
            Assert.Equal(governor.LastDeniedReservationBytes, error.Attribution?.Requested);
            Assert.Equal(new LythonSourceSpan(18, 13, 3, 1), error.Span);
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
            AssertUnwound(context);
        }
    }

    [Fact]
    public void CancellationPreventsMutation()
    {
        foreach (var asynchronous in new[] { false, true })
        {
            using var cancellation = new CancellationTokenSource();
            var context = Create("4", "7", cancellation.Token);
            cancellation.Cancel();
            var error = Assert.Throws<LythonRuntimeException>(() => Run(context, asynchronous));
            Assert.Equal("execution canceled", error.Message);
            Assert.Null(error.Span);
            AssertUnwound(context);
        }
    }

    private sealed class BudgetPad(long available, PyString result) : LythonRuntime.ICallable
    {
        internal long Bytes;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            var governor = context.MemoryGovernor;
            Bytes = HostAllowanceBytes - governor.CurrentAccountedBytes - available;
            governor.Reserve(Bytes, span);
            governor.Commit(Bytes);
            return result;
        }
    }

    private static Context Create(string left, string right, CancellationToken cancellation = default)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        {
            MemoryBudget = new LythonMemoryBudget(HostAllowanceBytes),
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
