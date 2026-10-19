using System.Globalization;
using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class NativeScalarDispatchBoundaryTests
{

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MixedBooleanIntegerAdmissionRefusesBeforePublication(bool augmented)
    {
        var source = "pad()\nglobal left\n" + (augmented ? "left+=right\nreturn left" : "return left+right");
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            using (var owner = budget.CreateScope())
            {
                var context = new Context(new MockLythonHost(), new LythonRunOptions
                { MemoryBudget = owner});
                object original = true;
                context.Variables["left"] = original;
                var right = BigInteger.One << 4096;
                context.Variables["right"] = right;
                // Fund operand observation and minor work, but not the result
                // plus computation overlap. Return an already-owned marker.
                var available = RuntimeMemoryEstimates.EstimateBigIntegerBytes(right) + 64;
                var padding = new FillAllowance(budget, available,
                    PyString.FromString("funded", context.MemoryGovernor, null));
                context.Variables["pad"] = padding;
                var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Run(code, context, asynchronous));
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
                Assert.Same(original, context.Variables["left"]);
                // A result-only observation fits the original headroom. This
                // refusal must be the larger, before-allocation overlap request.
                Assert.True(error.Attribution?.Requested > available);
                Assert.NotNull(error.Span);
                Assert.Contains('+', source.Substring(error.Span.Start, error.Span.Length));
                CheckCleanup(context);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    public static TheoryData<string, string, object, object, object?, string?> Cases => new()
    {
        { "float_add", "return left+right", 1.5, 2.25, 3.75, null },
        { "bool_and", "return left&right", true, false, false, null },
        { "bool_float", "return left+right", true, 1.5, 2.5, null },
        { "float_int", "return left*right", 1.5, new BigInteger(2), 3.0, null },
        { "aug_float", "global left\nleft*=right\nreturn left", 1.5, 2.0, 3.0, null },
        { "aug_bool", "global left\nleft^=right\nreturn left", true, true, false, null },
        { "aug_mixed", "global left\nleft+=right\nreturn left", true, new BigInteger(2), new BigInteger(3), null },
        { "minus", "return -left", 1.5, false, -1.5, null },
        { "plus_bool", "return +left", true, false, BigInteger.One, null },
        { "not_float", "return not left", 0.0, false, true, null },
        { "invert_bool", "return ~left", true, false, new BigInteger(-2), null },
        { "less", "return left<right", 0.5, 1.0, true, null },
        { "divide_zero", "return left/right", 1.5, 0.0, null, "ZeroDivisionError" },
        { "shift_float", "return left<<right", 1.5, BigInteger.One, null, "TypeError" },
        { "invert_float", "return ~left", 1.5, false, null, "TypeError" },
        { "wide_bool", "return left+right", true, BigInteger.One << 80, (BigInteger.One << 80) + 1, null },
        { "text_add", "return left+right", PyString.FromString("café"), PyString.FromString("😀"), PyString.FromString("café😀"), null },
        { "text_aug", "global left\nleft+=right\nreturn left", PyString.FromString("café"), PyString.FromString("😀"), PyString.FromString("café😀"), null },
        { "text_less", "return left<right", PyString.FromString("café"), PyString.FromString("😀"), true, null },
        { "text_in", "return left in right", PyString.FromString("😀"), PyString.FromString("café😀tail"), true, null },
        { "text_format", "return left%right", PyString.FromString("café:%s"), PyString.FromString("😀"), PyString.FromString("café:😀"), null },
        { "text_power", "return left**right", PyString.FromString("café"), PyString.FromString("😀"), null, "TypeError" },
        { "text_matrix", "return left@right", PyString.FromString("café"), PyString.FromString("😀"), null, "TypeError" },
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StringConcatenationAdmissionRefusesBeforePublication(bool augmented)
    {
        var source = "pad()\nglobal left\n" + (augmented ? "left+=right\nreturn left" : "return left+right");
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            using (var owner = budget.CreateScope())
            {
                var context = new Context(new MockLythonHost(), new LythonRunOptions
                { MemoryBudget = owner});
                var left = PyString.FromString("café" + new string('x', 1024), context.MemoryGovernor, null);
                var right = PyString.FromString("😀" + new string('y', 1024), context.MemoryGovernor, null);
                context.Variables["left"] = left;
                context.Variables["right"] = right;
                context.Variables["pad"] = new FillAllowance(budget, 256, PyString.FromString("funded", context.MemoryGovernor, null));
                var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Run(code, context, asynchronous));
                Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
                Assert.True(failure.Attribution?.Requested >= left.Utf8Bytes.Length + right.Utf8Bytes.Length);
                Assert.Contains('+', source.Substring(failure.Span!.Start, failure.Span.Length));
                Assert.Same(left, context.Variables["left"]);
                Assert.Same(right, context.Variables["right"]);
                CheckCleanup(context);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ValuesErrorsPublicationAndCleanupMatchAcrossModes(
        string label, string source, object left, object right, object? expected, string? exception)
    {
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            string? completedError = null;
            using (var owner = budget.CreateScope())
            {
                var context = Prepare(owner, left, right);
                object? result = null;
                var failure = await Record.ExceptionAsync(async () => result = await Run(code, context, asynchronous));
                if (exception is null)
                {
                    Assert.True(failure is null, label + ": " + failure);
                    Assert.Equal(expected, result);
                    Assert.Equal(expected!.GetType(), result!.GetType());
                }
                else
                {
                    var error = Assert.IsType<LythonRuntimeException>(failure);
                    Assert.Equal(exception, error.ExceptionType);
                    completedError = Describe(error);
                    Assert.Same(left, context.Variables["left"]);
                }

                CheckCleanup(context);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    private static Context Prepare(ILythonMemoryBudget owner, object left, object right)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner});
        context.Variables["left"] = left;
        context.Variables["right"] = right;
        return context;
    }

    private static string Describe(LythonRuntimeException error)
        => $"{error.ExceptionType}:{error.Message}:{error.Span?.Start},{error.Span?.Length},{error.Span?.Line},{error.Span?.Column}";

    private static ValueTask<object?> Run(ExecutableCodeObject code, Context context, bool asynchronous)
        => asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
            : new(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));

    private static void CheckCleanup(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class FillAllowance(LythonMemoryBudget budget, long available, PyString marker) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            var governor = context.MemoryGovernor;
            var bytes = budget.LimitBytes!.Value - governor.CurrentAccountedBytes - available;
            governor.Reserve(bytes, span);
            governor.Commit(bytes);
            return marker;
        }
    }
}
