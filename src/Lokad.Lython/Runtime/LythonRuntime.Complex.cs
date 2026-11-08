using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static object ConstructComplex(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        => ConstructComplexCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> ConstructComplexAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        => ConstructComplexCoreAsync(arguments, span, context, true);
    private static async ValueTask<object> ConstructComplexCoreAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        object real = BigInteger.Zero, imaginary = BigInteger.Zero;
        var seenReal = false; var seenImaginary = false; var positional = 0;
        foreach (var argument in arguments)
        {
            var slot = argument.IsKeyword ? argument.KeywordName switch { "real" => 0, "imag" => 1, _ => -1 } : positional++;
            if (slot is < 0 or > 1) throw new LythonRuntimeException("TypeError", "complex() accepts real and imag arguments", span);
            if (slot == 0) { if (seenReal) throw new LythonRuntimeException("TypeError", "complex() got multiple values for real", span); real = argument.Value; seenReal = true; }
            else { if (seenImaginary) throw new LythonRuntimeException("TypeError", "complex() got multiple values for imag", span); imaginary = argument.Value; seenImaginary = true; }
        }
        if (real is PyString text)
        {
            if (seenImaginary) throw new LythonRuntimeException("TypeError", "complex() can't take second arg if first is a string", span);
            return ParseComplexString(text, context, span);
        }
        if (imaginary is PyString) throw new LythonRuntimeException("TypeError", "complex() second arg can't be a string", span);
        if (!seenImaginary && real is PyComplex existing) return existing;
        var firstWasComplex = false;
        var first = await ConvertComplex(real, allowComplexProtocol: true).ConfigureAwait(false);
        if (!seenImaginary) return PyComplex.Own(first, context, span);
        var second = await ConvertComplex(imaginary, allowComplexProtocol: false).ConfigureAwait(false);
        return PyComplex.Create(first.Real - second.Imaginary, firstWasComplex ? first.Imaginary + second.Real : second.Real, context, span);

        async ValueTask<Complex> ConvertComplex(object value, bool allowComplexProtocol)
        {
            if (value is PyComplex complex) { if (allowComplexProtocol) firstWasComplex = true; return complex.Value; }
            if (PyNumberOps.TryAsNumber(value, out var number))
            {
                try { return new(PyNumberOps.ToDoubleChecked(number), 0); }
                catch (OverflowException) { throw new LythonRuntimeException("OverflowError", "int too large to convert to float", span); }
            }
            if (value is PyDecimal dec) return new((double)dec.Value, 0);
            if (value is PyInstance)
            {
                if (allowComplexProtocol)
                {
                    var converted = await Hook(value, "__complex__").ConfigureAwait(false);
                    if (converted.Kind == SpecialMethodInvocationKind.Invoked)
                    {
                        firstWasComplex = true;
                        return converted.Value is PyComplex result ? result.Value : throw new LythonRuntimeException("TypeError", "__complex__ returned non-complex", span);
                    }
                }
                var floating = await Hook(value, "__float__").ConfigureAwait(false);
                if (floating.Kind == SpecialMethodInvocationKind.Invoked)
                    return floating.Value is double result ? new(result, 0) : throw new LythonRuntimeException("TypeError", "__float__ returned non-float", span);
                var integer = await Hook(value, "__index__").ConfigureAwait(false);
                if (integer.Kind == SpecialMethodInvocationKind.Invoked)
                {
                    if (integer.Value is not BigInteger result) throw new LythonRuntimeException("TypeError", "__index__ returned non-int", span);
                    try { return new(PyNumberOps.BigIntegerToDouble(result), 0); }
                    catch (OverflowException) { throw new LythonRuntimeException("OverflowError", "int too large to convert to float", span); }
                }
            }
            throw new LythonRuntimeException("TypeError", "complex() argument must be a string or a number, not '" + RuntimeErrors.OperandTypeName(value) + "'", span);
        }
        ValueTask<SpecialMethodInvocation> Hook(object value, string name) => asynchronous
            ? InvokeUnarySpecialMethodAsync(value, name, context, span) : new(TryInvokeUnarySpecialMethod(value, name, context, span, out var result) ? SpecialMethodInvocation.Invoked(result) : SpecialMethodInvocation.Missing);
    }

    private static PyComplex ParseComplexString(PyString value, ExecutionContext context, LythonSourceSpan span)
    {
        using var scratch = context.MemoryGovernor.ReserveTemporary(128L + 32L * value.ByteLength, span);
        var text = value.AsString().Trim();
        if (text.StartsWith('(') && text.EndsWith(')')) text = text[1..^1].Trim();
        if (text.Length == 0 || text.Any(char.IsWhiteSpace)) throw Invalid();
        if (!text.EndsWith('j') && !text.EndsWith('J')) return PyComplex.Create(Parse(text), 0, context, span);
        text = text[..^1];
        var split = -1;
        for (var i = 1; i < text.Length; i++)
        {
            if ((i & 255) == 0) context.CheckExecution(span);
            if (text[i] is '+' or '-' && text[i - 1] is not ('e' or 'E')) split = i;
        }
        var real = split < 0 ? 0 : Parse(text[..split]);
        var imaginary = split < 0 ? text : text[split..];
        return PyComplex.Create(real, imaginary is "" or "+" ? 1 : imaginary == "-" ? -1 : Parse(imaginary), context, span);
        double Parse(string component)
        {
            try
            {
                var owned = PyString.FromString(component, context.MemoryGovernor, span);
                return (double)Float([owned], span, context);
            }
            catch (LythonRuntimeException ex) when (ex.ExceptionType == "ValueError") { throw Invalid(); }
        }
        LythonRuntimeException Invalid() => new("ValueError", "complex() arg is a malformed string", span);
    }

    private static bool TryComplexOperand(object value, LythonSourceSpan span, out Complex result)
    {
        if (value is PyComplex complex) { result = complex.Value; return true; }
        if (PyNumberOps.TryAsNumber(value, out var numeric))
        {
            try { result = new(PyNumberOps.ToDoubleChecked(numeric), 0); return true; }
            catch (OverflowException) { throw new LythonRuntimeException("OverflowError", "int too large to convert to float", span); }
        }
        result = default; return false;
    }
    private static object EvaluateComplexBinary(BinaryOperatorSyntax op, object left, object right, ExecutionContext context, LythonSourceSpan span, string? operation = null)
    {
        var symbol = operation ?? (op switch { BinaryOperatorSyntax.Add => "+", BinaryOperatorSyntax.Subtract => "-", BinaryOperatorSyntax.Multiply => "*", BinaryOperatorSyntax.Divide => "/", BinaryOperatorSyntax.Power => "** or pow()", BinaryOperatorSyntax.FloorDivide => "//", BinaryOperatorSyntax.Modulo => "%", BinaryOperatorSyntax.BitwiseAnd => "&", BinaryOperatorSyntax.BitwiseOr => "|", BinaryOperatorSyntax.BitwiseXor => "^", BinaryOperatorSyntax.LeftShift => "<<", BinaryOperatorSyntax.RightShift => ">>", _ => op.ToString() });
        if (op is not (BinaryOperatorSyntax.Add or BinaryOperatorSyntax.Subtract or BinaryOperatorSyntax.Multiply or BinaryOperatorSyntax.Divide or BinaryOperatorSyntax.Power) ||
            !TryComplexOperand(left, span, out var lhs) || !TryComplexOperand(right, span, out var rhs)) throw RuntimeErrors.UnsupportedOperands(symbol, left, right, span);
        var result = op switch
        {
            BinaryOperatorSyntax.Add => lhs + rhs,
            BinaryOperatorSyntax.Subtract => lhs - rhs,
            BinaryOperatorSyntax.Multiply => lhs * rhs,
            BinaryOperatorSyntax.Divide => ComplexDivide(lhs, rhs, span),
            BinaryOperatorSyntax.Power => ComplexPower(lhs, rhs, context, span),
            _ => default,
        };
        return PyComplex.Own(result, context, span);
    }
    private static Complex ComplexDivide(Complex lhs, Complex rhs, LythonSourceSpan span)
    {
        var br = rhs.Real; var bi = rhs.Imaginary;
        if (Math.Abs(br) >= Math.Abs(bi))
        {
            if (br == 0) throw new LythonRuntimeException("ZeroDivisionError", "complex division by zero", span);
            var ratio = bi / br; var denominator = br + bi * ratio;
            return new((lhs.Real + lhs.Imaginary * ratio) / denominator, (lhs.Imaginary - lhs.Real * ratio) / denominator);
        }
        else
        {
            var ratio = br / bi; var denominator = br * ratio + bi;
            return new((lhs.Real * ratio + lhs.Imaginary) / denominator, (lhs.Imaginary * ratio - lhs.Real) / denominator);
        }
    }
    private static Complex ComplexPower(Complex lhs, Complex rhs, ExecutionContext context, LythonSourceSpan span)
    {
        if (rhs == Complex.Zero) return Complex.One;
        if (lhs == Complex.Zero)
        {
            if (rhs.Imaginary != 0 || rhs.Real < 0) throw new LythonRuntimeException("ZeroDivisionError", "0.0 to a negative or complex power", span);
            return Complex.Zero;
        }
        Complex result;
        if (rhs.Imaginary == 0 && Math.Abs(rhs.Real) <= 100 && rhs.Real == Math.Truncate(rhs.Real))
        {
            var exponent = (int)Math.Abs(rhs.Real); result = Complex.One; var current = lhs;
            while (exponent != 0)
            {
                context.CheckExecution(span);
                if ((exponent & 1) != 0) result *= current;
                exponent >>= 1;
                if (exponent != 0) current *= current;
            }
            if (rhs.Real < 0) result = ComplexDivide(Complex.One, result, span);
        }
        else
        {
            var magnitude = double.Hypot(lhs.Real, lhs.Imaginary);
            var length = Math.Pow(magnitude, rhs.Real);
            var angle = Math.Atan2(lhs.Imaginary, lhs.Real) * rhs.Real;
            if (rhs.Imaginary != 0) { length *= Math.Exp(-Math.Atan2(lhs.Imaginary, lhs.Real) * rhs.Imaginary); angle += rhs.Imaginary * Math.Log(magnitude); }
            result = new(length * Math.Cos(angle), length * Math.Sin(angle));
        }
        if ((double.IsInfinity(result.Real) || double.IsInfinity(result.Imaginary)) && double.IsFinite(lhs.Real) && double.IsFinite(lhs.Imaginary) && double.IsFinite(rhs.Real) && double.IsFinite(rhs.Imaginary))
            throw new LythonRuntimeException("OverflowError", "complex exponentiation", span);
        return result;
    }

    private static void AppendComplexValue(Text.GovernedByteBuilder output, PyComplex value, string format, ExecutionContext context, LythonSourceSpan span,
        InterpolatedFormatSpecifier? prepared = null)
    {
        if (format.Length == 0) { output.AppendString(value.Render()); return; }
        var spec = prepared ?? ParseInterpolatedFormatSpecifier(format, value, span);
        if (spec.ZeroPad || spec.Fill == '0') throw new LythonRuntimeException("ValueError", "Zero padding is not allowed in complex format specifier", span);
        if (spec.Align == '=') throw new LythonRuntimeException("ValueError", "'=' alignment flag is not allowed in complex format specifier", span);
        if (spec.Type is not (null or 'e' or 'E' or 'f' or 'F' or 'g' or 'G' or 'n')) throw new LythonRuntimeException("ValueError", "Unknown format code for complex", span);
        var component = spec with { Width = null, Fill = -1, Align = null, ZeroPad = false,
            Type = spec.Type == 'n' || spec.Type is null && spec.Precision is not null ? 'g' : spec.Type };
        using var scratch = context.MemoryGovernor.ReserveTemporary(2048L + 16L * (spec.Precision ?? 32), span);
        var working = new Text.GovernedByteBuilder(context.MemoryGovernor, span);
        try
        {
            var imaginaryOnly = spec.Type is null && value.Real == 0 && !double.IsNegative(value.Real);
            if (spec.Type is null && !imaginaryOnly) working.Append((byte)'(');
            if (!imaginaryOnly) AppendComponent(value.Real, component);
            AppendComponent(value.Imaginary, component with { Sign = imaginaryOnly ? spec.Sign : '+' });
            working.Append((byte)'j');
            if (spec.Type is null && !imaginaryOnly) working.Append((byte)')');
            context.CheckExecution(span);
            var text = working.WrittenSpan;
            if (spec.Width is not { } width || width <= text.Length)
                output.Append(text);
            else
            {
                var padding = width - text.Length;
                var (_, fill, left) = GetNumericPadding(spec, padding, span);
                Span<byte> fillBytes = stackalloc byte[4];
                var fillLength = fill.EncodeToUtf8(fillBytes);
                var size = checked((long)text.Length + (long)padding * fillLength);
                if (size > Array.MaxLength) throw RuntimeErrors.Memory("padded string exceeds supported capacity", span);
                // The working body and old parent capacity stay funded through
                // final-window admission and copy, including refusal/cancellation.
                var destination = output.GetWriteWindow((int)size).Span;
                var leftBytes = left * fillLength;
                Text.PyStringOps.FillPadding(destination[..leftBytes], fillBytes[..fillLength], context.State.CancellationToken);
                text.CopyTo(destination[leftBytes..]);
                Text.PyStringOps.FillPadding(destination[(leftBytes + text.Length)..], fillBytes[..fillLength], context.State.CancellationToken);
                context.CheckExecution(span);
                output.Advance((int)size);
            }
            context.CheckExecution(span);
        }
        finally { working.Release(); }

        void AppendComponent(double number, InterpolatedFormatSpecifier formatSpec)
        {
            if (spec.Type is null && spec.Precision is null)
            {
                var text = FormatFloatingText(number, formatSpec, span, context.MemoryGovernor, "complex").AsSpan();
                if (text.EndsWith(".0", StringComparison.Ordinal))
                    text = text[..^(spec.Alternate ? 1 : 2)];
                AppendNumericText(working, text, formatSpec, context, span);
            }
            else AppendFloatingFormat(working, number, formatSpec, context, span, "complex");
        }

    }
}
