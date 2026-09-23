using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static class PathStatMembers
    {
        public static bool TryGetMember(
            LythonPathStat stat,
            string name,
            ExecutionContext context,
            LythonSourceSpan span,
            [MaybeNullWhen(false)] out object value)
        {
            if (string.Equals(name, "modified_at", StringComparison.Ordinal))
            {
                value = PyString.FromString(stat.ModifiedAt, context.MemoryGovernor, span);
                return true;
            }

            return TryGetMember(stat, name, out value);
        }

        public static bool TryGetMember(LythonPathStat stat, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "__module__" => LythonRuntime.ExceptionTypeValue.SharedModuleLabel("os"),
                "exists" => stat.Exists,
                "is_file" => stat.IsFile,
                "is_dir" => stat.IsDir,
                "size" => stat.Size,
                "modified_at" => stat.ModifiedAt,
                "st_size" => stat.Size,
                "st_mtime" => PathModifiedAtSeconds(stat.ModifiedAtTimestamp, null),
                "st_ctime" => PathModifiedAtSeconds(stat.ModifiedAtTimestamp, null),
                "st_atime" => PathModifiedAtSeconds(stat.ModifiedAtTimestamp, null),
                "st_mode" or "st_ino" or "st_dev" or "st_nlink" or "st_uid" or "st_gid"
                    => throw new LythonRuntimeException("NotImplementedError", "Rich stat_result metadata is not supported by Lython because the host path model only exposes existence, kind, size, and modified time.", null),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    internal static class ExceptionInstanceMembers
    {
        public static bool TryGetMember(PyException exception, string name, [MaybeNullWhen(false)] out object value)
        {
            // Custom attributes shadow fixed members like CPython (method
            // shadowing included); the args, chaining and __dict__ slots stay separate.
            if (name != "args" && name != "__dict__" && name != "__cause__" && name != "__context__" && name != "__suppress_context__" &&
                exception.CustomDict is not null &&
                exception.CustomDict.TryGetValue(PyString.FromString(name), out var customValue))
            {
                value = customValue;
                return true;
            }

            value = name switch
            {
                "type" => PyString.FromString(exception.TypeName),
                "message" => PyString.FromString(exception.Message),
                "args" => CreateExceptionArgs(exception),
                "add_note" => BoundCallable.Create(
                    (arguments, span, context) => AddExceptionNote(exception, arguments, span, context),
                    "add_note",
                    ["note"]),
                "with_traceback" => BoundCallable.Create(
                    (arguments, span, _) => WithTraceback(exception, arguments, span),
                    "with_traceback"),
                "__cause__" => (object?)exception.Cause ?? PyNone.Instance,
                "__context__" => (object?)exception.Context ?? PyNone.Instance,
                "__suppress_context__" => exception.SuppressContext,
                "code" when string.Equals(exception.TypeName, "SystemExit", StringComparison.Ordinal) => exception.Value,
                "value" when string.Equals(exception.TypeName, "StopIteration", StringComparison.Ordinal)
                    => CreateExceptionArgs(exception) is { Count: > 0 } stopped ? stopped[0] : PyNone.Instance,
                "__hash__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "BaseException.__hash__() expects no arguments.", span);
                    }

                    return ComputeBuiltinHash(exception, span);
                }, "BaseException.__hash__"),
                _ => MissingMemberValue.Instance,
            };

            if (ReferenceEquals(value, MissingMemberValue.Instance) &&
                exception.Value is PyDict payload &&
                payload.TryGetValue(PyString.FromString(name), out var payloadValue))
            {
                value = payloadValue;
            }

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        // Builtin exception instances report their run type object like CPython;
        // module exceptions stay missing until their type objects are interned.
        public static bool TryGetMember(
            PyException exception,
            string name,
            ExecutionContext context,
            LythonSourceSpan span,
            [MaybeNullWhen(false)] out object value)
        {
            if (name == "__dict__")
            {
                exception.CustomDict ??= new PyDict(context.MemoryGovernor, span);
                exception.CustomDict.AttachMemoryGovernor(context.MemoryGovernor, span);
                value = exception.CustomDict;
                return true;
            }

            // Contextual reads repeat the custom check so __dict__-first ordering
            // holds even when the non-contextual fast path missed it.
            if (name != "args" && name != "__cause__" && name != "__context__" && name != "__suppress_context__" &&
                exception.CustomDict is not null &&
                exception.CustomDict.TryGetValue(PyString.FromString(name), out var customValue))
            {
                value = customValue;
                return true;
            }

            if (name == "__class__" &&
                exception.Identity.IsBuiltin &&
                context.TryGetBuiltin(exception.Identity.TypeName, out var typeValue) &&
                typeValue is not null)
            {
                value = typeValue;
                return true;
            }

            // Module exceptions resolve through the run import registry, so identity
            // matches the imported type object (and its aliases); unimported modules
            // stay missing like any other unregistered member.
            if (name == "__class__" &&
                !exception.Identity.IsBuiltin &&
                context.State.ImportedModules.TryGetValue(exception.Identity.ModuleName, out var module) &&
                module.TryGetCachedMember(exception.Identity.TypeName, out var moduleTypeValue) &&
                moduleTypeValue is not null)
            {
                value = moduleTypeValue;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        // Notes charge like ordinary list growth: the list is governed
        // from creation and every note observes the retained count.
        private static object AddExceptionNote(PyException exception, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "add_note(note) expects one argument.", span);
            }

            if (!PyStringOps.TryAsString(arguments[0], out var note))
            {
                throw new LythonRuntimeException("TypeError", "add_note(note) expects one string argument.", span);
            }

            // Notes live in the custom dict like CPython: add_note appends to
            // the __notes__ list entry, creating it governed on first use.
            if (exception.CustomDict is not null &&
                exception.CustomDict.TryGetValue(PyString.FromString("__notes__"), out var existingNotes))
            {
                if (existingNotes is not PyList notes)
                {
                    throw new LythonRuntimeException("TypeError", "Cannot add note: __notes__ is not a list", span);
                }

                notes.Add(note);
                context.ObserveCollectionCount(notes.Count, span);
                return PyNone.Instance;
            }

            exception.CustomDict ??= new PyDict(context.MemoryGovernor, span);
            exception.CustomDict.AttachMemoryGovernor(context.MemoryGovernor, span);
            var freshNotes = new PyList([], context.MemoryGovernor, span);
            freshNotes.Add(note);
            exception.CustomDict.SetItem(PyString.FromString("__notes__", context.MemoryGovernor, span), freshNotes);
            context.ObserveCollectionCount(freshNotes.Count, span);
            return PyNone.Instance;
        }

        // Lython has no traceback values: clearing with None is a no-op returning
        // the exception itself like CPython, while anything else fails explicitly
        // since no guest value can satisfy the traceback check.
        private static object WithTraceback(PyException exception, object[] arguments, LythonSourceSpan span)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException(
                    "TypeError",
                    $"BaseException.with_traceback() takes exactly one argument ({arguments.Length} given)",
                    span);
            }

            if (arguments[0] is not PyNone)
            {
                throw new LythonRuntimeException("TypeError", "__traceback__ must be a traceback or None", span);
            }

            return exception;
        }

        private static PyTuple CreateExceptionArgs(PyException exception)
        {
            if (exception.ArgsOverride is not null)
            {
                return exception.ArgsOverride;
            }

            if (exception.ExplicitArgs is not null)
            {
                return exception.ExplicitArgs;
            }

            if (string.Equals(exception.TypeName, "SystemExit", StringComparison.Ordinal))
            {
                return ReferenceEquals(exception.Value, PyNone.Instance)
                    ? PyTuple.Empty
                    : PyTuple.FromOwnedArray([exception.Value]);
            }

            if (string.Equals(exception.TypeName, "JSONDecodeError", StringComparison.Ordinal) &&
                exception.Value is PyDict payload &&
                payload.TryGetValue(PyString.FromString("msg"), out var msg) &&
                payload.TryGetValue(PyString.FromString("doc"), out var doc) &&
                payload.TryGetValue(PyString.FromString("pos"), out var pos))
            {
                return PyTuple.FromOwnedArray([msg, doc, pos]);
            }

            if (string.Equals(exception.TypeName, "CalledProcessError", StringComparison.Ordinal) &&
                exception.Value is PyDict subprocessPayload &&
                subprocessPayload.TryGetValue(PyString.FromString("returncode"), out var returnCode) &&
                subprocessPayload.TryGetValue(PyString.FromString("cmd"), out var command))
            {
                return PyTuple.FromOwnedArray([returnCode, command]);
            }

            if (string.Equals(exception.TypeName, "TimeoutExpired", StringComparison.Ordinal) &&
                exception.Value is PyDict timeoutPayload &&
                timeoutPayload.TryGetValue(PyString.FromString("cmd"), out var timeoutCommand) &&
                timeoutPayload.TryGetValue(PyString.FromString("timeout"), out var timeout))
            {
                return PyTuple.FromOwnedArray([timeoutCommand, timeout]);
            }

            // Internally raised errors carry their message without
            // construction args; like CPython single-argument construction,
            // the message reads back as the lone argument. Empty messages
            // (bare raises) keep the empty tuple. The transient string
            // follows the neighboring ungoverned read convention.
            return exception.Value switch
            {
                PyNone when string.IsNullOrEmpty(exception.Message) => PyTuple.Empty,
                PyNone => PyTuple.FromOwnedArray([PyString.FromString(exception.Message)]),
                _ => PyTuple.FromOwnedArray([exception.Value])
            };
        }
    }

    internal static class DecimalMembers
    {
        // N17: hot fixed signatures hoisted per family (see ListMembers).
        private static readonly LythonCallableSignature DecimalQuantizeSignature = LythonCallableSignature.Create("Decimal.quantize", ["exp", "rounding", "context"], requiredCount: 1);
        private static readonly LythonCallableSignature DecimalNormalizeSignature = LythonCallableSignature.Create("Decimal.normalize", ["context"], requiredCount: 0);
        private static readonly LythonCallableSignature DecimalSqrtSignature = LythonCallableSignature.Create("Decimal.sqrt", ["context"], requiredCount: 0);
        private static readonly LythonCallableSignature DecimalExpSignature = LythonCallableSignature.Create("Decimal.exp", ["context"], requiredCount: 0);
        private static readonly LythonCallableSignature DecimalLnSignature = LythonCallableSignature.Create("Decimal.ln", ["context"], requiredCount: 0);
        private static readonly LythonCallableSignature DecimalLog10Signature = LythonCallableSignature.Create("Decimal.log10", ["context"], requiredCount: 0);
        private static readonly LythonCallableSignature DecimalToIntegralValueSignature = LythonCallableSignature.Create("Decimal.to_integral_value", ["rounding", "context"], requiredCount: 0);
        private static readonly LythonCallableSignature DecimalToIntegralExactSignature = LythonCallableSignature.Create("Decimal.to_integral_exact", ["rounding", "context"], requiredCount: 0);
        private static readonly LythonCallableSignature DecimalToIntegralSignature = LythonCallableSignature.Create("Decimal.to_integral", ["rounding", "context"], requiredCount: 0);
        private static readonly LythonCallableSignature DecimalCompareSignature = LythonCallableSignature.Create("Decimal.compare", ["other", "context"], requiredCount: 1);
        private static readonly LythonCallableSignature DecimalScalebSignature = LythonCallableSignature.Create("Decimal.scaleb", ["other", "context"], requiredCount: 1);
        private static readonly LythonCallableSignature DecimalFmaSignature = LythonCallableSignature.Create("Decimal.fma", ["other", "third", "context"], requiredCount: 2);
        private static readonly LythonCallableSignature DecimalLogbSignature = LythonCallableSignature.Create("Decimal.logb", ["context"], requiredCount: 0);
        private static readonly LythonCallableSignature DecimalCompareSignalSignature = LythonCallableSignature.Create("Decimal.compare_signal", ["other", "context"], requiredCount: 1);
        private static readonly LythonCallableSignature DecimalRemainderNearSignature = LythonCallableSignature.Create("Decimal.remainder_near", ["other", "context"], requiredCount: 1);
        private static readonly LythonCallableSignature DecimalMinSignature = LythonCallableSignature.Create("Decimal.min", ["other", "context"], requiredCount: 1);
        private static readonly LythonCallableSignature DecimalMaxSignature = LythonCallableSignature.Create("Decimal.max", ["other", "context"], requiredCount: 1);
        private static readonly LythonCallableSignature DecimalMinMagSignature = LythonCallableSignature.Create("Decimal.min_mag", ["other", "context"], requiredCount: 1);
        private static readonly LythonCallableSignature DecimalMaxMagSignature = LythonCallableSignature.Create("Decimal.max_mag", ["other", "context"], requiredCount: 1);
        private static readonly LythonCallableSignature DecimalAsTupleSignature = LythonCallableSignature.Create("Decimal.as_tuple", []);
        private static readonly LythonCallableSignature DecimalAdjustedSignature = LythonCallableSignature.Create("Decimal.adjusted", []);
        private static readonly LythonCallableSignature DecimalToEngStringSignature = LythonCallableSignature.Create("Decimal.to_eng_string", []);
        private static readonly LythonCallableSignature DecimalRadixSignature = LythonCallableSignature.Create("Decimal.radix", []);
        private static readonly LythonCallableSignature DecimalCanonicalSignature = LythonCallableSignature.Create("Decimal.canonical", []);
        private static readonly LythonCallableSignature DecimalConjugateSignature = LythonCallableSignature.Create("Decimal.conjugate", []);
        private static readonly LythonCallableSignature DecimalEqSignature = LythonCallableSignature.Create("Decimal.__eq__", ["value"]);
        private static readonly LythonCallableSignature DecimalNeSignature = LythonCallableSignature.Create("Decimal.__ne__", ["value"]);
        private static readonly LythonCallableSignature DecimalLtSignature = LythonCallableSignature.Create("Decimal.__lt__", ["value"]);
        private static readonly LythonCallableSignature DecimalLeSignature = LythonCallableSignature.Create("Decimal.__le__", ["value"]);
        private static readonly LythonCallableSignature DecimalGtSignature = LythonCallableSignature.Create("Decimal.__gt__", ["value"]);
        private static readonly LythonCallableSignature DecimalGeSignature = LythonCallableSignature.Create("Decimal.__ge__", ["value"]);
        private static readonly LythonCallableSignature DecimalBoolSignature = LythonCallableSignature.Create("Decimal.__bool__");
        private static readonly LythonCallableSignature DecimalHashSignature = LythonCallableSignature.Create("Decimal.__hash__");
        private static readonly LythonCallableSignature DecimalIntSignature = LythonCallableSignature.Create("Decimal.__int__");
        private static readonly LythonCallableSignature DecimalFloatSignature = LythonCallableSignature.Create("Decimal.__float__");
        private static readonly LythonCallableSignature DecimalTruncSignature = LythonCallableSignature.Create("Decimal.__trunc__");
        private static readonly LythonCallableSignature DecimalFloorSignature = LythonCallableSignature.Create("Decimal.__floor__");
        private static readonly LythonCallableSignature DecimalCeilSignature = LythonCallableSignature.Create("Decimal.__ceil__");
        private static readonly LythonCallableSignature DecimalRoundSignature = LythonCallableSignature.Create("Decimal.__round__");
        private static readonly LythonCallableSignature DecimalCopySignSignature = LythonCallableSignature.Create("Decimal.copy_sign", ["other"]);
        private static readonly LythonCallableSignature DecimalCompareTotalSignature = LythonCallableSignature.Create("Decimal.compare_total", ["other"]);
        private static readonly LythonCallableSignature DecimalIsNanSignature = LythonCallableSignature.Create("Decimal.is_nan", []);
        private static readonly LythonCallableSignature DecimalIsInfiniteSignature = LythonCallableSignature.Create("Decimal.is_infinite", []);
        private static readonly LythonCallableSignature DecimalIsFiniteSignature = LythonCallableSignature.Create("Decimal.is_finite", []);
        private static readonly LythonCallableSignature DecimalIsZeroSignature = LythonCallableSignature.Create("Decimal.is_zero", []);
        private static readonly LythonCallableSignature DecimalIsSignedSignature = LythonCallableSignature.Create("Decimal.is_signed", []);
        private static readonly LythonCallableSignature DecimalShiftSignature = LythonCallableSignature.Create("Decimal.shift", ["other"]);
        private static readonly LythonCallableSignature DecimalRotateSignature = LythonCallableSignature.Create("Decimal.rotate", ["other"]);
        private static readonly LythonCallableSignature DecimalSameQuantumSignature = LythonCallableSignature.Create("Decimal.same_quantum", ["other"]);
        private static readonly LythonCallableSignature DecimalAsIntegerRatioSignature = LythonCallableSignature.Create("Decimal.as_integer_ratio", []);
        private static readonly LythonCallableSignature DecimalIsQnanSignature = LythonCallableSignature.Create("Decimal.is_qnan", []);
        private static readonly LythonCallableSignature DecimalIsSnanSignature = LythonCallableSignature.Create("Decimal.is_snan", []);
        private static readonly LythonCallableSignature DecimalIsCanonicalSignature = LythonCallableSignature.Create("Decimal.is_canonical", []);
        public static bool TryGetMember(PyDecimal decimalValue, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "__module__" => LythonRuntime.ExceptionTypeValue.SharedModuleLabel("decimal"),
                "quantize" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length is < 1 or > 3 || arguments[0] is not PyDecimal exponent)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.quantize(exp[, rounding][, context]) expects a Decimal exponent plus optional rounding/context.", span);
                    }

                    var rounding = arguments.Length >= 2 ? arguments[1] : PyNone.Instance;
                    var decimalContext = arguments.Length >= 3 && arguments[2] is not PyNone
                        ? arguments[2] as PyDecimalContext ?? throw new LythonRuntimeException("TypeError", "Decimal.quantize(..., context=...) expects a Context or None.", span)
                        : context.DecimalContext;
                    return OwnFreshDecimal(PyDecimalOps.Quantize(decimalValue, exponent, rounding, decimalContext, span), decimalValue, context, span);
                }, DecimalQuantizeSignature),
                "normalize" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.Unary("normalize", decimalValue, arguments, span), decimalValue, context, span), DecimalNormalizeSignature),
                "sqrt" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.Unary("sqrt", decimalValue, arguments, span), decimalValue, context, span), DecimalSqrtSignature),
                "exp" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.Unary("exp", decimalValue, arguments, span), decimalValue, context, span), DecimalExpSignature),
                "ln" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.Unary("ln", decimalValue, arguments, span), decimalValue, context, span), DecimalLnSignature),
                "log10" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.Unary("log10", decimalValue, arguments, span), decimalValue, context, span), DecimalLog10Signature),
                "copy_abs" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.CopyAbsNegate(decimalValue, arguments, false, span), decimalValue, context, span)),
                "copy_negate" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.CopyAbsNegate(decimalValue, arguments, true, span), decimalValue, context, span)),
                "copy_sign" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.CopySign(decimalValue, arguments, span), decimalValue, context, span), DecimalCopySignSignature),
                "to_integral_value" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.ToIntegral(decimalValue, arguments, context.DecimalContext, span), decimalValue, context, span), DecimalToIntegralValueSignature),
                "to_integral_exact" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.ToIntegral(decimalValue, arguments, context.DecimalContext, span), decimalValue, context, span), DecimalToIntegralExactSignature),
                "to_integral" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.ToIntegral(decimalValue, arguments, context.DecimalContext, span), decimalValue, context, span), DecimalToIntegralSignature),
                "as_tuple" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.as_tuple() expects no arguments.", span);
                    }

                    return OwnDecimalValue(PyDecimalOps.AsTuple(decimalValue, context.MemoryGovernor, span), context, span);
                }, DecimalAsTupleSignature),
                "adjusted" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.adjusted() expects no arguments.", span);
                    }

                    return PyDecimalOps.Adjusted(decimalValue);
                }, DecimalAdjustedSignature),
                "compare" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.CompareValue(decimalValue, arguments, span), decimalValue, context, span), DecimalCompareSignature),
                "compare_total" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.CompareTotal(decimalValue, arguments, span), decimalValue, context, span), DecimalCompareTotalSignature),
                "is_nan" => BoundCallable.Create((arguments, span, _) => ExpectDecimalNoArguments("is_nan", arguments, span, false), DecimalIsNanSignature),
                "is_infinite" => BoundCallable.Create((arguments, span, _) => ExpectDecimalNoArguments("is_infinite", arguments, span, false), DecimalIsInfiniteSignature),
                "is_finite" => BoundCallable.Create((arguments, span, _) => ExpectDecimalNoArguments("is_finite", arguments, span, true), DecimalIsFiniteSignature),
                "is_zero" => BoundCallable.Create((arguments, span, _) => ExpectDecimalNoArguments("is_zero", arguments, span, decimalValue.Value == 0m), DecimalIsZeroSignature),
                "is_signed" => BoundCallable.Create((arguments, span, _) => ExpectDecimalNoArguments("is_signed", arguments, span, decimalValue.IsSigned), DecimalIsSignedSignature),
                "to_eng_string" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.to_eng_string() expects no arguments.", span);
                    }

                    return PyDecimalOps.ToEngineeringString(decimalValue, context.MemoryGovernor, span);
                }, DecimalToEngStringSignature),
                "scaleb" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.ScaleB(decimalValue, arguments, span), decimalValue, context, span), DecimalScalebSignature),
                "shift" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.Shift(decimalValue, arguments, span), decimalValue, context, span), DecimalShiftSignature),
                "rotate" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.Rotate(decimalValue, arguments, span), decimalValue, context, span), DecimalRotateSignature),
                "same_quantum" => BoundCallable.Create((arguments, span, _) => PyDecimalOps.SameQuantum(decimalValue, arguments, span), DecimalSameQuantumSignature),
                "fma" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.FusedMultiplyAdd(decimalValue, arguments, span), decimalValue, context, span), DecimalFmaSignature),
                "logb" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.LogB(decimalValue, arguments, span), decimalValue, context, span), DecimalLogbSignature),
                "compare_signal" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.CompareSignal(decimalValue, arguments, span), decimalValue, context, span), DecimalCompareSignalSignature),
                "radix" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.radix() expects no arguments.", span);
                    }

                    return OwnFreshDecimal(new PyDecimal(10m), decimalValue, context, span);
                }, DecimalRadixSignature),
                "canonical" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.canonical() expects no arguments.", span);
                    }

                    return OwnFreshDecimal(new PyDecimal(decimalValue.Value, decimalValue.Exponent), decimalValue, context, span);
                }, DecimalCanonicalSignature),
                "conjugate" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.conjugate() expects no arguments.", span);
                    }

                    return OwnFreshDecimal(new PyDecimal(decimalValue.Value, decimalValue.Exponent), decimalValue, context, span);
                }, DecimalConjugateSignature),
                "as_integer_ratio" => BoundCallable.Create((arguments, span, context) => PyDecimalOps.AsIntegerRatio(decimalValue, arguments, context.MemoryGovernor, span), DecimalAsIntegerRatioSignature),
                "is_qnan" => BoundCallable.Create((arguments, span, _) => ExpectDecimalNoArguments("is_qnan", arguments, span, false), DecimalIsQnanSignature),
                "is_snan" => BoundCallable.Create((arguments, span, _) => ExpectDecimalNoArguments("is_snan", arguments, span, false), DecimalIsSnanSignature),
                "is_canonical" => BoundCallable.Create((arguments, span, _) => ExpectDecimalNoArguments("is_canonical", arguments, span, true), DecimalIsCanonicalSignature),
                "remainder_near" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.RemainderNear(decimalValue, arguments, span), decimalValue, context, span), DecimalRemainderNearSignature),
                "min" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.MinMax(decimalValue, arguments, "min", span), decimalValue, context, span), DecimalMinSignature),
                "max" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.MinMax(decimalValue, arguments, "max", span), decimalValue, context, span), DecimalMaxSignature),
                "min_mag" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.MinMax(decimalValue, arguments, "min_mag", span), decimalValue, context, span), DecimalMinMagSignature),
                "max_mag" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.MinMax(decimalValue, arguments, "max_mag", span), decimalValue, context, span), DecimalMaxMagSignature),
                "__eq__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.__eq__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyDecimal && arguments[0] is not BigInteger && arguments[0] is not int && arguments[0] is not bool && arguments[0] is not double)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return PyDecimalOps.AreEqual(decimalValue, arguments[0]);
                }, DecimalEqSignature),
                "__ne__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.__ne__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyDecimal && arguments[0] is not BigInteger && arguments[0] is not int && arguments[0] is not bool && arguments[0] is not double)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return !PyDecimalOps.AreEqual(decimalValue, arguments[0]);
                }, DecimalNeSignature),
                "__lt__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.__lt__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyDecimal && arguments[0] is not BigInteger && arguments[0] is not int && arguments[0] is not bool && arguments[0] is not double)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return PyDecimalOps.Compare(decimalValue, arguments[0], span, "<") < 0;
                }, DecimalLtSignature),
                "__le__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.__le__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyDecimal && arguments[0] is not BigInteger && arguments[0] is not int && arguments[0] is not bool && arguments[0] is not double)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return PyDecimalOps.Compare(decimalValue, arguments[0], span, "<=") <= 0;
                }, DecimalLeSignature),
                "__gt__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.__gt__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyDecimal && arguments[0] is not BigInteger && arguments[0] is not int && arguments[0] is not bool && arguments[0] is not double)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return PyDecimalOps.Compare(decimalValue, arguments[0], span, ">") > 0;
                }, DecimalGtSignature),
                "__ge__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.__ge__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyDecimal && arguments[0] is not BigInteger && arguments[0] is not int && arguments[0] is not bool && arguments[0] is not double)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return PyDecimalOps.Compare(decimalValue, arguments[0], span, ">=") >= 0;
                }, DecimalGeSignature),
                "__bool__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.__bool__() expects no arguments.", span);
                    }

                    return decimalValue.IsTruthy();
                }, DecimalBoolSignature),
                "__hash__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.__hash__() expects no arguments.", span);
                    }

                    return ComputeBuiltinHash(decimalValue, span);
                }, DecimalHashSignature),
                "__int__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.__int__() expects no arguments.", span);
                    }

                    return OwnHeapInteger(new BigInteger(decimal.Truncate(decimalValue.Value)), context.MemoryGovernor, context.Services.State.CallTemporaries, span);
                }, DecimalIntSignature),
                "__float__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.__float__() expects no arguments.", span);
                    }

                    return (double)decimalValue.Value;
                }, DecimalFloatSignature),
                "__trunc__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.__trunc__() expects no arguments.", span);
                    }

                    return OwnHeapInteger(new BigInteger(decimal.Truncate(decimalValue.Value)), context.MemoryGovernor, context.Services.State.CallTemporaries, span);
                }, DecimalTruncSignature),
                "__floor__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.__floor__() expects no arguments.", span);
                    }

                    return OwnHeapInteger(new BigInteger(decimal.Floor(decimalValue.Value)), context.MemoryGovernor, context.Services.State.CallTemporaries, span);
                }, DecimalFloorSignature),
                "__ceil__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.__ceil__() expects no arguments.", span);
                    }

                    return OwnHeapInteger(new BigInteger(decimal.Ceiling(decimalValue.Value)), context.MemoryGovernor, context.Services.State.CallTemporaries, span);
                }, DecimalCeilSignature),
                "__round__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.__round__([ndigits]) expects zero or one argument.", span);
                    }

                    // Unlike int/float slots, Decimal rejects everything but
                    // integers (bool included) with its own message instead
                    // of consulting __index__.
                    if (arguments.Length == 1 && arguments[0] is not BigInteger && arguments[0] is not int && arguments[0] is not bool)
                    {
                        throw new LythonRuntimeException("TypeError", "optional arg must be an integer", span);
                    }

                    return Round(arguments.Length == 0 ? [decimalValue] : [decimalValue, arguments[0]], span, context);
                }, DecimalRoundSignature),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static bool ExpectDecimalNoArguments(string name, object[] arguments, LythonSourceSpan span, bool result)
        {
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", $"Decimal.{name}() expects no arguments.", span);
            }

            return result;
        }
    }

    internal static partial class PathMembers
    {
        private static readonly IPathMemberProvider[] Providers =
        [
            LexicalPathMemberProvider.Instance,
            HostStatusPathMemberProvider.Instance,
            HostMutationPathMemberProvider.Instance,
            UnsupportedHostPathMemberProvider.Instance,
            HostContentPathMemberProvider.Instance,
        ];

        private interface IPathMemberProvider
        {
            /// <summary>Resolves members owned by one cohesive path-operation family.</summary>
            bool TryGetMember(PyPath path, string name, [MaybeNullWhen(false)] out object value);
        }

        public static bool TryGetMember(PyPath path, string name, [MaybeNullWhen(false)] out object value)
        {
            foreach (var provider in Providers)
            {
                if (provider.TryGetMember(path, name, out value))
                {
                    return true;
                }
            }

            value = null;
            return false;
        }
        public static bool TryGetMember(PyPath path, string name, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            var governor = context.MemoryGovernor;
            value = name switch
            {
                "__module__" => LythonRuntime.ExceptionTypeValue.SharedModuleLabel("pathlib"),
                "name" => OwnMethodResult(PyString.FromString(PathOps.BaseName(path.Value.AsString())), path.Value, governor, span, context.Services.State.CallTemporaries),
                "suffix" => OwnMethodResult(PyString.FromString(PathOps.Suffix(path.Value.AsString())), path.Value, governor, span, context.Services.State.CallTemporaries),
                "stem" => OwnMethodResult(PyString.FromString(PathOps.Stem(path.Value.AsString())), path.Value, governor, span, context.Services.State.CallTemporaries),
                "parent" => OwnPathResult(PathOps.Parent(path.Value), path.Value, governor, span, context.Services.State.CallTemporaries),
                "suffixes" => GovernedSuffixes(path.Value, governor, span, context.Services.State.CallTemporaries),
                "parents" => PathOps.Parents(path.Value, context.MemoryGovernor, span, context.Services.State.CallTemporaries),
                "parts" => PathOps.Parts(path.Value, context.MemoryGovernor, span, context.Services.State.CallTemporaries),
                "__new__" => PathlibModule.PathType.GetNewSlot(),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static PyList GovernedSuffixes(PyString path, MemoryGovernor governor, LythonSourceSpan span, ChargeReclamationPool? pool)
        {
            var name = PathOps.BaseName(path.AsString());
            var values = new List<object>();
            var dot = name.IndexOf('.', name.StartsWith(".", StringComparison.Ordinal) ? 1 : 0);
            while (dot >= 0 && dot < name.Length - 1)
            {
                var next = name.IndexOf('.', dot + 1);
                var suffix = next < 0 ? name[dot..] : name[dot..next];
                values.Add(OwnMethodResult(PyString.FromString(suffix), path, governor, span, pool));
                dot = next;
            }

            // Items adopt above; the container backing never reaches a funnel, so adopt
            // it here with refund like the split-result choke does.
            return OwnSplitListResult(new PyList(values, governor, span), span, pool);
        }
    }
}
