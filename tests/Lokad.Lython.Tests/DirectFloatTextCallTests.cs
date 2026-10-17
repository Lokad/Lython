using System.Numerics;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class DirectFloatTextCallTests(ITestOutputHelper output)
{
    private const int MaximumSteps = 10000;

    public static TheoryData<string, string, string?> Programs => new()
    {
        { "text", "return float(text)", null },
        { "bytes", "return float(raw)", null },
        { "value", "return float(value())", null },
        { "bad_text", "return float(bad())", "ValueError" },
        { "bad_bytes", "return float(bad_raw())", "ValueError" },
        { "expanded", "return float(*[text])", null },
        { "alias", "return parse(text)", null },
        { "shadow", "float=shadow\nreturn float(text)", null },
        { "keyword", "return float(value=value())", "TypeError" },
        { "noarg", "return float()", null },
        { "number", "return float(number)", null },
        { "arity", "return float(value(),value())", "TypeError" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task NativeAndGeneralCallsPreserveEffectsFuelAndUnwind(string label, string source, string? expectedError)
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
                var (context, effects) = Prepare(owner);
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
                    Assert.Equal(label == "noarg" ? 0.0 : 17.5, Assert.IsType<double>(result));
                }
                fullEffects = effects.ToArray();
                Assert.Equal(label switch
                {
                    "value" or "keyword" => ["value"],
                    "bad_text" => ["bad"],
                    "bad_bytes" => ["bad_raw"],
                    "shadow" => ["shadow"],
                    "arity" => ["value", "value"],
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
                    var (context, effects) = Prepare(owner);
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
            output.WriteLine("DIRECT_FLOAT_TEXT_MATRIX:" + JsonSerializer.Serialize(new { label, asynchronous, steps, completedError, fullEffects, checkpoints }));
        }
    }

    private static (Context Context, List<string> Effects) Prepare(ILythonMemoryBudget owner)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, MaxExecutionSteps = MaximumSteps });
        var effects = new List<string>();
        var text = PyString.FromString("17.5", context.MemoryGovernor, null);
        context.Variables["text"] = text;
        context.Variables["raw"] = new PyBytes(System.Text.Encoding.ASCII.GetBytes("17.5"), context.MemoryGovernor, null);
        context.Variables["number"] = 17.5;
        context.Variables["parse"] = context.Variables["float"];
        context.Variables["value"] = new Argument(effects, "value", text);
        context.Variables["bad"] = new Argument(effects, "bad", PyString.FromString("bad", context.MemoryGovernor, null));
        context.Variables["bad_raw"] = new Argument(effects, "bad_raw", new PyBytes(System.Text.Encoding.ASCII.GetBytes("bad"), context.MemoryGovernor, null));
        context.Variables["shadow"] = new Argument(effects, "shadow", 17.5);
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

}
