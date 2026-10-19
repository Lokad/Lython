using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class AnnotatedHelperBoundaryTests
{

    public static TheoryData<string, string, bool> Programs => new()
    {
        { "definition", "def helper(value:int)->int:return value+1\nreturn helper", false },
        { "call", "def helper(value:int)->int:return value+1\nreturn helper(2)", false },
        { "effects", "def helper(value:note('annotation')=note('default'))->note('return'):\n return value+note('body')\nreturn helper(note('argument'))", false },
        { "postponed", "from __future__ import annotations\ndef helper(value:note('annotation')=note('default'))->note('return'):\n return value+note('body')\nreturn helper(note('argument'))", false },
        { "annotation_error", "helper=11\ndef helper(value:fail())->int:return value", true },
        { "default_error", "helper=11\ndef helper(value:note('annotation')=fail())->int:return value", true },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task DefinitionAndCallBoundariesPreserveEffectsAndUnwind(string label, string source, bool fails)
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
                    if (label == "definition") Assert.IsAssignableFrom<PyFunctionBase>(result);
                    else Assert.Equal(new BigInteger(label == "call" ? 3 : 2), result);
                }
                fullEffects = effects.ToArray();
                Assert.Equal(label switch
                {
                    "effects" => ["default", "annotation", "return", "argument", "body"],
                    "postponed" => ["default", "argument", "body"],
                    "annotation_error" or "default_error" => ["fail"],
                    _ => Array.Empty<string>()
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

    private sealed class Note(List<string> effects) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        { effects.Add(Assert.IsType<PyString>(arguments[0].Value).AsString()); return BigInteger.One; }
    }

    private sealed class Fail(List<string> effects) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Assert.Equal(new BigInteger(11), LythonRuntime.ResolveName("helper", span, context));
            effects.Add("fail");
            throw new LythonRuntimeException("ValueError", "annotation fixture", span);
        }
    }
}
