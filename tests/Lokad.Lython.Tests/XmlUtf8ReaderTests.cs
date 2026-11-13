using System.Text;
using System.Xml;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class XmlUtf8ReaderTests
{
    private static readonly LythonSourceSpan Span = new(7, 3, 1, 8);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(1023)]
    [InlineData(1024)]
    [InlineData(1025)]
    [InlineData(4096)]
    public void ArbitraryReadsAndPeeksPreserveUtf16UnitsAndBorrowedFunding(int capacity)
    {
        var text = new string('a', 1023) + "éΩ😀\r\n" + new string('z', 1021) + "😀éΩend";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            try
            {
                var source = PyString.FromString(text, context.MemoryGovernor, Span);
                using var parser = context.MemoryGovernor.ReserveTemporary(16 * 1024, Span);
                var baseline = budget.ReservedBytes;
                using (var reader = new LythonRuntime.Utf8XmlTextReader(source, context.State, Span))
                {
                    var buffer = new char[capacity];
                    var result = new StringBuilder();
                    while (result.Length < text.Length)
                    {
                        Assert.Equal(text[result.Length], reader.Peek());
                        Assert.Equal(text[result.Length], reader.Peek());
                        var read = reader.Read(buffer, 0, buffer.Length);
                        Assert.InRange(read, 1, Math.Min(capacity, text.Length - result.Length));
                        result.Append(buffer, 0, read);
                        if (result.Length < text.Length) result.Append((char)reader.Read());
                    }
                    Assert.Equal(text, result.ToString());
                    Assert.Equal(-1, reader.Peek());
                    Assert.Equal(-1, reader.Read());
                    Assert.Equal(0, reader.Read(buffer));
                }
                Assert.Equal(baseline, budget.ReservedBytes);
                Assert.Equal(text, source.AsString());
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void XmlParsingObservesCancellationDuringOneLongTextToken()
    {
        using var cancellation = new CancellationTokenSource();
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new()
                { MemoryBudget = owner, CancellationToken = cancellation.Token });
            try
            {
                var text = PyString.FromString("<r>" + new string('é', 12000) + "😀</r>", context.MemoryGovernor, Span);
                using var parser = context.MemoryGovernor.ReserveTemporary(16 * 1024 + 24L * text.ByteLength, Span);
                var baseline = budget.ReservedBytes;
                using var characters = new LythonRuntime.Utf8XmlTextReader(text, context.State, Span);
                using var observed = new CancelAfterTextReader(characters, cancellation);
                using var xml = XmlReader.Create(observed, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
                var error = Assert.Throws<LythonRuntimeException>(() =>
                {
                    while (xml.Read()) if (xml.HasValue) _ = xml.Value;
                });
                Assert.True(observed.SawText);
                Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
                Assert.Equal(Span, error.Span);
                Assert.Equal(baseline, budget.ReservedBytes);
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void RefusedAdmissionRemainsTerminalThroughInputReadsAndPeeks()
    {
        const long limit = 4 * 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            try
            {
                var text = PyString.FromString("<r>😀</r>", context.MemoryGovernor, Span);
                using var parser = context.MemoryGovernor.ReserveTemporary(16 * 1024, Span);
                using var reader = new LythonRuntime.Utf8XmlTextReader(text, context.State, Span);
                var first = Assert.Throws<LythonRuntimeException>(() => context.MemoryGovernor.Reserve(limit + 1, Span));
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, first.Attribution?.Reason);
                Assert.Same(first, Assert.Throws<LythonRuntimeException>(() => reader.Read()));
                Assert.Same(first, Assert.Throws<LythonRuntimeException>(() => reader.Peek()));
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    // Observe real decoded input, not execution checkpoints or elapsed time.
    private sealed class CancelAfterTextReader(TextReader inner, CancellationTokenSource cancellation) : TextReader
    {
        internal bool SawText { get; private set; }
        public override int Read(char[] buffer, int index, int count) => Read(buffer.AsSpan(index, count));
        public override int Read(Span<char> buffer)
        {
            var read = inner.Read(buffer);
            if (buffer[..read].Contains('é')) { SawText = true; cancellation.Cancel(); }
            return read;
        }
    }
}
