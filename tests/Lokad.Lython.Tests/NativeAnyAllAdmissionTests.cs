using System.Numerics;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class NativeAnyAllAdmissionTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("any", false)]
    [InlineData("any", true)]
    [InlineData("all", false)]
    [InlineData("all", true)]
    public async Task RangeRefusalPrecedesMutationAndKeepsItsAliasesFunded(string operation, bool denied)
    {
        var code = ExecutableScript.Compile(LoweredScript.Lower(
            LythonFrontend.Compile("target[0]=" + operation + "(arm(values))\nreturn target[0]").Script!)).EntryPoint;
        foreach (var asynchronous in new[] { false, true })
        {
            const long limit = 4 * 1024 * 1024;
            var budget = new LythonMemoryBudget(limit);
            using var owner = budget.CreateScope();
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var start = BigInteger.Pow(10, 40);
            var range = Assert.IsAssignableFrom<LythonRuntime.ICallable>(context.State.BuiltinVariables["range"]);
            var values = range.Invoke([CallArgumentValue.Positional(start), CallArgumentValue.Positional(start + 3)], new(0, 1, 1, 1), context);
            var target = new PyList([values], context.MemoryGovernor);
            context.Variables["values"] = values;
            context.Variables["target"] = target;
            using var arm = new Arm(budget, limit, denied);
            context.Variables["arm"] = arm;
            var baseline = budget.ReservedBytes;
            object? result = null;
            var failure = await Record.ExceptionAsync(async () => result = asynchronous
                ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
                : LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
            LythonRuntimeException? error = null;
            if (denied)
            {
                error = Assert.IsType<LythonRuntimeException>(failure);
                Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                Assert.True(context.MemoryGovernor.LastDeniedReservationBytes > 0);
                Assert.Same(values, Assert.Single(target));
            }
            else
            {
                Assert.Null(failure);
                Assert.True(Assert.IsType<bool>(result));
                Assert.True(Assert.IsType<bool>(Assert.Single(target)));
            }
            Assert.Equal(1, arm.Calls);
            Assert.Same(values, context.Variables["values"]);
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
            Assert.Equal(0, context.Limits.CurrentRecursionDepth);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            arm.Dispose();
            Assert.True(budget.ReservedBytes >= baseline);
            output.WriteLine("NATIVE_ANY_ALL_ADMISSION:" + JsonSerializer.Serialize(new
            {
                operation, denied, asynchronous, type = error?.ExceptionType,
                reason = error?.Attribution?.Reason.ToString(),
                span = error?.Span, demand = context.MemoryGovernor.LastDeniedReservationBytes,
            }));
            owner.Dispose();
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    private sealed class Arm(LythonMemoryBudget budget, long limit, bool denied) : LythonRuntime.ICallable, IDisposable
    {
        private ILythonMemoryReservation? _padding;
        public int Calls { get; private set; }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Calls++;
            if (denied) Assert.True(budget.TryReserve(limit - budget.ReservedBytes, out _padding));
            return arguments[0].Value;
        }

        public void Dispose() { _padding?.Dispose(); _padding = null; }
    }
}
