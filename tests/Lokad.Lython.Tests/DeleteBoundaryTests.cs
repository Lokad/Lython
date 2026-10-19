using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class DeleteBoundaryTests
{

    public static TheoryData<string, string, bool> Programs => new()
    {
        { "name", "value=7\ndel value\nreturn 7", false },
        { "item", "del table()[pick()]\nreturn 7", false },
        { "slice", "del table()[pick():end()]\nreturn 7", false },
        { "member", "class Box:\n def __delattr__(self,name):note('delete')\ndef receiver():note('base');return Box()\ndel receiver().value\nreturn 7", false },
        { "target_error", "del fail()[pick()]\nreturn 7", true },
        { "index_error", "del table()[fail()]\nreturn 7", true },
        { "partial", "del table()[pick()],fail()[pick()]\nreturn 7", true },
        { "protected", "try:del table()[pick()]\nfinally:note('cleanup')\nreturn 7", false },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task DeletionPreservesEffectsAndUnwind(string label, string source, bool fails)
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
                    "name" => [],
                    "item" => ["base", "index"],
                    "slice" => ["base", "index", "end"],
                    "member" => ["base", "delete"],
                    "target_error" => ["fail"],
                    "index_error" => ["base", "fail"],
                    "partial" => ["base", "index", "fail"],
                    _ => new[] { "base", "index", "cleanup" },
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
        context.Variables["table"] = new Argument(effects, "base", new PyList([new BigInteger(0)], context.MemoryGovernor));
        context.Variables["pick"] = new Argument(effects, "index", new BigInteger(0));
        context.Variables["end"] = new Argument(effects, "end", new BigInteger(1));
        context.Variables["note"] = new Note(effects);
        context.Variables["fail"] = new Fail(effects);
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
            return new BigInteger(7);
        }
    }

    private sealed class Fail(List<string> effects) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            effects.Add("fail");
            throw new LythonRuntimeException("ValueError", "annotation fixture", span);
        }
    }
}
