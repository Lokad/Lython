using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class ModuleAnnotationExecutionTests
{
    [Theory]
    [InlineData("value:int=7\nreturn value")]
    [InlineData("value:int\nreturn __annotations__")]
    [InlineData("from __future__ import annotations\nvalue:Missing=7\nreturn value")]
    [InlineData("if False:value:int\nreturn __annotations__")]
    public void OrdinaryModuleDeclarationsDoNotRequireFallbackOrMirroring(string source)
    {
        var code = Compile(source);
        Assert.Empty(code.StatementFallbacks);
        Assert.Empty(code.ExpressionFallbacks);
        Assert.False(code.RequiresLocalVariableMirroring);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task EscapingModuleLambdaKeepsFinalBindings(bool asynchronous, bool annotated)
    {
        var code = Compile(annotated
            ? "Kind=int\nvalue:(lambda:Kind)\nKind=str\nreturn __annotations__['value']"
            : "Kind=int\nvalue=lambda:Kind\nKind=str\nreturn value");
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
            context.Variables["__annotations__"] = new PyDict(context.MemoryGovernor, null);
            var result = asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
                : LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
            Assert.Null(context.CurrentExecutableFrame);
            var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(result);
            var returned = asynchronous ? await callable.InvokeAsync([], new(0, 0, 0, 0), context)
                : callable.Invoke([], new(0, 0, 0, 0), context);
            Assert.True(context.TryGetBuiltin("str", out var expected));
            Assert.Same(expected, returned);
            Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
            Assert.Equal(0, context.Limits.CurrentRecursionDepth);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static ExecutableCodeObject Compile(string source)
        => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ModuleNamespaceAdmissionPrecedesMirroring(bool asynchronous)
    {
        var source = string.Join('\n', Enumerable.Range(0, 64).Select(i => $"value{i}={i}")) + "\npin()\nreturn lambda:7";
        var code = Compile(source);
        Assert.False(code.RequiresLocalVariableMirroring);
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions
                { MemoryBudget = owner},regexProvider:Utf8RegexProvider.Instance);
            using var pin = new Pin(budget);
            context.Variables["pin"] = pin;
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            {
                if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
                else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
            });
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
            Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
            Assert.True(error.Attribution?.Requested > 2048);
            Assert.False(context.MirrorLocalStores);
            Assert.False(context.Variables.ContainsKey("value0"));
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class Pin(LythonMemoryBudget budget) : LythonRuntime.ICallable, IDisposable
    {
        private IDisposable? _storage;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            var governor = context.MemoryGovernor;
            _storage = governor.ReserveTemporary(budget.LimitBytes!.Value - governor.CurrentAccountedBytes - 2048, span);
            return PyNone.Instance;
        }
        public void Dispose() => _storage?.Dispose();
    }

    [Theory]
    [InlineData("def make():\n def inner():return Kind\n return inner\nvalue:make()", false)]
    [InlineData("def make():return lambda:Kind\nvalue:make()", false)]
    [InlineData("value:(lambda:Kind for _ in [0])", true)]
    public async Task EscapingMetadataFactoryKeepsItsModuleNamespace(string declaration, bool generator)
    {
        var code = Compile("Kind=int\n" + declaration + "\nKind=str\nreturn __annotations__['value']");
        var invoke = Compile(generator ? "return next(survivor)()" : "return survivor()");
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            using (var owner = budget.CreateScope())
            {
                var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
                context.Variables["__annotations__"] = new PyDict(context.MemoryGovernor, null);
                var result = asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
                    : LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
                Assert.Null(context.CurrentExecutableFrame);
                context.Variables["survivor"] = result!;
                var returned = asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(invoke, context)
                    : LythonRuntime.ExecuteExecutableCodeObject(invoke, context, default, null);
                Assert.True(context.TryGetBuiltin("str", out var expected));
                Assert.Same(expected, returned);
                Assert.Null(context.CurrentExecutableFrame);
                Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
                Assert.Equal(0, context.Limits.CurrentRecursionDepth);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}
