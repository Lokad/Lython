using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class DictionaryReadBoundaryTests
{

    [Theory]
    [InlineData("hit", 7, null)]
    [InlineData("mutate", 9, null)]
    [InlineData("rebind", 7, null)]
    [InlineData("missing", 11, "KeyError")]
    [InlineData("key_error", 11, "ValueError")]
    public async Task LookupEffectsAndErrorsUnwindAcrossModes(string label, int expected, string? exception)
    {
        const string source = "global published\npublished=table[pick()]\nreturn published";
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            string? completedError = null;
            using (var owner = budget.CreateScope())
            {
                var (context, effects) = Prepare(owner, label);
                object? result = null;
                var failure = await Record.ExceptionAsync(async () => result = await Run(code, context, asynchronous));
                if (exception is null)
                {
                    Assert.Null(failure);
                    Assert.Equal(new BigInteger(expected), result);
                }
                else
                {
                    var error = Assert.IsType<LythonRuntimeException>(failure);
                    Assert.Equal(exception, error.ExceptionType);
                    completedError = Describe(error);
                }
                Assert.Equal(new BigInteger(expected), context.Variables["published"]);
                Assert.Equal(new[] { "key" }, effects);

                CheckCleanup(context);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    private static (Context Context, List<string> Effects) Prepare(ILythonMemoryBudget owner, string label)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner},regexProvider:Utf8RegexProvider.Instance);
        var key = PyString.FromString("x", context.MemoryGovernor, null);
        var absent = PyString.FromString("absent", context.MemoryGovernor, null);
        var table = new PyDict(context.MemoryGovernor);
        table.SetItem(key, new BigInteger(7));
        var effects = new List<string>();
        context.Variables["table"] = table;
        context.Variables["published"] = new BigInteger(11);
        context.Variables["pick"] = new Pick(label, key, absent, effects);
        return (context, effects);
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

    private sealed class Pick(string label, PyString key, PyString absent, List<string> effects) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            effects.Add("key");
            if (label == "key_error") throw new LythonRuntimeException("ValueError", "key fixture", span);
            if (label == "mutate") ((PyDict)context.Variables["table"]).SetItem(key, new BigInteger(9));
            if (label == "rebind")
            {
                var replacement = new PyDict(context.MemoryGovernor, span);
                replacement.SetItem(key, new BigInteger(99));
                context.Variables["table"] = replacement;
            }
            return label == "missing" ? absent : key;
        }
    }
}
