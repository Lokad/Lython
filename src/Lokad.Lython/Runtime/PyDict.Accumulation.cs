using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class PyDict
{
    // The get and assignment share this one probe only while neither operation
    // nor the intervening addition can invoke a protocol or an admission hook.
    // Retained old values remain ordinary immutable integers with their owner.
    internal bool TryAccumulateWithoutAdmission(PyString key, object fallback, BigInteger increment,
        LythonRuntime.ExecutionContext context, LythonSourceSpan querySpan)
    {
        if (_nativeItems is null || _nativeValues is null || HasProtocolKeys
            || !ReferenceEquals(_memoryGovernor, context.MemoryGovernor) || increment.GetByteCount() >= 8) return false;
        var hash = HashNativeString(key, context, querySpan);
        var index = FindNativeIndex(key, hash, context, querySpan);
        return TryAccumulateAt(index, key, hash, fallback, increment);
    }

    internal bool CanAccumulateBorrowedWords(LythonRuntime.ExecutionContext context)
        => _nativeItems is not null && _nativeValues is not null && _recordKeys is null
            && !HasProtocolKeys && ReferenceEquals(_memoryGovernor, context.MemoryGovernor);

    // The private word segment validates storage once. Until it exits, its
    // prepaid cursor and exact native updates cannot admit or invoke Python.
    internal bool TryAccumulateBorrowedWord(ReadOnlySpan<byte> bytes, object fallback, BigInteger increment,
        LythonRuntime.ExecutionContext context, LythonSourceSpan querySpan, out uint? appendHash)
    {
        context.CheckExecution(querySpan);
        var items = _nativeItems!;
        var index = FindNativeTextIndex(items, _nativeCount, bytes, context, querySpan, out var hash);
        appendHash = null;
        if (index < 0)
        {
            // Wider words used to arrive materialized. Keep their existing
            // native append route when this hashed lookup proves absence and
            // the dictionary already owns the slot.
            if (bytes.Length > PyString.InlineUtf8Capacity && _nativeCount < items.Length) appendHash = hash;
            return false;
        }
        ref var entry = ref items[index];
        return TryAccumulateAt(index, (PyString)entry.Key!, entry.Hash, fallback, increment);
    }

    // The private cursor has materialized the prepaid key since the lookup.
    // Neither step can invoke a callback or change dictionary capacity.
    internal bool TryAppendBorrowedWord(PyString key, uint hash, object fallback, BigInteger increment)
        => TryAccumulateAt(-1, key, hash, fallback, increment);

    // The exact get owns this probe. Its position remains usable only while
    // the interpreter proves the remaining expression cannot call or admit.
    internal bool TryPrepareConvertedAccumulation(PyString key, object fallback,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span, out (int Index, uint Hash, long Prior) update)
    {
        update = default;
        if (!CanAccumulateBorrowedWords(context)) return false;
        var hash = HashNativeString(key, context, span);
        var index = FindNativeIndex(key, hash, context, span);
        if (!TryReadAccumulationValue(index, fallback, out var prior)) return false;
        update = (index, hash, prior);
        return true;
    }

    internal void CompleteConvertedAccumulation(PyString key, (int Index, uint Hash, long Prior) update, long increment)
        => StoreAccumulationValue(update.Index, key, update.Hash, update.Prior + increment);

    private bool TryReadAccumulationValue(int index, object fallback, out long prior)
    {
        prior = 0;
        if (index < 0 && (_recordKeys is not null || _nativeCount == _nativeItems!.Length)) return false;
        if (index >= 0 && _recordKeys is null && _nativeValues![index] is null)
        {
            prior = _nativeItems![index].Integer;
            return prior >= -(1L << 55) && prior < (1L << 55);
        }

        var value = index < 0 ? fallback : ReadNativeValue(index);
        if (value is not BigInteger integer || integer.GetByteCount() >= 8) return false;
        prior = (long)integer;
        return true;
    }

    private bool TryAccumulateAt(int index, PyString key, uint hash, object fallback, BigInteger increment)
    {
        // Both signed operands fit seven bytes. The exact result fits long.
        if (!TryReadAccumulationValue(index, fallback, out var prior)) return false;
        StoreAccumulationValue(index, key, hash, prior + (long)increment);
        return true;
    }

    private void StoreAccumulationValue(int index, PyString key, uint hash, long sum)
    {
        var before = _nativeCount;
        if (_recordKeys is null)
        {
            if (index < 0)
            {
                index = _nativeCount;
                AppendNativeItem(key, PyNone.Instance, hash);
            }
            _nativeItems![index].Integer = sum;
            _nativeValues![index] = null;
        }
        else
            PublishNativeValue(index, key, new BigInteger(sum), hash, protocol: false, replaceExisting: true);
        if (_nativeCount != before) _version++;
    }
}
