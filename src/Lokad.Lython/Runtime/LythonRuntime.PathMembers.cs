using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static class PathStatMembers
    {
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
                    : new PyTuple([exception.Value]);
            }

            if (string.Equals(exception.TypeName, "JSONDecodeError", StringComparison.Ordinal) &&
                exception.Value is PyDict payload &&
                payload.TryGetValue(PyString.FromString("msg"), out var msg) &&
                payload.TryGetValue(PyString.FromString("doc"), out var doc) &&
                payload.TryGetValue(PyString.FromString("pos"), out var pos))
            {
                return new PyTuple([msg, doc, pos]);
            }

            if (string.Equals(exception.TypeName, "CalledProcessError", StringComparison.Ordinal) &&
                exception.Value is PyDict subprocessPayload &&
                subprocessPayload.TryGetValue(PyString.FromString("returncode"), out var returnCode) &&
                subprocessPayload.TryGetValue(PyString.FromString("cmd"), out var command))
            {
                return new PyTuple([returnCode, command]);
            }

            if (string.Equals(exception.TypeName, "TimeoutExpired", StringComparison.Ordinal) &&
                exception.Value is PyDict timeoutPayload &&
                timeoutPayload.TryGetValue(PyString.FromString("cmd"), out var timeoutCommand) &&
                timeoutPayload.TryGetValue(PyString.FromString("timeout"), out var timeout))
            {
                return new PyTuple([timeoutCommand, timeout]);
            }

            return exception.Value switch
            {
                PyNone => PyTuple.Empty,
                _ => new PyTuple([exception.Value])
            };
        }
    }

    internal static class DecimalMembers
    {
        public static bool TryGetMember(PyDecimal decimalValue, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "quantize" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length is < 1 or > 3 || arguments[0] is not PyDecimal exponent)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.quantize(exp[, rounding][, context]) expects a Decimal exponent plus optional rounding/context.", span);
                    }

                    var rounding = arguments.Length >= 2 ? arguments[1] : PyNone.Instance;
                    var decimalContext = arguments.Length >= 3 && arguments[2] is not PyNone
                        ? arguments[2] as PyDecimalContext ?? throw new LythonRuntimeException("TypeError", "Decimal.quantize(..., context=...) expects a Context or None.", span)
                        : context.DecimalContext;
                    return PyDecimalOps.Quantize(decimalValue, exponent, rounding, decimalContext, span);
                }, new LythonCallableSignature("Decimal.quantize", ["exp", "rounding", "context"], RequiredCount: 1)),
                "normalize" => new BoundCallable((arguments, span, _) => PyDecimalOps.Unary("normalize", decimalValue, arguments, span), new LythonCallableSignature("Decimal.normalize", ["context"], RequiredCount: 0)),
                "sqrt" => new BoundCallable((arguments, span, _) => PyDecimalOps.Unary("sqrt", decimalValue, arguments, span), new LythonCallableSignature("Decimal.sqrt", ["context"], RequiredCount: 0)),
                "exp" => new BoundCallable((arguments, span, _) => PyDecimalOps.Unary("exp", decimalValue, arguments, span), new LythonCallableSignature("Decimal.exp", ["context"], RequiredCount: 0)),
                "ln" => new BoundCallable((arguments, span, _) => PyDecimalOps.Unary("ln", decimalValue, arguments, span), new LythonCallableSignature("Decimal.ln", ["context"], RequiredCount: 0)),
                "log10" => new BoundCallable((arguments, span, _) => PyDecimalOps.Unary("log10", decimalValue, arguments, span), new LythonCallableSignature("Decimal.log10", ["context"], RequiredCount: 0)),
                "copy_abs" => new BoundCallable((arguments, span, _) => PyDecimalOps.Unary("copy_abs", decimalValue, arguments, span)),
                "copy_negate" => new BoundCallable((arguments, span, _) => PyDecimalOps.Unary("copy_negate", decimalValue, arguments, span)),
                "copy_sign" => new BoundCallable((arguments, span, _) => PyDecimalOps.CopySign(decimalValue, arguments, span), "Decimal.copy_sign", ["other"]),
                "to_integral_value" => new BoundCallable((arguments, span, context) => PyDecimalOps.ToIntegral(decimalValue, arguments, context.DecimalContext, span), new LythonCallableSignature("Decimal.to_integral_value", ["rounding", "context"], RequiredCount: 0)),
                "to_integral_exact" => new BoundCallable((arguments, span, context) => PyDecimalOps.ToIntegral(decimalValue, arguments, context.DecimalContext, span), new LythonCallableSignature("Decimal.to_integral_exact", ["rounding", "context"], RequiredCount: 0)),
                "to_integral" => new BoundCallable((arguments, span, context) => PyDecimalOps.ToIntegral(decimalValue, arguments, context.DecimalContext, span), new LythonCallableSignature("Decimal.to_integral", ["rounding", "context"], RequiredCount: 0)),
                "as_tuple" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.as_tuple() expects no arguments.", span);
                    }

                    return PyDecimalOps.AsTuple(decimalValue);
                }, "Decimal.as_tuple", []),
                "adjusted" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.adjusted() expects no arguments.", span);
                    }

                    return PyDecimalOps.Adjusted(decimalValue);
                }, "Decimal.adjusted", []),
                "compare" => new BoundCallable((arguments, span, _) => PyDecimalOps.CompareValue(decimalValue, arguments, span), new LythonCallableSignature("Decimal.compare", ["other", "context"], RequiredCount: 1)),
                "compare_total" => new BoundCallable((arguments, span, _) => PyDecimalOps.CompareTotal(decimalValue, arguments, span), "Decimal.compare_total", ["other"]),
                "is_nan" => new BoundCallable((arguments, span, _) => ExpectDecimalNoArguments("is_nan", arguments, span, false), "Decimal.is_nan", []),
                "is_infinite" => new BoundCallable((arguments, span, _) => ExpectDecimalNoArguments("is_infinite", arguments, span, false), "Decimal.is_infinite", []),
                "is_finite" => new BoundCallable((arguments, span, _) => ExpectDecimalNoArguments("is_finite", arguments, span, true), "Decimal.is_finite", []),
                "is_zero" => new BoundCallable((arguments, span, _) => ExpectDecimalNoArguments("is_zero", arguments, span, decimalValue.Value == 0m), "Decimal.is_zero", []),
                "is_signed" => new BoundCallable((arguments, span, _) => ExpectDecimalNoArguments("is_signed", arguments, span, decimalValue.IsSigned), "Decimal.is_signed", []),
                "to_eng_string" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.to_eng_string() expects no arguments.", span);
                    }

                    return PyDecimalOps.ToEngineeringString(decimalValue);
                }, "Decimal.to_eng_string", []),
                "scaleb" => new BoundCallable((arguments, span, _) => PyDecimalOps.ScaleB(decimalValue, arguments, span), new LythonCallableSignature("Decimal.scaleb", ["other", "context"], RequiredCount: 1)),
                "shift" => new BoundCallable((arguments, span, _) => PyDecimalOps.Shift(decimalValue, arguments, span), "Decimal.shift", ["other"]),
                "rotate" => new BoundCallable((arguments, span, _) => PyDecimalOps.Rotate(decimalValue, arguments, span), "Decimal.rotate", ["other"]),
                "same_quantum" => new BoundCallable((arguments, span, _) => PyDecimalOps.SameQuantum(decimalValue, arguments, span), "Decimal.same_quantum", ["other"]),
                "remainder_near" => new BoundCallable((arguments, span, _) => PyDecimalOps.RemainderNear(decimalValue, arguments, span), new LythonCallableSignature("Decimal.remainder_near", ["other", "context"], RequiredCount: 1)),
                "min" => new BoundCallable((arguments, span, _) => PyDecimalOps.MinMax(decimalValue, arguments, "min", span), new LythonCallableSignature("Decimal.min", ["other", "context"], RequiredCount: 1)),
                "max" => new BoundCallable((arguments, span, _) => PyDecimalOps.MinMax(decimalValue, arguments, "max", span), new LythonCallableSignature("Decimal.max", ["other", "context"], RequiredCount: 1)),
                "min_mag" => new BoundCallable((arguments, span, _) => PyDecimalOps.MinMax(decimalValue, arguments, "min_mag", span), new LythonCallableSignature("Decimal.min_mag", ["other", "context"], RequiredCount: 1)),
                "max_mag" => new BoundCallable((arguments, span, _) => PyDecimalOps.MinMax(decimalValue, arguments, "max_mag", span), new LythonCallableSignature("Decimal.max_mag", ["other", "context"], RequiredCount: 1)),
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
            value = name switch
            {
                "parents" => PathOps.Parents(path.Value, context.MemoryGovernor, span),
                "parts" => PathOps.Parts(path.Value, context.MemoryGovernor, span),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }
}
