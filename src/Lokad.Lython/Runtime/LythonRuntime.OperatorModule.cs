using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class OperatorModule : PyModule
    {
        public static readonly OperatorModule Instance = new();

        private OperatorModule() : base("operator")
        {
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "truth" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorTruth, (arguments, span, _) => Unary(arguments, span, static (value, _) => IsTruthy(value))),
                "not_" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorNot, (arguments, span, _) => Unary(arguments, span, static (value, _) => !IsTruthy(value))),
                "is_" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorIs, (arguments, span, _) => CompareBool(arguments, span, static (left, right, _) => AreIdentical(left, right))),
                "is_not" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorIsNot, (arguments, span, _) => CompareBool(arguments, span, static (left, right, _) => !AreIdentical(left, right))),
                "abs" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorAbs, (arguments, span, _) => Unary(arguments, span, EvaluateAbsolute)),
                "neg" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorNeg, (arguments, span, _) => Unary(arguments, span, EvaluateUnaryMinus)),
                "pos" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorPos, (arguments, span, _) => Unary(arguments, span, EvaluateUnaryPlus)),
                "invert" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorInvert, (arguments, span, _) => Unary(arguments, span, EvaluateBitwiseNot)),
                "index" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorIndex, (arguments, span, context) => Unary(arguments, span, (value, innerSpan) => EvaluateIndex(value, context, innerSpan))),
                "add" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorAdd, (arguments, span, context) => Binary(arguments, span, (left, right, innerSpan) => EvaluateAdd(left, right, context, innerSpan))),
                "sub" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorSub, (arguments, span, _) => Binary(arguments, span, EvaluateSubtract)),
                "mul" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorMul, (arguments, span, context) => Binary(arguments, span, (left, right, innerSpan) => EvaluateMultiply(left, right, context, innerSpan))),
                "truediv" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorTrueDiv, (arguments, span, _) => Binary(arguments, span, EvaluateDivide)),
                "floordiv" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorFloorDiv, (arguments, span, _) => Binary(arguments, span, EvaluateFloorDivide)),
                "mod" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorMod, (arguments, span, context) => Binary(arguments, span, (left, right, innerSpan) => EvaluateModulo(left, right, context, innerSpan))),
                "pow" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorPow, (arguments, span, context) => Binary(arguments, span, (left, right, innerSpan) => EvaluatePower(left, right, context, innerSpan))),
                "matmul" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorMatMul, (arguments, span, _) => Binary(arguments, span, UnsupportedMatMul)),
                "lshift" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorLShift, (arguments, span, context) => Binary(arguments, span, (left, right, innerSpan) => EvaluateLeftShift(left, right, context, innerSpan))),
                "rshift" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorRShift, (arguments, span, _) => Binary(arguments, span, EvaluateRightShift)),
                "and_" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorAnd, (arguments, span, _) => Binary(arguments, span, EvaluateBitwiseAnd)),
                "or_" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorOr, (arguments, span, _) => Binary(arguments, span, EvaluateBitwiseOr)),
                "xor" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorXor, (arguments, span, _) => Binary(arguments, span, EvaluateBitwiseXor)),
                "concat" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorConcat, (arguments, span, context) => Binary(arguments, span, (left, right, innerSpan) => EvaluateConcat(left, right, context, innerSpan))),
                "eq" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorEq, (arguments, span, _) => CompareBool(arguments, span, static (left, right, _) => AreEqual(left, right))),
                "ne" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorNe, (arguments, span, _) => CompareBool(arguments, span, static (left, right, _) => !AreEqual(left, right))),
                "lt" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorLt, (arguments, span, _) => CompareBool(arguments, span, static (left, right, innerSpan) => Compare(left, right, innerSpan) < 0)),
                "le" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorLe, (arguments, span, _) => CompareBool(arguments, span, static (left, right, innerSpan) => Compare(left, right, innerSpan) <= 0)),
                "gt" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorGt, (arguments, span, _) => CompareBool(arguments, span, static (left, right, innerSpan) => Compare(left, right, innerSpan) > 0)),
                "ge" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorGe, (arguments, span, _) => CompareBool(arguments, span, static (left, right, innerSpan) => Compare(left, right, innerSpan) >= 0)),
                "getitem" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorGetItem, GetItem),
                "setitem" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorSetItem, SetItem),
                "delitem" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorDelItem, DelItem),
                "contains" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorContains, ContainsValue),
                "length_hint" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorLengthHint, LengthHint),
                "countOf" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorCountOf, CountOf),
                "indexOf" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorIndexOf, IndexOf),
                "iadd" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorIAdd, (arguments, span, context) => InPlaceBinary(arguments, span, context, AugmentedAssignmentOperatorSyntax.Add)),
                "isub" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorISub, (arguments, span, context) => InPlaceBinary(arguments, span, context, AugmentedAssignmentOperatorSyntax.Subtract)),
                "imul" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorIMul, (arguments, span, context) => InPlaceBinary(arguments, span, context, AugmentedAssignmentOperatorSyntax.Multiply)),
                "itruediv" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorITrueDiv, (arguments, span, context) => InPlaceBinary(arguments, span, context, AugmentedAssignmentOperatorSyntax.Divide)),
                "ifloordiv" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorIFloorDiv, (arguments, span, context) => InPlaceBinary(arguments, span, context, AugmentedAssignmentOperatorSyntax.FloorDivide)),
                "imod" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorIMod, (arguments, span, context) => InPlaceBinary(arguments, span, context, AugmentedAssignmentOperatorSyntax.Modulo)),
                "ipow" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorIPow, (arguments, span, context) => InPlaceBinary(arguments, span, context, AugmentedAssignmentOperatorSyntax.Power)),
                "ilshift" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorILShift, (arguments, span, context) => InPlaceBinary(arguments, span, context, AugmentedAssignmentOperatorSyntax.LeftShift)),
                "irshift" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorIRShift, (arguments, span, context) => InPlaceBinary(arguments, span, context, AugmentedAssignmentOperatorSyntax.RightShift)),
                "iand" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorIAnd, (arguments, span, context) => InPlaceBinary(arguments, span, context, AugmentedAssignmentOperatorSyntax.BitwiseAnd)),
                "ior" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorIOr, (arguments, span, context) => InPlaceBinary(arguments, span, context, AugmentedAssignmentOperatorSyntax.BitwiseOr)),
                "ixor" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorIXor, (arguments, span, context) => InPlaceBinary(arguments, span, context, AugmentedAssignmentOperatorSyntax.BitwiseXor)),
                "iconcat" => BuiltinCallable.Create(LythonKnownCallableSignatures.OperatorIConcat, InPlaceConcat),
                "call" => new OperatorFactoryCallable("operator.call", CallTarget),
                "itemgetter" => new OperatorFactoryCallable("operator.itemgetter", CreateItemGetter),
                "attrgetter" => new OperatorFactoryCallable("operator.attrgetter", CreateAttrGetter),
                "methodcaller" => new OperatorFactoryCallable("operator.methodcaller", CreateMethodCaller),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    private sealed class OperatorFactoryCallable : ICallable, INamedRuntimeCallable, IPyRenderableValue
    {
        private readonly string _name;
        private readonly Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, object> _implementation;

        public OperatorFactoryCallable(string name, Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, object> implementation)
        {
            _name = name;
            _implementation = implementation;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return _implementation(arguments, span, context);
        }

        public string Name => _name;

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString(_name);
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public override string ToString() => _name;
    }

    private sealed class PyItemGetter : ICallable, IPyRenderableValue
    {
        private readonly object[] _items;

        public PyItemGetter(object[] items)
        {
            _items = items;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1 || arguments[0].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "operator.itemgetter(...)(obj) expects one positional argument.", span);
            }

            var target = arguments[0].Value;
            if (_items.Length == 1)
            {
                return ReadItem(target, _items[0], span, context);
            }

            var values = new object[_items.Length];
            for (var i = 0; i < _items.Length; i++)
            {
                values[i] = ReadItem(target, _items[i], span, context);
            }

            return new PyTuple(values, context.MemoryGovernor, span);
        }

        public PyString RenderPython(PyRenderingContext context)
            => PyRendering.JoinRenderedSequence(
                "operator.itemgetter(",
                _items.Select(item => PyRendering.ToReprPyString(item, context)),
                ")");

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class PyAttrGetter : ICallable, IPyRenderableValue
    {
        private readonly string[][] _paths;

        public PyAttrGetter(string[][] paths)
        {
            _paths = paths;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1 || arguments[0].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "operator.attrgetter(...)(obj) expects one positional argument.", span);
            }

            var target = arguments[0].Value;
            if (_paths.Length == 1)
            {
                return ReadPath(target, _paths[0], span, context);
            }

            var values = new object[_paths.Length];
            for (var i = 0; i < _paths.Length; i++)
            {
                values[i] = ReadPath(target, _paths[i], span, context);
            }

            return new PyTuple(values, context.MemoryGovernor, span);
        }

        public PyString RenderPython(PyRenderingContext context)
            => PyRendering.JoinRenderedSequence(
                "operator.attrgetter(",
                _paths.Select(path => PyRendering.ToReprPyString(PyString.FromString(string.Join(".", path)), context)),
                ")",
                context);

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        private static object ReadPath(object target, IReadOnlyList<string> path, LythonSourceSpan span, ExecutionContext context)
        {
            var current = target;
            foreach (var part in path)
            {
                var memberTarget = current;
                if (!PyMemberAccess.TryResolve(memberTarget, part, context, span, out current))
                {
                    throw PyMemberAccess.CreateMissingMemberError(memberTarget, part, span);
                }
            }

            return current;
        }
    }

    private sealed class PyMethodCaller : ICallable, IPyRenderableValue
    {
        private readonly string _name;
        private readonly CallArgumentValue[] _arguments;

        public PyMethodCaller(string name, CallArgumentValue[] arguments)
        {
            _name = name;
            _arguments = arguments;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1 || arguments[0].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "operator.methodcaller(...)(obj) expects one positional argument.", span);
            }

            if (!PyMemberAccess.TryResolve(arguments[0].Value, _name, context, span, out var member))
            {
                throw PyMemberAccess.CreateMissingMemberError(arguments[0].Value, _name, span);
            }

            return InvokeCallableTarget(member, span, span, context, () => _arguments);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            var rendered = new List<PyString> { PyRendering.ToReprPyString(PyString.FromString(_name), context) };
            foreach (var argument in _arguments)
            {
                if (argument.IsPositional)
                {
                    rendered.Add(PyRendering.ToReprPyString(argument.Value, context));
                    continue;
                }

                rendered.Add(PyString.FromString(argument.KeywordName + "=" + PyRendering.ToReprPyString(argument.Value, context).AsString()));
            }

            return PyRendering.JoinRenderedSequence("operator.methodcaller(", rendered, ")", context);
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private static object Unary(object[] arguments, LythonSourceSpan span, Func<object, LythonSourceSpan, object> operation)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "operator unary function expects one argument.", span);
        }

        return operation(arguments[0], span);
    }

    private static object Binary(object[] arguments, LythonSourceSpan span, Func<object, object, LythonSourceSpan, object> operation)
    {
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "operator function expects two arguments.", span);
        }

        return operation(arguments[0], arguments[1], span);
    }

    private static object EvaluateAbsolute(object value, LythonSourceSpan span)
    {
        if (value is PyDecimal decimalValue)
        {
            return new PyDecimal(decimal.Abs(decimalValue.Value), decimalValue.Exponent);
        }

        if (!PyNumberOps.TryAsNumber(value, out var number))
        {
            throw new LythonRuntimeException("TypeError", "operator.abs(obj) expects a numeric value.", span);
        }

        return number.IsFloat ? Math.Abs(number.Floating) : BigInteger.Abs(number.Integer);
    }

    private static object EvaluateIndex(object value, ExecutionContext context, LythonSourceSpan span)
    {
        var converted = CoerceIndexProtocol(value, context, span);
        if (!PyNumberOps.TryAsInteger(converted, out var integer))
        {
            throw new LythonRuntimeException("TypeError", "operator.index(obj) expects an integer-compatible value.", span);
        }

        return integer;
    }

    private static object UnsupportedMatMul(object left, object right, LythonSourceSpan span)
    {
        _ = left;
        _ = right;
        throw new LythonRuntimeException("TypeError", "operator.matmul(a, b) is unsupported because Lython has no matrix multiplication operator.", span);
    }

    private static object EvaluateConcat(object left, object right, ExecutionContext context, LythonSourceSpan span)
    {
        if (PyStringOps.TryAsString(left, out _) && PyStringOps.TryAsString(right, out _) ||
            left is PyList && right is PyList ||
            left is PyTuple && right is PyTuple)
        {
            return EvaluateAdd(left, right, context, span);
        }

        throw new LythonRuntimeException("TypeError", "operator.concat(a, b) expects two compatible sequence values.", span);
    }

    private static object CompareBool(object[] arguments, LythonSourceSpan span, Func<object, object, LythonSourceSpan, bool> operation)
    {
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "operator comparison expects two arguments.", span);
        }

        return operation(arguments[0], arguments[1], span);
    }

    private static object GetItem(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "operator.getitem(obj, key) expects two arguments.", span);
        }

        return ReadItem(arguments[0], arguments[1], span, context);
    }

    private static object ReadItem(object target, object index, LythonSourceSpan span, ExecutionContext context)
    {
        if (target is PyDefaultDict defaultDict)
        {
            return defaultDict.GetOrCreate(ValidateDictionaryKey(index, span, context.MemoryGovernor), context, span);
        }

        return PyIndexing.ReadIndex(target, index, span);
    }

    private static object DelItem(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "operator.delitem(obj, key) expects two arguments.", span);
        }

        var target = arguments[0];
        var index = arguments[1];
        switch (target)
        {
            case IDeletablePySubscriptableValue subscriptable:
                subscriptable.DeleteSubscript(index, span);
                return PyNone.Instance;
            case IMutablePySequenceValue sequence:
                sequence.RemoveAt(PyIndexing.NormalizeIndex(index, sequence.Count, span));
                return PyNone.Instance;
            case PyDict dict:
                if (!dict.Remove(ValidateDictionaryKey(index, span, context.MemoryGovernor)))
                {
                    throw new LythonRuntimeException("KeyError", "Key was not found.", span);
                }

                return PyNone.Instance;
            case PyDefaultDict defaultDict:
                if (!defaultDict.Remove(ValidateDictionaryKey(index, span, context.MemoryGovernor)))
                {
                    throw new LythonRuntimeException("KeyError", "Key was not found.", span);
                }

                return PyNone.Instance;
            case PyCounter counter:
                _ = counter.Remove(ValidateDictionaryKey(index, span, context.MemoryGovernor));
                return PyNone.Instance;
            case PyTuple:
                throw new LythonRuntimeException("TypeError", "Tuple does not support item deletion.", span);
            default:
                if (PyStringOps.TryAsString(target, out _))
                {
                    throw new LythonRuntimeException("TypeError", "String does not support item deletion.", span);
                }

                throw new LythonRuntimeException("TypeError", "Object does not support item deletion.", span);
        }
    }

    private static object SetItem(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 3)
        {
            throw new LythonRuntimeException("TypeError", "operator.setitem(obj, key, value) expects three arguments.", span);
        }

        SetSubscriptValue(arguments[0], arguments[1], arguments[2], span, context);
        return PyNone.Instance;
    }

    private static object ContainsValue(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "operator.contains(obj, value) expects two arguments.", span);
        }

        return Contains(arguments[0], arguments[1], span);
    }

    private static object LengthHint(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 1 or > 2)
        {
            throw new LythonRuntimeException("TypeError", "operator.length_hint(obj, default=0) expects one or two arguments.", span);
        }

        try
        {
            return Len([arguments[0]], span, context);
        }
        catch (LythonRuntimeException exception) when (exception.ExceptionType == "TypeError")
        {
            // CPython clears a missing/failed len() lookup before consulting
            // __length_hint__ and finally the caller's default.
        }

        if (arguments[0] is PyInstance instance &&
            instance.TryGetAttribute("__length_hint__", context, span, out var member) &&
            member is ICallable callable)
        {
            var hinted = callable.Invoke([], span, context);
            if (!PyNumberOps.TryAsInteger(hinted, out var length))
            {
                throw new LythonRuntimeException("TypeError", "__length_hint__ must be an integer, not a non-integer value", span);
            }

            if (length < 0)
            {
                throw new LythonRuntimeException("ValueError", "__length_hint__() should return >= 0", span);
            }

            return length;
        }

        if (arguments.Length == 1)
        {
            return BigInteger.Zero;
        }

        var defaultValue = ExpectInteger(arguments[1], "operator.length_hint(obj, default=0) expects an integer default.", span);
        if (defaultValue < BigInteger.Zero)
        {
            throw new LythonRuntimeException("ValueError", "operator.length_hint default must be non-negative.", span);
        }

        return defaultValue;
    }

    private static object CountOf(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "operator.countOf(a, b) expects two arguments.", span);
        }

        var count = BigInteger.Zero;
        foreach (var item in ToSequence(arguments[0], span, context))
        {
            if (AreEqual(item, arguments[1]))
            {
                count++;
            }
        }

        return count;
    }

    private static object IndexOf(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "operator.indexOf(a, b) expects two arguments.", span);
        }

        var index = BigInteger.Zero;
        foreach (var item in ToSequence(arguments[0], span, context))
        {
            if (AreEqual(item, arguments[1]))
            {
                return index;
            }

            index++;
        }

        throw new LythonRuntimeException("ValueError", "sequence.index(x): x not in sequence", span);
    }

    private static object InPlaceBinary(
        object[] arguments,
        LythonSourceSpan span,
        ExecutionContext context,
        AugmentedAssignmentOperatorSyntax op)
        => Binary(arguments, span, (left, right, innerSpan) => EvaluateAugmentedAssignment(left, right, op, context, innerSpan));

    private static object InPlaceConcat(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => Binary(arguments, span, (left, right, innerSpan) =>
        {
            if (left is PyList || PyStringOps.TryAsString(left, out _) && PyStringOps.TryAsString(right, out _) || left is PyTuple && right is PyTuple)
            {
                return EvaluateAugmentedAssignment(left, right, AugmentedAssignmentOperatorSyntax.Add, context, innerSpan);
            }

            throw new LythonRuntimeException("TypeError", "operator.iconcat(a, b) expects sequence-compatible values.", innerSpan);
        });

    private static object CallTarget(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 0 || arguments[0].IsKeyword)
        {
            throw new LythonRuntimeException("TypeError", "operator.call(obj, /, *args, **kwargs) expects a callable first positional argument.", span);
        }

        return InvokeCallableTarget(arguments[0].Value, span, span, context, () => arguments[1..]);
    }

    private static object CreateItemGetter(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length == 0)
        {
            throw new LythonRuntimeException("TypeError", "operator.itemgetter(item[, ...]) expects one or more positional arguments.", span);
        }

        var items = new object[arguments.Length];
        for (var i = 0; i < arguments.Length; i++)
        {
            if (arguments[i].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "operator.itemgetter(item[, ...]) expects one or more positional arguments.", span);
            }

            items[i] = RuntimeValue(arguments[i].Value);
        }

        return new PyItemGetter(items);
    }

    private static object CreateAttrGetter(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length == 0)
        {
            throw new LythonRuntimeException("TypeError", "operator.attrgetter(attr[, ...]) expects one or more positional string arguments.", span);
        }

        var paths = new string[arguments.Length][];
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            if (argument.IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "operator.attrgetter(attr[, ...]) expects one or more positional string arguments.", span);
            }

            if (!PyStringOps.TryAsString(argument.Value, out var text))
            {
                throw new LythonRuntimeException("TypeError", "operator.attrgetter(attr[, ...]) expects string arguments.", span);
            }

            var parts = text.AsString().Split('.');
            if (parts.Length == 0 || parts.Any(static part => part.Length == 0))
            {
                throw new LythonRuntimeException("TypeError", "operator.attrgetter(attr[, ...]) expects non-empty dotted attribute names.", span);
            }

            paths[i] = parts;
        }

        return new PyAttrGetter(paths);
    }

    private static object CreateMethodCaller(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length == 0 || arguments[0].IsKeyword || !PyStringOps.TryAsString(arguments[0].Value, out var name))
        {
            throw new LythonRuntimeException("TypeError", "operator.methodcaller(name, ...) expects the first argument to be a method name string.", span);
        }

        return new PyMethodCaller(name.AsString(), arguments[1..]);
    }
}
