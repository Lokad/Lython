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
            value = name switch
            {
                "type" => PyString.FromString(exception.TypeName),
                "message" => PyString.FromString(exception.Message),
                "args" => CreateExceptionArgs(exception),
                "code" when string.Equals(exception.TypeName, "SystemExit", StringComparison.Ordinal) => exception.Value,
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
            _ = span;
            if (name == "__class__" &&
                exception.Identity.IsBuiltin &&
                context.TryGetBuiltin(exception.Identity.TypeName, out var typeValue) &&
                typeValue is not null)
            {
                value = typeValue;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        private static PyTuple CreateExceptionArgs(PyException exception)
        {
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

            return exception.Value switch
            {
                PyNone => PyTuple.Empty,
                _ => PyTuple.FromOwnedArray([exception.Value])
            };
        }
    }

    internal static class DecimalMembers
    {
        public static bool TryGetMember(PyDecimal decimalValue, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
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
                }, LythonCallableSignature.Create("Decimal.quantize", ["exp", "rounding", "context"], requiredCount: 1)),
                "normalize" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.Unary("normalize", decimalValue, arguments, span), decimalValue, context, span), LythonCallableSignature.Create("Decimal.normalize", ["context"], requiredCount: 0)),
                "sqrt" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.Unary("sqrt", decimalValue, arguments, span), decimalValue, context, span), LythonCallableSignature.Create("Decimal.sqrt", ["context"], requiredCount: 0)),
                "exp" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.Unary("exp", decimalValue, arguments, span), decimalValue, context, span), LythonCallableSignature.Create("Decimal.exp", ["context"], requiredCount: 0)),
                "ln" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.Unary("ln", decimalValue, arguments, span), decimalValue, context, span), LythonCallableSignature.Create("Decimal.ln", ["context"], requiredCount: 0)),
                "log10" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.Unary("log10", decimalValue, arguments, span), decimalValue, context, span), LythonCallableSignature.Create("Decimal.log10", ["context"], requiredCount: 0)),
                "copy_abs" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.Unary("copy_abs", decimalValue, arguments, span), decimalValue, context, span)),
                "copy_negate" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.Unary("copy_negate", decimalValue, arguments, span), decimalValue, context, span)),
                "copy_sign" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.CopySign(decimalValue, arguments, span), decimalValue, context, span), "Decimal.copy_sign", ["other"]),
                "to_integral_value" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.ToIntegral(decimalValue, arguments, context.DecimalContext, span), decimalValue, context, span), LythonCallableSignature.Create("Decimal.to_integral_value", ["rounding", "context"], requiredCount: 0)),
                "to_integral_exact" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.ToIntegral(decimalValue, arguments, context.DecimalContext, span), decimalValue, context, span), LythonCallableSignature.Create("Decimal.to_integral_exact", ["rounding", "context"], requiredCount: 0)),
                "to_integral" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.ToIntegral(decimalValue, arguments, context.DecimalContext, span), decimalValue, context, span), LythonCallableSignature.Create("Decimal.to_integral", ["rounding", "context"], requiredCount: 0)),
                "as_tuple" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.as_tuple() expects no arguments.", span);
                    }

                    return OwnDecimalValue(PyDecimalOps.AsTuple(decimalValue, context.MemoryGovernor, span), context, span);
                }, "Decimal.as_tuple", []),
                "adjusted" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.adjusted() expects no arguments.", span);
                    }

                    return PyDecimalOps.Adjusted(decimalValue);
                }, "Decimal.adjusted", []),
                "compare" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.CompareValue(decimalValue, arguments, span), decimalValue, context, span), LythonCallableSignature.Create("Decimal.compare", ["other", "context"], requiredCount: 1)),
                "compare_total" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.CompareTotal(decimalValue, arguments, span), decimalValue, context, span), "Decimal.compare_total", ["other"]),
                "is_nan" => BoundCallable.Create((arguments, span, _) => ExpectDecimalNoArguments("is_nan", arguments, span, false), "Decimal.is_nan", []),
                "is_infinite" => BoundCallable.Create((arguments, span, _) => ExpectDecimalNoArguments("is_infinite", arguments, span, false), "Decimal.is_infinite", []),
                "is_finite" => BoundCallable.Create((arguments, span, _) => ExpectDecimalNoArguments("is_finite", arguments, span, true), "Decimal.is_finite", []),
                "is_zero" => BoundCallable.Create((arguments, span, _) => ExpectDecimalNoArguments("is_zero", arguments, span, decimalValue.Value == 0m), "Decimal.is_zero", []),
                "is_signed" => BoundCallable.Create((arguments, span, _) => ExpectDecimalNoArguments("is_signed", arguments, span, decimalValue.IsSigned), "Decimal.is_signed", []),
                "to_eng_string" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.to_eng_string() expects no arguments.", span);
                    }

                    return PyDecimalOps.ToEngineeringString(decimalValue, context.MemoryGovernor, span);
                }, "Decimal.to_eng_string", []),
                "scaleb" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.ScaleB(decimalValue, arguments, span), decimalValue, context, span), LythonCallableSignature.Create("Decimal.scaleb", ["other", "context"], requiredCount: 1)),
                "shift" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.Shift(decimalValue, arguments, span), decimalValue, context, span), "Decimal.shift", ["other"]),
                "rotate" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.Rotate(decimalValue, arguments, span), decimalValue, context, span), "Decimal.rotate", ["other"]),
                "same_quantum" => BoundCallable.Create((arguments, span, _) => PyDecimalOps.SameQuantum(decimalValue, arguments, span), "Decimal.same_quantum", ["other"]),
                "remainder_near" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.RemainderNear(decimalValue, arguments, span), decimalValue, context, span), LythonCallableSignature.Create("Decimal.remainder_near", ["other", "context"], requiredCount: 1)),
                "min" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.MinMax(decimalValue, arguments, "min", span), decimalValue, context, span), LythonCallableSignature.Create("Decimal.min", ["other", "context"], requiredCount: 1)),
                "max" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.MinMax(decimalValue, arguments, "max", span), decimalValue, context, span), LythonCallableSignature.Create("Decimal.max", ["other", "context"], requiredCount: 1)),
                "min_mag" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.MinMax(decimalValue, arguments, "min_mag", span), decimalValue, context, span), LythonCallableSignature.Create("Decimal.min_mag", ["other", "context"], requiredCount: 1)),
                "max_mag" => BoundCallable.Create((arguments, span, context) => OwnFreshDecimal(PyDecimalOps.MinMax(decimalValue, arguments, "max_mag", span), decimalValue, context, span), LythonCallableSignature.Create("Decimal.max_mag", ["other", "context"], requiredCount: 1)),
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
                "name" => OwnMethodResult(PyString.FromString(PathOps.BaseName(path.Value.AsString())), path.Value, governor, span),
                "suffix" => OwnMethodResult(PyString.FromString(PathOps.Suffix(path.Value.AsString())), path.Value, governor, span),
                "stem" => OwnMethodResult(PyString.FromString(PathOps.Stem(path.Value.AsString())), path.Value, governor, span),
                "parent" => OwnPathResult(PathOps.Parent(path.Value), path.Value, governor, span),
                "suffixes" => GovernedSuffixes(path.Value, governor, span),
                "parents" => PathOps.Parents(path.Value, context.MemoryGovernor, span),
                "parts" => PathOps.Parts(path.Value, context.MemoryGovernor, span),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static PyList GovernedSuffixes(PyString path, MemoryGovernor governor, LythonSourceSpan span)
        {
            var name = PathOps.BaseName(path.AsString());
            var values = new List<object>();
            var dot = name.IndexOf('.', name.StartsWith(".", StringComparison.Ordinal) ? 1 : 0);
            while (dot >= 0 && dot < name.Length - 1)
            {
                var next = name.IndexOf('.', dot + 1);
                var suffix = next < 0 ? name[dot..] : name[dot..next];
                values.Add(OwnMethodResult(PyString.FromString(suffix), path, governor, span));
                dot = next;
            }

            return new PyList(values, governor, span);
        }
    }
}
