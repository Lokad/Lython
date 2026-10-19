using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ExecutableSliceStoreTests
{
    [Theory]
    [InlineData("def run(target,start,stop,step,value):\n target[start:stop:step]=value")]
    [InlineData("def run(target,values):\n for target[:] in values: pass")]
    [InlineData("def run(target,values):\n return [list(target) for target[:] in values]")]
    public void SliceHeadersUseExecutableStores(string source)
    {
        var syntax = LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance).Script!;
        var code = Assert.Single(ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint.Functions).CodeObject!;
        Assert.Empty(code.ExpressionFallbacks);
        Assert.Empty(code.StatementFallbacks);
        Assert.Single(code.Constants.OfType<ExecutableStoreSlice>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeniedReplacementStorageAfterHeadersDoesNotMutate(bool asynchronous)
    {
        var span = new LythonSourceSpan(0, 0, 0, 0);
        const long limit = 1024 * 1024;
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(limit) },regexProvider:Utf8RegexProvider.Instance);
        var target = new PyList([BigInteger.Zero], context.MemoryGovernor, span);
        using var deny = new DenyCapacity(context.MemoryGovernor, limit);
        context.Variables["target"] = target;
        context.Variables["values"] = new PyList([BigInteger.One], context.MemoryGovernor, span);
        context.Variables["step"] = deny;
        var syntax = LythonFrontend.Compile("target[::step()]=values",regexProvider:Utf8RegexProvider.Instance).Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        try
        {
            LythonRuntimeException error;
            if (asynchronous)
                error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context));
            else error = Assert.Throws<LythonRuntimeException>(() => LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(1, deny.Calls);
            Assert.Equal(BigInteger.Zero, Assert.Single(target.Iterate()));
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
            _reservation = governor.ReserveTemporary(limit - governor.CurrentAccountedBytes, span);
            return BigInteger.One;
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
