using System.Numerics;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class FormatProtocolBoundaryTests(ITestOutputHelper output)
{
    private const int MaximumSteps = 10000;

    public static TheoryData<string, string, bool, bool> Programs => new()
    {
        { "builtin", "format(value,spec())", false, false },
        { "field", "f'{value:{spec()}}'", false, false },
        { "method", "'{0:{1}}'.format(value,spec())", false, false },
        { "map", "'{value:>4}'.format_map({'value':value})", false, false },
        { "bad_return", "format(value,spec())", true, false },
        { "descriptor_builtin", "format(value,spec())", false, true },
        { "descriptor_field", "f'{value:{spec()}}'", false, true },
        { "empty_builtin", "format(value)", false, false },
        { "empty_method", "'{}'.format(value)", false, false },
        { "empty_map", "'{value}'.format_map({'value':value})", false, false },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task FormattingPreservesFuelEffectsAndCleanup(string label, string expression, bool fails, bool descriptor)
    {
        var source = "global published\npublished=" + expression + "\nreturn published";
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            long steps;
            string[] fullEffects;
            string? completedError = null;
            using (var owner = budget.CreateScope())
            {
                var (context, effects) = Prepare(owner, fails, descriptor);
                object? result = null;
                var failure = await Record.ExceptionAsync(async () => result = await Run(code, context, asynchronous));
                if (fails)
                {
                    var error = Assert.IsType<LythonRuntimeException>(failure);
                    Assert.Equal("TypeError", error.ExceptionType);
                    completedError = Describe(error);
                }
                else
                {
                    Assert.Null(failure);
                    Assert.Equal("rendered", Assert.IsType<PyString>(result).AsString());
                }
                var expected = new List<string>();
                if (label is not ("map" or "empty_builtin" or "empty_method" or "empty_map")) expected.Add("spec");
                if (descriptor) expected.Add("bind");
                expected.Add(label.StartsWith("empty_", StringComparison.Ordinal) ? "call:" : "call:>4");
                Assert.Equal(expected, effects);
                fullEffects = effects.ToArray();
                steps = context.Limits.ExecutionStepCount;
                CheckCleanup(context);
            }
            Assert.Equal(0, budget.ReservedBytes);
            var checkpoints = new List<string>();
            for (var remaining = 0; remaining < steps; remaining++)
            {
                using (var owner = budget.CreateScope())
                {
                    var (context, effects) = Prepare(owner, fails, descriptor);
                    context.Limits.ExecutionStepCount = MaximumSteps - remaining;
                    var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Run(code, context, asynchronous));
                    Assert.Equal(LythonFailureReason.ExecutionStepLimit, failure.Attribution?.Reason);
                    Assert.Equal(MaximumSteps, failure.Attribution?.Limit);
                    Assert.Equal(fullEffects.Take(effects.Count), effects);
                    checkpoints.Add(Describe(failure) + ":checks=" + context.Limits.ExecutionStepCount
                        + ":published=" + context.Variables["published"] + ":" + string.Join(';', effects));
                    CheckCleanup(context);
                }
                Assert.Equal(0, budget.ReservedBytes);
            }
            output.WriteLine("FORMAT_PROTOCOL_MATRIX:" + JsonSerializer.Serialize(new { label, asynchronous, steps, completedError, fullEffects, checkpoints }));
        }
    }

    private static (Context Context, List<string> Effects) Prepare(ILythonMemoryBudget owner, bool fails, bool descriptor)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, MaxExecutionSteps = MaximumSteps });
        var effects = new List<string>();
        object rendered = fails ? new BigInteger(7) : PyString.FromString("rendered", context.MemoryGovernor, null);
        var call = new Format(effects, rendered);
        var members = new Dictionary<string, object> { ["__format__"] = descriptor ? new Descriptor(effects, call) : call };
        Assert.True(context.TryGetBuiltin("object", out var root));
        context.Variables["value"] = new PyInstance(new PyType("Value", [Assert.IsType<PyType>(root)], members), context.MemoryGovernor, null);
        context.Variables["spec"] = new Spec(effects);
        context.Variables["published"] = new BigInteger(11);
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

    private sealed class Format(List<string> effects, object result) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Assert.InRange(context.Limits.ExecutionStepCount, 0, MaximumSteps);
            var argument = Assert.Single(arguments);
            Assert.True(argument.IsPositional);
            effects.Add("call:" + Assert.IsType<PyString>(argument.Value).AsString());
            return result;
        }
    }

    private sealed class Spec(List<string> effects) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Assert.InRange(context.Limits.ExecutionStepCount, 0, MaximumSteps);
            Assert.Empty(arguments);
            effects.Add("spec");
            return PyString.FromString(">4", context.MemoryGovernor, span);
        }
    }

    private sealed class Descriptor(List<string> effects, object callable) : IPyDescriptor
    {
        public object Get(object? instance, PyType owner, Context? context, LythonSourceSpan? span)
        {
            Assert.NotNull(context);
            Assert.InRange(context.Limits.ExecutionStepCount, 0, MaximumSteps);
            effects.Add("bind");
            return callable;
        }
    }
}
