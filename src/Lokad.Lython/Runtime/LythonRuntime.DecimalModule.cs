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
    ];

    private sealed class DecimalModule : PyModule
    {
        public static readonly DecimalModule Instance = new();

        private DecimalModule() : base("decimal")
        {
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "Decimal" => BuiltinCallable.Create(LythonKnownCallableSignatures.Decimal, DecimalCtor),
                "DecimalTuple" => BuiltinCallable.Create(LythonKnownCallableSignatures.DecimalTuple, DecimalTupleCtor),
                "Context" => BuiltinCallable.Create(LythonKnownCallableSignatures.DecimalContext, DecimalContextCtor),
                "getcontext" => BuiltinCallable.Create(LythonKnownCallableSignatures.DecimalGetContext, DecimalGetContext),
                "setcontext" => BuiltinCallable.Create(LythonKnownCallableSignatures.DecimalSetContext, DecimalSetContext),
                "localcontext" => BuiltinCallable.Create(LythonKnownCallableSignatures.DecimalLocalContext, DecimalLocalContext),
                "DefaultContext" => PyDecimalContext.Default(),
                "BasicContext" => PyDecimalContext.Basic(),
                "ExtendedContext" => PyDecimalContext.Extended(),
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
    }

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

        return arguments.Length == 0 || arguments[0] is PyNone
            ? new PyDecimal(0m)
            : PyDecimalOps.Parse(arguments[0], span);
    }

    private static object DecimalTupleCtor(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 3)
        {
            throw new LythonRuntimeException("TypeError", "decimal.DecimalTuple(sign, digits, exponent) expects three arguments.", span);
        }

        return PyDecimalOps.CreateTuple(arguments[0], arguments[1], arguments[2], span);
    }

    private static object DecimalContextCtor(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length > 8)
        {
            throw new LythonRuntimeException("TypeError", "decimal.Context(...) expects supported context options.", span);
        }

        var template = PyDecimalContext.Default();
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
            : null;
        var traps = arguments.Length >= 8 && arguments[7] is not PyNone
            ? ExpectPyDict(arguments[7], "decimal.Context(..., traps=...) expects a dict or None.", span)
            : null;

        return new PyDecimalContext(precision, rounding, emin, emax, capitals, clamp, flags, traps);
    }

    private static object DecimalGetContext(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 0)
        {
            throw new LythonRuntimeException("TypeError", "decimal.getcontext() expects no arguments.", span);
        }

        return context.DecimalContext;
    }

    private static object DecimalSetContext(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1 || arguments[0] is not PyDecimalContext decimalContext)
        {
            throw new LythonRuntimeException("TypeError", "decimal.setcontext(context) expects one Context argument.", span);
        }

        context.SetDecimalContext(decimalContext.Copy());
        return PyNone.Instance;
    }

    private static object DecimalLocalContext(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length > 1)
        {
            throw new LythonRuntimeException("TypeError", "decimal.localcontext([context]) expects zero or one argument.", span);
        }

        var requested = arguments.Length == 0 ? null : ExpectDecimalContextOrNone(arguments[0], "decimal.localcontext([context]) expects a Context or None.", span);
        return new PyDecimalLocalContext(context, requested);
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
