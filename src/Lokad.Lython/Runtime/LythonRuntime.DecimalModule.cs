namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static readonly string[] DecimalExceptionNames =
    [
        "DecimalException",
        "InvalidOperation",
        "DivisionByZero",
        "Inexact",
        "Rounded",
        "Overflow",
        "Underflow",
        "Subnormal",
        "Clamped",
        "FloatOperation",
        "DivisionUndefined",
    ];

    internal sealed class DecimalModule : PyModule
    {
        private readonly PyDecimalContext _defaultContext;
        private readonly PyDecimalContext _basicContext;
        private readonly PyDecimalContext _extendedContext;

        public DecimalModule(ExecutionContext context) : base("decimal")
        {
            _defaultContext = PyDecimalContext.Default(context.MemoryGovernor);
            _basicContext = PyDecimalContext.Basic(context.MemoryGovernor);
            _extendedContext = PyDecimalContext.Extended(context.MemoryGovernor);
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "Decimal" => BuiltinCallable.Create(LythonKnownCallableSignatures.Decimal, DecimalCtor),
                "DecimalTuple" => BuiltinCallable.Create(LythonKnownCallableSignatures.DecimalTuple, DecimalTupleCtor, DecimalTupleCtorAsync),
                "Context" => BuiltinCallable.Create(LythonKnownCallableSignatures.DecimalContext, DecimalContextCtor),
                "getcontext" => BuiltinCallable.Create(LythonKnownCallableSignatures.DecimalGetContext, DecimalGetContext),
                "setcontext" => BuiltinCallable.Create(LythonKnownCallableSignatures.DecimalSetContext, DecimalSetContext),
                "localcontext" => BuiltinCallable.Create(LythonKnownCallableSignatures.DecimalLocalContext, DecimalLocalContext),
                "DefaultContext" => _defaultContext,
                "BasicContext" => _basicContext,
                "ExtendedContext" => _extendedContext,
                "ROUND_CEILING" => Runtime.Text.PyString.FromString(PyDecimalContext.RoundCeiling),
                "ROUND_FLOOR" => Runtime.Text.PyString.FromString(PyDecimalContext.RoundFloor),
                "ROUND_HALF_UP" => Runtime.Text.PyString.FromString(PyDecimalContext.RoundHalfUp),
                "ROUND_HALF_DOWN" => Runtime.Text.PyString.FromString(PyDecimalContext.RoundHalfDown),
                "ROUND_HALF_EVEN" => Runtime.Text.PyString.FromString(PyDecimalContext.RoundHalfEven),
                "ROUND_DOWN" => Runtime.Text.PyString.FromString(PyDecimalContext.RoundDown),
                "ROUND_UP" => Runtime.Text.PyString.FromString(PyDecimalContext.RoundUp),
                "ROUND_05UP" => Runtime.Text.PyString.FromString(PyDecimalContext.Round05Up),
                _ when DecimalExceptionNames.Contains(name, StringComparer.Ordinal) => new ExceptionTypeValue(ModuleException("decimal", name)),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private object DecimalContextCtor(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length > 8)
            {
                throw new LythonRuntimeException("TypeError", "decimal.Context(...) expects supported context options.", span);
            }

            var template = _defaultContext;
            var precision = arguments.Length >= 1 && arguments[0] is not PyNone
                ? ExpectDecimalContextInt(arguments[0], 1, 28, "decimal.Context(..., prec=...) must be from 1 to 28.", span)
                : template.Precision;
            var rounding = arguments.Length >= 2 && arguments[1] is not PyNone
                ? PyDecimalContext.ExpectRounding(arguments[1])
                : template.Rounding;
            var emin = arguments.Length >= 3 && arguments[2] is not PyNone
                ? ExpectDecimalContextInt(arguments[2], -999999, 0, "decimal.Context(..., Emin=...) must be from -999999 to 0.", span)
                : template.Emin;
            var emax = arguments.Length >= 4 && arguments[3] is not PyNone
                ? ExpectDecimalContextInt(arguments[3], 0, 999999, "decimal.Context(..., Emax=...) must be from 0 to 999999.", span)
                : template.Emax;
            var capitals = arguments.Length >= 5 && arguments[4] is not PyNone
                ? ExpectDecimalContextInt(arguments[4], 0, 1, "decimal.Context(..., capitals=...) must be 0 or 1.", span)
                : template.Capitals;
            var clamp = arguments.Length >= 6 && arguments[5] is not PyNone
                ? ExpectDecimalContextInt(arguments[5], 0, 1, "decimal.Context(..., clamp=...) must be 0 or 1.", span)
                : template.Clamp;
            var flags = arguments.Length >= 7 && arguments[6] is not PyNone
                ? ExpectPyDict(arguments[6], "decimal.Context(..., flags=...) expects a dict or None.", span)
                : template.Flags;
            var traps = arguments.Length >= 8 && arguments[7] is not PyNone
                ? ExpectPyDict(arguments[7], "decimal.Context(..., traps=...) expects a dict or None.", span)
                : template.Traps;

            return OwnDecimalValue(new PyDecimalContext(precision, rounding, emin, emax, capitals, clamp, flags, traps, context.MemoryGovernor, span), context, span);
        }

        private object DecimalSetContext(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1 || arguments[0] is not PyDecimalContext decimalContext)
            {
                throw new LythonRuntimeException("TypeError", "decimal.setcontext(context) expects one Context argument.", span);
            }

            if (ReferenceEquals(decimalContext, _defaultContext) || ReferenceEquals(decimalContext, _basicContext) || ReferenceEquals(decimalContext, _extendedContext))
            {
                decimalContext = (PyDecimalContext)OwnDecimalValue(decimalContext.Copy(context.MemoryGovernor, span), context, span);
                decimalContext.ClearFlags(context, span);
            }
            context.SetDecimalContext(decimalContext);
            return PyNone.Instance;
        }
    }

    // Charge the fixed-size scalar/shell estimate for fresh decimal-family values.
    // Digit backing and substantial parser scratch have their own admission;
    // aliased inputs retain their existing funding.
    private const long DecimalValueBytes = 64;

    internal static object OwnDecimalValue(object value, ExecutionContext context, LythonSourceSpan? span)
    {
        context.MemoryGovernor.Reserve(DecimalValueBytes, span);
        context.MemoryGovernor.Commit(DecimalValueBytes);
        return value;
    }

    // Counter-derived decimal counts are always fresh values; charge them against
    // the owning counter's governor when present. Ungoverned counters stay free.
    internal static object OwnDecimalValue(object value, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        if (governor is null)
        {
            return value;
        }

        governor.Reserve(DecimalValueBytes, span);
        governor.Commit(DecimalValueBytes);
        return value;
    }

    // Decimal method results reuse the input for aliasing winners (min/max,
    // single-digit rotate); charge only fresh values.
    internal static object OwnFreshDecimal(object result, PyDecimal input, ExecutionContext context, LythonSourceSpan span)
        => ReferenceEquals(result, input) ? result : OwnDecimalValue(result, context, span);

    private static object DecimalCtor(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length > 2)
        {
            throw new LythonRuntimeException("TypeError", "decimal.Decimal([value][, context]) expects zero to two arguments.", span);
        }

        if (arguments.Length == 2)
        {
            _ = ExpectDecimalContextOrNone(arguments[1], "decimal.Decimal(..., context=...) expects a Context or None.", span);
        }

        if (arguments.Length == 0 || arguments[0] is PyNone)
        {
            return OwnDecimalValue(new PyDecimal(0m), context, span);
        }

        if (arguments[0] is PyDecimal)
        {
            return arguments[0];
        }

        return OwnDecimalValue(PyDecimalOps.Parse(arguments[0], span, context), context, span);
    }

    private static object DecimalTupleCtor(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => DecimalTupleCtorCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();

    private static ValueTask<object> DecimalTupleCtorAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => DecimalTupleCtorCoreAsync(arguments, span, context, true);

    private static async ValueTask<object> DecimalTupleCtorCoreAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        if (arguments.Length != 3)
        {
            throw new LythonRuntimeException("TypeError", "decimal.DecimalTuple(sign, digits, exponent) expects three arguments.", span);
        }

        return OwnDecimalValue(await PyDecimalOps.CreateTupleAsync(arguments[0], arguments[1], arguments[2], span, context, asynchronous).ConfigureAwait(false), context, span);
    }

    private static object DecimalGetContext(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 0)
        {
            throw new LythonRuntimeException("TypeError", "decimal.getcontext() expects no arguments.", span);
        }

        return context.DecimalContext;
    }

    private static object DecimalLocalContext(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length > 1)
        {
            throw new LythonRuntimeException("TypeError", "decimal.localcontext([context]) expects zero or one argument.", span);
        }

        var requested = arguments.Length == 0 ? null : ExpectDecimalContextOrNone(arguments[0], "decimal.localcontext([context]) expects a Context or None.", span);
        return OwnDecimalValue(new PyDecimalLocalContext(context, requested, span), context, span);
    }

    private static PyDecimalContext? ExpectDecimalContextOrNone(object value, string message, LythonSourceSpan span)
        => value is PyNone
            ? null
            : value as PyDecimalContext ?? throw new LythonRuntimeException("TypeError", message, span);

    private static PyDict ExpectPyDict(object value, string message, LythonSourceSpan span)
        => value as PyDict ?? throw new LythonRuntimeException("TypeError", message, span);

    private static int ExpectDecimalContextInt(object value, int min, int max, string message, LythonSourceSpan span)
    {
        if (!Numbers.PyNumberOps.TryAsInteger(value, out var integer) ||
            integer < min ||
            integer > max)
        {
            throw new LythonRuntimeException("ValueError", message, span);
        }

        return (int)integer;
    }
}
