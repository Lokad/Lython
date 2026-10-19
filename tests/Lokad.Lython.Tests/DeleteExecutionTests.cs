using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class DeleteExecutionTests
{
    [Theory]
    [InlineData("value=7\ndel value\nreturn 7")]
    [InlineData("del box.value\nreturn 7")]
    [InlineData("del values[key]\nreturn 7")]
    [InlineData("del values[start:stop:step]\nreturn 7")]
    [InlineData("try:del values[key]\nfinally:cleanup()\nreturn 7")]
    public void OrdinaryDeletionStaysExecutableWithoutMirroring(string source)
    {
        var code = Compile(source);
        Assert.Empty(code.StatementFallbacks);
        Assert.Empty(code.ExpressionFallbacks);
        Assert.False(code.RequiresLocalVariableMirroring);
    }

    [Fact]
    public void HelperDeletionStaysExecutableWithoutMirroring()
    {
        var code = Compile("def clear(values):\n del values[0]\n return values\nreturn clear([7])");
        var helper = Assert.IsType<ExecutableCodeObject>(Assert.Single(code.Functions).CodeObject);
        Assert.Empty(helper.StatementFallbacks);
        Assert.Empty(helper.ExpressionFallbacks);
        Assert.False(helper.RequiresLocalVariableMirroring);
    }

    [Theory]
    [InlineData("def clear():\n first=7\n gone=9\n last=11\n del gone\n return first+last\nreturn clear()", 18)]
    [InlineData("def clear():\n values=[0,1,2]\n kept=7\n del values[1]\n return values[1]+kept\nreturn clear()", 9)]
    [InlineData("def make():\n kept=7\n gone=9\n def read():return kept\n del gone\n return read\nreturn make()()", 7)]
    public async Task DeletionPreservesUnrelatedLocalsAndCells(string source, int expected)
    {
        var code = Compile(source);
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            using (var owner = budget.CreateScope())
            {
                var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
                var result = asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
                    : LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
                Assert.Equal(new BigInteger(expected), result);
                Assert.Null(context.CurrentExecutableFrame);
                Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
                Assert.Equal(0, context.Limits.CurrentRecursionDepth);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    private static ExecutableCodeObject Compile(string source)
        => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SliceAdmissionFailsBeforeCallingTheMutationProtocol(bool asynchronous)
    {
        var code = Compile("class Box:\n def __delitem__(self,key):mark()\nbox=Box()\npin()\ndel box[1:3]");
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                new LythonRunOptions { MemoryBudget = owner});
            using var pin = new Pin(budget);
            var mark = new Mark();
            context.Variables["pin"] = pin;
            context.Variables["mark"] = mark;
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            {
                if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
                else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
            });
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
            Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
            Assert.Equal(0, mark.Calls);
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
            Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class Pin(LythonMemoryBudget budget) : LythonRuntime.ICallable, IDisposable
    {
        private IDisposable? _storage;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            var governor = context.MemoryGovernor;
            _storage = governor.ReserveTemporary(budget.LimitBytes!.Value - governor.CurrentAccountedBytes, span);
            return PyNone.Instance;
        }
        public void Dispose() => _storage?.Dispose();
    }

    private sealed class Mark : LythonRuntime.ICallable
    {
        public int Calls { get; private set; }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Calls++;
            return PyNone.Instance;
        }
    }
}
