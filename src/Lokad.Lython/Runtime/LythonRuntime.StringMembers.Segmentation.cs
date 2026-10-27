using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static partial class StringMembers
    {
        internal static PyString StripWhitespace(PyString text, LythonSourceSpan span, ExecutionContext context)
        {
            using var ambient = PyStructuralGuard.PushAmbient(context, span);
            return OwnMethodResult(PyStringOps.Strip(text), text, context.MemoryGovernor, span);
        }

        internal static PyList Split(PyString text, object? separatorArgument, object? maxSplitArgument,
            LythonSourceSpan span, ExecutionContext context)
        {
            var count = maxSplitArgument is null ? -1 : NormalizeSplitCount(
                maxSplitArgument is PyInstance
                    ? CoerceTypeIndexAsync(maxSplitArgument, context, span, false).GetAwaiter().GetResult() ?? PyNone.Instance
                    : maxSplitArgument, context, span);
            return SplitWithCount(text, separatorArgument, count, span, context);
        }

        internal static ValueTask<object> SplitAsync(PyString text, object? separatorArgument, object? maxSplitArgument,
            LythonSourceSpan span, ExecutionContext context)
            => maxSplitArgument is PyInstance instance
                ? SplitWithGuestCountAsync(text, separatorArgument, instance, span, context)
                : new ValueTask<object>(Split(text, separatorArgument, maxSplitArgument, span, context));

        private static async ValueTask<object> SplitWithGuestCountAsync(PyString text, object? separatorArgument,
            PyInstance count, LythonSourceSpan span, ExecutionContext context)
        {
            var indexed = await CoerceTypeIndexAsync(count, context, span, true).ConfigureAwait(false);
            return SplitWithCount(text, separatorArgument, NormalizeSplitCount(indexed ?? PyNone.Instance, context, span), span, context);
        }

        private static int NormalizeSplitCount(object value, ExecutionContext context, LythonSourceSpan span)
        {
            if (!PyNumberOps.TryAsInteger(value, out var integer))
                throw new LythonRuntimeException("TypeError", "'" + RuntimeErrors.DatetimeQualifiedTypeName(value, context) + "' object cannot be interpreted as an integer", span);
            if (integer > long.MaxValue || integer < long.MinValue)
                throw new LythonRuntimeException("OverflowError", "Python int too large to convert to C ssize_t", span);
            // Only actual subject parts allocate storage. Larger representable
            // counts are equivalent to the managed kernel's maximum count.
            return integer.Sign < 0 ? -1 : integer > int.MaxValue ? int.MaxValue : (int)integer;
        }

        private static PyList SplitWithCount(PyString text, object? separatorArgument, int count,
            LythonSourceSpan span, ExecutionContext context)
        {
            // Null is internal omission; Python None selects whitespace splitting.
            if (separatorArgument is null or PyNone)
            {
                return PyStringOps.SplitWhitespace(text, count, context.MemoryGovernor, span);
            }

            if (!PyStringOps.TryAsString(separatorArgument, out var separator))
                throw new LythonRuntimeException("TypeError", "must be str or None, not " + RuntimeErrors.OperandTypeName(separatorArgument), span);

            try
            {
                return PyStringOps.Split(text, separator, count, context.MemoryGovernor, span);
            }
            catch (InvalidOperationException ex)
            {
                throw new LythonRuntimeException("ValueError", ex.Message, span);
            }
        }

        private sealed class StringSegmentationMemberProvider : IStringMemberProvider
        {
            // N17: hot fixed signatures hoisted per family.
        private static readonly LythonCallableSignature StrStripSignature = LythonCallableSignature.Create(
            "str.strip", ["chars"], requiredCount: 0, maximumPositionalArgumentCount: 1,
            variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
        private static readonly LythonCallableSignature StrLstripSignature = LythonCallableSignature.Create("str.lstrip", ["chars"], 0);
        private static readonly LythonCallableSignature StrRstripSignature = LythonCallableSignature.Create("str.rstrip", ["chars"], 0);
            private static readonly LythonCallableSignature StrSplitSignature = LythonCallableSignature.Create("str.split", ["sep", "maxsplit"], 0);
            private static readonly LythonCallableSignature StrRSplitSignature = LythonCallableSignature.Create("str.rsplit", ["sep", "maxsplit"], 0);
            private static readonly LythonCallableSignature StrExpandTabsSignature = LythonCallableSignature.Create("str.expandtabs", ["tabsize"], 0);
            public static readonly StringSegmentationMemberProvider Instance = new();

            public bool TryGetMember(PyString text, string name, [MaybeNullWhen(false)] out object value)
            {
                value = name switch
                {
                    "split" => BoundCallable.Create((arguments, span, context) =>
                    {
                        return Split(text, arguments.Length >= 1 ? arguments[0] : null,
                            arguments.Length == 2 ? arguments[1] : null, span, context);
                    }, StrSplitSignature, (arguments, span, context) => SplitAsync(text,
                        arguments.Length >= 1 ? arguments[0] : null, arguments.Length == 2 ? arguments[1] : null, span, context)),
                    "rsplit" => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length == 0)
                        {
                            return PyStringOps.RSplitWhitespace(text, -1, context.MemoryGovernor, span);
                        }

                        int maxSplit;
                        if (arguments[0] is PyNone)
                        {
                            maxSplit = arguments.Length == 2 ? NormalizeSequenceCount(CoerceIoIntegerAsync(arguments[1], span, context, false).GetAwaiter().GetResult()) : -1;
                            return PyStringOps.RSplitWhitespace(text, maxSplit, context.MemoryGovernor, span);
                        }

                        if (arguments.Length > 2)
                        {
                            throw new LythonRuntimeException("TypeError", "str.rsplit([sep[, maxsplit]]) expects zero, one, or two arguments with string separator and optional integer maxsplit.", span);
                        }

                        if (!PyStringOps.TryAsString(arguments[0], out var separator))
                        {
                            throw new LythonRuntimeException("TypeError", "must be str or None, not " + RuntimeErrors.OperandTypeName(arguments[0]), span);
                        }

                        maxSplit = arguments.Length == 2 ? NormalizeSequenceCount(CoerceIoIntegerAsync(arguments[1], span, context, false).GetAwaiter().GetResult()) : -1;
                        try
                        {
                            return PyStringOps.RSplit(text, separator, maxSplit, context.MemoryGovernor, span);
                        }
                        catch (InvalidOperationException ex)
                        {
                            throw new LythonRuntimeException("ValueError", ex.Message, span);
                        }
                    }, StrRSplitSignature),
                    "splitlines" => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length > 1)
                        {
                            throw new LythonRuntimeException("TypeError", "str.splitlines([keepends]) expects zero or one bool argument.", span);
                        }

                        var keepEnds = arguments.Length == 1 && IsTruthy(arguments[0]);
                        return PyStringOps.SplitLines(text, keepEnds, context.MemoryGovernor, span);
                    }, LythonCallableSignature.Create("str.splitlines", ["keepends"], requiredCount: 0, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 0)),
                    "expandtabs" => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length > 1)
                        {
                            throw new LythonRuntimeException("TypeError", "str.expandtabs([tabsize]) expects zero or one integer argument.", span);
                        }

                        var tabSize = arguments.Length == 1 ? RuntimeArgumentValidation.CoerceIndexInt32Async(arguments[0], context, span, false).GetAwaiter().GetResult() : 8;
                        return PyStringOps.ExpandTabs(text, tabSize, context, span);
                    }, StrExpandTabsSignature),
                    "strip" => CreateStripMethod(text, name, PyStringOps.Strip, StrStripSignature),
                    "lstrip" => CreateStripMethod(text, name, PyStringOps.LStrip, StrLstripSignature),
                    "rstrip" => CreateStripMethod(text, name, PyStringOps.RStrip, StrRstripSignature),
                    _ => MissingMemberValue.Instance,
                };

                return !ReferenceEquals(value, MissingMemberValue.Instance);

                static BoundCallable CreateStripMethod(PyString target, string methodName, Func<PyString, PyString> whitespaceOperation, LythonCallableSignature signature)
                    => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length > 1)
                        {
                            throw new LythonRuntimeException("TypeError", $"str.{methodName}([chars]) expects zero or one string argument.", span);
                        }

                        using var ambient = PyStructuralGuard.PushAmbient(context, span);
                        PyString result;
                        if (arguments.Length == 0 || ReferenceEquals(arguments[0], PyNone.Instance))
                        {
                            result = whitespaceOperation(target);
                        }
                        else if (!PyStringOps.TryAsString(arguments[0], out var chars))
                        {
                            throw new LythonRuntimeException("TypeError", $"{methodName} arg must be None or str", span);
                        }
                        else
                        {
                            // UTF-8 byte count bounds scalar count. Fund bucket/entry
                            // capacity and old/new growth arrays before constructing
                            // the set, through final result ownership. Duplicate-heavy
                            // sets deliberately reserve more than their distinct runes.
                            using var working = context.MemoryGovernor.ReserveTemporary(
                                chars.Utf8Bytes.IsEmpty ? 0 : checked(256L + 64L * chars.Utf8Bytes.Length), span);
                            result = methodName switch
                            {
                                "strip" => PyStringOps.Strip(target, chars),
                                "lstrip" => PyStringOps.LStrip(target, chars),
                                _ => PyStringOps.RStrip(target, chars),
                            };
                            return OwnMethodResult(result,target,context.MemoryGovernor,span);
                        }

                        return OwnMethodResult(result,target,context.MemoryGovernor,span);
                    }, signature);
            }
        }
    }
}
