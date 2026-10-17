using System.Numerics;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class ModuleAnnotationBoundaryTests(ITestOutputHelper output)
{
    private const int MaximumSteps = 10000;

    public static TheoryData<string, string, bool> Programs => new()
    {
        { "value", "value:note('annotation')=note('value')\nreturn value", false },
        { "declaration", "value:note('annotation')\nreturn 7", false },
        { "parenthesized", "(value):note('annotation')=note('value')\nreturn value", false },
        { "item", "table()[pick()]:note('annotation')=note('value')\nreturn 7", false },
        { "target_error", "fail().item:note('annotation')\nreturn 7", true },
        { "annotation_error", "value:fail()=note('value')\nreturn 7", true },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ModuleMetadataPreservesEffectsFuelAndUnwind(string label, string source, bool fails)
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
                if (fails)
                {
                    var error = Assert.IsType<LythonRuntimeException>(failure);
                    Assert.Equal("ValueError", error.ExceptionType);
                    completedError = Describe(error);
                }
                else
                {
                    Assert.Null(failure);
                    Assert.Equal(new BigInteger(7), result);
                }
                fullEffects = effects.ToArray();
                Assert.Equal(label switch
                {
                    "value" or "parenthesized" => ["value", "annotation"],
                    "declaration" => ["annotation"],
                    "item" => ["value", "base", "index", "annotation"],
                    "target_error" => ["fail"],
                    _ => new[] { "value", "fail" },
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
            output.WriteLine("MODULE_ANNOTATION_MATRIX:" + JsonSerializer.Serialize(new { label, asynchronous, steps, completedError, fullEffects, checkpoints }));
        }
    }

    private static (Context Context, List<string> Effects) Prepare(ILythonMemoryBudget owner)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, MaxExecutionSteps = MaximumSteps });
        var effects = new List<string>();
        context.Variables["__annotations__"] = new PyDict(context.MemoryGovernor, null);
        context.Variables["note"] = new Note(effects);
        context.Variables["fail"] = new Fail(effects);
        context.Variables["table"] = new Argument(effects, "base", new PyList([new BigInteger(0)], context.MemoryGovernor));
        context.Variables["pick"] = new Argument(effects, "index", new BigInteger(0));
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
            Assert.InRange(context.Limits.ExecutionStepCount, 0, MaximumSteps);
            effects.Add(Assert.IsType<PyString>(arguments[0].Value).AsString());
            return new BigInteger(7);
        }
    }

    private sealed class Fail(List<string> effects) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Assert.InRange(context.Limits.ExecutionStepCount, 0, MaximumSteps);
            effects.Add("fail");
            throw new LythonRuntimeException("ValueError", "annotation fixture", span);
        }
    }
}
