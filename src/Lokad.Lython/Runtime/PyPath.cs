using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyPath : IPyTruthyValue, IPyHashableValue, IPyRenderableValue
{
    public PyPath(PyString value, bool isPure = false)
    {
        Value = value;
        IsPure = isPure;
    }

    public PyString Value { get; }
    public bool IsPure { get; }

    public bool IsTruthy() => Value.Length != 0;

    public int GetPyHashCode() => Value.GetPyHashCode();

    public PyString RenderPython(PyRenderingContext context)
    {
        var builder = new GovernedByteBuilder(context.Context.MemoryGovernor, null);
        try
        {
            builder.Append(IsPure ? "PurePosixPath("u8 : "PosixPath("u8);
            builder.Append(PyRendering.ToReprPyString(Value, context).Utf8Bytes.Span);
            builder.Append((byte)')');
            return builder.ToPyStringAndRelease();
        }
        finally { builder.Release(); }
    }

    public PyString RenderInterpolated(PyRenderingContext context)
    {
        _ = context;
        return Value;
    }

    public override string ToString() => Value.AsString();
}
