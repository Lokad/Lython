using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PySlice : IPyDynamicAttributes, IPyRenderableValue
{
    public PySlice(object start, object stop, object step)
    {
        Start = start;
        Stop = stop;
        Step = step;
    }

    public object Start { get; }

    public object Stop { get; }

    public object Step { get; }

    public object? StartBound => Start is PyNone ? null : Start;

    public object? StopBound => Stop is PyNone ? null : Stop;

    public object? StepBound => Step is PyNone ? null : Step;

    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        if (name is "indices")
        {
            value = new SliceIndicesMethod(this);
            return true;
        }

        value = name switch
        {
            "start" => Start,
            "stop" => Stop,
            "step" => Step,
            _ => PyNone.Instance
        };

        return value is not PyNone || name is "start" or "stop" or "step";
    }
    public PyString RenderPython(PyRenderingContext context)
            => PyString.FromString(
                "slice(" +
                PyRendering.ToReprPyString(Start, context).AsString() +
                ", " +
                PyRendering.ToReprPyString(Stop, context).AsString() +
                ", " +
                PyRendering.ToReprPyString(Step, context).AsString() +
                ")");

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    // slice.indices(length) normalises the stored bounds against an explicit
    // length like CPython, coercing the length and any index bounds on the way.
    private sealed class SliceIndicesMethod(PySlice owner) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecutionBudget(span);

            var positionals = 0;
            object? length = null;
            foreach (var argument in arguments)
            {
                if (argument.IsKeyword)
                {
                    throw new LythonRuntimeException("TypeError", "slice.indices() takes no keyword arguments", span);
                }

                positionals++;
                if (positionals == 1)
                {
                    length = argument.Value;
                }
            }

            if (positionals != 1)
            {
                throw new LythonRuntimeException("TypeError", "slice.indices() takes exactly one argument (" + positionals + " given)", span);
            }

            var coercedLength = LythonRuntime.CoerceIndexProtocol(length!, context, span);
            BigInteger bigLength;
            if (coercedLength is int smallLength)
            {
                bigLength = new BigInteger(smallLength);
            }
            else if (!PyNumberOps.TryAsInteger(coercedLength, out bigLength))
            {
                throw new LythonRuntimeException("TypeError", "'" + LythonRuntime.UnboundTypeMethod.PythonTypeName(length, context) + "' object cannot be interpreted as an integer", span);
            }

            if (bigLength < 0)
            {
                throw new LythonRuntimeException("ValueError", "length should not be negative", span);
            }

            // Huge lengths clamp to the addressable range like the other
            // bounded helpers; int-range behaviour stays exact.
            var intLength = bigLength > int.MaxValue ? int.MaxValue : (int)bigLength;
            var bounds = PyIndexing.NormalizeSliceBounds(
                intLength,
                PyIndexing.CoerceSliceBound(owner.StartBound, context, span),
                PyIndexing.CoerceSliceBound(owner.StopBound, context, span),
                PyIndexing.CoerceSliceBound(owner.StepBound, context, span),
                span);
            return new PyTuple(
                [(object)new BigInteger(bounds.Start), new BigInteger(bounds.End), new BigInteger(bounds.Step)],
                context.MemoryGovernor,
                span);
        }
    }
}
