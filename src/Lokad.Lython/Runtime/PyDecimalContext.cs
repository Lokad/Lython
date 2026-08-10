using System.Globalization;
using System.Numerics;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyDecimalContext : IPyMutableDynamicAttributes, IPyRenderableValue, IPyTruthyValue
{
    public const string RoundCeiling = "ROUND_CEILING";
    public const string RoundFloor = "ROUND_FLOOR";
    public const string RoundHalfUp = "ROUND_HALF_UP";
    public const string RoundHalfDown = "ROUND_HALF_DOWN";
    public const string RoundHalfEven = "ROUND_HALF_EVEN";
    public const string RoundDown = "ROUND_DOWN";
    public const string RoundUp = "ROUND_UP";
    public const string Round05Up = "ROUND_05UP";

    private PyDict _flags;
    private PyDict _traps;

    public PyDecimalContext(int precision, string rounding, int emin, int emax, int capitals, int clamp) : this(precision, rounding, emin, emax, capitals, clamp, null, null) { }

    public PyDecimalContext(int precision, string rounding, int emin, int emax, int capitals, int clamp, PyDict? flags) : this(precision, rounding, emin, emax, capitals, clamp, flags, null) { }

    public PyDecimalContext(
        int precision,
        string rounding,
        int emin,
        int emax,
        int capitals,
        int clamp,
        PyDict? flags,
        PyDict? traps)
    {
        Precision = precision;
        Rounding = rounding;
        Emin = emin;
        Emax = emax;
        Capitals = capitals;
        Clamp = clamp;
        _flags = flags is null ? new PyDict() : new PyDict(flags);
        _traps = traps is null ? new PyDict() : new PyDict(traps);
    }

    public int Precision { get; private set; }

    public string Rounding { get; private set; }

    public int Emin { get; private set; }

    public int Emax { get; private set; }

    public int Capitals { get; private set; }

    public int Clamp { get; private set; }

    public static PyDecimalContext Default()
        => new(28, RoundHalfEven, -999999, 999999, 1, 0);

    public static PyDecimalContext Basic()
        => new(9, RoundHalfUp, -999999, 999999, 1, 0);

    public static PyDecimalContext Extended()
        => new(9, RoundHalfEven, -999999, 999999, 1, 0);

    public PyDecimalContext Copy()
        => new(Precision, Rounding, Emin, Emax, Capitals, Clamp, _flags, _traps);

    public bool IsTruthy() => true;

    public PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        return PyString.FromString(
            $"Context(prec={Precision}, rounding='{Rounding}', Emin={Emin}, Emax={Emax}, capitals={Capitals}, clamp={Clamp})");
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        value = name switch
        {
            "prec" => new BigInteger(Precision),
            "rounding" => PyString.FromString(Rounding),
            "Emin" => new BigInteger(Emin),
            "Emax" => new BigInteger(Emax),
            "capitals" => new BigInteger(Capitals),
            "clamp" => new BigInteger(Clamp),
            "flags" => _flags,
            "traps" => _traps,
            "copy" => new PyDecimalBoundCallable((arguments, span) =>
            {
                if (arguments.Length != 0)
                {
                    throw new LythonRuntimeException("TypeError", "Context.copy() expects no arguments.", span);
                }

                return Copy();
            }, "Context.copy", []),
            "clear_flags" => new PyDecimalBoundCallable((arguments, span) =>
            {
                if (arguments.Length != 0)
                {
                    throw new LythonRuntimeException("TypeError", "Context.clear_flags() expects no arguments.", span);
                }

                _flags = new PyDict();
                return PyNone.Instance;
            }, "Context.clear_flags", []),
            "create_decimal" => new PyDecimalBoundCallable((arguments, span) =>
            {
                if (arguments.Length is < 1 or > 2)
                {
                    throw new LythonRuntimeException("TypeError", "Context.create_decimal(value) expects one value argument.", span);
                }

                return PyDecimalOps.Parse(arguments[0], span);
            }, "Context.create_decimal", ["value"]),
            "create_decimal_from_float" => new PyDecimalBoundCallable((arguments, span) =>
            {
                if (arguments.Length != 1 || arguments[0] is not double floating || !double.IsFinite(floating))
                {
                    throw new LythonRuntimeException("TypeError", "Context.create_decimal_from_float(f) expects one finite float.", span);
                }

                return new PyDecimal((decimal)floating);
            }, "Context.create_decimal_from_float", ["f"]),
            _ => MissingMemberValue.Instance,
        };

        return !ReferenceEquals(value, MissingMemberValue.Instance);
    }

    public bool TrySetMember(string name, object value)
    {
        switch (name)
        {
            case "prec":
                Precision = ExpectIntInRange(value, 1, 28, "Context.prec must be an integer from 1 to 28.");
                return true;
            case "rounding":
                Rounding = ExpectRounding(value);
                return true;
            case "Emin":
                Emin = ExpectIntInRange(value, -999999, 0, "Context.Emin must be an integer from -999999 to 0.");
                return true;
            case "Emax":
                Emax = ExpectIntInRange(value, 0, 999999, "Context.Emax must be an integer from 0 to 999999.");
                return true;
            case "capitals":
                Capitals = ExpectIntInRange(value, 0, 1, "Context.capitals must be 0 or 1.");
                return true;
            case "clamp":
                Clamp = ExpectIntInRange(value, 0, 1, "Context.clamp must be 0 or 1.");
                return true;
            case "flags":
                _flags = value as PyDict ?? throw new LythonRuntimeException("TypeError", "Context.flags must be a dict.", null);
                return true;
            case "traps":
                _traps = value as PyDict ?? throw new LythonRuntimeException("TypeError", "Context.traps must be a dict.", null);
                return true;
            default:
                return false;
        }
    }

    public static string ExpectRounding(object value)
    {
        if (!PyStringOps.TryAsString(value, out var text))
        {
            throw new LythonRuntimeException("TypeError", "Decimal rounding mode must be a rounding constant.", null);
        }

        var mode = text.AsString();
        return IsSupportedRounding(mode)
            ? mode
            : throw new LythonRuntimeException("ValueError", "Unsupported decimal rounding mode.", null);
    }

    public static bool IsSupportedRounding(string mode)
        => mode is RoundCeiling or
            RoundFloor or
            RoundHalfUp or
            RoundHalfDown or
            RoundHalfEven or
            RoundDown or
            RoundUp or
            Round05Up;

    private static int ExpectIntInRange(object value, int min, int max, string message)
    {
        if (!Numbers.PyNumberOps.TryAsInteger(value, out var integer) ||
            integer < min ||
            integer > max)
        {
            throw new LythonRuntimeException("ValueError", message, null);
        }

        return (int)integer;
    }
}

internal sealed class PyDecimalBoundCallable : LythonRuntime.ICallable, IPyRenderableValue
{
    private readonly Func<object[], LythonSourceSpan, object> _implementation;
    private readonly LythonCallableSignature _signature;

    public PyDecimalBoundCallable(Func<object[], LythonSourceSpan, object> implementation, string name) : this(implementation, name, null, null) { }

    public PyDecimalBoundCallable(Func<object[], LythonSourceSpan, object> implementation, string name, string[]? parameterNames) : this(implementation, name, parameterNames, null) { }

    public PyDecimalBoundCallable(
        Func<object[], LythonSourceSpan, object> implementation,
        string name,
        string[]? parameterNames,
        int? requiredCount)
    {
        _implementation = implementation;
        _signature = new LythonCallableSignature(name, parameterNames, requiredCount);
    }

    public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecutionBudget(span);
        var positional = CallBinder.BindNamedArguments(arguments, span, _signature, PythonCallableKind.Method);
        return _implementation(positional, span);
    }

    public PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        return PyString.FromString(_signature.Name);
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
}

internal sealed class PyDecimalLocalContext : IPyContextManager
{
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly PyDecimalContext? _requested;
    private PyDecimalContext? _previous;

    public PyDecimalLocalContext(LythonRuntime.ExecutionContext context, PyDecimalContext? requested)
    {
        _context = context;
        _requested = requested;
    }

    public object Enter()
    {
        _previous = _context.DecimalContext.Copy();
        _context.SetDecimalContext((_requested ?? _context.DecimalContext).Copy());
        return _context.DecimalContext;
    }

    public bool Exit(object exceptionType, object exceptionValue, object traceback)
    {
        _ = exceptionType;
        _ = exceptionValue;
        _ = traceback;
        if (_previous is not null)
        {
            _context.SetDecimalContext(_previous);
        }

        return false;
    }
}

internal sealed class PyDecimalTuple : IPySequenceValue, IPyIndexableValue, IPyIterableValue, IPyRenderableValue, IPyDynamicAttributes, IPyHashableValue
{
    private readonly object[] _items;

    public PyDecimalTuple(int sign, PyTuple digits, BigInteger exponent)
    {
        Sign = sign;
        Digits = digits;
        Exponent = exponent;
        _items = [new BigInteger(Sign), Digits, Exponent];
    }

    public int Sign { get; }

    public PyTuple Digits { get; }

    public BigInteger Exponent { get; }

    public int Count => 3;

    public int Length => 3;

    public object this[int index] => _items[index];

    public object GetItem(int index) => _items[index];

    public object CreateSlice(IEnumerable<object> items) => new PyTuple(items);

    public object GetIndex(int index) => _items[index];

    public object GetSlice(IEnumerable<int> indices) => new PyTuple(indices.Select(index => _items[index]));

    public IEnumerable<object> Iterate() => _items;

    public IEnumerator<object> GetEnumerator() => ((IEnumerable<object>)_items).GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _items.GetEnumerator();

    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        value = name switch
        {
            "sign" => new BigInteger(Sign),
            "digits" => Digits,
            "exponent" => Exponent,
            _ => MissingMemberValue.Instance,
        };

        return !ReferenceEquals(value, MissingMemberValue.Instance);
    }
public int GetPyHashCode()
    {
        var hash = new HashCode();
        hash.Add(Sign);
        foreach (var digit in Digits)
        {
            hash.Add(PyValueComparer.Instance.GetHashCode(digit));
        }

        hash.Add(Exponent);
        return hash.ToHashCode();
    }

    public PyString RenderPython(PyRenderingContext context)
        => PyString.FromString(
            string.Create(
                CultureInfo.InvariantCulture,
                $"DecimalTuple(sign={Sign}, digits={PyRendering.ToPythonString(Digits, context)}, exponent={Exponent})"));

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
}
