using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PySlice : IPyDynamicAttributes, IPyRenderableValue, IPyHashableValue
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
    {
        var builder = new GovernedByteBuilder(context.Context.MemoryGovernor);
        try
        {
            builder.AppendAscii("slice(");
            builder.Append(PyRendering.ToReprPyString(Start, context));
            builder.AppendAscii(", ");
            builder.Append(PyRendering.ToReprPyString(Stop, context));
            builder.AppendAscii(", ");
            builder.Append(PyRendering.ToReprPyString(Step, context));
            builder.Append((byte)')');
            return builder.ToPyStringAndRelease();
        }
        finally { builder.Release(); }
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    // Slices hash structurally like CPython; bounds hash through the shared
    // comparer so nested values behave uniformly.
    public int GetPyHashCode()
        => HashCode.Combine(
            PyValueComparer.Instance.GetHashCode(StartBound ?? PyNone.Instance),
            PyValueComparer.Instance.GetHashCode(StopBound ?? PyNone.Instance),
            PyValueComparer.Instance.GetHashCode(StepBound ?? PyNone.Instance));

    // slice.indices(length) normalises the stored bounds against an explicit
    // length like CPython, coercing the length and any index bounds on the way.
    private sealed class SliceIndicesMethod(PySlice owner) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => InvokeCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();

        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => InvokeCoreAsync(arguments, span, context, true);

        private async ValueTask<object> InvokeCoreAsync(CallArgumentValue[] arguments, LythonSourceSpan span,
            LythonRuntime.ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);

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

            var coercedLength = await LythonRuntime.CoerceTypeIndexAsync(length, context, span, asynchronous).ConfigureAwait(false) ?? PyNone.Instance;
            BigInteger bigLength;
            if (coercedLength is int smallLength)
            {
                bigLength = new BigInteger(smallLength);
            }
            else if (!PyNumberOps.TryAsInteger(coercedLength, out bigLength))
            {
                throw new LythonRuntimeException("TypeError", "'" + RuntimeErrors.DatetimeQualifiedTypeName(length, context) + "' object cannot be interpreted as an integer", span);
            }

            if (bigLength < 0)
            {
                throw new LythonRuntimeException("ValueError", "length should not be negative", span);
            }

            var (start, stop, step) = await LythonRuntime.CoerceSliceBoundsAsync(
                owner.StartBound, owner.StopBound, owner.StepBound, context, span, asynchronous).ConfigureAwait(false);
            // Exact metadata arithmetic may create large magnitudes even though
            // no sequence is allocated. Fund scratch and tuple-construction
            // overlap before calculating them; the returned tuple owns its bits.
            static long Size(object? value) => value is BigInteger integer ? integer.GetByteCount() : 8;
            using var working = context.MemoryGovernor.ReserveTemporary(
                checked(256L + 12L * (bigLength.GetByteCount() + Size(start) + Size(stop) + Size(step))), span);
            var bounds = PyIndexing.NormalizeSliceIndices(bigLength, start, stop, step, span);
            return new PyTuple(
                [(object)bounds.Start, bounds.Stop, bounds.Step],
                context.MemoryGovernor,
                span);
        }
    }
}
