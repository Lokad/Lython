using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class StringProtocolBoundaryTests
{

    public static TheoryData<string, string, bool> Programs => new()
    {
        { "str", "global published\npublished=str(value)\nreturn published", false },
        { "repr", "global published\npublished=repr(value)\nreturn published", false },
        { "str_repr_fallback", "global published\npublished=str(value)\nreturn published", false },
        { "str_bad_return", "global published\npublished=str(value)\nreturn published", true },
        { "repr_bad_return", "global published\npublished=repr(value)\nreturn published", true },
        { "descriptor", "global published\npublished=str(value)\nreturn published", false },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task RootConversionPreservesEffectsAndCleanup(string label, string source, bool fails)
    {
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            string[] fullEffects;
            string? completedError = null;
            using (var owner = budget.CreateScope())
            {
                var (context, effects) = Prepare(owner, label);
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
                fullEffects = effects.ToArray();
                Assert.Equal(label == "descriptor" ? ["bind", "call"] : new[] { "call" }, fullEffects);
                CheckCleanup(context);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    private static (Context Context, List<string> Effects) Prepare(ILythonMemoryBudget owner, string label)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner});
        var effects = new List<string>();
        object rendered = label.EndsWith("bad_return", StringComparison.Ordinal) ? new BigInteger(7)
            : PyString.FromString("rendered", context.MemoryGovernor, null);
        var call = new Render(effects, rendered);
        var members = new Dictionary<string, object>
        {
            [label.StartsWith("repr", StringComparison.Ordinal) || label == "str_repr_fallback" ? "__repr__" : "__str__"]
                = label == "descriptor" ? new Descriptor(effects, call) : call
        };
        Assert.True(context.TryGetBuiltin("object", out var root));
        var type = new PyType("Value", [Assert.IsType<PyType>(root)], members);
        context.Variables["value"] = new PyInstance(type, context.MemoryGovernor, null);
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

    private sealed class Render(List<string> effects, object result) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            effects.Add("call");
            return result;
        }
    }

    private sealed class Descriptor(List<string> effects, object callable) : IPyDescriptor
    {
        public object Get(object? instance, PyType owner, Context? context, LythonSourceSpan? span)
        {
            Assert.NotNull(context);
            effects.Add("bind");
            return callable;
        }
    }
}
