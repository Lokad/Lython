using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime.Numbers;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class UrllibParseModule
    {
        private static async ValueTask<object> CachedUrlSplitAsync(CallArgumentValue[] arguments,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            // Keys use the original call, before defaults and argument binding.
            // Keep positional values ahead of keywords even when *args appeared
            // after a keyword in source. Keyword insertion order stays observable.
            var key = UrlSplitCache.Key.Create(arguments, context, span);
            var retained = false;
            try
            {
                key.Hash = await UrlKeyHashAsync(key, span, context, asynchronous).ConfigureAwait(false);
                var cache = context.State.UrlSplitCache;
                var hit = cache is null ? null : await cache.FindAsync(key, span, context, asynchronous).ConfigureAwait(false);
                if (hit is not null)
                {
                    cache!.Touch(hit);
                    return hit.Result;
                }

                var bound = CallBinder.BindNamedArgumentsWithPresence(key.Arguments, span,
                    LythonKnownCallableSignatures.UrlSplit, PythonCallableKind.Builtin);
                if (!bound.Assigned[1]) bound.Values[1] = Text.PyString.Empty;
                if (!bound.Assigned[2]) bound.Values[2] = true;
                var result = (UrlResult)await SplitUrlCoreAsync(bound.Values, span, context, asynchronous).ConfigureAwait(false);

                // A guest callback may recursively populate this same key.
                // CPython returns the outer result while preserving the inner
                // cached result. A second lookup must reuse the original hash.
                cache = context.State.UrlSplitCache;
                if (cache is not null && await cache.FindAsync(key, span, context, asynchronous).ConfigureAwait(false) is not null)
                    return result;
                if (cache is null)
                {
                    cache = new UrlSplitCache(context.MemoryGovernor, span);
                    context.State.UrlSplitCache = cache;
                }
                try
                {
                    retained = await cache.InsertAsync(key, result, span, context, asynchronous).ConfigureAwait(false);
                }
                catch
                {
                    if (cache.Count == 0)
                    {
                        context.State.UrlSplitCache = null;
                        cache.Clear();
                    }
                    throw;
                }
                return result;
            }
            finally
            {
                if (!retained) key.Release();
            }
        }

        private static async ValueTask<ulong> UrlKeyHashAsync(UrlSplitCache.Key key,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var hash = 2870177450012600261UL;
            var count = 0;
            var keywords = false;
            foreach (var argument in key.Arguments)
            {
                if (argument.IsKeyword)
                {
                    if (!keywords) { MixUrlHash(ref hash, 0x117AC43DUL); count++; keywords = true; }
                    MixUrlHash(ref hash, unchecked((ulong)StringComparer.Ordinal.GetHashCode(argument.KeywordName))); count++;
                }
                MixUrlHash(ref hash, await UrlValueHashAsync(argument.Value, span, context, asynchronous).ConfigureAwait(false)); count++;
            }
            foreach (var type in key.Types)
            {
                MixUrlHash(ref hash, unchecked((ulong)RuntimeHelpers.GetHashCode(type))); count++;
            }
            return FinishUrlHash(hash, count);
        }

        private static async ValueTask<ulong> UrlValueHashAsync(object value, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            if (value is PyInstance instance)
            {
                if (PyHashProtocols.IsEqWithoutHash(instance)) throw RuntimeErrors.UnhashableType(value, span);
                if (instance.Type.TryLookupInMro("__hash__", 0, out var slot, out _))
                {
                    if (slot is PyNone) throw RuntimeErrors.UnhashableType(value, span);
                    var bound = asynchronous ? await PyAttributeLookup.BindForInstanceAsync(instance, slot, context, span).ConfigureAwait(false)
                        : PyAttributeLookup.BindForInstance(instance, slot, context, span);
                    var hash = await CallAsync(bound, [], span, context, asynchronous).ConfigureAwait(false);
                    if (!PyNumberOps.TryAsInteger(hash, out var integer)) throw RuntimeErrors.Type("__hash__ method should return an integer", span);
                    if (integer < long.MinValue || integer > long.MaxValue)
                    {
                        var modulus = (BigInteger.One << 61) - 1;
                        integer = BigInteger.Abs(integer) % modulus * integer.Sign;
                    }
                    var normalized = (long)integer;
                    return unchecked((ulong)(normalized == -1 ? -2 : normalized));
                }
            }
            if (PyTupleLike.TryGetItems(value, out var items))
            {
                using var guard = PyStructuralGuard.EnterSingle(value, span, context);
                var hash = 2870177450012600261UL;
                foreach (var item in items)
                    MixUrlHash(ref hash, await UrlValueHashAsync(item, span, context, asynchronous).ConfigureAwait(false));
                return FinishUrlHash(hash, items.Count);
            }
            using (PyStructuralGuard.PushAmbient(context, span))
                return unchecked((ulong)(long)(BigInteger)ComputeBuiltinHash(value, span));
        }

        private static void MixUrlHash(ref ulong hash, ulong value)
        {
            unchecked
            {
                hash += value * 14029467366897019727UL;
                hash = (hash << 31) | (hash >> 33);
                hash *= 11400714785074694791UL;
            }
        }

        private static ulong FinishUrlHash(ulong hash, int count)
        {
            hash = unchecked(hash + ((ulong)count ^ (2870177450012600261UL ^ 3527539UL)));
            return hash == ulong.MaxValue ? 1546275796UL : hash;
        }

        private static async ValueTask<bool> UrlKeyValueEqualsAsync(object left, object right,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            if (ReferenceEquals(left, right)) return true;
            if (PyTupleLike.TryGetItems(left, out var first) && PyTupleLike.TryGetItems(right, out var second))
            {
                using var guard = PyStructuralGuard.EnterPair(left, right, span, context);
                for (var i = 0; i < Math.Min(first.Count, second.Count); i++)
                    if (!await UrlKeyValueEqualsAsync(first[i], second[i], span, context, asynchronous).ConfigureAwait(false)) return false;
                return first.Count == second.Count;
            }
            return asynchronous ? await MembershipEqualsAsync(left, right, context, span).ConfigureAwait(false)
                : MembershipEquals(left, right, context, span);
        }

        internal sealed class UrlSplitCache
        {
            private const int Capacity = 128;
            private const long ShellBytes = 96;
            private const long EntryBytes = 80;
            private readonly MemoryGovernor _governor;
            private Entry? _first;
            private Entry? _last;
            private Entry? _oldest;
            private Entry? _newest;
            internal int Count { get; private set; }
            internal long CommittedBytes { get; private set; } = ShellBytes;

            internal UrlSplitCache(MemoryGovernor governor, LythonSourceSpan span)
            {
                governor.Reserve(ShellBytes, span);
                governor.Commit(ShellBytes);
                _governor = governor;
            }

            // Lookup order is independent of recency: a hit moves the LRU link
            // without changing which colliding stored key compares first.
            internal async ValueTask<Entry?> FindAsync(Key key, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
            {
                while (true)
                {
                    var restart = false;
                    for (var entry = _first; entry is not null; entry = entry.Next)
                    {
                        context.CheckExecution(span);
                        if (entry.Key.Hash != key.Hash) continue;
                        var equal = await entry.Key.EqualsAsync(key, span, context, asynchronous).ConfigureAwait(false);
                        // A guest equality may evict the key being compared.
                        // An unrelated insertion keeps this stored key valid.
                        if (!entry.Active) { restart = true; break; }
                        if (equal) return entry;
                    }
                    if (!restart) return null;
                }
            }

            internal void Touch(Entry entry)
            {
                UnlinkRecency(entry);
                AppendRecency(entry);
            }

            internal async ValueTask<bool> InsertAsync(Key key, UrlResult result,
                LythonSourceSpan span, ExecutionContext context, bool asynchronous)
            {
                // Reserve a new link before eviction; a budget denial leaves
                // the existing cache intact and the caller refunds its key.
                _governor.Reserve(EntryBytes, span);
                _governor.Commit(EntryBytes);
                var published = false;
                try
                {
                    var entry = new Entry(key, result);
                    if (Count == Capacity) Remove(_oldest!);
                    var collision = await FindAsync(key, span, context, asynchronous).ConfigureAwait(false);
                    if (collision is not null) Remove(collision);
                    // Equality can itself populate the cache while suspended.
                    if (Count == Capacity) Remove(_oldest!);
                    CommittedBytes += EntryBytes + key.Bytes;
                    entry.Previous = _last;
                    if (_last is null) _first = entry;
                    else _last.Next = entry;
                    _last = entry;
                    AppendRecency(entry);
                    Count++;
                    entry.Active = true;
                    published = true;
                    return true;
                }
                finally
                {
                    if (!published) _governor.Release(EntryBytes);
                }
            }

            private void AppendRecency(Entry entry)
            {
                entry.Older = _newest;
                entry.Newer = null;
                if (_newest is null) _oldest = entry;
                else _newest.Newer = entry;
                _newest = entry;
            }

            private void UnlinkRecency(Entry entry)
            {
                if (entry.Older is null) _oldest = entry.Newer;
                else entry.Older.Newer = entry.Newer;
                if (entry.Newer is null) _newest = entry.Older;
                else entry.Newer.Older = entry.Older;
            }

            private void Remove(Entry entry)
            {
                if (entry.Previous is null) _first = entry.Next;
                else entry.Previous.Next = entry.Next;
                if (entry.Next is null) _last = entry.Previous;
                else entry.Next.Previous = entry.Previous;
                UnlinkRecency(entry);
                CommittedBytes -= EntryBytes + entry.Key.Bytes;
                entry.Key.Release();
                _governor.Release(EntryBytes);
                Count--;
                entry.Active = false;
            }

            internal void Clear()
            {
                while (_first is not null) Remove(_first);
                _governor.Release(CommittedBytes);
                CommittedBytes = 0;
            }

            internal sealed class Entry(Key key, UrlResult result)
            {
                internal readonly Key Key = key;
                internal readonly UrlResult Result = result;
                internal Entry? Previous, Next, Older, Newer;
                internal bool Active;
            }

            internal sealed class Key(CallArgumentValue[] arguments, object[] types, MemoryGovernor governor, long bytes)
            {
                internal readonly CallArgumentValue[] Arguments = arguments;
                internal readonly object[] Types = types;
                internal readonly long Bytes = bytes;
                internal ulong Hash;

                internal static Key Create(CallArgumentValue[] arguments, ExecutionContext context, LythonSourceSpan span)
                {
                    var bytes = 128L + 40L * arguments.Length;
                    context.MemoryGovernor.Reserve(bytes, span);
                    try
                    {
                        var ordered = new CallArgumentValue[arguments.Length];
                        var types = new object[arguments.Length];
                        var index = 0;
                        foreach (var argument in arguments)
                            if (argument.IsPositional) ordered[index++] = argument;
                        foreach (var argument in arguments)
                            if (argument.IsKeyword) ordered[index++] = argument;
                        for (var i = 0; i < ordered.Length; i++)
                        {
                            var value = ordered[i].Value;
                            types[i] = value is PyInstance instance ? instance.Type
                                : value is UrlResult result ? result.Type
                                : value is PyNamedTupleObject namedTuple ? namedTuple.Type
                                : TryGetValueClass(value, context, out var type) ? type : value.GetType();
                        }
                        var key = new Key(ordered, types, context.MemoryGovernor, bytes);
                        context.MemoryGovernor.Commit(bytes);
                        return key;
                    }
                    catch
                    {
                        context.MemoryGovernor.ReleaseReserved(bytes);
                        throw;
                    }
                }

                internal async ValueTask<bool> EqualsAsync(Key other, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
                {
                    if (Arguments.Length != other.Arguments.Length) return false;
                    for (var i = 0; i < Arguments.Length; i++)
                    {
                        var left = Arguments[i]; var right = other.Arguments[i];
                        if (left.Placement != right.Placement || left.IsKeyword && left.KeywordName != right.KeywordName) return false;
                        if (!await UrlKeyValueEqualsAsync(left.Value, right.Value, span, context, asynchronous).ConfigureAwait(false)) return false;
                    }
                    for (var i = 0; i < Types.Length; i++)
                        if (!ReferenceEquals(Types[i], other.Types[i])) return false;
                    return true;
                }

                internal void Release() => governor.Release(Bytes);
            }
        }
    }
}
