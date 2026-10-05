using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyComplex(double real, double imaginary) : IPyRenderableValue, IPyTruthyValue,
    IPyContextualDynamicAttributes, IPyHashableValue, IPyGovernedValue, IPyOwnershipSnapshot
{
    private const long ValueBytes = 96;
    public double Real { get; } = real;
    public double Imaginary { get; } = imaginary;
    public Complex Value => new(Real, Imaginary);
    public MemoryGovernor? OwnerMemoryGovernor { get; private init; }
    public LythonSourceSpan? AllocationSpan { get; private init; }
    bool IPyOwnershipSnapshot.TrySnapshotOwnership(out long bytes) => OwnershipSnapshot.Owned(OwnerMemoryGovernor, ValueBytes, out bytes);
    public static PyComplex Own(Complex value, LythonRuntime.ExecutionContext context, LythonSourceSpan span) => Create(value.Real, value.Imaginary, context, span);
    public static PyComplex Create(double real, double imaginary, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        context.MemoryGovernor.Reserve(ValueBytes, span); context.MemoryGovernor.Commit(ValueBytes);
        var value = new PyComplex(real, imaginary) { OwnerMemoryGovernor = context.MemoryGovernor, AllocationSpan = span };
        context.Services.State.CallTemporaries.TrackFreshMutable(value, ValueBytes, span);
        return value;
    }
    public bool IsTruthy() => Real != 0 || Imaginary != 0;
    public double Magnitude(LythonSourceSpan span)
    {
        var result = double.Hypot(Real, Imaginary);
        if (double.IsInfinity(result) && double.IsFinite(Real) && double.IsFinite(Imaginary))
            throw new LythonRuntimeException("OverflowError", "absolute value too large", span);
        return result;
    }
    public static bool AreEqual(object left, object right)
    {
        if (left is not PyComplex complex) return AreEqual(right, left);
        if (right is PyComplex other) return complex.Real == other.Real && complex.Imaginary == other.Imaginary;
        if (complex.Imaginary != 0) return false;
        if (right is PyDecimal) return PyDecimalOps.AreEqual(complex.Real, right);
        return PyNumberOps.TryAsNumber(right, out var numeric) && PyNumberOps.AreEqual(PyNumber.FromFloat(complex.Real), numeric);
    }
    public int GetPyHashCode() => unchecked(PyNumberOps.GetHashCode(PyNumber.FromFloat(Real)) + 1000003 * PyNumberOps.GetHashCode(PyNumber.FromFloat(Imaginary)));
    internal static string RenderComponent(double value)
    {
        var text = PyNumberOps.RenderFloat(value);
        return text.EndsWith(".0", StringComparison.Ordinal) ? text[..^2] : text;
    }
    internal string Render()
    {
        var imaginaryNegative = !double.IsNaN(Imaginary) && double.IsNegative(Imaginary);
        var imaginary = RenderComponent(imaginaryNegative ? -Imaginary : Imaginary);
        if (Real == 0 && !double.IsNegative(Real)) return (imaginaryNegative ? "-" : "") + imaginary + "j";
        return "(" + RenderComponent(Real) + (imaginaryNegative ? "-" : "+") + imaginary + "j)";
    }
    public PyString RenderPython(PyRenderingContext context) => PyString.FromString(Render(), context.Context.MemoryGovernor, AllocationSpan);
    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    public bool TryGetMember(string name, LythonRuntime.ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
    {
        value = name switch
        {
            "real" => Real,
            "imag" => Imaginary,
            "conjugate" => CreateMethod(true, context, span),
            "__complex__" => CreateMethod(false, context, span),
            _ => PyNone.Instance,
        };
        return value is not PyNone;
    }
    private object CreateMethod(bool conjugate, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        context.MemoryGovernor.Reserve(96, span); context.MemoryGovernor.Commit(96);
        var method = new Method(this, conjugate, context.MemoryGovernor, span);
        context.Services.State.CallTemporaries.TrackFreshMutable(method, 96, span);
        return method;
    }
    private sealed class Method(PyComplex receiver, bool conjugate, MemoryGovernor governor, LythonSourceSpan span) : LythonRuntime.ICallable,
        IPyDynamicAttributes, LythonRuntime.IPyBoundEngineMethod, IPyGovernedValue, IPyOwnershipSnapshot
    {
        public MemoryGovernor? OwnerMemoryGovernor => governor;
        public LythonSourceSpan? AllocationSpan => span;
        bool IPyOwnershipSnapshot.TrySnapshotOwnership(out long bytes) { bytes = 96; return true; }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan callSpan, LythonRuntime.ExecutionContext context)
        {
            if (arguments.Length != 0) throw new LythonRuntimeException("TypeError", "complex method takes no arguments", callSpan);
            return conjugate ? Create(receiver.Real, -receiver.Imaginary, context, callSpan) : receiver;
        }
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch { "__self__" => receiver, "__name__" => PyString.FromString(conjugate ? "conjugate" : "__complex__"), _ => PyNone.Instance };
            return value is not PyNone;
        }
    }
}
