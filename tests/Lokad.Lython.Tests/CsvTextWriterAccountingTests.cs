using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CsvTextWriterAccountingTests
{
    [Fact]
    public void DeniedEscapingRowNeverReachesTheCallbackOrLeaksBuilderCapacity()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 500 });
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var callback = new CapturingWrite();
        var writer = CreateWriter(context, span, callback);
        var row = new LythonRuntime.CsvCell[] { new(PyString.FromString("x"), LythonRuntime.CsvCellKind.Text) };
        // This cap allows the intermediate row and byte builder, then denies
        // the callback-visible owned string. Repeating proves cleanup.
        for (var i = 0; i < 100; i++)
        {
            var error = Assert.Throws<LythonRuntimeException>(() =>
                LythonRuntime.CsvWriterMembers.WriteRow(writer, row, span, context, 0));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Null(callback.Value);
            Assert.Empty(writer.Rows);
            Assert.Equal(128, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallbackReceivesAnOwnedStringAndItsResultIsPreserved(bool asynchronous)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var callback = new CapturingWrite();
        var writer = CreateWriter(context, span, callback);
        var row = new LythonRuntime.CsvCell[] { new(PyString.FromString("é😀"), LythonRuntime.CsvCellKind.Text) };
        var result = asynchronous
            ? await LythonRuntime.CsvWriterMembers.WriteRowAsync(writer, row, span, context, 0)
            : LythonRuntime.CsvWriterMembers.WriteRow(writer, row, span, context, 0);
        Assert.Same(callback.Result, result);
        Assert.NotNull(callback.Value);
        Assert.Equal("é😀\n", callback.Value.AsString());
        Assert.Same(context.MemoryGovernor, callback.Value.OwnerMemoryGovernor);
        Assert.Empty(writer.Rows);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private static LythonRuntime.CsvWriterObject CreateWriter(LythonRuntime.ExecutionContext context,
        LythonSourceSpan span, CapturingWrite callback)
        => new(new LythonRuntime.CsvOptions(PyStringOps.CommaLiteral, PyString.FromString("\""),
            LythonRuntime.CsvQuotingMode.Minimal, true, null, false, PyString.FromString("\n"), false),
            callback, context.MemoryGovernor, span);

    private sealed class CapturingWrite : LythonRuntime.ICallable
    {
        public PyString? Value { get; private set; }
        public object Result { get; } = new BigInteger(42);
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Value = Assert.IsType<PyString>(Assert.Single(arguments).Value);
            return Result;
        }
    }
}
