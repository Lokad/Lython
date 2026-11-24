using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal readonly record struct RegexPatternKey(PyString Pattern, LythonRegexOptions Options)
{
    internal static bool TextEquals(PyString left, PyString right, CancellationToken token, LythonSourceSpan? span)
    {
        if (ReferenceEquals(left, right)) return true;
        if (!token.CanBeCanceled) return left.Equals(right);
        if (left.ByteLength != right.ByteLength) return false;

        // Cached hashes do not cover comparisons of separately allocated text.
        var a = left.Utf8Span;
        var b = right.Utf8Span;
        for (var offset = 0; offset < a.Length;)
        {
            var count = Math.Min(4096, a.Length - offset);
            if (!a.Slice(offset, count).SequenceEqual(b.Slice(offset, count))) return false;
            offset += count;
            if (token.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
        }
        return true;
    }

    // Only a lookup carries cancellation state. Published keys and compiled
    // programs can be shared by concurrent operations with different tokens.
    internal readonly record struct Lookup(RegexPatternKey Key, CancellationToken Token, LythonSourceSpan? Span);

    internal sealed class Comparer : IEqualityComparer<RegexPatternKey>, IAlternateEqualityComparer<Lookup, RegexPatternKey>
    {
        internal static readonly Comparer Instance = new();

        public bool Equals(RegexPatternKey left, RegexPatternKey right) => left.Equals(right);
        public int GetHashCode(RegexPatternKey key) => key.GetHashCode();
        public RegexPatternKey Create(Lookup lookup) => lookup.Key;

        public int GetHashCode(Lookup lookup)
        {
            if (lookup.Token.IsCancellationRequested) throw RuntimeErrors.Cancelled(lookup.Span);
            // Keep the ordinary record hash, including the cached text hash.
            _ = lookup.Key.Pattern.GetHashCode(lookup.Token, lookup.Span);
            return lookup.Key.GetHashCode();
        }

        public bool Equals(Lookup lookup, RegexPatternKey stored)
        {
            var token = lookup.Token;
            if (!token.CanBeCanceled) return Equals(lookup.Key, stored);
            if (token.IsCancellationRequested) throw RuntimeErrors.Cancelled(lookup.Span);
            var key = lookup.Key;
            return key.Options == stored.Options && TextEquals(key.Pattern, stored.Pattern, token, lookup.Span);
        }
    }
}
