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

    private readonly PyDict _flags;
    private readonly PyDict _traps;

    public PyDecimalContext(
        int precision,
        DecimalRoundingMode rounding,
        PyDict? flags,
        PyDict? traps,
        MemoryGovernor? governor = null,
        LythonSourceSpan? span = null)
    {
        Precision = precision;
        Rounding = rounding;
        _flags = flags is null ? CreateSignals(false, governor, span) : CopySignals(flags, governor, span);
        _traps = traps is null ? CreateSignals(true, governor, span) : CopySignals(traps, governor, span);
    }

    // N17: hot fixed signatures hoisted per family (see ListMembers).
    private static readonly LythonCallableSignature ContextCopySignature = LythonCallableSignature.Create("Context.copy", []);
    private static readonly LythonCallableSignature ContextClearFlagsSignature = LythonCallableSignature.Create("Context.clear_flags", []);
    private static readonly LythonCallableSignature ContextCreateDecimalSignature = LythonCallableSignature.Create("Context.create_decimal", ["value"]);
    private static readonly LythonCallableSignature ContextCreateDecimalFromFloatSignature = LythonCallableSignature.Create("Context.create_decimal_from_float", ["f"]);

    public int Precision { get; private set; }

    public DecimalRoundingMode Rounding { get; private set; }

    public int Emin => -999999;

    public int Emax => 999999;

    public int Capitals => 1;

    public int Clamp => 0;

    internal PyDict Flags => _flags;

    internal PyDict Traps => _traps;

    public static PyDecimalContext Default(MemoryGovernor? governor = null)
        => new(28, DecimalRoundingMode.HalfEven, null, null, governor);

    public static PyDecimalContext Basic(MemoryGovernor? governor = null)
    {
        var result = new PyDecimalContext(9, DecimalRoundingMode.HalfUp, null, null, governor);
        foreach (var signal in Signals)
            if (signal.TypeName is "Underflow" or "Clamped") result._traps.SetItem(signal, true);
        return result;
    }

    public static PyDecimalContext Extended(MemoryGovernor? governor = null)
    {
        var result = new PyDecimalContext(9, DecimalRoundingMode.HalfEven, null, null, governor);
        foreach (var signal in Signals) result._traps.SetItem(signal, false);
        return result;
    }

    public PyDecimalContext Copy(MemoryGovernor governor, LythonSourceSpan span)
        => new(Precision, Rounding, _flags, _traps, governor, span);

    private static readonly LythonRuntime.ExceptionTypeValue InexactSignal = new(LythonRuntime.ModuleException("decimal", "Inexact"));
    private static readonly LythonRuntime.ExceptionTypeValue RoundedSignal = new(LythonRuntime.ModuleException("decimal", "Rounded"));
    private static readonly LythonRuntime.ExceptionTypeValue FloatOperationSignal = new(LythonRuntime.ModuleException("decimal", "FloatOperation"));
    private static readonly LythonRuntime.ExceptionTypeValue InvalidOperationSignal = new(LythonRuntime.ModuleException("decimal", "InvalidOperation"));
    private static readonly LythonRuntime.ExceptionTypeValue ConversionSyntaxType = new(LythonRuntime.ModuleException("decimal", "ConversionSyntax"));
    private static readonly LythonRuntime.ExceptionTypeValue DivisionImpossibleType = new(LythonRuntime.ModuleException("decimal", "DivisionImpossible"));
    private static readonly LythonRuntime.ExceptionTypeValue DivisionByZeroSignal = new(LythonRuntime.ModuleException("decimal", "DivisionByZero"));

    private static readonly LythonRuntime.ExceptionTypeValue[] Signals =
    [
        InvalidOperationSignal,
        FloatOperationSignal,
        DivisionByZeroSignal,
        new(LythonRuntime.ModuleException("decimal", "Overflow")),
        new(LythonRuntime.ModuleException("decimal", "Underflow")),
        new(LythonRuntime.ModuleException("decimal", "Subnormal")),
        InexactSignal, RoundedSignal,
        new(LythonRuntime.ModuleException("decimal", "Clamped")),
    ];

    private static PyDict CreateSignals(bool traps, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        var result = governor is null ? new PyDict() : new PyDict(governor, span);
        foreach (var signal in Signals)
            result.SetItem(signal, traps && (signal.TypeName is "InvalidOperation" or "DivisionByZero" or "Overflow"));
        return result;
    }

    private static PyDict CopySignals(PyDict source, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        ValidateSignals(source, span);
        return new PyDict(source, governor ?? source.OwnerMemoryGovernor, span);
    }

    private static void ValidateSignals(PyDict mapping, LythonSourceSpan? span)
    {
        if (mapping.Count != Signals.Length)
            throw new LythonRuntimeException("KeyError", "invalid signal dict", span);
        foreach (var pair in mapping)
        {
            if (pair.Key is not LythonRuntime.ExceptionTypeValue signal || signal.ExceptionIdentity.ModuleName != "decimal" ||
                signal.TypeName is not ("InvalidOperation" or "FloatOperation" or "DivisionByZero" or "Overflow" or "Underflow" or "Subnormal" or "Inexact" or "Rounded" or "Clamped"))
                throw new LythonRuntimeException("KeyError", "invalid signal dict", span);
            if (pair.Value is not bool)
                throw new LythonRuntimeException("TypeError", "Decimal signals must be booleans.", span);
        }
    }

    internal void ValidateSignalMaps(LythonSourceSpan span)
    {
        ValidateSignals(_flags, span);
        ValidateSignals(_traps, span);
    }

    internal void RecordFloatConversion(LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        ValidateSignalMaps(span);
        LythonRuntimeException? error = null;
        if (_traps.GetItem(FloatOperationSignal) is true)
            error = new LythonRuntimeException(FloatOperationSignal.ExceptionIdentity, "[<class 'decimal.FloatOperation'>]", span, null,
                new PyList(new object[] { FloatOperationSignal }, context.MemoryGovernor, span));
        _flags.SetItem(FloatOperationSignal, true);
        if (error is not null) throw error;
    }

    internal LythonRuntimeException ConversionSyntaxError(LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        => InvalidOperationError(ConversionSyntaxType, "[<class 'decimal.ConversionSyntax'>]", context, span);

    internal LythonRuntimeException DivisionImpossibleError(LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        => InvalidOperationError(DivisionImpossibleType, "[<class 'decimal.DivisionImpossible'>]", context, span);

    private LythonRuntimeException InvalidOperationError(LythonRuntime.ExceptionTypeValue detail, string message,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        ValidateSignalMaps(span);
        LythonRuntimeException error = _traps.GetItem(InvalidOperationSignal) is true
            ? new LythonRuntimeException(InvalidOperationSignal.ExceptionIdentity, message, span, null,
                new PyList(new object[] { detail }, context.MemoryGovernor, span))
            : new LythonRuntimeException("NotImplementedError", "Untrapped invalid Decimal operation requires an unsupported nonfinite result.", span);
        _flags.SetItem(InvalidOperationSignal, true);
        return error;
    }

    internal LythonRuntimeException SignalError(string name, LythonRuntimeException trapped, LythonSourceSpan span)
    {
        ValidateSignalMaps(span);
        foreach (var signal in Signals)
        {
            if (signal.TypeName != name) continue;
            var error = _traps.GetItem(signal) is true ? trapped
                : new LythonRuntimeException("NotImplementedError", "Untrapped Decimal operation requires an unsupported nonfinite result.", span);
            _flags.SetItem(signal, true);
            return error;
        }
        throw new InvalidOperationException("Unknown Decimal signal.");
    }

    internal LythonRuntimeException DivmodZeroError(LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        ValidateSignalMaps(span);
        var invalid = _traps.GetItem(InvalidOperationSignal) is true;
        var divide = _traps.GetItem(DivisionByZeroSignal) is true;
        LythonRuntimeException error = invalid
            ? divide ? PyDecimalOps.DivmodByZero(span) : PyDecimalOps.InvalidOperationSignal(span)
            : divide ? new LythonRuntimeException(DivisionByZeroSignal.ExceptionIdentity, "[<class 'decimal.DivisionByZero'>]", span, null,
                new PyList(new object[] { DivisionByZeroSignal }, context.MemoryGovernor, span))
            : new LythonRuntimeException("NotImplementedError", "Untrapped Decimal divmod requires unsupported nonfinite results.", span);
        _flags.SetItem(InvalidOperationSignal, true);
        _flags.SetItem(DivisionByZeroSignal, true);
        return error;
    }

    private static void AssignSignals(PyDict destination, object value)
    {
        var source = value as PyDict ?? throw new LythonRuntimeException("TypeError", "Decimal signals must be a dict.", null);
        ValidateSignals(source, null);
        foreach (var pair in source) destination.SetItem(pair.Key, pair.Value);
    }

    internal void RecordRounding(bool inexact, bool rounded, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (!rounded) return;
        var trapInexact = inexact && IsTrapped(InexactSignal);
        var trapRounded = IsTrapped(RoundedSignal);
        LythonRuntimeException? error = null;
        if (trapInexact || trapRounded)
        {
            var signals = new PyList(trapInexact && trapRounded ? [InexactSignal, RoundedSignal]
                : new object[] { trapInexact ? InexactSignal : RoundedSignal }, context.MemoryGovernor, span);
            var signal = trapInexact ? InexactSignal : RoundedSignal;
            var message = trapInexact ? trapRounded ? "[<class 'decimal.Inexact'>, <class 'decimal.Rounded'>]" : "[<class 'decimal.Inexact'>]"
                : "[<class 'decimal.Rounded'>]";
            error = new LythonRuntimeException(signal.ExceptionIdentity, message, span, null, signals);
        }
        _flags.SetItem(RoundedSignal, true);
        if (inexact) _flags.SetItem(InexactSignal, true);
        if (error is not null) throw error;

        bool IsTrapped(LythonRuntime.ExceptionTypeValue key)
        {
            if (!_traps.TryGetValue(key, out var value)) return false;
            return value is bool enabled ? enabled : throw new LythonRuntimeException("TypeError", "Decimal traps must be booleans.", span);
        }
    }

    internal void ClearFlags(LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        ValidateSignals(_flags, span);
        foreach (var pair in _flags)
        {
            context.CheckExecution(span);
            _flags.SetItem(pair.Key, false);
        }
    }

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

                return LythonRuntime.OwnDecimalValue(Copy(context.MemoryGovernor, span), context, span);
            }, ContextCopySignature),
            "clear_flags" => new PyDecimalBoundCallable((arguments, span, context) =>
            {
                if (arguments.Length != 0)
                {
                    throw new LythonRuntimeException("TypeError", "Context.clear_flags() expects no arguments.", span);
                }

                ClearFlags(context, span);
                return PyNone.Instance;
            }, ContextClearFlagsSignature),
            "create_decimal" => new PyDecimalBoundCallable((arguments, span, context) =>
            {
                if (arguments.Length != 1)
                {
                    throw new LythonRuntimeException("TypeError", "Context.create_decimal(value) expects one value argument.", span);
                }

                return LythonRuntime.OwnDecimalValue(PyDecimalOps.Parse(arguments[0], span, context, this), context, span);
            }, ContextCreateDecimalSignature),
            "create_decimal_from_float" => new PyDecimalBoundCallable((arguments, span, context) =>
            {
                if (arguments.Length != 1)
                {
                    throw new LythonRuntimeException("TypeError", "Context.create_decimal_from_float(f) expects one finite float or integer.", span);
                }
                if (arguments[0] is double floating && double.IsFinite(floating))
                    return LythonRuntime.OwnDecimalValue(PyDecimalOps.ApplyFloatContext(floating, this, context, span), context, span);
                if (Numbers.PyNumberOps.TryAsInteger(arguments[0], out _))
                    return LythonRuntime.OwnDecimalValue(PyDecimalOps.Parse(arguments[0], span, context, this), context, span);
                throw new LythonRuntimeException("TypeError", "Context.create_decimal_from_float(f) expects one finite float or integer.", span);
            }, ContextCreateDecimalFromFloatSignature),
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
                RequireDefaultSetting(ExpectIntInRange(value, -999999, 0, "Context.Emin must be an integer from -999999 to 0."), Emin, name);
                return true;
            case "Emax":
                RequireDefaultSetting(ExpectIntInRange(value, 0, 999999, "Context.Emax must be an integer from 0 to 999999."), Emax, name);
                return true;
            case "capitals":
                RequireDefaultSetting(ExpectIntInRange(value, 0, 1, "Context.capitals must be 0 or 1."), Capitals, name);
                return true;
            case "clamp":
                RequireDefaultSetting(ExpectIntInRange(value, 0, 1, "Context.clamp must be 0 or 1."), Clamp, name);
                return true;
            case "flags":
                AssignSignals(_flags, value);
                return true;
            case "traps":
                AssignSignals(_traps, value);
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

    internal static void RequireDefaultSetting(int value, int supported, string name, LythonSourceSpan? span = null)
    {
        if (value != supported)
            throw new LythonRuntimeException("NotImplementedError", $"Decimal Context.{name} settings other than {supported} are unsupported.", span);
    }

    internal static int ExpectIntInRange(object value, int min, int max, string message, LythonSourceSpan? span = null)
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

    public PyDecimalBoundCallable(
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

    public PyDecimalBoundCallable(
        Func<object[], LythonSourceSpan, LythonRuntime.ExecutionContext, object> implementation,
        LythonCallableSignature signature)
    {
        _contextImplementation = implementation;
        _signature = signature;
    }

    public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecution(span);
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
    private readonly PyDecimalContext _local;
    private readonly PyDecimalContext _previous;

    public PyDecimalLocalContext(LythonRuntime.ExecutionContext context, PyDecimalContext? requested, LythonSourceSpan span)
    {
        _context = context;
        // Python captures both contexts when creating the manager. Re-entry
        // reuses that child; native exit must restore the exact parent even
        // after cancellation or terminal admission refusal.
        _previous = context.DecimalContext;
        _local = (PyDecimalContext)LythonRuntime.OwnDecimalValue((requested ?? _previous).Copy(context.MemoryGovernor, span), context, span);
    }

    public object Enter()
    {
        _context.SetDecimalContext(_local);
        return _local;
    }

    public bool Exit(object exceptionType, object exceptionValue, object traceback)
    {
        _ = exceptionType;
        _ = exceptionValue;
        _ = traceback;
        _context.SetDecimalContext(_previous);

        return false;
    }
}

internal sealed class PyDecimalTuple : IPySequenceValue, IPyIndexableValue, IPyIterableValue, IPyRenderableValue, IPyDynamicAttributes, IPyHashableValue
{
    private readonly object[] _items;

    public PyDecimalTuple(int sign, PyTuple digits, BigInteger exponent)
    {
        _items = [new BigInteger(sign), digits, exponent];
    }

    public int Sign => (int)(BigInteger)_items[0];

    public PyTuple Digits => (PyTuple)_items[1];

    public BigInteger Exponent => (BigInteger)_items[2];

    public int Count => 3;

    public int Length => 3;

    public object this[int index] => _items[index];

    public object GetItem(int index) => _items[index];

    public object CreateSlice(IEnumerable<object> items) => new PyTuple(items);

    public object GetIndex(int index) => _items[index];

    public object GetSlice(IEnumerable<int> indices) => PyTupleLike.CreateSlice(this, indices);

    public IEnumerable<object> Iterate() => _items;

    public IEnumerator<object> GetEnumerator() => ((IEnumerable<object>)_items).GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _items.GetEnumerator();

    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        value = name switch
        {
            "sign" => _items[0],
            "digits" => Digits,
            "exponent" => _items[2],
            _ => MissingMemberValue.Instance,
        };

        return !ReferenceEquals(value, MissingMemberValue.Instance);
    }
    public int GetPyHashCode() => PyTupleLike.ComputeHashCode(this);

    public PyString RenderPython(PyRenderingContext context)
    {
        var builder = new GovernedByteBuilder(context.Context.MemoryGovernor, PyStructuralGuard.AmbientSpan);
        try
        {
            builder.AppendAscii("DecimalTuple(sign=");
            builder.Append(PyRendering.ToPythonPyString(_items[0], context));
            builder.AppendAscii(", digits=");
            builder.Append(PyRendering.ToPythonPyString(Digits, context));
            builder.AppendAscii(", exponent=");
            builder.Append(PyRendering.ToPythonPyString(_items[2], context));
            builder.Append((byte)')');
            return builder.ToPyStringAndRelease();
        }
        finally { builder.Release(); }
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
}
