using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal readonly record struct RegexPatternKey(PyString Pattern, LythonRegexOptions Options, int? RequestedFlags = null)
{
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
            if (key.Options != stored.Options || key.RequestedFlags != stored.RequestedFlags) return false;
            if (ReferenceEquals(key.Pattern, stored.Pattern)) return true;
            if (key.Pattern.ByteLength != stored.Pattern.ByteLength) return false;

            // A cache hit on equal, separately allocated UTF-8 patterns can
            // compare the whole borrowed input even when its hash is cached.
            var left = key.Pattern.Utf8Span;
            var right = stored.Pattern.Utf8Span;
            for (var offset = 0; offset < left.Length;)
            {
                var count = Math.Min(4096, left.Length - offset);
                if (!left.Slice(offset, count).SequenceEqual(right.Slice(offset, count))) return false;
                offset += count;
                if (token.IsCancellationRequested) throw RuntimeErrors.Cancelled(lookup.Span);
            }
            return true;
        }
    }
}
