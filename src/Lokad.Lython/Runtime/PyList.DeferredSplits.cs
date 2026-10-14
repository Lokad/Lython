using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class PyList
{
    // Restricted to the fresh unique slices constructed by string split-family
    // producers. No arbitrary caller may attach aliases or previously exposed
    // untracked values: the list must be their sole owner before publication.
    internal bool TryDeferFreshSplitOwnership(ChargeReclamationPool pool, LythonSourceSpan? span)
    {
        if (_memoryGovernor is null || !pool.OwnsGovernor(_memoryGovernor) ||
            ChargeReclamationPool.IsTrackedValue(this)) return false;

        long payloadBytes = 0;
        var count = 0;
        foreach (var item in _items)
        {
            if (item is not PyString text) return false;
            if (ReferenceEquals(text, PyString.Empty)) continue;
            if (!ReferenceEquals(text.OwnerMemoryGovernor, _memoryGovernor) || text.ReclamationEntry is not null)
                return false;
            payloadBytes = checked(payloadBytes + text.CommittedOwnedBytes);
            count++;
        }

        if (count == 0) return false;
        var metadataAndFees = checked(DeferredSplitStorage.MetadataBytes + count * ChargeReclamationPool.EntryChargeBytes);
        var original = _items;
        DeferredSplitStorage deferred;
        var reserved = false;
        try
        {
            _memoryGovernor.Reserve(metadataAndFees, span);
            reserved = true;
            deferred = new DeferredSplitStorage(this, original, pool, _memoryGovernor, span,
                checked(payloadBytes + metadataAndFees));
            _memoryGovernor.Commit(metadataAndFees);
            reserved = false;
        }
        catch
        {
            if (reserved) _memoryGovernor.ReleaseReserved(metadataAndFees);
            // This construction transaction exposed none of its fresh slices.
            _memoryGovernor.Release(checked(original.CommittedBytes + payloadBytes));
            throw;
        }

        _items = deferred;
        try
        {
            pool.TrackFreshMutable(this, CommittedStorageBytes, span);
        }
        catch
        {
            // TrackFreshMutable already refunded the full unpublished snapshot.
            _items = original;
            throw;
        }

        return true;
    }

    // Only the synchronous native join consumes this borrowed sequence. It
    // copies UTF-8 bytes without publishing items or entering guest callbacks.
    internal bool TryBorrowSplitForJoin([NotNullWhen(true)] out IEnumerable<object>? items)
    {
        if (_items is DeferredSplitStorage deferred)
        {
            items = deferred.BorrowForJoin();
            return true;
        }

        items = null;
        return false;
    }

    // No string holds this decorator. Reads acquire the exposed identity;
    // wholesale copies/mutations acquire all remaining identities. The
    // owner reference keeps the list's aggregate live during exhaustion relief.
    internal sealed class DeferredSplitStorage(
        PyList owner, IPyListStorage inner, ChargeReclamationPool pool,
        MemoryGovernor governor, LythonSourceSpan? span, long remainingBytes) : IPyListStorage
    {
        internal const long MetadataBytes = 128;
        private long _remainingBytes = remainingBytes;
        private int _nextIndex;

        public int Count => inner.Count;
        public long CommittedBytes => checked(inner.CommittedBytes + _remainingBytes);

        internal IPyListStorage Acquire()
        {
            if (_remainingBytes == 0) return inner;
            try
            {
                while (_nextIndex < inner.Count)
                {
                    AcquirePart((PyString)inner[_nextIndex]);
                    _nextIndex++;
                }

                if (_remainingBytes != 0 && _remainingBytes != MetadataBytes)
                    throw new InvalidOperationException("Pending split ownership did not reconcile.");
                FinishAcquisition();
                return inner;
            }
            finally
            {
                GC.KeepAlive(owner);
            }
        }

        private void AcquirePart(PyString text)
        {
            if (ReferenceEquals(text, PyString.Empty) || text.ReclamationEntry is not null) return;
            pool.TrackPrefundedString(text, span);
            _remainingBytes -= checked(text.CommittedOwnedBytes + ChargeReclamationPool.EntryChargeBytes);
            ChargeReclamationPool.NotifyStorageReplaced(owner, owner.CommittedStorageBytes);
        }

        private void FinishAcquisition()
        {
            if (_remainingBytes != MetadataBytes) return;
            _remainingBytes = 0;
            owner._items = inner;
            governor.Release(MetadataBytes);
            ChargeReclamationPool.NotifyStorageReplaced(owner, owner.CommittedStorageBytes);
        }

        internal IEnumerable<object> BorrowForJoin()
        {
            for (var index = 0; index < owner.Count; index++)
            {
                var storage = owner._items;
                yield return storage is DeferredSplitStorage pending ? pending.RawItem(index) : storage[index];
            }
        }

        private object RawItem(int index) => inner[index];

        public object this[int index]
        {
            get
            {
                var item = inner[index]; // Preserve validation before acquisition.
                if (_remainingBytes != 0)
                {
                    try
                    {
                        AcquirePart((PyString)item);
                        FinishAcquisition();
                    }
                    finally
                    {
                        GC.KeepAlive(owner);
                    }
                }
                return item;
            }
            set => Acquire()[index] = value;
        }

        public void Add(object value) => Acquire().Add(value);
        public void AddRange(IEnumerable<object> values) => Acquire().AddRange(values);
        public void InsertAt(int index, object value) => Acquire().InsertAt(index, value);
        public void RemoveRangeAt(int index, int count) => Acquire().RemoveRangeAt(index, count);
        public void ReplaceRange(int index, int count, IReadOnlyList<object> values) => Acquire().ReplaceRange(index, count, values);
        public void RepeatFill(int originalCount, int totalCount) => Acquire().RepeatFill(originalCount, totalCount);
        public void RemoveAt(int index) => Acquire().RemoveAt(index);
        public void Clear() => Acquire().Clear();
        public object[] ToArray() => Acquire().ToArray();
        public IPyListStorage Clone() => Acquire().Clone();
        public IEnumerator<object> GetEnumerator() => Acquire().GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public long ReleaseCommittedBytes()
        {
            // The existing clear/replacement caller releases this snapshot.
            // Independently transferred members retain their own entries.
            var released = checked(inner.ReleaseCommittedBytes() + _remainingBytes);
            _remainingBytes = 0;
            return released;
        }
    }
}
