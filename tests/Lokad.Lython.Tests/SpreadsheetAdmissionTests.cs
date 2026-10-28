using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class SpreadsheetAdmissionTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Fact]
    public void WorksheetCreationRefusesBeforePublishingAnotherSheet()
    {
        var root = new LythonMemoryBudget(64);
        using (var owner = root.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var book = LythonRuntime.OpenPyxlWorkbook.CreateNew(false, false);
            Assert.True(book.TryGetMember("create_sheet", out var member));
            var error = Assert.Throws<LythonRuntimeException>(() => Assert.IsAssignableFrom<LythonRuntime.ICallable>(member).Invoke([], Span, context));
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
            Assert.Single(book.Worksheets);
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Fact]
    public void CopyRefusesBeforeDuplicatingNativeCellsAndPreservesSourceAliases()
    {
        var root = new LythonMemoryBudget(65536);
        using (var owner = root.CreateScope())
        {
            var governor = new MemoryGovernor(owner);
            var sheet = new LythonRuntime.OpenPyxlWorksheet("Sheet");
            sheet.AttachMemoryGovernor(governor, Span);
            for (var i = 1; i <= 32; i++) sheet.SetCellValue(i, 1, BigInteger.One);
            var alias = sheet.GetCellObject(1, 1);
            sheet.SetCellNumberFormat(alias, PyString.FromString("0.00"));
            var copy = sheet.Copy("Copy");
            Assert.NotSame(alias, copy.GetCellObject(1, 1));
            Assert.Equal("0.00", copy.GetCellNumberFormat(1, 1));
            copy.GetCellObject(1, 1).Value = new BigInteger(2);
            Assert.Equal(BigInteger.One, alias.Value);
            Assert.True(root.TryReserve(root.LimitBytes!.Value - root.ReservedBytes - 64, out var hold));
            using (hold)
            {
                var error = Assert.Throws<LythonRuntimeException>(() => sheet.Copy("Refused"));
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                Assert.Equal(BigInteger.One, alias.Value);
                Assert.Equal(32, sheet.Cells.Count);
            }
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SavePayloadAdmissionOrCancellationPrecedesTheFinalCopy(bool cancelAfterAdmission)
    {
        var root = cancelAfterAdmission ? new LythonMemoryBudget() : new LythonMemoryBudget(64);
        using var cancellation = new CancellationTokenSource();
        using var stream = new MemoryStream(new byte[32768], writable: false);
        using (var owner = root.CreateScope())
        {
            var observer = new CancelOnAdmission(owner, cancellation);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions
            { MemoryBudget = cancelAfterAdmission ? observer : owner, CancellationToken = cancellation.Token });
            observer.Armed = true;
            var before = GC.GetAllocatedBytesForCurrentThread();
            var error = Assert.Throws<LythonRuntimeException>(() => LythonRuntime.OpenPyxlPackage.CopySavePayload(stream, context, Span));
#if !DEBUG
            Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < stream.Length);
#endif
            Assert.Equal(cancelAfterAdmission ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(0, root.ReservedBytes);
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    private static LythonRuntime.ICallable Member(LythonRuntime.OpenPyxlWorksheet sheet, string name, LythonRuntime.ExecutionContext context)
    {
        Assert.True(sheet.TryGetMember(name, context, Span, out var value));
        return Assert.IsAssignableFrom<LythonRuntime.ICallable>(value);
    }

    [Fact]
    public void EmptyCellsAreAdmittedAndClearingValuesDoesNotRefundRetainedCapacity()
    {
        var root = new LythonMemoryBudget(65536);
        using (var owner = root.CreateScope())
        {
            var governor = new MemoryGovernor(owner);
            var sheet = new LythonRuntime.OpenPyxlWorksheet("Sheet");
            sheet.AttachMemoryGovernor(governor, Span);
            var cells = Enumerable.Range(1, 32).Select(i => sheet.GetCellObject(i, 1)).ToArray();
            foreach (var cell in cells) cell.Value = BigInteger.One;
            var retained = root.ReservedBytes;
            foreach (var cell in cells) cell.Value = PyNone.Instance;
            Assert.Equal(retained, root.ReservedBytes);
            Assert.Equal(32, sheet.Cells.Count);
            Assert.True(root.TryReserve(root.LimitBytes!.Value - root.ReservedBytes - 64, out var hold));
            using (hold)
            {
                var error = Assert.Throws<LythonRuntimeException>(() => sheet.GetCellObject(33, 1));
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                Assert.Equal(32, sheet.Cells.Count);
                Assert.Same(cells[0], sheet.Cells[new(1, 1)]);
            }
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Theory]
    [InlineData("insert_rows")]
    [InlineData("move_range")]
    [InlineData("iter_rows")]
    public void WorkingStorageRefusesBeforeCellsOrAliasesAreMutated(string operation)
    {
        var root = new LythonMemoryBudget(65536);
        using (var owner = root.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var sheet = new LythonRuntime.OpenPyxlWorksheet("Sheet");
            sheet.AttachMemoryGovernor(context.MemoryGovernor, Span, context);
            for (var i = 1; i <= 32; i++) sheet.SetCellValue(i, 1, BigInteger.One);
            var alias = sheet.GetCellObject(1, 1);
            var callable = Member(sheet, operation, context);
            var iterator = operation == "iter_rows"
                ? Assert.IsAssignableFrom<IPyIteratorValue>(callable.Invoke([
                    CallArgumentValue.Keyword("min_row", BigInteger.One), CallArgumentValue.Keyword("max_row", BigInteger.One),
                    CallArgumentValue.Keyword("max_col", new BigInteger(32))], Span, context)) : null;
            Assert.Equal(32, sheet.Cells.Count);
            Assert.True(root.TryReserve(root.LimitBytes!.Value - root.ReservedBytes - 64, out var hold));
            using (hold)
            {
                var error = Assert.Throws<LythonRuntimeException>(() =>
                {
                    if (iterator is not null) { iterator.TryMoveNext(out _); return; }
                    _ = operation == "move_range"
                        ? callable.Invoke([CallArgumentValue.Positional(PyString.FromString("A1:A32")), CallArgumentValue.Keyword("rows", BigInteger.One)], Span, context)
                        : callable.Invoke([CallArgumentValue.Positional(BigInteger.One)], Span, context);
                });
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                Assert.Equal(32, sheet.Cells.Count);
                Assert.Equal(1, alias.Row);
                Assert.Same(alias, sheet.Cells[new(1, 1)]);
                Assert.Equal(BigInteger.One, alias.Value);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Fact]
    public void StructuralWorkObservesCancellationAfterAdmissionAndReturnsScratch()
    {
        var root = new LythonMemoryBudget();
        using var cancellation = new CancellationTokenSource();
        using (var owner = root.CreateScope())
        {
            var observer = new CancelOnAdmission(owner, cancellation);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = observer, CancellationToken = cancellation.Token });
            var sheet = new LythonRuntime.OpenPyxlWorksheet("Sheet");
            sheet.AttachMemoryGovernor(context.MemoryGovernor, Span, context);
            for (var i = 1; i <= 32; i++) sheet.SetCellValue(i, 1, BigInteger.One);
            var callable = Member(sheet, "insert_rows", context);
            var baseline = root.ReservedBytes;
            observer.Armed = true;
            var error = Assert.Throws<LythonRuntimeException>(() => callable.Invoke([CallArgumentValue.Positional(BigInteger.One)], Span, context));
            Assert.True(observer.Triggered);
            Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
            Assert.Equal(baseline, root.ReservedBytes);
            Assert.Equal(1, sheet.Cells[new(1, 1)].Row);
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    private sealed class CancelOnAdmission(ILythonMemoryBudget inner, CancellationTokenSource cancellation) : ILythonMemoryBudget
    {
        public bool Armed, Triggered;
        public ILythonMemoryScope CreateScope() => inner.CreateScope();
        public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
        {
            if (!inner.TryReserve(bytes, out var actual)) { reservation = null; return false; }
            reservation = new Observed(this, actual);
            return true;
        }
        private void Cancel() { Triggered = true; cancellation.Cancel(); }
        private sealed class Observed(CancelOnAdmission owner, ILythonMemoryReservation inner) : ILythonMemoryReservation
        {
            public long Bytes => inner.Bytes;
            public bool TryResize(long bytes)
            {
                var growth = bytes > inner.Bytes;
                if (!inner.TryResize(bytes)) return false;
                if (owner.Armed && growth) { owner.Cancel(); }
                return true;
            }
            public void Dispose() => inner.Dispose();
        }
    }
}
