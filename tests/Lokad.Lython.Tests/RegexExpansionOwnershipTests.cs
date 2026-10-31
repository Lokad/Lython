using System.Diagnostics.CodeAnalysis;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class RegexExpansionOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(0, 1, 1, 1);

    [Theory]
    [InlineData(64)]
    [InlineData(8192)]
    public void ExpansionFundsGrowthAndFinalCopyOverlapAndRefundsWorkingStorage(long available)
    {
        var root = new LythonMemoryBudget(1024 * 1024);
        using (var owner = root.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner }, regexProvider: Utf8RegexProvider.Instance);
            var match = Match(context);
            using var compiled = match.Pattern.Regex;
            Assert.True(owner.TryReserve(root.LimitBytes!.Value - root.ReservedBytes - available, out var pad));
            using (pad)
            {
                var before = root.ReservedBytes;
                var error = Assert.Throws<LythonRuntimeException>(() => LythonRuntime.ReMatchMembers.ExpandReplacementTemplate(
                    match, PyString.FromString(@"\1"), context, Span));
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                Assert.Equal(before, root.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal(new string('x', 4096), match.Value.AsString());
                Assert.Same(error, Assert.Throws<LythonRuntimeException>(() => context.CheckExecution(Span)));
            }
        }
        Assert.Equal(0, root.ReservedBytes);
        Assert.True(root.TryReserve(root.LimitBytes!.Value, out var reused));
        reused.Dispose();
        Assert.Equal(0, root.ReservedBytes);
    }

    [Fact]
    public void NativeExpansionObservesCancellationWithoutAnInterpreterExitCheck()
    {
        var root = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = root.CreateScope())
        {
            var hooked = new AdmissionHook(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new()
            {
                MemoryBudget = hooked, CancellationToken = cancellation.Token,
            }, regexProvider: Utf8RegexProvider.Instance);
            var match = Match(context);
            using var compiled = match.Pattern.Regex;
            var before = root.ReservedBytes;
            hooked.Admitted = bytes => { if (bytes >= 4096) cancellation.Cancel(); };
            var error = Assert.Throws<LythonRuntimeException>(() => LythonRuntime.ReMatchMembers.ExpandReplacementTemplate(
                match, PyString.FromString(@"\1"), context, Span));
            Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
            Assert.True(cancellation.IsCancellationRequested);
            Assert.Equal(before, root.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Fact]
    public void RepeatedNamedEmptyExpansionReturnsAllPrivatePlanAndLookupStorage()
    {
        var root = new LythonMemoryBudget(1024 * 1024);
        using (var owner = root.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner }, regexProvider: Utf8RegexProvider.Instance);
            var match = Match(context, "(?P<é>x)?", "");
            using var compiled = match.Pattern.Regex;
            var template = PyString.FromString(string.Concat(Enumerable.Repeat(@"\g<é>", 8)));
            var before = root.ReservedBytes;
            for (var iteration = 0; iteration < 4; iteration++)
            {
                Assert.Same(PyString.Empty, LythonRuntime.ReMatchMembers.ExpandReplacementTemplate(match, template, context, Span));
                Assert.Equal(before, root.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Fact]
    public void ExpandedValueRemainsFundedAfterWorkingStorageIsReleased()
    {
        var root = new LythonMemoryBudget(1024 * 1024);
        using (var owner = root.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner }, regexProvider: Utf8RegexProvider.Instance);
            var match = Match(context);
            using var compiled = match.Pattern.Regex;
            var before = root.ReservedBytes;
            var result = LythonRuntime.ReMatchMembers.ExpandReplacementTemplate(match, PyString.FromString(@"<\1>"), context, Span);
            Assert.Equal("<" + new string('x', 4096) + ">", result.AsString());
            Assert.True(root.ReservedBytes > before);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    private static LythonRuntime.ReMatchObject Match(LythonRuntime.ExecutionContext context, string patternText = "(x+)", string? text = null)
    {
        // Use the real regex boundary and funded native captures; call expansion
        // directly so a VM checkpoint cannot hide missing native cancellation.
        var pattern = LythonRuntime.RegexCompiler.CreatePattern([PyString.FromString(patternText)], "re.compile", Span,
            context);
        Assert.True(LythonRuntime.RePatternMembers.TryGetMember(pattern, "match", out var callable));
        return Assert.IsType<LythonRuntime.ReMatchObject>(Assert.IsAssignableFrom<LythonRuntime.ICallable>(callable).Invoke(
            [CallArgumentValue.Positional(PyString.FromString(text ?? new string('x', 4096), context.MemoryGovernor, Span))], Span, context));
    }

    private sealed class AdmissionHook(ILythonMemoryBudget inner) : ILythonMemoryBudget
    {
        internal Action<long>? Admitted;
        public ILythonMemoryScope CreateScope() => inner.CreateScope();
        public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
        {
            if (!inner.TryReserve(bytes, out var admitted)) { reservation = null; return false; }
            reservation = new Reservation(this, admitted);
            Admitted?.Invoke(bytes);
            return true;
        }

        private sealed class Reservation(AdmissionHook probe, ILythonMemoryReservation inner) : ILythonMemoryReservation
        {
            public long Bytes => inner.Bytes;
            public bool TryResize(long bytes)
            {
                var previous = inner.Bytes;
                if (!inner.TryResize(bytes)) return false;
                probe.Admitted?.Invoke(bytes - previous);
                return true;
            }
            public void Dispose() => inner.Dispose();
        }
    }
}
