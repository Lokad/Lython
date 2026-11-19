using System.Globalization;
using System.Numerics;
using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static partial class StringMembers
    {
        // N17: hot fixed signatures hoisted per family (see ListMembers).
        private static readonly LythonCallableSignature StrContainsSignature = LythonCallableSignature.Create("str.__contains__", ["item"]);
        private static readonly LythonCallableSignature StrEqSignature = LythonCallableSignature.Create("str.__eq__", ["value"]);
        private static readonly LythonCallableSignature StrNeSignature = LythonCallableSignature.Create("str.__ne__", ["value"]);
        private static readonly LythonCallableSignature StrLtSignature = LythonCallableSignature.Create("str.__lt__", ["value"]);
        private static readonly LythonCallableSignature StrLeSignature = LythonCallableSignature.Create("str.__le__", ["value"]);
        private static readonly LythonCallableSignature StrGtSignature = LythonCallableSignature.Create("str.__gt__", ["value"]);
        private static readonly LythonCallableSignature StrGeSignature = LythonCallableSignature.Create("str.__ge__", ["value"]);
        private static readonly LythonCallableSignature StrHashSignature = LythonCallableSignature.Create("str.__hash__");
        private static readonly LythonCallableSignature StrGetItemSignature = LythonCallableSignature.Create("str.__getitem__", ["index"]);
        private static readonly LythonCallableSignature StrAddSignature = LythonCallableSignature.Create("str.__add__", ["value"]);
        private static readonly LythonCallableSignature StrMulSignature = LythonCallableSignature.Create("str.__mul__", ["value"]);
        private static readonly LythonCallableSignature StrRMulSignature = LythonCallableSignature.Create("str.__rmul__", ["value"]);
        private readonly record struct StringSearchBounds(int StartByte, int EndByte, bool CanMatch);

        private static readonly IStringMemberProvider[] Providers =
        [
            StringAffixFormatMemberProvider.Instance,
            StringTranslateMemberProvider.Instance,
        ];

        private interface IStringMemberProvider
        {
            /// <summary>Resolves members owned by one cohesive string-operation family.</summary>
            bool TryGetMember(PyString text, string name, [MaybeNullWhen(false)] out object value);
        }

        public static bool TryGetMember(PyString text, string name, [MaybeNullWhen(false)] out object value)
        {
            // maketrans is a staticmethod shape (no receiver), so it rides
            // the shared singleton instead of a text-bound provider.
            if (name == "__iter__")
            {
                value = BoundCallable.CreateNoArguments(text, "str.__iter__", static (receiver, span, context) =>
                {
                    PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
                    var strIterResult = new PyEnumerableIterator(receiver, span, context);
                    return strIterResult;
                });
                return true;
            }

            if (name == "__len__")
            {
                value = BoundCallable.CreateNoArguments(text, "str.__len__", static (receiver, span, context) => Len([receiver], span, context));
                return true;
            }

            if (name == "__contains__")
            {
                value = BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "str.__contains__(item) expects one argument.", span);
                    }

                    return PyContainment.Contains(text, arguments[0], span);
                }, StrContainsSignature);
                return true;
            }
            if (name == "__eq__")
            {
                value = BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "str.__eq__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyString other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return text.Equals(other, context, span);
                }, StrEqSignature);
                return true;
            }

            if (name == "__ne__")
            {
                value = BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "str.__ne__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyString other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return !text.Equals(other, context, span);
                }, StrNeSignature);
                return true;
            }

            if (name == "__lt__")
            {
                value = BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "str.__lt__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyString other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return PyString.CompareOrdinal(text, other, context, span) < 0;
                }, StrLtSignature);
                return true;
            }

            if (name == "__le__")
            {
                value = BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "str.__le__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyString other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return PyString.CompareOrdinal(text, other, context, span) <= 0;
                }, StrLeSignature);
                return true;
            }

            if (name == "__gt__")
            {
                value = BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "str.__gt__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyString other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return PyString.CompareOrdinal(text, other, context, span) > 0;
                }, StrGtSignature);
                return true;
            }

            if (name == "__ge__")
            {
                value = BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "str.__ge__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyString other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return PyString.CompareOrdinal(text, other, context, span) >= 0;
                }, StrGeSignature);
                return true;
            }

            if (name == "__hash__")
            {
                value = BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "str.__hash__() expects no arguments.", span);
                    }

                    return ComputeBuiltinHash(text, span, context);
                }, StrHashSignature);
                return true;
            }

            if (name == "__getitem__")
            {
                value = BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "str.__getitem__(index) expects one argument.", span);
                    }

                    return ReadSubscriptValue(text, arguments[0], span, context);
                }, StrGetItemSignature);
                return true;
            }

            if (name == "__add__")
            {
                value = BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "str.__add__(value) expects one argument.", span);
                    }

                    return EvaluateAdd(text, arguments[0], context, span);
                }, StrAddSignature);
                return true;
            }

            if (name == "__mul__")
            {
                value = BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "str.__mul__(value) expects one argument.", span);
                    }

                    RequireRepeatCount(arguments[0], context, span);

                    return EvaluateMultiply(text, arguments[0], context, span);
                }, StrMulSignature, (arguments, span, context) =>
                {
                    RequireRepeatCount(arguments[0], context, span);
                    return EvaluateMultiplyAsync(text, arguments[0], context, span);
                });
                return true;
            }

            if (name == "__rmul__")
            {
                value = BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "str.__rmul__(value) expects one argument.", span);
                    }

                    RequireRepeatCount(arguments[0], context, span);

                    return EvaluateMultiply(arguments[0], text, context, span);
                }, StrRMulSignature, (arguments, span, context) =>
                {
                    RequireRepeatCount(arguments[0], context, span);
                    return EvaluateMultiplyAsync(arguments[0], text, context, span);
                });
                return true;
            }

            if (name == "maketrans")
            {
                value = BuiltinTypeMethod.StrMaketrans;
                return true;
            }

            if (GetMethod(name) is { } method)
            {
                value = method.Bind(text);
                return true;
            }

            foreach (var provider in Providers)
            {
                if (provider.TryGetMember(text, name, out value))
                {
                    return true;
                }
            }

            value = null;
            return false;
        }
        internal static ValueTask<object> InvokeStringAffix(
            PyString text, ReceiverArguments arguments, LythonSourceSpan span, ExecutionContext context,
            bool isStart, bool asynchronous)
        {
            if (arguments.Length is < 1 or > 3)
                throw RuntimeErrors.Type(isStart
                    ? "str.startswith(prefix[, start[, end]]) expects a string or tuple of strings, plus optional integer bounds."
                    : "str.endswith(suffix[, start[, end]]) expects a string or tuple of strings, plus optional integer bounds.", span);

            // Ordinary bounds never enter an async state machine. Only guest
            // hooks can suspend; retain the same receiver/arguments through them.
            if ((arguments.Length >= 2 && arguments[1] is PyInstance) ||
                (arguments.Length == 3 && arguments[2] is PyInstance))
                return InvokeStringAffixWithGuestBoundsAsync(text, arguments, span, context, isStart, asynchronous);

            var bounds = NormalizeStringBounds(text,
                arguments.Length >= 2 ? arguments[1] : null,
                arguments.Length == 3 ? arguments[2] : null, span);
            return new(StartsOrEndsWith(text, arguments[0]!, bounds.StartByte, bounds.EndByte, bounds.CanMatch, isStart, span));
        }

        private static async ValueTask<object> InvokeStringAffixWithGuestBoundsAsync(
            PyString text, ReceiverArguments arguments, LythonSourceSpan span, ExecutionContext context,
            bool isStart, bool asynchronous)
        {
            var start = await CoerceTypeIndexAsync(arguments[1]!, context, span, asynchronous).ConfigureAwait(false);
            // A rejected start must prevent the end hook, including descriptor
            // binding, rather than validating only after both conversions.
            _ = PyIndexing.NormalizeSliceBound(start, span);
            var end = arguments.Length == 3
                ? await CoerceTypeIndexAsync(arguments[2]!, context, span, asynchronous).ConfigureAwait(false)
                : null;
            var bounds = NormalizeStringBounds(text, start, end, span);
            return StartsOrEndsWith(text, arguments[0]!, bounds.StartByte, bounds.EndByte, bounds.CanMatch, isStart, span);
        }

        private static StringSearchBounds ParseStringBounds(
            PyString text,
            ReceiverArguments arguments,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            // Search bounds coerce through __index__ like CPython; bad
            // __index__ results propagate while other rejections stay Invalid.
            object? start = arguments.Length >= 2 ? arguments[1] : null;
            object? end = arguments.Length == 3 ? arguments[2] : null;
            if (start is not null)
            {
                start = CoerceIndexProtocol(start, context, span);
                _ = PyIndexing.NormalizeSliceBound(start, span);
            }

            if (end is not null)
            {
                end = CoerceIndexProtocol(end, context, span);
            }

            return NormalizeStringBounds(text, start, end, span);
        }

        private static StringSearchBounds NormalizeStringBounds(PyString text, object? start, object? end, LythonSourceSpan span)
        {
            try
            {
                if ((start is null or PyNone) && (end is null or PyNone))
                    return new(0, text.ByteLength, true);

                var textLength = text.Length;
                var normalized = PyStringOps.NormalizeRange(textLength, start, end);
                var startBeyondLength = start switch
                {
                    BigInteger integer => integer > textLength,
                    int integer => integer > textLength,
                    bool flag => flag && textLength == 0,
                    _ => false
                };
                return startBeyondLength || normalized.End < normalized.Start
                    ? new(0, 0, false)
                    : new(text.GetByteIndexForRuneBoundary(normalized.Start),
                        text.GetByteIndexForRuneBoundary(normalized.End), true);
            }
            catch (InvalidOperationException)
            {
                throw new LythonRuntimeException("TypeError", "slice indices must be integers or None or have an __index__ method", span);
            }
        }

        internal static bool StartsOrEndsWith(
            PyString text,
            object prefixOrTuple,
            int startByte,
            int endByte,
            bool canMatch,
            bool isStart,
            LythonSourceSpan span)
        {
            // An impossible range still validates the affix and tuple entries.
            // Only a real earlier match can skip a later invalid tuple item.
            if (PyStringOps.TryAsString(prefixOrTuple, out var single))
            {
                return canMatch &&
                    (isStart ? PyStringOps.StartsWith(text, single, startByte, endByte) : PyStringOps.EndsWith(text, single, startByte, endByte));
            }

            if (prefixOrTuple is not PyTuple tuple)
            {
                throw new LythonRuntimeException("TypeError",
                    isStart
                        ? "startswith first arg must be str or a tuple of str, not " + RuntimeErrors.OperandTypeName(prefixOrTuple)
                        : "endswith first arg must be str or a tuple of str, not " + RuntimeErrors.OperandTypeName(prefixOrTuple),
                    span);
            }

            foreach (var item in tuple)
            {
                if (!PyStringOps.TryAsString(item, out var textItem))
                {
                    throw new LythonRuntimeException("TypeError",
                        isStart
                            ? "tuple for startswith must only contain str, not " + RuntimeErrors.OperandTypeName(item)
                            : "tuple for endswith must only contain str, not " + RuntimeErrors.OperandTypeName(item),
                        span);
                }

                if (canMatch &&
                    (isStart ? PyStringOps.StartsWith(text, textItem, startByte, endByte) : PyStringOps.EndsWith(text, textItem, startByte, endByte)))
                {
                    return true;
                }
            }

            return false;
        }

        private static PyString? RequireFillChar(object value, LythonSourceSpan span)
        {
            if (!PyStringOps.TryAsString(value, out var fill))
            {
                throw new LythonRuntimeException("TypeError", "The fill character must be a unicode character, not " + RuntimeErrors.OperandTypeName(value), span);
            }

            return fill;
        }

        private static string TypeName(object value)
        {
            return value switch
            {
                bool => "bool",
                _ when Numbers.PyNumberOps.IsInteger(value) => "int",
                PyString => "str",
                PyTuple => "tuple",
                PyList => "list",
                PyDict => "dict",
                PyNone => "NoneType",
                _ => value.GetType().Name
            };
        }
    }
}
