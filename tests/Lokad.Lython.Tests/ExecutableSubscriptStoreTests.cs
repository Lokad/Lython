using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ExecutableSubscriptStoreTests
{
    [Fact]
    public void OrdinaryStoreHeadersAreExecutableWithoutLoopTargetEvaluation()
    {
        var syntax = LythonFrontend.Compile("def run(container,key,value):\n container[key]=value",regexProvider:Utf8RegexProvider.Instance).Script!;
        var module = ExecutableScript.Compile(LoweredScript.Lower(syntax));
        var code = Assert.Single(module.EntryPoint.Functions).CodeObject!;
        Assert.Empty(code.ExpressionFallbacks);
        Assert.Empty(code.StatementFallbacks);
        Assert.Single(code.Constants.OfType<ExecutableStoreSubscript>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeniedInsertionAfterHeaderEvaluationDoesNotMutate(bool asynchronous)
    {
        const long limit = 1024 * 1024;
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(limit) },regexProvider:Utf8RegexProvider.Instance);
        var container = new PyDict(context.MemoryGovernor, null);
        using var deny = new DenyCapacity(context.MemoryGovernor, limit);
        context.Variables["container"] = container;
        context.Variables["value"] = true;
        context.Variables["key"] = deny;
        var syntax = LythonFrontend.Compile("container[key()]=value",regexProvider:Utf8RegexProvider.Instance).Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        try
        {
            LythonRuntimeException error;
            if (asynchronous)
                error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context));
            else error = Assert.Throws<LythonRuntimeException>(() => LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(1, deny.Calls);
            Assert.Equal(0, container.Count);
            Assert.Null(context.CurrentExecutableFrame);
            Assert.True(context.MemoryGovernor.PeakAccountedBytes <= limit);
        }
        finally { deny.Dispose(); }
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class DenyCapacity(MemoryGovernor governor, long limit) : LythonRuntime.ICallable, IDisposable
    {
        private IDisposable? _reservation;
        public int Calls { get; private set; }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Calls++;
            _reservation = governor.ReserveTemporary(limit - governor.CurrentAccountedBytes - 64, span);
            return BigInteger.Zero;
        }
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => ValueTask.FromResult(Invoke(arguments, span, context));
        public void Dispose()
        {
            _reservation?.Dispose();
            _reservation = null;
        }
    }
}
