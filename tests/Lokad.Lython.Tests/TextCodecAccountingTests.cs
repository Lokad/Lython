using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// UTF-8 decoding admits substantial output before allocation; backslash
/// escapes can expand every invalid byte into four native bytes.
/// </summary>
[Collection("AllocationSensitive")]
public sealed class TextCodecAccountingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReturnedUtf8SurvivesReaderBufferReuse(bool asynchronous)
    {
        var host = new MockLythonHost();
        const string first = "aé😀\n";
        host.SeedFile("/input", first + new string('x', 32768) + "\nend\n");
        var budget = new LythonMemoryBudget();
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(host, new() { MemoryBudget = owner });
            var handle = LythonRuntime.ExecutionContext.TextFileHandle.ForRead("/input", context,
                LythonRuntime.TextEncodingMode.Utf8, LythonRuntime.TextErrorMode.Strict,
                LythonRuntime.TextNewlineMode.TranslateUniversal);
            var retained = asynchronous ? await handle.ReadLineAsync(-1) : handle.ReadLine();
            _ = asynchronous ? await handle.ReadAsync(-1) : handle.Read(-1);
            handle.Exit();
            Assert.Equal(first, retained.ToString());
            Assert.True(budget.ReservedBytes > 0);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SmallReadNeedsRawCapacityAndItsResultWithoutASecondFullWindow(bool asynchronous)
    {
        const int window = 4096;
        var host = new MockLythonHost();
        host.SeedFile("/input", new string('a', 2 * window));
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(host, new() { MemoryBudget = owner });
            var file = new OpenedByteFile(context.State);
            var beforeOpen = budget.ReservedBytes;
            await file.OpenAsync("/input", new(LythonFileAccess.Read), null, asynchronous);
            var transportStorage = budget.ReservedBytes - beforeOpen;
            using var state = new LythonRuntime.ExecutionContext.ChunkedTextFileReadState(file, context,
                LythonRuntime.TextEncodingMode.Utf8, LythonRuntime.TextErrorMode.Strict,
                LythonRuntime.TextNewlineMode.TranslateUniversal, window);
            // Room for the raw window/carry and generous small-result headroom,
            // but no second window-sized backing. No tiny threshold is pinned.
            Assert.True(owner.TryReserve(budget.LimitBytes!.Value - budget.ReservedBytes - window - 8 - 1024,
                out var pressure));
            using (pressure)
            {
                var before = budget.ReservedBytes;
                var value = asynchronous ? await state.ReadAsync(1) : state.Read(1);
                Assert.Equal("a", value.ToString());
                state.Release();
                Assert.Equal(before + value.CommittedOwnedBytes - transportStorage, budget.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void CancellationDuringUtf8ResultAdmissionPreservesInputFunding(int mode)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                new() { MemoryBudget = hook, CancellationToken = cancellation.Token });
            var input = PyString.FromString("aé😀", context.MemoryGovernor, null);
            var before = budget.ReservedBytes;
            hook.AfterGrowth = _ => cancellation.Cancel();
            var error = Assert.Throws<LythonRuntimeException>(() => LythonRuntime.DecodeUtf8Text(
                input.Utf8Bytes, context.MemoryGovernor, null, (LythonRuntime.TextErrorMode)mode,
                LythonRuntime.TextNewlineMode.TranslateUniversal, context.State));
            Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
            Assert.Equal(before, budget.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal("aé😀", input.AsString());
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void BackslashDecodePeakStaysBounded()
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(65536));
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var payload = new byte[200000];
        Array.Fill(payload, (byte)0xFF);
        _ = LythonRuntime.DecodeUtf8Text(
            new ReadOnlyMemory<byte>(new byte[4] { 0xFF, 0xFF, 0xFF, 0xFF }),
            new MemoryGovernor(new LythonMemoryBudget(null)),
            span,
            LythonRuntime.TextErrorMode.BackslashReplace,
            LythonRuntime.TextNewlineMode.TranslateUniversal);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var failure = Assert.Throws<LythonRuntimeException>(() =>
            LythonRuntime.DecodeUtf8Text(
                new ReadOnlyMemory<byte>(payload),
                governor,
                span,
                LythonRuntime.TextErrorMode.BackslashReplace,
                LythonRuntime.TextNewlineMode.TranslateUniversal));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.True(allocated < 1048576, $"codec scratch allocated {allocated} bytes before failing");
    }
}
