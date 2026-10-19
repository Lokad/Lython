using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class NativeTruthEntryTests
{

    public static TheoryData<string, string, string?> Programs => new()
    {
        { "native-bool", "return bool(native)", null },
        { "native-any", "return any([False,native])", null },
        { "native-all", "return all([True,native])", null },
        { "native-argument", "return bool(value())", null },
        { "native-error", "return bool(bad)", "ValueError" },
        { "native-all-error", "return all([True,bad])", "ValueError" },
        { "guest-bool", "class Value:\n def __bool__(self):note('guest');return True\nreturn bool(Value())", null },
        { "guest-length", "class Value:\n def __len__(self):note('guest');return 1\nreturn bool(Value())", null },
        { "guest-default", "class Value:pass\nreturn bool(Value())", null },
        { "guest-invalid", "class Value:\n def __bool__(self):note('guest');return 7\nreturn bool(Value())", "TypeError" },
        { "guest-priority", "class Value:\n def __bool__(self):note('guest');return True\n def __len__(self):note('len');return 0\nreturn bool(Value())", null },
        { "guest-short-circuit", "class Value:\n def __bool__(self):note('guest');return True\nreturn any([True,Value()])", null },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task TruthPreservesEffectsAndUnwind(string label, string source, string? expectedError)
    {
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
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
                    Assert.True(Assert.IsType<bool>(result));
                }
                fullEffects = effects.ToArray();
                Assert.Equal(label switch
                {
                    "native-bool" or "native-any" or "native-all" => ["native"],
                    "native-argument" => ["value", "native"],
                    "native-error" or "native-all-error" => ["bad"],
                    "guest-bool" or "guest-length" or "guest-invalid" or "guest-priority" => ["guest"],
                    _ => Array.Empty<string>(),
                }, fullEffects);
                CheckCleanup(context);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    private static (Context Context, List<string> Effects) Prepare(ILythonMemoryBudget owner)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner});
        var effects = new List<string>();
        var native = new Flag(effects, "native", false);
        context.Variables["native"] = native;
        context.Variables["bad"] = new Flag(effects, "bad", true);
        context.Variables["value"] = new Argument(effects, "value", native);
        context.Variables["note"] = new Note(effects);
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

    private sealed class Flag(List<string> effects, string name, bool fails) : IPyTruthyValue
    {
        public bool IsTruthy()
        {
            effects.Add(name);
            if (fails) throw new LythonRuntimeException("ValueError", "native truth fixture", null);
            return true;
        }
    }
}
