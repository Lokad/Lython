using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ExecutableMemberStoreTests
{
    [Fact]
    public void OrdinaryMemberReceiverUsesInstructionsWithoutLoopTargetEvaluation()
    {
        var syntax = LythonFrontend.Compile("def run(receiver,value):\n receiver().value=value").Script!;
        var code = Assert.Single(ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint.Functions).CodeObject!;
        Assert.Empty(code.ExpressionFallbacks);
        Assert.Empty(code.StatementFallbacks);
        Assert.Equal("value", Assert.Single(code.Constants.OfType<ExecutableStoreMember>()).MemberName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeniedAttributeAdmissionAfterReceiverDoesNotMutate(bool asynchronous)
    {
        const long limit = 1024 * 1024;
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(limit) });
        var objectType = Assert.IsType<PyType>(context.State.BuiltinVariables["object"]);
        var container = new PyInstance(new PyType("Box", [objectType], []), context.MemoryGovernor, null);
        using var deny = new DenyCapacity(container, context.MemoryGovernor, limit);
        context.Variables["receiver"] = deny;
        context.Variables["value"] = true;
        var syntax = LythonFrontend.Compile("receiver().value=value").Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        try
        {
            LythonRuntimeException error;
            if (asynchronous)
                error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context));
            else error = Assert.Throws<LythonRuntimeException>(() => LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(1, deny.Calls);
            Assert.Equal(0, container.OwnAttributeCount);
            Assert.Null(context.CurrentExecutableFrame);
            Assert.True(context.MemoryGovernor.PeakAccountedBytes <= limit);
        }
        finally { deny.Dispose(); }
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class DenyCapacity(PyInstance receiver, MemoryGovernor governor, long limit) : LythonRuntime.ICallable, IDisposable
    {
        private IDisposable? _reservation;
        public int Calls { get; private set; }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Calls++;
            _reservation = governor.ReserveTemporary(limit - governor.CurrentAccountedBytes - 32, span);
            return receiver;
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
