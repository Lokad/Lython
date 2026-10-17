using System.Numerics;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class ComparisonFuelTests(ITestOutputHelper output)
{
    private const int MaximumSteps = 10000;

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task EveryFuelBoundaryStopsWithoutReplayingOperands(bool asynchronous, bool truthy)
    {
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile("return first()<middle()<last()").Script!)).EntryPoint;
        var expected = truthy ? new[] { "first", "middle", "last" } : new[] { "first", "middle" };
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        long steps;
        using (var owner = budget.CreateScope())
        {
            var (context, effects) = Prepare(owner, truthy);
            Assert.Equal(truthy, Assert.IsType<bool>(await Run(code, context, asynchronous)));
            Assert.Equal(expected, effects);
            steps = context.Limits.ExecutionStepCount;
            CheckCleanup(context);
        }
        Assert.Equal(0, budget.ReservedBytes);
        var checkpoints = new List<string>();
        for (var remaining = 0; remaining < steps; remaining++)
        {
            using (var owner = budget.CreateScope())
            {
                var (context, effects) = Prepare(owner, truthy);
                context.Limits.ExecutionStepCount = MaximumSteps - remaining;
                var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Run(code, context, asynchronous));
                Assert.Equal(LythonFailureReason.ExecutionStepLimit, failure.Attribution?.Reason);
                Assert.Equal(MaximumSteps + 1, context.Limits.ExecutionStepCount);
                Assert.Equal(expected.Take(effects.Count), effects);
                Assert.InRange(effects.Count, 0, expected.Length);
                var span = failure.Span;
                checkpoints.Add($"{span?.Start},{span?.Length},{span?.Line},{span?.Column}:" + string.Join(';', effects));
                CheckCleanup(context);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
        output.WriteLine("COMPARISON_FUEL_MATRIX:" + JsonSerializer.Serialize(new { asynchronous, truthy, steps, checkpoints }));
    }

    private static (Context Context, List<string> Effects) Prepare(ILythonMemoryBudget owner, bool truthy)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, MaxExecutionSteps = MaximumSteps });
        var effects = new List<string>();
        context.Variables["first"] = new Operand("first", truthy ? 0 : 2, effects);
        context.Variables["middle"] = new Operand("middle", 1, effects);
        context.Variables["last"] = new Operand("last", 2, effects);
        return (context, effects);
    }

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

    private sealed class Operand(string name, int value, List<string> effects) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        { effects.Add(name); return new BigInteger(value); }
    }
}
