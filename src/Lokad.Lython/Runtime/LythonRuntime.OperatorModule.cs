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
                "truth" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorTruth, (arguments, span, _) => Unary(arguments, span, static (value, _) => IsTruthy(value))),
                "not_" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorNot, (arguments, span, _) => Unary(arguments, span, static (value, _) => !IsTruthy(value))),
                "is_" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorIs, (arguments, span, _) => CompareBool(arguments, span, static (left, right, _) => AreIdentical(left, right))),
                "is_not" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorIsNot, (arguments, span, _) => CompareBool(arguments, span, static (left, right, _) => !AreIdentical(left, right))),
                "abs" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorAbs, (arguments, span, _) => Unary(arguments, span, EvaluateAbsolute)),
                "neg" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorNeg, (arguments, span, _) => Unary(arguments, span, EvaluateUnaryMinus)),
                "pos" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorPos, (arguments, span, _) => Unary(arguments, span, EvaluateUnaryPlus)),
                "invert" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorInvert, (arguments, span, _) => Unary(arguments, span, EvaluateBitwiseNot)),
                "index" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorIndex, (arguments, span, context) => Unary(arguments, span, (value, innerSpan) => EvaluateIndex(value, context, innerSpan))),
                "add" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorAdd, (arguments, span, context) => Binary(arguments, span, (left, right, innerSpan) => EvaluateAdd(left, right, context, innerSpan))),
                "sub" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorSub, (arguments, span, _) => Binary(arguments, span, EvaluateSubtract)),
                "mul" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorMul, (arguments, span, context) => Binary(arguments, span, (left, right, innerSpan) => EvaluateMultiply(left, right, context, innerSpan))),
                "truediv" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorTrueDiv, (arguments, span, _) => Binary(arguments, span, EvaluateDivide)),
                "floordiv" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorFloorDiv, (arguments, span, _) => Binary(arguments, span, EvaluateFloorDivide)),
                "mod" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorMod, (arguments, span, context) => Binary(arguments, span, (left, right, innerSpan) => EvaluateModulo(left, right, context, innerSpan))),
                "pow" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorPow, (arguments, span, context) => Binary(arguments, span, (left, right, innerSpan) => EvaluatePower(left, right, context, innerSpan))),
                "matmul" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorMatMul, (arguments, span, _) => Binary(arguments, span, UnsupportedMatMul)),
                "lshift" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorLShift, (arguments, span, context) => Binary(arguments, span, (left, right, innerSpan) => EvaluateLeftShift(left, right, context, innerSpan))),
                "rshift" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorRShift, (arguments, span, _) => Binary(arguments, span, EvaluateRightShift)),
                "and_" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorAnd, (arguments, span, _) => Binary(arguments, span, EvaluateBitwiseAnd)),
                "or_" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorOr, (arguments, span, _) => Binary(arguments, span, EvaluateBitwiseOr)),
                "xor" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorXor, (arguments, span, _) => Binary(arguments, span, EvaluateBitwiseXor)),
                "concat" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorConcat, (arguments, span, context) => Binary(arguments, span, (left, right, innerSpan) => EvaluateConcat(left, right, context, innerSpan))),
                "eq" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorEq, (arguments, span, _) => CompareBool(arguments, span, static (left, right, _) => AreEqual(left, right))),
                "ne" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorNe, (arguments, span, _) => CompareBool(arguments, span, static (left, right, _) => !AreEqual(left, right))),
                "lt" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorLt, (arguments, span, _) => CompareBool(arguments, span, static (left, right, innerSpan) => Compare(left, right, innerSpan) < 0)),
                "le" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorLe, (arguments, span, _) => CompareBool(arguments, span, static (left, right, innerSpan) => Compare(left, right, innerSpan) <= 0)),
                "gt" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorGt, (arguments, span, _) => CompareBool(arguments, span, static (left, right, innerSpan) => Compare(left, right, innerSpan) > 0)),
                "ge" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorGe, (arguments, span, _) => CompareBool(arguments, span, static (left, right, innerSpan) => Compare(left, right, innerSpan) >= 0)),
                "getitem" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorGetItem, GetItem),
                "setitem" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorSetItem, SetItem),
                "delitem" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorDelItem, DelItem),
                "contains" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorContains, ContainsValue),
                "length_hint" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorLengthHint, LengthHint),
                "countOf" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorCountOf, CountOf),
                "indexOf" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorIndexOf, IndexOf),
                "iadd" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorIAdd, (arguments, span, context) => InPlaceBinary(arguments, span, context, AugmentedAssignmentOperatorSyntax.Add)),
                "isub" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorISub, (arguments, span, context) => InPlaceBinary(arguments, span, context, AugmentedAssignmentOperatorSyntax.Subtract)),
                "imul" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorIMul, (arguments, span, context) => InPlaceBinary(arguments, span, context, AugmentedAssignmentOperatorSyntax.Multiply)),
                "itruediv" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorITrueDiv, (arguments, span, context) => InPlaceBinary(arguments, span, context, AugmentedAssignmentOperatorSyntax.Divide)),
                "ifloordiv" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorIFloorDiv, (arguments, span, context) => InPlaceBinary(arguments, span, context, AugmentedAssignmentOperatorSyntax.FloorDivide)),
                "imod" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorIMod, (arguments, span, context) => InPlaceBinary(arguments, span, context, AugmentedAssignmentOperatorSyntax.Modulo)),
                "ipow" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorIPow, (arguments, span, context) => InPlaceBinary(arguments, span, context, AugmentedAssignmentOperatorSyntax.Power)),
                "ilshift" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorILShift, (arguments, span, context) => InPlaceBinary(arguments, span, context, AugmentedAssignmentOperatorSyntax.LeftShift)),
                "irshift" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorIRShift, (arguments, span, context) => InPlaceBinary(arguments, span, context, AugmentedAssignmentOperatorSyntax.RightShift)),
                "iand" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorIAnd, (arguments, span, context) => InPlaceBinary(arguments, span, context, AugmentedAssignmentOperatorSyntax.BitwiseAnd)),
                "ior" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorIOr, (arguments, span, context) => InPlaceBinary(arguments, span, context, AugmentedAssignmentOperatorSyntax.BitwiseOr)),
                "ixor" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorIXor, (arguments, span, context) => InPlaceBinary(arguments, span, context, AugmentedAssignmentOperatorSyntax.BitwiseXor)),
                "iconcat" => new BuiltinCallable(LythonKnownCallableSignatures.OperatorIConcat, InPlaceConcat),
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
            if (arguments.Length != 1 || arguments[0].Name is not null)
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
            if (arguments.Length != 1 || arguments[0].Name is not null)
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
            if (arguments.Length != 1 || arguments[0].Name is not null)
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
                if (argument.Name is null)
                {
                    rendered.Add(PyRendering.ToReprPyString(argument.Value, context));
                    continue;
                }

                rendered.Add(PyString.FromString(argument.Name + "=" + PyRendering.ToReprPyString(argument.Value, context).AsString()));
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

        var target = arguments[0];
        var index = arguments[1];
        var value = arguments[2];

        switch (target)
        {
            case IMutablePySequenceValue sequence:
                sequence.SetItem(PyIndexing.NormalizeIndex(index, sequence.Count, span), value);
                return PyNone.Instance;
            case PyDict dict:
                dict.AttachMemoryGovernor(context.MemoryGovernor, span);
                dict.SetItem(ValidateDictionaryKey(index, span, context.MemoryGovernor), value);
                context.ObserveCollectionCount(dict.Count, span);
                return PyNone.Instance;
            case PyDefaultDict defaultDict:
                defaultDict.AttachMemoryGovernor(context.MemoryGovernor, span);
                defaultDict.SetItem(ValidateDictionaryKey(index, span, context.MemoryGovernor), value);
                context.ObserveCollectionCount(defaultDict.Count, span);
                return PyNone.Instance;
            case PyCounter counter:
                counter.AttachMemoryGovernor(context.MemoryGovernor, span);
                counter.SetItem(ValidateDictionaryKey(index, span, context.MemoryGovernor), value);
                context.ObserveCollectionCount(counter.Count, span);
                return PyNone.Instance;
            case PyTuple:
                throw new LythonRuntimeException("TypeError", "Tuple does not support item assignment.", span);
            default:
                throw new LythonRuntimeException("TypeError", "Object does not support item assignment.", span);
        }
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
        _ = context;
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "operator.countOf(a, b) expects two arguments.", span);
        }

        var count = BigInteger.Zero;
        foreach (var item in ToSequence(arguments[0], span))
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
        _ = context;
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "operator.indexOf(a, b) expects two arguments.", span);
        }

        var index = BigInteger.Zero;
        foreach (var item in ToSequence(arguments[0], span))
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
        if (arguments.Length == 0 || arguments[0].Name is not null)
        {
            throw new LythonRuntimeException("TypeError", "operator.call(obj, /, *args, **kwargs) expects a callable first positional argument.", span);
        }

        return InvokeCallableTarget(arguments[0].Value, span, span, context, () => arguments[1..]);
    }

    private static bool TryGetLength(object value, out BigInteger length)
    {
        switch (value)
        {
            case PyString text:
                length = text.Length;
                return true;
            case PyBytes bytes:
                length = bytes.Length;
                return true;
            case string text:
                length = PyString.FromString(text).Length;
                return true;
            case ReFindAllResult matches:
                length = matches.Items.Count;
                return true;
            case PyDict dict:
                length = dict.Count;
                return true;
            case PyDefaultDict defaultDict:
                length = defaultDict.Count;
                return true;
            case PyCounter counter:
                length = counter.Count;
                return true;
            case PyDeque deque:
                length = deque.Count;
                return true;
            case PySet set:
                length = set.Count;
                return true;
            case IPyIndexableValue indexable:
                length = indexable.Length;
                return true;
            case IReadOnlyCollection<object> collection:
                length = collection.Count;
                return true;
            case System.Collections.ICollection collection:
                length = collection.Count;
                return true;
            default:
                length = BigInteger.Zero;
                return false;
        }
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
            if (arguments[i].Name is not null)
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
            if (argument.Name is not null)
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
        if (arguments.Length == 0 || arguments[0].Name is not null || !PyStringOps.TryAsString(arguments[0].Value, out var name))
        {
            throw new LythonRuntimeException("TypeError", "operator.methodcaller(name, ...) expects the first argument to be a method name string.", span);
        }

        return new PyMethodCaller(name.AsString(), arguments[1..]);
    }
}
