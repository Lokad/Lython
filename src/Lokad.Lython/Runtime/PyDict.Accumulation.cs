using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class PyDict
{
    // The get and assignment share this one probe only while neither operation
    // nor the intervening addition can invoke a protocol or an admission hook.
    // Retained old values remain ordinary immutable integers with their owner.
    internal bool TryAccumulateWithoutAdmission(PyString key, object fallback, BigInteger increment,
        LythonRuntime.ExecutionContext context, LythonSourceSpan querySpan, LythonSourceSpan addSpan, LythonSourceSpan storeSpan)
    {
        if (_nativeItems is null || _nativeValues is null || HasProtocolKeys
            || !ReferenceEquals(_memoryGovernor, context.MemoryGovernor) || increment.GetByteCount() >= 8) return false;
        var hash = MixNativeHash(LythonRuntime.ComputeStructuralHashCode(key, querySpan, context));
        var index = FindNativeIndex(key, hash, context, querySpan);
        if (index < 0 && (_recordKeys is not null || _nativeCount == _nativeItems.Length)) return false;
        long prior;
        if (index >= 0 && _recordKeys is null && _nativeValues[index] is null)
        {
            prior = _nativeItems[index].Integer;
            if (prior < -(1L << 55) || prior >= (1L << 55)) return false;
        }
        else
        {
            var value = index < 0 ? fallback : ReadNativeValue(index);
            if (value is not BigInteger integer || integer.GetByteCount() >= 8) return false;
            prior = (long)integer;
        }

        context.CheckExecution(addSpan);
        // Both signed operands fit seven bytes. The exact result fits long;
        // wider subsequent updates retain the ordinary promotion/admission path.
        var sum = prior + (long)increment;
        context.CheckExecution(storeSpan);
        var before = _nativeCount;
        if (_recordKeys is null)
        {
            if (index < 0)
            {
                index = _nativeCount;
                AppendNativeItem(key, PyNone.Instance, hash);
            }
            _nativeItems[index].Integer = sum;
            _nativeValues[index] = null;
        }
        else
            PublishNativeValue(index, key, new BigInteger(sum), hash, protocol: false, replaceExisting: true);
        if (_nativeCount != before) _version++;
        context.CheckExecution(storeSpan);
        return true;
    }
}
