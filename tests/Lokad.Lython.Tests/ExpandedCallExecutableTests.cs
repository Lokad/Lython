using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class ExpandedCallExecutableTests
{
    [Theory]
    [InlineData("target(*values)")]
    [InlineData("target(**keywords)")]
    [InlineData("target(1,*values,flag=2,**keywords)")]
    [InlineData("target(*values,flag=marker)")]
    public void OrdinaryExpansionDoesNotForceFallbackOrLocalMirroring(string expression)
    {
        var syntax = LythonFrontend.Compile($"def invoke(target,values,keywords,marker):\n    return {expression}",regexProvider:Utf8RegexProvider.Instance).Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax));
        var body = Assert.Single(code.EntryPoint.FunctionBindings()).CodeObject;
        Assert.NotNull(body);
        Assert.Empty(body.StatementFallbacks);
        Assert.False(body.RequiresLocalVariableMirroring);
        Assert.False(code.EntryPoint.RequiresLocalVariableMirroring);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public async Task TrailingExactKeywordsKeepAdmissionAtomic(bool asynchronous, bool refuse, bool sharedKeys = false)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var hook = new AdmissionHookMemoryBudget(owner);
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = hook });
        var key = PyString.FromString("début");
        var added = PyString.FromString("reentered");
        var value = PyString.FromString("aliased");
        var dictionary = new PyDict(4, context.MemoryGovernor, null);
        dictionary.SetItem(key, value);
        if (sharedKeys)
        {
            dictionary.SetItem(added, value);
            Assert.NotNull(dictionary.ShareRecordKeys(context, new(0, 1, 1, 1)));
        }
        var source = new ReentrantKeywords(hook, dictionary, added, value, refuse, sharedKeys);
        context.Variables["prepare"] = source;
        var syntax = LythonFrontend.Compile("def collect(tag,/,**kw): return kw\nreturn collect(17, **prepare())").Script!;
        var executable = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        try
        {
            if (refuse)
            {
                var failure = await Assert.ThrowsAsync<LythonRuntimeException>(Run);
                Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
                Assert.Same(value, dictionary.GetItem(key));
                Assert.Equal(sharedKeys ? 2 : 1, dictionary.Count);
            }
            else
            {
                var result = Assert.IsType<PyDict>(await Run());
                Assert.NotSame(dictionary, result);
                Assert.Same(key, result.Keys.First());
                Assert.Same(value, result.GetItem(key));
                if (sharedKeys)
                    Assert.False(result.TryGetValue(added, out _));
                else
                    Assert.Same(value, result.GetItem(added));
                Assert.Same(context.MemoryGovernor, result.OwnerMemoryGovernor);
                Assert.True(result.CommittedStorageBytes > 0);
                result.SetItem(key, PyNone.Instance);
                Assert.Same(value, dictionary.GetItem(key));
            }
            Assert.Equal(1, source.Admissions);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(0, context.State.CurrentInterpreterDepth);
            Assert.Equal(0, context.State.CurrentRecursionDepth);
            Assert.Null(context.CurrentExecutableFrame);
        }
        finally { context.State.CloseResources(); }
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);

        async Task<object?> Run()
            => asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(executable, context)
                : LythonRuntime.ExecuteExecutableCodeObject(executable, context, default, null);
    }

    private sealed class ReentrantKeywords(AdmissionHookMemoryBudget hook, PyDict source, PyString added,
        PyString value, bool refuse, bool sharedKeys) : LythonRuntime.ICallable
    {
        internal int Admissions;

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            if (refuse) hook.RefuseGrowth = _ => { Admissions++; return true; };
            else hook.AfterGrowth = _ =>
            {
                hook.AfterGrowth = null;
                Admissions++;
                if (sharedKeys)
                    Assert.True(source.Remove(added));
                else
                    source.SetItem(added, value);
            };
            return source;
        }
    }
}
