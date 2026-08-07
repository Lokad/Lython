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
        value = name switch
        {
            "start" => Start,
            "stop" => Stop,
            "step" => Step,
            _ => PyNone.Instance
        };

        return value is not PyNone || name is "start" or "stop" or "step";
    }

    public bool TrySetMember(string name, object value)
    {
        _ = name;
        _ = value;
        return false;
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
}
