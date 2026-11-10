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
        var prior = index < 0 ? fallback : ReadNativeValue(index);
        if (prior is not BigInteger integer || integer.GetByteCount() >= 8) return false;

        context.CheckExecution(addSpan);
        // GuardIntegerAddition admits these magnitudes without a reservation;
        // reuse its exact arithmetic rather than duplicate a scalar operator.
        var value = LythonRuntime.AddExactIntegers(integer, increment, context.MemoryGovernor, addSpan);
        context.CheckExecution(storeSpan);
        var before = _nativeCount;
        PublishNativeValue(index, key, value, hash, protocol: false, replaceExisting: true);
        if (_nativeCount != before) _version++;
        context.CheckExecution(storeSpan);
        return true;
    }
}
