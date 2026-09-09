using System.Globalization;
using System.Numerics;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal enum DecimalRoundingMode
{
    Ceiling,
    Floor,
    HalfUp,
    HalfDown,
    HalfEven,
    Down,
    Up,
    ZeroFiveUp,
}

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

    public PyDecimalContext(int precision, DecimalRoundingMode rounding, int emin, int emax, int capitals, int clamp) : this(precision, rounding, emin, emax, capitals, clamp, null, null) { }

    public PyDecimalContext(int precision, DecimalRoundingMode rounding, int emin, int emax, int capitals, int clamp, PyDict? flags) : this(precision, rounding, emin, emax, capitals, clamp, flags, null) { }

    public PyDecimalContext(
        int precision,
        DecimalRoundingMode rounding,
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

    public DecimalRoundingMode Rounding { get; private set; }

    public int Emin { get; private set; }

    public int Emax { get; private set; }

    public int Capitals { get; private set; }

    public int Clamp { get; private set; }

    public static PyDecimalContext Default()
        => new(28, DecimalRoundingMode.HalfEven, -999999, 999999, 1, 0);

    public static PyDecimalContext Basic()
        => new(9, DecimalRoundingMode.HalfUp, -999999, 999999, 1, 0);

    public static PyDecimalContext Extended()
        => new(9, DecimalRoundingMode.HalfEven, -999999, 999999, 1, 0);

    public PyDecimalContext Copy()
        => new(Precision, Rounding, Emin, Emax, Capitals, Clamp, _flags, _traps);

    public bool IsTruthy() => true;

    public PyString RenderPython(PyRenderingContext context)
    {
        return PyString.FromString(
            $"Context(prec={Precision}, rounding='{RoundingName(Rounding)}', Emin={Emin}, Emax={Emax}, capitals={Capitals}, clamp={Clamp})", context.Context.MemoryGovernor);
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        value = name switch
        {
            "prec" => new BigInteger(Precision),
            "rounding" => PyString.FromString(RoundingName(Rounding)),
            "Emin" => new BigInteger(Emin),
            "Emax" => new BigInteger(Emax),
            "capitals" => new BigInteger(Capitals),
            "clamp" => new BigInteger(Clamp),
            "flags" => _flags,
            "traps" => _traps,
            "copy" => new PyDecimalBoundCallable((arguments, span, context) =>
            {
                if (arguments.Length != 0)
                {
                    throw new LythonRuntimeException("TypeError", "Context.copy() expects no arguments.", span);
                }

                return LythonRuntime.OwnDecimalValue(Copy(), context, span);
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
            "create_decimal" => new PyDecimalBoundCallable((arguments, span, context) =>
            {
                if (arguments.Length is < 1 or > 2)
                {
                    throw new LythonRuntimeException("TypeError", "Context.create_decimal(value) expects one value argument.", span);
                }

                if (arguments[0] is PyDecimal)
                {
                    return arguments[0];
                }

                return LythonRuntime.OwnDecimalValue(PyDecimalOps.Parse(arguments[0], span), context, span);
            }, "Context.create_decimal", ["value"]),
            "create_decimal_from_float" => new PyDecimalBoundCallable((arguments, span, context) =>
            {
                if (arguments.Length != 1 || arguments[0] is not double floating || !double.IsFinite(floating))
                {
                    throw new LythonRuntimeException("TypeError", "Context.create_decimal_from_float(f) expects one finite float.", span);
                }

                return LythonRuntime.OwnDecimalValue(new PyDecimal((decimal)floating), context, span);
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

    public static DecimalRoundingMode ExpectRounding(object value)
    {
        if (!PyStringOps.TryAsString(value, out var text))
        {
            throw new LythonRuntimeException("TypeError", "Decimal rounding mode must be a rounding constant.", null);
        }

        return ParseRoundingName(text.AsString())
            ?? throw new LythonRuntimeException("ValueError", "Unsupported decimal rounding mode.", null);
    }

    public static DecimalRoundingMode? ParseRoundingName(string mode)
        => mode switch
        {
            RoundCeiling => DecimalRoundingMode.Ceiling,
            RoundFloor => DecimalRoundingMode.Floor,
            RoundHalfUp => DecimalRoundingMode.HalfUp,
            RoundHalfDown => DecimalRoundingMode.HalfDown,
            RoundHalfEven => DecimalRoundingMode.HalfEven,
            RoundDown => DecimalRoundingMode.Down,
            RoundUp => DecimalRoundingMode.Up,
            Round05Up => DecimalRoundingMode.ZeroFiveUp,
            _ => null,
        };

    public static string RoundingName(DecimalRoundingMode mode)
        => mode switch
        {
            DecimalRoundingMode.Ceiling => RoundCeiling,
            DecimalRoundingMode.Floor => RoundFloor,
            DecimalRoundingMode.HalfUp => RoundHalfUp,
            DecimalRoundingMode.HalfDown => RoundHalfDown,
            DecimalRoundingMode.HalfEven => RoundHalfEven,
            DecimalRoundingMode.Down => RoundDown,
            DecimalRoundingMode.Up => RoundUp,
            DecimalRoundingMode.ZeroFiveUp => Round05Up,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown decimal rounding mode."),
        };

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
    private readonly Func<object[], LythonSourceSpan, object>? _implementation;
    private readonly Func<object[], LythonSourceSpan, LythonRuntime.ExecutionContext, object>? _contextImplementation;
    private readonly LythonCallableSignature _signature;

    public PyDecimalBoundCallable(Func<object[], LythonSourceSpan, object> implementation, string name)
        : this(implementation, LythonCallableSignature.Create(name))
    {
    }

    public PyDecimalBoundCallable(Func<object[], LythonSourceSpan, object> implementation, string name, string[] parameterNames)
        : this(implementation, LythonCallableSignature.Create(name, parameterNames))
    {
    }

    public PyDecimalBoundCallable(
        Func<object[], LythonSourceSpan, object> implementation,
        string name,
        string[] parameterNames,
        int requiredCount)
        : this(implementation, LythonCallableSignature.Create(name, parameterNames, requiredCount))
    {
    }

    private PyDecimalBoundCallable(
        Func<object[], LythonSourceSpan, object> implementation,
        LythonCallableSignature signature)
    {
        _implementation = implementation;
        _signature = signature;
    }

    public PyDecimalBoundCallable(Func<object[], LythonSourceSpan, LythonRuntime.ExecutionContext, object> implementation, string name, string[] parameterNames)
        : this(implementation, LythonCallableSignature.Create(name, parameterNames))
    {
    }

    private PyDecimalBoundCallable(
        Func<object[], LythonSourceSpan, LythonRuntime.ExecutionContext, object> implementation,
        LythonCallableSignature signature)
    {
        _contextImplementation = implementation;
        _signature = signature;
    }

    public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecutionBudget(span);
        var positional = CallBinder.BindNamedArguments(arguments, span, _signature, PythonCallableKind.Method);
        return _contextImplementation is not null
            ? _contextImplementation(positional, span, context)
            : _implementation!(positional, span);
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
                $"DecimalTuple(sign={Sign}, digits={PyRendering.ToPythonString(Digits, context)}, exponent={Exponent})"),
            context.Context.MemoryGovernor);

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
}
