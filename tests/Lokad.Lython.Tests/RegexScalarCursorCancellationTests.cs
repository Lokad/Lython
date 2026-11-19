using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Capture = Lokad.Lython.Runtime.LythonRuntime.ReCaptureRange;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;
using Position = Lokad.Lython.Runtime.LythonRuntime.RegexScalarPosition;
using SubjectRange = Lokad.Lython.Runtime.LythonRuntime.RegexSubjectRange;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class RegexScalarCursorCancellationTests
{
    private static readonly LythonSourceSpan Span = new(13, 4, 2, 6);
    private delegate void Project(SubjectRange range, LythonRegexWindow window, Capture[] captures, ref Position previous, Span<long> coordinates,
        Context context, LythonSourceSpan span, Comparison<long>? coordinateComparison);
    private static readonly Project ProjectCaptures = typeof(LythonRuntime)
        .GetNestedType("RegexMatcher", BindingFlags.NonPublic)!
        .GetMethod("ProjectScalarBounds", BindingFlags.NonPublic | BindingFlags.Static)!
        .CreateDelegate<Project>();

    [Fact]
    public async Task ActiveBackwardProjectionCancelsBeforePublishingTheCursor()
    {
        // Exercise nonallocating rewinds without depending on any particular
        // rewind taking longer than the scheduler needs to deliver cancellation.
        const int characters = 512 * 1024;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions
                { MemoryBudget = owner, CancellationToken = cancellation.Token });
            var byteLength = checked(2 * (characters + 2));
            using var inputStorage = context.MemoryGovernor.ReserveTemporary(
                PyString.EstimateApproximateBytes(byteLength), Span);
            var input = new byte[byteLength];
            for (var i = 0; i < input.Length; i += 2) { input[i] = 0xce; input[i + 1] = 0xb1; }
            var text = PyString.FromOwnedUtf8(input, context.MemoryGovernor, Span, inputStorage);
            _ = text.Length;
            // Fund captures and projection scratch through their actual user's
            // completion. No reservation or provider callback interrupts this scan.
            using var scratch = context.MemoryGovernor.ReserveTemporary(512, Span);
            var captures = new Capture[]
            {
                new(2 * characters, 2 * (characters + 1), -1, -1),
                new(2 * (characters * 3 / 5), 2 * characters, -1, -1),
            };
            scratch.Commit(512);
            var previous = new Position(2 * characters, characters);
            var coordinates = new long[2 * captures.Length];
            var original = previous;
            var baseline = budget.ReservedBytes;
            using var started = new ManualResetEventSlim();
            var stop = false;
            var run = Task.Factory.StartNew(() =>
            {
                while (!Volatile.Read(ref stop))
                {
                    previous = original;
                    ProjectCaptures(new(text, 0, characters + 2), new(0, characters + 2, 0, byteLength),
                        captures, ref previous, coordinates, context, Span, null);
                    // Signal only after an actual rewind completes. A later
                    // attempt must observe cancellation before updating its cursor.
                    started.Set();
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            try
            {
                Assert.True(started.Wait(TimeSpan.FromSeconds(10)));
                Assert.False(run.IsCompleted);
                Assert.Equal(baseline, budget.ReservedBytes);
                cancellation.Cancel();
                var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                    await run.WaitAsync(TimeSpan.FromSeconds(10)));
                Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
                Assert.Equal(Span, failure.Span);
                Assert.Equal(original, previous);
                Assert.Equal(baseline, budget.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal(0, context.State.CurrentInterpreterDepth);
            }
            finally
            {
                cancellation.Cancel();
                Volatile.Write(ref stop, true);
                // Keep subject/capture funding until this user actually ends,
                // including when an assertion fails or cancellation is broken.
                try { await run; }
                catch (LythonRuntimeException error) when (error.Attribution?.Reason == LythonFailureReason.Cancelled) { }
                context.State.CloseResources();
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
        using (var script = new LythonEngine().Compile("return 42", budget))
        using (var result = script.Run(new MockLythonHost())) Assert.True(result.Success);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void CancellationDuringCoordinateOrderingDoesNotPublishScalarPositions()
    {
        using var cancellation = new CancellationTokenSource();
        var budget = new LythonMemoryBudget(4096);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
            { MemoryBudget = owner, CancellationToken = cancellation.Token });
        try
        {
            using var scratch = context.MemoryGovernor.ReserveTemporary(2048, Span);
            var text = PyString.FromString("éx");
            var range = new SubjectRange(text, 0, 2);
            var window = new LythonRegexWindow(0, 2, 0, 3);
            var captures = new Capture[40];
            Array.Fill(captures, new Capture(0, 2, -1, -1));
            var coordinates = new long[2 * captures.Length];
            var previous = new Position(0, 0);
            var baseline = budget.ReservedBytes;
            var failure = Assert.Throws<LythonRuntimeException>(() =>
                ProjectCaptures(range, window, captures, ref previous, coordinates, context, Span, (left, right) =>
                {
                    cancellation.Cancel();
                    context.CheckExecution(Span);
                    return left.CompareTo(right);
                }));
            Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
            Assert.Equal(Span, failure.Span);
            Assert.Equal(new Position(0, 0), previous);
            Assert.All(captures, capture => Assert.Equal((-1, -1), (capture.Start, capture.End)));
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        finally { context.State.CloseResources(); }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
