using System.Numerics;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class NativeAnyAllTests(ITestOutputHelper output)
{
    private const int MaximumSteps = 10000;

    public static IEnumerable<object?[]> Programs()
    {
        foreach (var operation in new[] { "any", "all" })
        foreach (var count in new[] { 0, 1, 63, 64, 65 })
            yield return ["bytes-" + operation + "-" + count, "return " + operation + "(values)", "bytes-" + operation, count, operation == "all", null];
        foreach (var count in new[] { 0, 63, 64, 65 })
            yield return ["range-all-" + count, "return all(values)", "range-positive", count, true, null];
        foreach (var count in new[] { 0, 1, 2 })
            yield return ["range-any-" + count, "return any(values)", "range-zero", count, count > 1, null];
        yield return ["range-big", "return all(values)", "range-big", 3, true, null];
        yield return ["guest-any", "class Values:\n def __iter__(self):\n  note('iter')\n  yield False\n  note('next')\n  yield True\n  note('unreached')\nreturn any(Values())", "bytes-all", 2, true, null];
        yield return ["guest-all", "class Values:\n def __iter__(self):\n  note('iter')\n  yield False\n  note('unreached')\nreturn all(Values())", "bytes-all", 2, false, null];
        yield return ["alias", "operation=any\nreturn operation(values)", "bytes-all", 2, true, null];
        yield return ["arity", "return all(value(),value())", "bytes-all", 2, false, "TypeError"];
        yield return ["keyword", "return all(iterable=value())", "bytes-all", 2, false, "TypeError"];
        yield return ["noncallable", "any=[]\nreturn any(value())", "bytes-all", 2, false, "TypeError"];
    }

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task NativeAndGeneralInputsPreserveEffectsFuelAndUnwind(string label, string source, string input, int count, bool expected, string? expectedError)
    {
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            long steps;
            string[] fullEffects;
            string? completedError = null;
            using (var owner = budget.CreateScope())
            {
                var (context, effects) = Prepare(owner, input, count);
                object? result = null;
                var failure = await Record.ExceptionAsync(async () => result = await Run(code, context, asynchronous));
                if (expectedError is not null)
                {
                    var error = Assert.IsType<LythonRuntimeException>(failure);
                    Assert.Equal(expectedError, error.ExceptionType);
                    completedError = Describe(error);
                }
                else
                {
                    Assert.Null(failure);
                    Assert.Equal(expected, Assert.IsType<bool>(result));
                }
                fullEffects = effects.ToArray();
                Assert.Equal(label switch
                {
                    "guest-any" => ["iter", "next"],
                    "guest-all" => ["iter"],
                    "arity" => ["value", "value"],
                    "keyword" or "noncallable" => ["value"],
                    _ => Array.Empty<string>(),
                }, fullEffects);
                steps = context.Limits.ExecutionStepCount;
                CheckCleanup(context);
            }
            Assert.Equal(0, budget.ReservedBytes);
            var checkpoints = new List<string>();
            for (var remaining = 0; remaining < steps; remaining++)
            {
                using (var owner = budget.CreateScope())
                {
                    var (context, effects) = Prepare(owner, input, count);
                    context.Limits.ExecutionStepCount = MaximumSteps - remaining;
                    var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Run(code, context, asynchronous));
                    Assert.Equal(LythonFailureReason.ExecutionStepLimit, failure.Attribution?.Reason);
                    Assert.Equal(MaximumSteps, failure.Attribution?.Limit);
                    Assert.Equal(fullEffects.Take(effects.Count), effects);
                    checkpoints.Add(Describe(failure) + ":checks=" + context.Limits.ExecutionStepCount + ":" + string.Join(';', effects));
                    CheckCleanup(context);
                }
                Assert.Equal(0, budget.ReservedBytes);
            }
            output.WriteLine("NATIVE_ANY_ALL_MATRIX:" + JsonSerializer.Serialize(new { label, asynchronous, steps, completedError, fullEffects, checkpoints }));
        }
    }

    private static (Context Context, List<string> Effects) Prepare(ILythonMemoryBudget owner, string input, int count)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, MaxExecutionSteps = MaximumSteps });
        var effects = new List<string>();
        object values;
        if (input.StartsWith("bytes", StringComparison.Ordinal))
            values = new PyBytes(Enumerable.Repeat((byte)(input == "bytes-any" ? 0 : 1), count).ToArray(), context.MemoryGovernor, null);
        else
        {
            var start = input == "range-big" ? BigInteger.Pow(10, 30) : input == "range-zero" ? BigInteger.Zero : BigInteger.One;
            var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(context.Variables["range"]);
            values = callable.Invoke([CallArgumentValue.Positional(start), CallArgumentValue.Positional(start + count)], new(0, 1, 1, 1), context);
        }
        context.Variables["values"] = values;
        context.Variables["value"] = new Argument(effects, "value", values);
        context.Variables["note"] = new Note(effects);
        // Input construction is outside the measured program's fuel boundary.
        context.Limits.ExecutionStepCount = 0;
        return (context, effects);
    }

    private static string Describe(LythonRuntimeException error)
        => $"{error.ExceptionType}:{error.Span?.Start},{error.Span?.Length},{error.Span?.Line},{error.Span?.Column}";

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

    private sealed class Argument(List<string> effects, string name, object value) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Assert.InRange(context.Limits.ExecutionStepCount, 0, MaximumSteps);
            effects.Add(name);
            return value;
        }
    }

    private sealed class Note(List<string> effects) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            effects.Add(Assert.IsType<PyString>(arguments[0].Value).AsString());
            return PyNone.Instance;
        }
    }

}
