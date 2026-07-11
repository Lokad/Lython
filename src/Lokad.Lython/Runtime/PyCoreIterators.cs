using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyRange : IPyIterableValue, IPySliceableValue, IPySubscriptableValue, IPyRenderableValue, IPyTruthyValue
{
    public PyRange(BigInteger start, BigInteger stop, BigInteger step)
    {
        Start = start;
        Stop = stop;
        Step = step;
    }

    public BigInteger Start { get; }
    public BigInteger Stop { get; }
    public BigInteger Step { get; }
    public BigInteger Length => Step > 0
        ? Stop <= Start ? 0 : (Stop - Start - 1) / Step + 1
        : Stop >= Start ? 0 : (Start - Stop - 1) / -Step + 1;

    public bool IsTruthy() => Length != 0;

    public IEnumerable<object> Iterate()
    {
        for (var value = Start; Step > 0 ? value < Stop : value > Stop; value += Step)
        {
            yield return value;
        }
    }

    public object GetSubscript(object index, LythonSourceSpan span)
    {
        if (!Numbers.PyNumberOps.TryAsInteger(index, out var integer))
        {
            throw new LythonRuntimeException("TypeError", "range indices must be integers", span);
        }

        if (integer < 0)
        {
            integer += Length;
        }

        if (integer < 0 || integer >= Length)
        {
            throw new LythonRuntimeException("IndexError", "range object index out of range", span);
        }

        return Start + integer * Step;
    }

    public object GetSlice(object? start, object? end, object? step, LythonSourceSpan span)
    {
        var sliceStep = Bound(step, BigInteger.One, span);
        if (sliceStep == 0)
        {
            throw new LythonRuntimeException("ValueError", "slice step cannot be zero", span);
        }

        if (sliceStep < 0)
        {
            throw new LythonRuntimeException("NotImplementedError", "negative range slice steps are not supported", span);
        }

        var from = Bound(start, BigInteger.Zero, span);
        var to = Bound(end, Length, span);
        if (from < 0) from = BigInteger.Max(BigInteger.Zero, from + Length);
        if (to < 0) to = BigInteger.Max(BigInteger.Zero, to + Length);
        from = BigInteger.Min(from, Length);
        to = BigInteger.Min(to, Length);
        var newStart = Start + from * Step;
        var newStop = Start + BigInteger.Max(from, to) * Step;
        return new PyRange(newStart, newStop, Step * sliceStep);
    }

    public PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        var text = Step == 1
            ? "range(" + Start + ", " + Stop + ")"
            : "range(" + Start + ", " + Stop + ", " + Step + ")";
        return PyString.FromString(text);
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    private static BigInteger Bound(object? value, BigInteger fallback, LythonSourceSpan span)
        => value is null || value is PyNone
            ? fallback
            : Numbers.PyNumberOps.TryAsInteger(value, out var integer)
                ? integer
                : throw new LythonRuntimeException("TypeError", "slice indices must be integers", span);
}

internal sealed class PyEnumerateIterator : PyIteratorBase
{
    private readonly PyIteration.Cursor _cursor;
    private BigInteger _index;

    public PyEnumerateIterator(object iterable, BigInteger start, LythonSourceSpan span)
    {
        _cursor = PyIteration.Cursor.Create(iterable, span);
        _index = start;
    }

    public override bool TryMoveNext(out object value)
    {
        if (!_cursor.TryMoveNext(out var item))
        {
            value = PyNone.Instance;
            return false;
        }

        value = new PyTuple([_index, item]);
        _index++;
        return true;
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<enumerate object>");
}

internal sealed class PyZipIterator : PyIteratorBase
{
    private readonly PyIteration.Cursor[] _cursors;
    private readonly bool _strict;
    private readonly LythonSourceSpan _span;
    private bool _finished;

    public PyZipIterator(object[] iterables, bool strict, LythonSourceSpan span)
    {
        _cursors = iterables.Select(value => PyIteration.Cursor.Create(value, span)).ToArray();
        _strict = strict;
        _span = span;
    }

    public override bool TryMoveNext(out object value)
    {
        if (_finished || _cursors.Length == 0)
        {
            value = PyNone.Instance;
            return false;
        }

        var items = new object[_cursors.Length];
        for (var i = 0; i < _cursors.Length; i++)
        {
            if (_cursors[i].TryMoveNext(out items[i]))
            {
                continue;
            }

            _finished = true;
            if (_strict)
            {
                if (i > 0 || _cursors.Skip(i + 1).Any(cursor => cursor.TryMoveNext(out _)))
                {
                    throw new LythonRuntimeException("ValueError", "zip() argument lengths differ", _span);
                }
            }

            value = PyNone.Instance;
            return false;
        }

        value = new PyTuple(items);
        return true;
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<zip object>");
}
