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
    [InlineData(4096)]
    public void ExpansionFundsPlanAndOutputAndRefundsWorkingStorage(long available)
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
                Assert.Equal(new string('x', 4096), match.String.AsString());
                Assert.Same(error, Assert.Throws<LythonRuntimeException>(() => context.CheckExecution(Span)));
            }
        }
        Assert.Equal(0, root.ReservedBytes);
        Assert.True(root.TryReserve(root.LimitBytes!.Value, out var reused));
        reused.Dispose();
        Assert.Equal(0, root.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GroupAccessObservesCancellationAfterAdmissionAndRefundsScratch(bool commandOwnedSubject)
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
            var text = "!" + new string('x', 4096) + "!";
            var match = Match(context, "!(x+)!", text, commandOwnedSubject);
            using var compiled = match.Pattern.Regex;
            var before = root.ReservedBytes;
            hooked.Admitted = bytes => { if (bytes > 0) cancellation.Cancel(); };
            var error = Assert.Throws<LythonRuntimeException>(() => match.MaterializeGroup(1, context, Span));
            Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
            Assert.True(cancellation.IsCancellationRequested);
            Assert.Equal(before, root.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(text, match.String.AsString());
        }
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
            Assert.True(root.ReservedBytes - before >= result.CommittedOwnedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Fact]
    public void LongLiteralTemplateFitsAlongsideItsRetainedExpansion()
    {
        // Enough for the private plan and one exact result, with no room for
        // the earlier growable output's 32 KiB → 64 KiB replacement.
        const long allowance = 36 * 1024;
        var root = new LythonMemoryBudget(1024 * 1024);
        using (var owner = root.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner },
                regexProvider: Utf8RegexProvider.Instance);
            var match = Match(context, "(?P<g>x)", "x");
            using var compiled = match.Pattern.Regex;
            var literal = new string('z', 32768);
            var template = PyString.FromString(literal + @"\g<g>\é\377", context.MemoryGovernor, Span);
            var templateBytes = template.CommittedOwnedBytes;
            Assert.True(owner.TryReserve(root.LimitBytes!.Value - root.ReservedBytes - allowance, out var pad));
            using (pad)
            {
                var before = root.ReservedBytes;
                var result = LythonRuntime.ReMatchMembers.ExpandReplacementTemplate(match, template, context, Span);
                Assert.Equal(literal + "x\\éÿ", result.AsString());
                Assert.Equal(before + result.CommittedOwnedBytes, root.ReservedBytes);
                Assert.Equal(templateBytes, template.CommittedOwnedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    private static LythonRuntime.ReMatchObject Match(LythonRuntime.ExecutionContext context, string patternText = "(x+)", string? text = null, bool commandOwnedSubject = true)
    {
        // Use the real regex boundary and funded native captures; call expansion
        // directly so a VM checkpoint cannot hide missing native cancellation.
        var pattern = LythonRuntime.RegexCompiler.CreatePattern([PyString.FromString(patternText)], "re.compile", Span,
            context, asynchronous: false).GetAwaiter().GetResult();
        Assert.True(pattern.TryGetMember("match", context, Span, out var callable));
        text ??= new string('x', 4096);
        var subject = commandOwnedSubject ? PyString.FromString(text, context.MemoryGovernor, Span) : PyString.FromString(text);
        return Assert.IsType<LythonRuntime.ReMatchObject>(Assert.IsAssignableFrom<LythonRuntime.ICallable>(callable).Invoke(
            [CallArgumentValue.Positional(subject)], Span, context));
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
