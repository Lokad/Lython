using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static readonly Func<object, LythonSourceSpan, ExecutionContext, ExecutableSlot> FloatTextOperation
        = static (value, span, context) =>
        {
            ExecutableSlot result = default;
            result.SetDouble(ParseOwnedFloatText(value, context, span));
            return result;
        };

    private static object Float(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length > 1)
        {
            throw new LythonRuntimeException("TypeError", "float([value]) expects at most one argument.", span);
        }

        if (arguments.Length == 0)
        {
            return 0.0;
        }

        if (arguments[0] is PyInstance floatInstance)
        {
            // CPython consults __float__, then __index__ (but neither
            // __int__ nor __trunc__). Every present hook is called like
            // CPython, so a non-callable hook raises the typed not-callable
            // error.
            if (TryGetNumericConversionMember(floatInstance, "__float__", context, span, out var member))
            {
                if (member is not ICallable callable)
                {
                    throw NotCallableError(member, context, span);
                }

                return ValidateFloatConversionResult(callable.Invoke([], span, context), floatInstance, context, span);
            }

            if (TryConvertIntegerIndex(floatInstance, context, span, out var indexed))
            {
                return FloatFromInteger((BigInteger)indexed, span);
            }

            throw FloatArgumentError(floatInstance, context, span);
        }

        return arguments[0] switch
        {
            double => arguments[0],
            _ when PyNumberOps.TryAsInteger(arguments[0], out var integer) => FloatFromInteger(integer, span),
            PyDecimal decimalValue => (double)decimalValue.Value,
            PyString or PyBytes => ParseOwnedFloatText(arguments[0], context, span),
            _ => throw FloatArgumentError(arguments[0], context, span)
        };

    }

    private static double ParseOwnedFloatText(object source, ExecutionContext context, LythonSourceSpan span)
    {
        if (source is PyString text) return ParseFloatText(text.Utf8Span, context, span);
        var bytes = (PyBytes)source;
        return ParseFloatText(bytes.Bytes, context, span, bytes);
    }

    private const int FloatNormalizationStackBytes = 256;

    private static double ParseFloatText(ReadOnlySpan<byte> source, ExecutionContext context, LythonSourceSpan span,
        PyBytes? byteSource = null)
    {
        context.CheckExecution(span);
        if (TryParseShortDecimalFloat(source, out var exact))
        {
            context.CheckExecution(span);
            return exact;
        }

        var unicode = byteSource is null;
        var input = TrimNumericWhitespace(source, unicode, context, span);
        if (input.Length is > 0 and <= 4096)
        {
            // Only a decimal lead can bypass classification. Require full
            // consumption so an embedded separator or NUL reaches Python's
            // existing normalization and error handling.
            var lead = input[0];
            if (lead is (byte)'+' or (byte)'-' && input.Length > 1) lead = input[1];
            if ((lead is >= (byte)'0' and <= (byte)'9' or (byte)'.') &&
                Utf8Parser.TryParse(input, out double direct, out var consumed) && consumed == input.Length)
            {
                context.CheckExecution(span);
                return direct;
            }
            if (unicode && TryParseShortDecimalFloat(input, out direct, allowUnicodeDigits: true))
            {
                context.CheckExecution(span);
                return direct;
            }
        }
        // Short tokens can validate and normalize together in existing stack
        // scratch. Wide ASCII tokens still avoid admitting a temporary buffer.
        var valid = input.Length <= FloatNormalizationStackBytes
            ? TryParseNormalizedFloat(input, unicode, context, span, out var number)
            : TryParseWideFloat(input, unicode, context, span, out number);
        context.CheckExecution(span);
        return valid ? number : throw TextConversionError(source, "could not convert string to float: ", context, span, byteSource);
    }

    private static bool TryParseShortDecimalFloat(ReadOnlySpan<byte> input, out double result, bool allowUnicodeDigits = false)
    {
        result = default;
        if (input.IsEmpty) return false;

        var negative = input[0] == (byte)'-';
        var start = input[0] is (byte)'+' or (byte)'-' ? 1 : 0;
        var length = input.Length - start;
        const int maximumDigits = 17;
        // The Unicode spelling can contain four bytes per digit and one
        // separator per digit. Larger inputs use the existing full parser.
        if (length < 1 || length > (allowUnicodeDigits ? 5 * maximumDigits : maximumDigits)) return false;

        ulong significand = 0;
        var digits = 0;
        var point = -1;
        for (var offset = start; offset < input.Length;)
        {
            var character = input[offset++];
            var digit = (uint)(character - (byte)'0');
            if (digit > 9)
            {
                if (character == (byte)'.' && point < 0)
                {
                    point = digits;
                    continue;
                }
                if (!allowUnicodeDigits) return false;
                if (character == (byte)'_')
                {
                    if (digits == 0 || point == digits || offset == input.Length) return false;
                    character = input[offset++];
                    digit = (uint)(character - (byte)'0');
                }
                if (digit > 9)
                {
                    if (character < 128 || Rune.DecodeFromUtf8(input[(offset - 1)..], out var rune, out var width)
                        != OperationStatus.Done) return false;
                    digit = (uint)PyStringOps.GetDecimalDigitValue(rune);
                    if (digit > 9) return false;
                    offset += width - 1;
                }
            }
            if (++digits > maximumDigits) return false;
            significand = significand * 10 + digit;
        }

        if (digits == 0 || significand > 1UL << 53) return false;
        var fractionDigits = point < 0 ? 0 : digits - point;
        result = ConvertExactDecimalToDouble(significand, fractionDigits, negative);
        return true;
    }

    private static ReadOnlySpan<double> ExactDecimalDivisors =>
    [
        1e0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9, 1e10, 1e11,
        1e12, 1e13, 1e14, 1e15, 1e16, 1e17, 1e18,
    ];

    // Both operands are exactly representable. One division rounds the
    // decimal quotient to binary64. Apply the sign last to keep negative zero.
    private static double ConvertExactDecimalToDouble(ulong significand, int fractionDigits, bool negative)
    {
        var number = (double)significand / ExactDecimalDivisors[fractionDigits];
        return negative ? -number : number;
    }

    private static bool TryParseWideFloat(ReadOnlySpan<byte> input, bool unicode, ExecutionContext context,
        LythonSourceSpan span, out double result)
    {
        var normalize = false;
        result = default;
        for (var offset = 0; offset < input.Length;)
        {
            var count = Math.Min(4096, input.Length - offset);
            var part = input.Slice(offset, count);
            var special = part.IndexOfAny((byte)0, (byte)'_');
            if (special >= 0 && part[special] == 0) return false;
            var ascii = System.Text.Ascii.IsValid(part);
            if (!ascii && !unicode) return false;
            normalize |= special >= 0 || !ascii;
            offset += count;
            context.CheckExecution(span);
        }
        return normalize ? TryParseNormalizedFloat(input, unicode, context, span, out result)
            : TryParseAsciiFloat(input, out result);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryParseNormalizedFloat(ReadOnlySpan<byte> input, bool unicode, ExecutionContext context,
        LythonSourceSpan span, out double result)
    {
        // Unicode decimal digits and underscores can only shrink the token.
        // Small scratch stays inside this synchronous call. Wider tokens admit
        // the capacity needed for the ASCII form before allocating the buffer.
        using var storage = input.Length <= FloatNormalizationStackBytes ? null
            : context.MemoryGovernor.ReserveTemporary(32L + input.Length, span);
        context.CheckExecution(span);
        Span<byte> normalized = storage is null ? stackalloc byte[FloatNormalizationStackBytes] : new byte[input.Length];
        var written = 0;
        var previousDigit = false;
        var previousUnderscore = false;
        var nextCheck = Math.Min(4096, input.Length);
        result = default;
        for (var offset = 0; offset < input.Length;)
        {
            if (offset >= nextCheck)
            {
                context.CheckExecution(span);
                nextCheck = offset + Math.Min(4096, input.Length - offset);
            }
            var character = input[offset++];
            if (character == 0) return false;
            if (character == (byte)'_')
            {
                if (!previousDigit || previousUnderscore) return false;
                previousUnderscore = true;
                continue;
            }
            if (character >= 128)
            {
                // Outer whitespace was already trimmed. Interior whitespace,
                // non-decimal scalars and invalid encodings cannot be digits.
                if (!unicode || Rune.DecodeFromUtf8(input[(offset - 1)..], out var rune, out var width)
                    != OperationStatus.Done) return false;
                var decimalDigit = PyStringOps.GetDecimalDigitValue(rune);
                if (decimalDigit < 0) return false;
                offset += width - 1;
                character = (byte)('0' + decimalDigit);
            }
            var digit = character is >= (byte)'0' and <= (byte)'9';
            if (previousUnderscore && !digit) return false;
            normalized[written++] = character;
            previousDigit = digit;
            previousUnderscore = false;
        }
        context.CheckExecution(span);
        return !previousUnderscore && TryParseAsciiFloat(normalized[..written], out result);
    }

    private static bool TryParseAsciiFloat(ReadOnlySpan<byte> input, out double result)
    {
        var negative = !input.IsEmpty && input[0] == (byte)'-';
        var unsigned = !input.IsEmpty && input[0] is (byte)'+' or (byte)'-' ? input[1..] : input;
        if (!unsigned.IsEmpty)
        {
            switch (unsigned[0] | 0x20)
            {
                case 'i' when System.Text.Ascii.EqualsIgnoreCase(unsigned, "inf"u8) ||
                    System.Text.Ascii.EqualsIgnoreCase(unsigned, "infinity"u8):
                    result = negative ? double.NegativeInfinity : double.PositiveInfinity;
                    return true;
                case 'n' when System.Text.Ascii.EqualsIgnoreCase(unsigned, "nan"u8):
                    result = negative ? -PythonNaN : PythonNaN;
                    return true;
            }
        }
        return double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    private static object ValidateFloatConversionResult(object converted, PyInstance instance, ExecutionContext context, LythonSourceSpan span)
        => converted is double ? converted : throw new LythonRuntimeException("TypeError", instance.Type.Name +
            ".__float__ returned non-float (type " + UnboundTypeMethod.PythonTypeName(converted, context) + ")", span);

    private static LythonRuntimeException FloatArgumentError(object value, ExecutionContext context, LythonSourceSpan span)
        => new("TypeError", "float() argument must be a string or a real number, not '" + UnboundTypeMethod.PythonTypeName(value, context) + "'", span);

    // Conversion rounds exactly; out-of-range integers fail instead of saturating.
    private static double FloatFromInteger(BigInteger integer, LythonSourceSpan span)
    {
        try
        {
            return PyNumberOps.BigIntegerToDouble(integer);
        }
        catch (OverflowException ex)
        {
            throw new LythonRuntimeException("OverflowError", ex.Message, span);
        }
    }

    private static LythonRuntimeException TextConversionError(object source, string prefix, ExecutionContext context,
        LythonSourceSpan span)
        => source is PyString text ? TextConversionError(text.Utf8Span, prefix, context, span)
            : TextConversionError(default(ReadOnlySpan<byte>), prefix, context, span, (PyBytes)source);

    private static LythonRuntimeException TextConversionError(ReadOnlySpan<byte> source, string prefix,
        ExecutionContext context, LythonSourceSpan span, PyBytes? byteSource = null)
    {
        var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
        context.EnterInterpreterFrame(span);
        try
        {
            context.CheckExecution(span);
            builder.AppendAscii(prefix);
            if (byteSource is null) PyRendering.AppendStringLiteral(builder, source, context, span);
            else byteSource.AppendLiteral(builder, context, span);

            // Admit the CLR message while the UTF-8 builder and any parsing
            // scratch are still live. The message can escape with the error.
            var bytes = checked(32L + 2L * builder.Length);
            using var messageStorage = context.MemoryGovernor.ReserveTemporary(bytes, span);
            context.CheckExecution(span);
            var error = new LythonRuntimeException("ValueError", Encoding.UTF8.GetString(builder.WrittenSpan), span);
            messageStorage.Commit(bytes);
            return error;
        }
        finally
        {
            try { builder.Release(); }
            finally { context.LeaveInterpreterFrame(); }
        }
    }

    private static object Bytes(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 0)
        {
            return new PyBytes(Array.Empty<byte>());
        }

        if (arguments.Length > 3)
        {
            throw new LythonRuntimeException("TypeError", "bytes([source[, encoding[, errors]]]) expects zero to three arguments.", span);
        }

        if (arguments.Length >= 2)
        {
            // CPython validates the codec names before the source.
            RequireCodecNameType(arguments[1], "bytes()", "encoding", span);
            if (arguments.Length == 3)
            {
                RequireCodecNameType(arguments[2], "bytes()", "errors", span);
            }

            if (!PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "encoding without a string argument", span);
            }

            var encoding = ParseTextEncoding(arguments[1], "bytes()", span);
            var errors = arguments.Length == 3 ? ParseTextErrors(arguments[2], "bytes()", span) : TextErrorMode.Strict;
            return EncodeGuestBytes(text, encoding, errors, context, span);
        }

        return arguments[0] switch
        {
            PyBytes bytes => bytes,
            PyString => throw new LythonRuntimeException("TypeError", "string argument without an encoding", span),
            string => throw new LythonRuntimeException("TypeError", "string argument without an encoding", span),
            bool size => CreateZeroBytes(size ? BigInteger.One : BigInteger.Zero, context, span),
            _ when PyNumberOps.TryAsInteger(arguments[0], out var size) => CreateSizedBytes(size, "int", context, span),
            PyInstance instance => CreateBytesFromInstanceAsync(instance, context, span, false).GetAwaiter().GetResult(),
            _ => BytesFromIterable(arguments[0], context, span)
        };
    }

    private static async ValueTask<object> BytesAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1 || arguments[0] is PyBytes or PyString or string || PyNumberOps.IsInteger(arguments[0]))
            return Bytes(arguments, span, context);
        if (arguments[0] is PyInstance instance)
            return await CreateBytesFromInstanceAsync(instance, context, span, true).ConfigureAwait(false);
        var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
        try
        {
            await FillBytesOperandsAsync(arguments[0], builder, context, span).ConfigureAwait(false);
            return builder.ToPyBytesAndRelease();
        }
        finally { builder.Release(); }
    }

    private static PyBytes BytesFromIterable(object source, ExecutionContext context, LythonSourceSpan span)
    {
        var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
        try
        {
            FillBytesOperands(source, builder, context, span);
            return builder.ToPyBytesAndRelease();
        }
        finally { builder.Release(); }
    }

    private static async ValueTask<PyBytes> CreateBytesFromInstanceAsync(PyInstance value, ExecutionContext context,
        LythonSourceSpan span, bool asynchronous)
    {
        var protocol = asynchronous
            ? await InvokeUnarySpecialMethodAsync(value, "__bytes__", context, span).ConfigureAwait(false)
            : TryInvokeUnarySpecialMethod(value, "__bytes__", context, span, out var bytesValue)
                ? SpecialMethodInvocation.Invoked(bytesValue) : SpecialMethodInvocation.Missing;
        if (protocol.Kind == SpecialMethodInvocationKind.Invoked)
            return ValidateBytesConversionResult(protocol.Value, context, span);

        // A TypeError from __index__ falls back to iteration; other errors and
        // admitted-sized allocation failures propagate without a retry.
        BigInteger? size = null;
        try
        {
            protocol = asynchronous
                ? await InvokeUnarySpecialMethodAsync(value, "__index__", context, span).ConfigureAwait(false)
                : TryInvokeUnarySpecialMethod(value, "__index__", context, span, out var indexValue)
                    ? SpecialMethodInvocation.Invoked(indexValue) : SpecialMethodInvocation.Missing;
            if (protocol.Kind == SpecialMethodInvocationKind.Invoked)
                size = (BigInteger)ValidateIntegerConversionResult(protocol.Value, "__index__", context, span);
        }
        catch (LythonRuntimeException error) when (error.ExceptionType == "TypeError" && context.MemoryGovernor.AdmissionFailure is null) { }
        if (size is { } count) return CreateSizedBytes(count, value.Type.Name, context, span);
        if (!asynchronous) return BytesFromIterable(value, context, span);
        var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
        try
        {
            await FillBytesOperandsAsync(value, builder, context, span).ConfigureAwait(false);
            return builder.ToPyBytesAndRelease();
        }
        finally { builder.Release(); }
    }

    private static PyBytes ValidateBytesConversionResult(object value, ExecutionContext context, LythonSourceSpan span)
        => value as PyBytes ?? throw new LythonRuntimeException("TypeError",
            "__bytes__ returned non-bytes (type " + UnboundTypeMethod.PythonTypeName(value, context) + ")", span);

    private static PyBytes CreateSizedBytes(BigInteger size, string typeName, ExecutionContext context, LythonSourceSpan span)
    {
        // Out-of-ssize sizes name the source type like CPython; smaller
        // negatives report negative count and CLR-limited sizes stay too-large.
        if (size > long.MaxValue || size < long.MinValue)
        {
            throw new LythonRuntimeException("OverflowError", "cannot fit '" + typeName + "' into an index-sized integer", span);
        }

        return CreateZeroBytes(size, context, span);
    }

    private static PyBytes CreateZeroBytes(BigInteger size, ExecutionContext context, LythonSourceSpan span)
    {
        if (size < 0)
        {
            throw new LythonRuntimeException("ValueError", "negative count", span);
        }

        if (size > int.MaxValue)
        {
            throw new LythonRuntimeException("OverflowError", "bytes object is too large", span);
        }

        using var storage = context.MemoryGovernor.ReserveTemporary(PyBytes.EstimateApproximateBytes((int)size), span);
        context.CheckExecution(span);
        return new PyBytes(new byte[(int)size], context.MemoryGovernor, span, storage);
    }

    private static object Property(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var getter = arguments.Length >= 1 ? arguments[0] : PyNone.Instance;
        var setter = arguments.Length >= 2 ? arguments[1] : PyNone.Instance;
        var deleter = arguments.Length >= 3 ? arguments[2] : PyNone.Instance;
        var doc = arguments.Length >= 4 ? arguments[3] : PyNone.Instance;
        return PyProperty.Create(getter, setter, deleter, doc, context, span);
    }

    private static async ValueTask<object> PropertyAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var getter = arguments.Length >= 1 ? arguments[0] : PyNone.Instance;
        var setter = arguments.Length >= 2 ? arguments[1] : PyNone.Instance;
        var deleter = arguments.Length >= 3 ? arguments[2] : PyNone.Instance;
        var doc = arguments.Length >= 4 ? arguments[3] : PyNone.Instance;
        return await PyProperty.CreateAsync(getter, setter, deleter, doc, context, span).ConfigureAwait(false);
    }

    private static object StaticMethod(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1 || arguments[0] is not ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", "staticmethod(func) expects one callable argument.", span);
        }

        context.MemoryGovernor.Reserve(64L, span);
        context.MemoryGovernor.Commit(64L);
        return new PyStaticMethod(callable);
    }

    private static object ClassMethod(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1 || arguments[0] is not ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", "classmethod(func) expects one callable argument.", span);
        }

        context.MemoryGovernor.Reserve(64L, span);
        context.MemoryGovernor.Commit(64L);
        return new PyClassMethod(callable);
    }

    internal static bool IsDefaultObjectNew(object slot) => slot is ObjectNewMethod;

    private sealed class ObjectNewMethod : ICallable, IPyDynamicAttributes, IClassOwnedMember, IPyRenderableValue
    {
        // object.__new__ is a builtin method: CPython reports the short
        // __name__, the qualified __qualname__ and a None __module__.
        // The owning type is threaded at class construction so __self__
        // reports the defining type like CPython.
        private PyType? _owner;

        public void BindOwner(PyType owner) => _owner ??= owner;

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString("__new__");
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object.__new__");
                return true;
            }

            if (name is "__module__")
            {
                value = PyNone.Instance;
                return true;
            }

            if (name is "__self__" && _owner is not null)
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        // object.__new__ renders like CPython builtin methods bound to the
        // type, minus the address suffix (like the other new slots).
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<built-in method __new__ of type object>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);

            if (arguments.Length == 0 || arguments[0].Value is not PyType type)
            {
                throw new LythonRuntimeException("TypeError", "object.__new__(cls, ...) expects the first argument to be a class.", span);
            }

            var instance = new PyInstance(type, context.MemoryGovernor, span);
            return instance;
        }
    }

    private sealed class ObjectInitMethod : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes, IPySlotWrapper, IClassOwnedMember, IPyRenderableValue
    {
        // Unbound object slots render like CPython slot wrappers (quoted
        // owner, plural objects).
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<slot wrapper '__init__' of 'object' objects>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public string Name => "__init__";

        // The owning type is threaded at class construction so
        // __objclass__ reports the defining type like CPython.
        private PyType? _owner;

        public void BindOwner(PyType owner) => _owner ??= owner;

        // Slot wrappers expose the short __name__ and the qualified
        // __qualname__ like CPython; __module__ stays missing (CPython
        // raises AttributeError there), unlike builtin methods.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object." + Name);
                return true;
            }

            if (name is "__objclass__" && _owner is not null)
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            // Any single receiver initializes to None like CPython; only the
            // arity is enforced here (attribute support is resolved by the
            // receiver type, not by this slot).
            if (arguments.Length != 1 || arguments[0].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "object.__init__(self) does not accept additional arguments.", span);
            }

            return PyNone.Instance;
        }
    }

    private sealed class ObjectInitSubclassMethod : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes, IPyBoundEngineMethod
    {
        public string Name => "__init_subclass__";

        // __init_subclass__ surfaces as a bound builtin method: CPython
        // reports the qualified __qualname__ and a None __module__.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object." + Name);
                return true;
            }

            if (name is "__module__")
            {
                value = PyNone.Instance;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            // The bound receiver is always class-like at this point (user types
            // arrive as PyType, builtin values as their type-denoting object);
            // only the arity is enforced, like CPython.
            if (arguments.Length != 1 || arguments[0].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "object.__init_subclass__() takes no arguments.", span);
            }

            return PyNone.Instance;
        }
    }

    private sealed class ObjectSetAttrMethod : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes, IPySlotWrapper, IClassOwnedMember, IPyRenderableValue
    {
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length == 3 && !arguments.Any(static argument => argument.IsKeyword) &&
                arguments[0].Value is PyInstance instance && PyStringOps.TryAsString(arguments[1].Value, out var name))
            {
                await SetObjectInstanceAttributeAsync(instance, name.AsString(), arguments[2].Value, context, span).ConfigureAwait(false);
                return PyNone.Instance;
            }
            return Invoke(arguments, span, context);
        }

        // Unbound object slots render like CPython slot wrappers (quoted
        // owner, plural objects).
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<slot wrapper '__setattr__' of 'object' objects>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public string Name => "__setattr__";

        // The owning type is threaded at class construction so
        // __objclass__ reports the defining type like CPython.
        private PyType? _owner;

        public void BindOwner(PyType owner) => _owner ??= owner;

        // Slot wrappers expose the short __name__ and the qualified
        // __qualname__ like CPython; __module__ stays missing (CPython
        // raises AttributeError there), unlike builtin methods.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object." + Name);
                return true;
            }

            if (name is "__objclass__" && _owner is not null)
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 3 ||
                arguments[0].IsKeyword ||
                arguments[1].IsKeyword ||
                arguments[2].IsKeyword ||
                !PyStringOps.TryAsString(arguments[1].Value, out var name))
            {
                throw new LythonRuntimeException("TypeError", "object.__setattr__(self, name, value) expects an instance, a string name, and a value.", span);
            }

            if (arguments[0].Value is PyInstance instance)
            {
                SetObjectInstanceAttribute(instance, name.AsString(), arguments[2].Value, context, span);
                return PyNone.Instance;
            }

            // Other receivers follow statement assignment: writable modules,
            // types and mutable members accept; anything else has no
            // attribute table to write.
            if (PyMemberAccess.TryAssign(arguments[0].Value, name.AsString(), arguments[2].Value, context, span))
            {
                return PyNone.Instance;
            }

            throw PyMemberAccess.CreateMissingMemberError(arguments[0].Value, name.AsString(), span, context, operation: MissingMemberOperation.Write);
        }
    }

    private sealed class ObjectDelAttrMethod : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes, IPySlotWrapper, IClassOwnedMember, IPyRenderableValue
    {
        // Unbound object slots render like CPython slot wrappers (quoted
        // owner, plural objects).
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<slot wrapper '__delattr__' of 'object' objects>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public string Name => "__delattr__";

        // The owning type is threaded at class construction so
        // __objclass__ reports the defining type like CPython.
        private PyType? _owner;

        public void BindOwner(PyType owner) => _owner ??= owner;

        // Slot wrappers expose the short __name__ and the qualified
        // __qualname__ like CPython; __module__ stays missing (CPython
        // raises AttributeError there), unlike builtin methods.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object." + Name);
                return true;
            }

            if (name is "__objclass__" && _owner is not null)
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 2 || arguments[0].IsKeyword || arguments[1].IsKeyword ||
                !PyStringOps.TryAsString(arguments[1].Value, out var name))
                throw new LythonRuntimeException("TypeError", "object.__delattr__(self, name) expects an instance and a string name.", span);
            if (arguments[0].Value is not PyInstance instance) return Invoke(arguments, span, context);
            var memberName = name.AsString();
            if (instance.Type.TryLookupInMro(memberName, 0, out var rawValue, out _) &&
                await PyAttributeLookup.TryDeleteDescriptorValueAsync(rawValue, instance, context, span).ConfigureAwait(false))
                return PyNone.Instance;
            if (!instance.RemoveAttribute(memberName))
                throw PyMemberAccess.CreateMissingMemberError(instance, memberName, span, context, operation: MissingMemberOperation.Delete);
            return PyNone.Instance;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 2 ||
                arguments[0].IsKeyword ||
                arguments[1].IsKeyword ||
                !PyStringOps.TryAsString(arguments[1].Value, out var name))
            {
                throw new LythonRuntimeException("TypeError", "object.__delattr__(self, name) expects an instance and a string name.", span);
            }

            var memberName = name.AsString();
            if (arguments[0].Value is PyInstance instance)
            {
                if (instance.Type.TryLookupInMro(memberName, 0, out var rawValue, out _) &&
                    PyAttributeLookup.TryDeleteDescriptorValue(rawValue, instance, context, span))
                {
                    return PyNone.Instance;
                }

                if (!instance.RemoveAttribute(memberName))
                {
                    throw PyMemberAccess.CreateMissingMemberError(instance, memberName, span, context, operation: MissingMemberOperation.Delete);
                }

                return PyNone.Instance;
            }

            // Other receivers follow statement deletion, except that
            // object.__delattr__ refuses type objects like CPython; anything
            // else has no attribute table to delete from.
            if (arguments[0].Value is PyType)
            {
                throw new LythonRuntimeException("TypeError", "can't apply this __delattr__ to type object", span);
            }

            if (PyMemberAccess.TryDelete(arguments[0].Value, memberName, context, span))
            {
                return PyNone.Instance;
            }

            throw PyMemberAccess.CreateMissingMemberError(arguments[0].Value, memberName, span, context, operation: MissingMemberOperation.Delete);
        }
    }

    private sealed class ObjectGetAttrMethod : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes, IPySlotWrapper, IClassOwnedMember, IPyRenderableValue
    {
        // Unbound object slots render like CPython slot wrappers (quoted
        // owner, plural objects).
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<slot wrapper '__getattribute__' of 'object' objects>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public string Name => "__getattribute__";

        // The owning type is threaded at class construction so
        // __objclass__ reports the defining type like CPython.
        private PyType? _owner;

        public void BindOwner(PyType owner) => _owner ??= owner;

        // Slot wrappers expose the short __name__ and the qualified
        // __qualname__ like CPython; __module__ stays missing (CPython
        // raises AttributeError there), unlike builtin methods.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object." + Name);
                return true;
            }

            if (name is "__objclass__" && _owner is not null)
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 2 ||
                arguments[0].IsKeyword ||
                arguments[1].IsKeyword ||
                !PyStringOps.TryAsString(arguments[1].Value, out var name))
            {
                throw new LythonRuntimeException("TypeError", "object.__getattribute__(self, name) expects an instance and a string name.", span);
            }

            var memberName = name.AsString();
            if (arguments[0].Value is PyInstance instance)
            {
                return GetObjectInstanceAttribute(instance, memberName, context, span);
            }

            // Other receivers resolve through the same choke as ordinary
            // member reads, including bound-method receivers, since reads
            // never route back through this slot.
            if (TryResolveRuntimeMember(arguments[0].Value, memberName, context, span, out var resolved))
            {
                return resolved;
            }

            throw PyMemberAccess.CreateMissingMemberError(arguments[0].Value, memberName, span, context);
        }

        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 2 || arguments[0].IsKeyword || arguments[1].IsKeyword ||
                !PyStringOps.TryAsString(arguments[1].Value, out var name))
                throw new LythonRuntimeException("TypeError", "object.__getattribute__(self, name) expects an instance and a string name.", span);

            var memberName = name.AsString();
            if (arguments[0].Value is PyInstance instance)
                return await GetObjectInstanceAttributeAsync(instance, memberName, context, span).ConfigureAwait(false);
            var resolved = await TryResolveRuntimeMemberAsync(arguments[0].Value, memberName, context, span).ConfigureAwait(false);
            if (resolved.Found) return resolved.Value;
            throw PyMemberAccess.CreateMissingMemberError(arguments[0].Value, memberName, span, context);
        }
    }

    private sealed class ObjectEqMethod : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes, IPySlotWrapper, IClassOwnedMember, IPyRenderableValue
    {
        // Unbound object slots render like CPython slot wrappers (quoted
        // owner, plural objects).
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<slot wrapper '__eq__' of 'object' objects>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public string Name => "__eq__";

        // The owning type is threaded at class construction so
        // __objclass__ reports the defining type like CPython.
        private PyType? _owner;

        public void BindOwner(PyType owner) => _owner ??= owner;

        // Slot wrappers expose the short __name__ and the qualified
        // __qualname__ like CPython; __module__ stays missing (CPython
        // raises AttributeError there), unlike builtin methods.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object." + Name);
                return true;
            }

            if (name is "__objclass__" && _owner is not null)
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            // Default equality answers identity like CPython (value and dataclass
            // comparisons keep their own paths); distinct operands get NotImplemented
            // so reflected dispatch can proceed. Only the arity is enforced here.
            if (arguments.Length != 2 || arguments[0].IsKeyword || arguments[1].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "object.__eq__(self, other) expects exactly two arguments.", span);
            }

            if (ReferenceEquals(arguments[0].Value, arguments[1].Value))
            {
                return true;
            }

            return PyNotImplemented.Instance;
        }
    }

    private sealed class ObjectNeMethod : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes, IPySlotWrapper, IClassOwnedMember, IPyRenderableValue
    {
        // Unbound object slots render like CPython slot wrappers (quoted
        // owner, plural objects).
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<slot wrapper '__ne__' of 'object' objects>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public string Name => "__ne__";

        // The owning type is threaded at class construction so
        // __objclass__ reports the defining type like CPython.
        private PyType? _owner;

        public void BindOwner(PyType owner) => _owner ??= owner;

        // Slot wrappers expose the short __name__ and the qualified
        // __qualname__ like CPython; __module__ stays missing (CPython
        // raises AttributeError there), unlike builtin methods.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object." + Name);
                return true;
            }

            if (name is "__objclass__" && _owner is not null)
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            // Default inequality negates identity like CPython (value and dataclass
            // comparisons keep their own paths); distinct operands get NotImplemented
            // so reflected dispatch can proceed. Only the arity is enforced here.
            if (arguments.Length != 2 || arguments[0].IsKeyword || arguments[1].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "object.__ne__(self, other) expects exactly two arguments.", span);
            }

            if (ReferenceEquals(arguments[0].Value, arguments[1].Value))
            {
                return false;
            }

            return PyNotImplemented.Instance;
        }
    }
    private sealed class ObjectLtMethod : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes, IPySlotWrapper, IClassOwnedMember, IPyRenderableValue
    {
        // Unbound object slots render like CPython slot wrappers (quoted
        // owner, plural objects).
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<slot wrapper '__lt__' of 'object' objects>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public string Name => "__lt__";

        // The owning type is threaded at class construction so
        // __objclass__ reports the defining type like CPython.
        private PyType? _owner;

        public void BindOwner(PyType owner) => _owner ??= owner;

        // Slot wrappers expose the short __name__ and the qualified
        // __qualname__ like CPython; __module__ stays missing (CPython
        // raises AttributeError there), unlike builtin methods.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object." + Name);
                return true;
            }

            if (name is "__objclass__" && _owner is not null)
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            // Default ordering is undefined like CPython (value and dataclass
            // comparisons keep their own paths); the slot always answers
            // NotImplemented, even for identical operands. Only the arity is
            // enforced here.
            if (arguments.Length != 2 || arguments[0].IsKeyword || arguments[1].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "object.__lt__(self, other) expects exactly two arguments.", span);
            }

            return PyNotImplemented.Instance;
        }
    }    private sealed class ObjectLeMethod : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes, IPySlotWrapper, IClassOwnedMember, IPyRenderableValue
    {
        // Unbound object slots render like CPython slot wrappers (quoted
        // owner, plural objects).
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<slot wrapper '__le__' of 'object' objects>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public string Name => "__le__";

        // The owning type is threaded at class construction so
        // __objclass__ reports the defining type like CPython.
        private PyType? _owner;

        public void BindOwner(PyType owner) => _owner ??= owner;

        // Slot wrappers expose the short __name__ and the qualified
        // __qualname__ like CPython; __module__ stays missing (CPython
        // raises AttributeError there), unlike builtin methods.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object." + Name);
                return true;
            }

            if (name is "__objclass__" && _owner is not null)
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            // Default ordering is undefined like CPython (value and dataclass
            // comparisons keep their own paths); the slot always answers
            // NotImplemented, even for identical operands. Only the arity is
            // enforced here.
            if (arguments.Length != 2 || arguments[0].IsKeyword || arguments[1].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "object.__le__(self, other) expects exactly two arguments.", span);
            }

            return PyNotImplemented.Instance;
        }
    }    private sealed class ObjectGtMethod : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes, IPySlotWrapper, IClassOwnedMember, IPyRenderableValue
    {
        // Unbound object slots render like CPython slot wrappers (quoted
        // owner, plural objects).
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<slot wrapper '__gt__' of 'object' objects>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public string Name => "__gt__";

        // The owning type is threaded at class construction so
        // __objclass__ reports the defining type like CPython.
        private PyType? _owner;

        public void BindOwner(PyType owner) => _owner ??= owner;

        // Slot wrappers expose the short __name__ and the qualified
        // __qualname__ like CPython; __module__ stays missing (CPython
        // raises AttributeError there), unlike builtin methods.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object." + Name);
                return true;
            }

            if (name is "__objclass__" && _owner is not null)
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            // Default ordering is undefined like CPython (value and dataclass
            // comparisons keep their own paths); the slot always answers
            // NotImplemented, even for identical operands. Only the arity is
            // enforced here.
            if (arguments.Length != 2 || arguments[0].IsKeyword || arguments[1].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "object.__gt__(self, other) expects exactly two arguments.", span);
            }

            return PyNotImplemented.Instance;
        }
    }    private sealed class ObjectGeMethod : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes, IPySlotWrapper, IClassOwnedMember, IPyRenderableValue
    {
        // Unbound object slots render like CPython slot wrappers (quoted
        // owner, plural objects).
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<slot wrapper '__ge__' of 'object' objects>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public string Name => "__ge__";

        // The owning type is threaded at class construction so
        // __objclass__ reports the defining type like CPython.
        private PyType? _owner;

        public void BindOwner(PyType owner) => _owner ??= owner;

        // Slot wrappers expose the short __name__ and the qualified
        // __qualname__ like CPython; __module__ stays missing (CPython
        // raises AttributeError there), unlike builtin methods.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object." + Name);
                return true;
            }

            if (name is "__objclass__" && _owner is not null)
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            // Default ordering is undefined like CPython (value and dataclass
            // comparisons keep their own paths); the slot always answers
            // NotImplemented, even for identical operands. Only the arity is
            // enforced here.
            if (arguments.Length != 2 || arguments[0].IsKeyword || arguments[1].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "object.__ge__(self, other) expects exactly two arguments.", span);
            }

            return PyNotImplemented.Instance;
        }
    }    private sealed class ObjectStrMethod : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes, IPySlotWrapper, IClassOwnedMember, IPyRenderableValue
    {
        // Unbound object slots render like CPython slot wrappers (quoted
        // owner, plural objects).
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<slot wrapper '__str__' of 'object' objects>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public string Name => "__str__";

        private PyType? _owner;

        public void BindOwner(PyType owner) => _owner ??= owner;

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object." + Name);
                return true;
            }

            if (name is "__objclass__" && _owner is not null)
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var receiver = Receiver(arguments, span, context);
            return receiver is PyInstance instance
                ? instance.RenderPython(new PyRenderingContext(context))
                : PyRendering.ToInterpolatedPyString(receiver, new PyRenderingContext(context));
        }

        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var receiver = Receiver(arguments, span, context);
            return receiver is PyInstance instance
                ? RenderReprAsync(instance, context)
                : new(PyRendering.ToInterpolatedPyString(receiver, new PyRenderingContext(context)));
        }

        private static object Receiver(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            if (arguments.Length != 1 || arguments[0].IsKeyword)
                throw new LythonRuntimeException("TypeError", "object.__str__(self) expects exactly one argument.", span);
            return arguments[0].Value;
        }

        private static async ValueTask<object> RenderReprAsync(PyInstance instance, ExecutionContext context)
            => await instance.RenderPythonAsync(new PyRenderingContext(context)).ConfigureAwait(false);
    }
    private sealed class ObjectReprMethod : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes, IPySlotWrapper, IClassOwnedMember, IPyRenderableValue
    {
        // Unbound object slots render like CPython slot wrappers (quoted
        // owner, plural objects).
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<slot wrapper '__repr__' of 'object' objects>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public string Name => "__repr__";

        private PyType? _owner;

        public void BindOwner(PyType owner) => _owner ??= owner;

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object." + Name);
                return true;
            }

            if (name is "__objclass__" && _owner is not null)
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 1 || arguments[0].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "object.__repr__(self) expects exactly one argument.", span);
            }

            // The default representation never consults overrides (explicit
            // root-slot calls bypass them like CPython); user and generated
            // representations shadow this slot through member lookup.
            if (arguments[0].Value is PyInstance instance)
            {
                return instance.RenderObjectRepresentation();
            }

            return PyRendering.ToReprPyString(arguments[0].Value, new PyRenderingContext(context));
        }
    }
    private sealed class ObjectHashMethod : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes, IPySlotWrapper, IClassOwnedMember, IPyRenderableValue
    {
        // Unbound object slots render like CPython slot wrappers (quoted
        // owner, plural objects).
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<slot wrapper '__hash__' of 'object' objects>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public string Name => "__hash__";

        private PyType? _owner;

        public void BindOwner(PyType owner) => _owner ??= owner;

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object." + Name);
                return true;
            }

            if (name is "__objclass__" && _owner is not null)
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            if (arguments.Length != 1 || arguments[0].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "object.__hash__(self) expects exactly one argument.", span);
            }

            // Identity hashes surface as BigInteger like every other guest
            // integer (raw CLR integers stay second-class in numeric paths).
            return new BigInteger(RuntimeHelpers.GetHashCode(arguments[0].Value));
        }
    }
    // object.__format__ behaves like CPython method descriptors: the unbound
    // shape renders like one, calls forward through a bound engine method,
    // and binding reports the receiver type through the shared helper.
    private sealed class ObjectFormatMethod : ICallable, IPyBindableCallable, IPyDynamicAttributes, IPyHashableValue, IPyRenderableValue, IClassOwnedMember
    {
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<method '__format__' of 'object' objects>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public int GetPyHashCode() => HashCode.Combine(StringComparer.Ordinal.GetHashCode("object"), StringComparer.Ordinal.GetHashCode("__format__"));

        private PyType? _owner;

        public void BindOwner(PyType owner) => _owner ??= owner;

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString("__format__");
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object.__format__");
                return true;
            }

            if (name is "__objclass__" && _owner is not null)
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self)
        {
            var bound = BoundCallable.Create(
                (arguments, span, context) => FormatImpl(self, arguments, span, context),
                (arguments, span, context) => FormatImplAsync(self, arguments, span, context),
                "object.__format__",
                ["spec"],
                1);
            bound.AttachReceiver(self);
            return bound;
        }

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => InvokeCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();

        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => InvokeCoreAsync(arguments, span, context, true);

        private ValueTask<object> InvokeCoreAsync(CallArgumentValue[] arguments, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            var receiverIndex = -1;
            for (var i = 0; i < arguments.Length; i++)
            {
                if (arguments[i].IsPositional)
                {
                    receiverIndex = i;
                    break;
                }
            }

            if (receiverIndex < 0)
            {
                throw new LythonRuntimeException("TypeError", "unbound method object.__format__() needs an argument", span);
            }

            var receiver = arguments[receiverIndex].Value;
            var bound = (ICallable)Bind(receiver);
            var rest = new CallArgumentValue[arguments.Length - 1];
            Array.Copy(arguments, 0, rest, 0, receiverIndex);
            Array.Copy(arguments, receiverIndex + 1, rest, receiverIndex, rest.Length - receiverIndex);
            return asynchronous ? bound.InvokeAsync(rest, span, context) : new(bound.Invoke(rest, span, context));
        }

        private static object FormatImpl(object receiver, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            ValidateSpecifier(receiver, arguments, span);
            return ToInterpolatedPyString(receiver, context);
        }

        private static ValueTask<object> FormatImplAsync(object receiver, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            ValidateSpecifier(receiver, arguments, span);
            return receiver is PyInstance instance
                ? RenderValueStringAsync(instance, 's', context, span)
                : new(ToInterpolatedPyString(receiver, context));
        }

        private static void ValidateSpecifier(object receiver, object[] arguments, LythonSourceSpan span)
        {
            if (!PyStringOps.TryAsString(arguments[0], out var spec))
            {
                throw new LythonRuntimeException("TypeError", "object.__format__(self, spec) expects the format specification to be a string.", span);
            }

            // Only the empty spec formats through str() like CPython; any
            // other spec on the default implementation reports TypeError
            // naming the receiver type, mirroring the format builtin.
            if (spec.AsString().Length != 0)
            {
                throw new LythonRuntimeException("TypeError", $"unsupported format string passed to {RuntimeErrors.OperandTypeName(receiver)}.__format__", span);
            }
        }
    }
    // object.__dir__ behaves like CPython method descriptors: the unbound
    // shape renders like one, calls forward through a bound engine method,
    // and binding reports the receiver type through the shared helper.
    private sealed class ObjectDirMethod : ICallable, IPyBindableCallable, IPyDynamicAttributes, IPyHashableValue, IPyRenderableValue, IClassOwnedMember
    {
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<method '__dir__' of 'object' objects>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public int GetPyHashCode() => HashCode.Combine(StringComparer.Ordinal.GetHashCode("object"), StringComparer.Ordinal.GetHashCode("__dir__"));

        private PyType? _owner;

        public void BindOwner(PyType owner) => _owner ??= owner;

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString("__dir__");
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object.__dir__");
                return true;
            }

            if (name is "__objclass__" && _owner is not null)
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self)
        {
            var bound = BoundCallable.Create(
                (arguments, span, context) => DirImpl(self, span, context),
                "object.__dir__");
            bound.AttachReceiver(self);
            return bound;
        }

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            var receiverIndex = -1;
            for (var i = 0; i < arguments.Length; i++)
            {
                if (arguments[i].IsPositional)
                {
                    receiverIndex = i;
                    break;
                }
            }

            if (receiverIndex < 0)
            {
                throw new LythonRuntimeException("TypeError", "unbound method object.__dir__() needs an argument", span);
            }

            var receiver = arguments[receiverIndex].Value;
            var bound = (ICallable)Bind(receiver);
            var rest = new CallArgumentValue[arguments.Length - 1];
            Array.Copy(arguments, 0, rest, 0, receiverIndex);
            Array.Copy(arguments, receiverIndex + 1, rest, receiverIndex, rest.Length - receiverIndex);
            return bound.Invoke(rest, span, context);
        }

        // Listing delegates to the same helpers as the dir builtin so
        // explicit calls always agree with dir() by construction.
        private static object DirImpl(object receiver, LythonSourceSpan span, ExecutionContext context)
        {
            var names = EnumerateDirNames(receiver);
            if (names is null)
            {
                throw new LythonRuntimeException("TypeError", "dir(object) is not supported for this object.", span);
            }

            return CreateUnsortedNameList(names, context, span);
        }
    }

}
