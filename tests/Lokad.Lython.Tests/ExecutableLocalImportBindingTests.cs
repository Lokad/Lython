using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ExecutableLocalImportBindingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NestedImportFailureKeepsTheInnerSourceAndReleasesLoadingState(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var host = new MockLythonHost();
        host.SeedFile("/outer.py", "import inner");
        host.SeedFile("/inner.py", "raise ValueError('boom')");
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions
        {
            MemoryBudget = owner,
            AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal) { "outer", "inner" }
        },regexProvider:Utf8RegexProvider.Instance);
        var syntax = LythonFrontend.Compile("import outer",regexProvider:Utf8RegexProvider.Instance).Script.RequireNotNull();
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        async Task Run()
        {
            if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
            else LythonRuntime.ExecuteExecutableCodeObject(code, context);
        }
        var failure = await Assert.ThrowsAsync<LythonRuntimeException>(Run);
        Assert.Equal("ValueError", failure.ExceptionType);
        Assert.Equal("/inner.py", failure.SourcePath);
        Assert.Empty(context.State.LoadingModules);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        host.SeedFile("/inner.py", "value=7");
        await Run();
        Assert.True(context.State.ImportedModules["inner"].TryGetMember("value", out var value));
        Assert.Equal(new BigInteger(7), value);
        Assert.Empty(context.State.LoadingModules);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void RefusedNamespaceGrowthLeavesBothViewsUnchangedAndRefundsPrivateAdmission()
    {
        const long capacity = 65536;
        var budget = new LythonMemoryBudget(capacity);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
        context.MirrorLocalStores = true;
        var syntax = LythonFrontend.Compile("scope=globals()",regexProvider:Utf8RegexProvider.Instance).Script.RequireNotNull();
        LythonRuntime.ExecuteExecutableCodeObject(ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint, context);
        var module = new ScriptPyModule("selected", context, new(1, 1, 1, 1));
        var held = budget.ReservedBytes;
        // The wrapper's private entry estimate fits, but the canonical namespace's
        // new name/key does not. Failure must release that private estimate.
        Assert.True(budget.TryReserve(capacity - held - 32, out var blocker));
        using (blocker)
        {
            var failure = Assert.Throws<LythonRuntimeException>(() => module.TrySetMember("new_name", PyNone.Instance));
            Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
            Assert.False(module.TryGetMember("new_name", out _));
            Assert.False(context.Variables.ContainsKey("new_name"));
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(capacity - 32, budget.ReservedBytes);
        }
        Assert.Equal(held, budget.ReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImportedOrdinaryFunctionsAndLambdasKeepExecutableCodeAndModuleGlobals(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var host = new MockLythonHost();
        host.SeedFile("/selected.py",
            "seed=4\ndef ordinary(value=2):return value+seed\nlambda_value=lambda:seed\nseed=9");
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions
        {
            MemoryBudget = owner,
            AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal) { "selected" }
        },regexProvider:Utf8RegexProvider.Instance);
        var syntax = LythonFrontend.Compile("import selected",regexProvider:Utf8RegexProvider.Instance).Script.RequireNotNull();
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
        else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);

        var module = context.State.ImportedModules["selected"];
        foreach (var (name, expected) in new[] { ("ordinary", 11), ("lambda_value", 9) })
        {
            Assert.True(module.TryGetMember(name, out var value));
            var function = Assert.IsType<PyExecutableFunction>(value);
            var result = asynchronous ? await function.InvokeAsync([], new(1, 1, 1, 1), context)
                : function.Invoke([], new(1, 1, 1, 1), context);
            Assert.Equal(new BigInteger(expected), result);
        }
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.True(budget.ReservedBytes > 0);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
