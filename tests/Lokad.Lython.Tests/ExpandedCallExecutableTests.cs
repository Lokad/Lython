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
        var code = ExecutableScript.Compile(syntax);
        var body = Assert.Single(code.EntryPoint.FunctionBindings()).CodeObject;
        Assert.NotNull(body);
        Assert.False(body.RequiresLocalVariableMirroring);
        Assert.False(code.EntryPoint.RequiresLocalVariableMirroring);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task JsonKeywordBindingAdmitsBeforeReadingMapping(bool asynchronous, bool refuse)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = hook });
            var alias = new PyList([1]);
            var converter = new ParsedValue(alias);
            var key = PyString.FromString("parse_int");
            var options = new PyDict(4, context.MemoryGovernor, null);
            options.SetItem(key, PyNone.Instance);
            context.Variables["prepare"] = new ReentrantKeywords(hook, options, key, converter, refuse, sharedKeys: false);
            var executable = ExecutableScript.Compile(LythonFrontend.Compile("""
                import json
                return json.loads('7', **prepare())
                """).Script!).EntryPoint;
            try
            {
                if (refuse)
                {
                    var failure = await Assert.ThrowsAsync<LythonRuntimeException>(Run);
                    Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
                    Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
                    Assert.Same(PyNone.Instance, options.GetItem(key));
                    Assert.Equal(0, converter.Visits);
                }
                else
                {
                    Assert.Same(alias, await Run());
                    Assert.Same(converter, options.GetItem(key));
                    Assert.Equal(1, converter.Visits);
                }
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal(0, context.State.CurrentInterpreterDepth);
                Assert.Null(context.CurrentExecutableFrame);
            }
            finally { context.State.CloseResources(); }

            async Task<object?> Run() => asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(executable, context)
                : LythonRuntime.ExecuteExecutableCodeObject(executable, context, default, null);
        }
        Assert.Equal(0, budget.ReservedBytes);
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
        var executable = ExecutableScript.Compile(syntax).EntryPoint;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DictionaryCopyHandlesSourceGrowthDuringAdmission(bool refuseReplacement)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new Context(new MockLythonHost(), new() { MemoryBudget = hook });
            var span = new LythonSourceSpan(0, 1, 1, 1);
            var dictionary = new PyDict(2, context.MemoryGovernor, span);
            var first = PyString.FromString("first");
            var second = PyString.FromString("second");
            var added = PyString.FromString("added");
            var alias = new PyList();
            dictionary.SetItem(first, alias);
            dictionary.SetItem(second, PyNone.Instance);
            var otherStorage = budget.ReservedBytes - dictionary.CommittedStorageBytes;
            hook.AfterGrowth = _ =>
            {
                hook.AfterGrowth = null;
                dictionary.SetItem(added, alias);
                if (refuseReplacement) hook.RefuseGrowth = _ => true;
            };
            if (refuseReplacement)
            {
                var failure = Assert.Throws<LythonRuntimeException>(() => new PyDict(dictionary, context.MemoryGovernor, span));
                Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
                Assert.Equal(otherStorage + dictionary.CommittedStorageBytes, budget.ReservedBytes);
            }
            else
            {
                var copy = new PyDict(dictionary, context.MemoryGovernor, span);
                Assert.Equal([first, second, added], copy.Keys.Cast<PyString>());
                Assert.Same(added, copy.Keys.Last());
                Assert.Same(alias, copy.GetItem(added));
                Assert.True(copy.Remove(added));
            }
            Assert.Same(alias, dictionary.GetItem(added));
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class ParsedValue(object alias) : LythonRuntime.ICallable
    {
        internal int Visits;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Visits++;
            return alias;
        }
    }

    private sealed class ReentrantKeywords(AdmissionHookMemoryBudget hook, PyDict source, PyString added,
        object value, bool refuse, bool sharedKeys) : LythonRuntime.ICallable
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
