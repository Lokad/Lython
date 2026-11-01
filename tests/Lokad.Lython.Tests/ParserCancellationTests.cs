using System.Collections;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class ParserCancellationTests
{
    [Theory]
    [InlineData("value = 1\n")]
    [InlineData("\n\n")]
    [InlineData("value = (\n")]
    [InlineData("value = 'text'\n")]
    public void CancellationAfterTokenizationStopsOrdinaryParsing(string source)
    {
        var tokens = PythonTokenization.Read(source);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var error = Assert.Throws<LythonRuntimeException>(() =>
            new Parser(tokens, cancellationToken: cancellation.Token).Parse());
        Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActiveParsingObservesCancellationAndPreservesFirstAdmissionRefusal(bool refuse)
    {
        var root = new LythonMemoryBudget(1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = root.CreateScope())
        {
            var governor = new MemoryGovernor(owner);
            governor.Reserve(128, null);
            governor.Commit(128);
            LythonRuntimeException? refusal = null;
            var original = PythonTokenization.Read("value = 1\n");
            var tokens = new TokenizedSource(original.Buffer, new CancellingTokens(original.Tokens, () =>
            {
                if (refuse) refusal = governor.RefuseAdmission("test admission refused", 2048, null);
                cancellation.Cancel();
            }), [original.Buffer.Length - 1], false);
            Assert.False(cancellation.IsCancellationRequested);
            var error = Assert.Throws<LythonRuntimeException>(() => new Parser(tokens, governor, cancellation.Token).Parse());
            Assert.True(cancellation.IsCancellationRequested);
            if (refuse) Assert.Same(refusal, error);
            else Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
            Assert.Equal(128, root.ReservedBytes);
        }
        Assert.Equal(0, root.ReservedBytes);
        using var reused = root.CreateScope();
        Assert.True(reused.TryReserve(1024, out _));
    }

    // TokenizedSource already accepts an immutable token view. Trigger cancellation
    // during a real parser read, with no timer, new production hook or check count.
    private sealed class CancellingTokens(IReadOnlyList<SourceToken> tokens, Action cancel) : IReadOnlyList<SourceToken>
    {
        private Action? _cancel = cancel;
        public int Count => tokens.Count;
        public SourceToken this[int index]
        {
            get
            {
                var token = tokens[index];
                if (_cancel is null && token.Token == Token.End)
                    throw new InvalidOperationException("Parsing reached the end after cancellation; an exit-only check is too late.");
                Interlocked.Exchange(ref _cancel, null)?.Invoke();
                return token;
            }
        }
        public IEnumerator<SourceToken> GetEnumerator() => tokens.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
