using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

// Python's sequence fallback starts at zero, resolves the type's item slot
// per pull, and becomes permanently exhausted after IndexError/StopIteration.
internal sealed class PySequenceIterator : PyIteratorBase
{
    private readonly PyInstance _sequence;
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;
    private long _index;
    private bool _exhausted;

    internal PySequenceIterator(PyInstance sequence, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        _sequence = sequence;
        _context = context;
        _span = span;
        ChargeIteratorValue(context.MemoryGovernor, span);
        context.State.CallTemporaries.TrackFreshMutable(this, IteratorValueBytes, span);
    }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        var result = AdvanceAsync(false).GetAwaiter().GetResult();
        value = result.HasValue ? result.Value : null;
        return result.HasValue;
    }

    public override ValueTask<PyIterationResult> TryMoveNextAsync() => AdvanceAsync(true);

    private async ValueTask<PyIterationResult> AdvanceAsync(bool asynchronous)
    {
        if (_exhausted) return PyIterationResult.End;
        _context.CheckExecution(_span);
        if (_index == long.MaxValue) throw new LythonRuntimeException("OverflowError", "sequence iterator index too large", _span);
        try
        {
            if (!_sequence.Type.TryLookupInMro("__getitem__", 0, out var raw, out _))
                throw RuntimeErrors.Type("sequence no longer supports item access", _span);
            var member = asynchronous
                ? await PyAttributeLookup.BindForInstanceAsync(_sequence, raw, _context, _span).ConfigureAwait(false)
                : PyAttributeLookup.BindForInstance(_sequence, raw, _context, _span);
            CallArgumentValue[] args = [CallArgumentValue.Positional(new BigInteger(_index))];
            var value = asynchronous
                ? await LythonRuntime.InvokeCallableTargetAsync(member, _span, _span, _context, () => ValueTask.FromResult(args)).ConfigureAwait(false)
                : LythonRuntime.InvokeCallableTarget(member, _span, _span, _context, () => args);
            _index++;
            return PyIterationResult.Yield(LythonRuntime.RuntimeValue(value));
        }
        catch (LythonRuntimeException ex) when (ex.ExceptionType is "IndexError" or "StopIteration")
        {
            _exhausted = true;
            return PyIterationResult.End;
        }
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<iterator object>");
}
