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
                return PyStringOps.SplitWhitespace(text, count, context.MemoryGovernor, span, context);
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

        private static readonly ReceiverMethod RSplitMethod = new(
            LythonCallableSignature.Create("str.rsplit", ["sep", "maxsplit"], 0),
            static (receiver, arguments, span, context) => RSplit((PyString)receiver, arguments, span, context, false).GetAwaiter().GetResult(),
            static (receiver, arguments, span, context) => RSplit((PyString)receiver, arguments, span, context, true));

        private static readonly ReceiverMethod SplitLinesMethod = new(
            LythonCallableSignature.Create("str.splitlines", ["keepends"], 0, 1, LythonVariadicParameters.None, 0),
            static (receiver, arguments, span, context) => PyStringOps.SplitLines((PyString)receiver,
                arguments.Length == 1 && IsTruthy(arguments.Span[0]!, context, span), context.MemoryGovernor, span),
            static (receiver, arguments, span, context) => arguments.Length == 1 && arguments.Span[0] is PyInstance
                ? SplitLinesWithGuestTruthAsync((PyString)receiver, arguments.Span[0]!, span, context)
                : new(PyStringOps.SplitLines((PyString)receiver, arguments.Length == 1 && IsTruthy(arguments.Span[0]!), context.MemoryGovernor, span)));

        private static readonly ReceiverMethod ExpandTabsMethod = new(
            LythonCallableSignature.Create("str.expandtabs", ["tabsize"], 0),
            static (receiver, arguments, span, context) => PyStringOps.ExpandTabs((PyString)receiver,
                arguments.Length == 1 ? RuntimeArgumentValidation.CoerceIndexInt32Async(arguments.Span[0]!, context, span, false).GetAwaiter().GetResult() : 8, context, span),
            static (receiver, arguments, span, context) => arguments.Length == 1 && arguments.Span[0] is PyInstance
                ? ExpandTabsWithGuestWidthAsync((PyString)receiver, arguments.Span[0]!, span, context)
                : new(PyStringOps.ExpandTabs((PyString)receiver,
                    arguments.Length == 1 ? RuntimeArgumentValidation.CoerceIndexInt32Async(arguments.Span[0]!, context, span, false).GetAwaiter().GetResult() : 8, context, span)));

        private static readonly ReceiverMethod StripMethod = CreateStripMethod("strip", PyStringOps.Strip,
            LythonCallableSignature.Create("str.strip", ["chars"], 0, 1, LythonVariadicParameters.None, 1));
        private static readonly ReceiverMethod LStripMethod = CreateStripMethod("lstrip", PyStringOps.LStrip,
            LythonCallableSignature.Create("str.lstrip", ["chars"], 0));
        private static readonly ReceiverMethod RStripMethod = CreateStripMethod("rstrip", PyStringOps.RStrip,
            LythonCallableSignature.Create("str.rstrip", ["chars"], 0));

        private static ReceiverMethod? GetSegmentationMethod(string name) => name switch
        {
            "split" => ReceiverMethods.StringSplit,
            "rsplit" => RSplitMethod,
            "splitlines" => SplitLinesMethod,
            "expandtabs" => ExpandTabsMethod,
            "strip" => StripMethod,
            "lstrip" => LStripMethod,
            "rstrip" => RStripMethod,
            _ => null,
        };

        private static ValueTask<object> RSplit(PyString text, ReadOnlyMemory<object?> arguments,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            PyString? separator = null;
            if (arguments.Length != 0 && arguments.Span[0] is not PyNone &&
                !PyStringOps.TryAsString(arguments.Span[0]!, out separator))
                throw RuntimeErrors.Type("must be str or None, not " + RuntimeErrors.OperandTypeName(arguments.Span[0]!), span);
            if (arguments.Length == 2 && arguments.Span[1] is PyInstance)
                return RSplitWithGuestCountAsync(text, separator, arguments.Span[1]!, span, context, asynchronous);
            var count = arguments.Length == 2 ? NormalizeSplitCount(arguments.Span[1]!, context, span) : -1;
            return new(RSplitWithCount(text, separator, count, span, context));
        }

        private static async ValueTask<object> RSplitWithGuestCountAsync(PyString text, PyString? separator,
            object count, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var indexed = await CoerceTypeIndexAsync(count, context, span, asynchronous).ConfigureAwait(false);
            return RSplitWithCount(text, separator, NormalizeSplitCount(indexed ?? PyNone.Instance, context, span), span, context);
        }

        private static PyList RSplitWithCount(PyString text, PyString? separator, int count,
            LythonSourceSpan span, ExecutionContext context)
        {
            if (separator is null) return PyStringOps.RSplitWhitespace(text, count, context.MemoryGovernor, span, context);
            try { return PyStringOps.RSplit(text, separator, count, context.MemoryGovernor, span); }
            catch (InvalidOperationException error) { throw new LythonRuntimeException("ValueError", error.Message, span); }
        }

        private static async ValueTask<object> SplitLinesWithGuestTruthAsync(PyString text, object keepEnds,
            LythonSourceSpan span, ExecutionContext context)
            => PyStringOps.SplitLines(text, await IsTruthyAsync(keepEnds, context, span).ConfigureAwait(false), context.MemoryGovernor, span);

        private static async ValueTask<object> ExpandTabsWithGuestWidthAsync(PyString text, object width,
            LythonSourceSpan span, ExecutionContext context)
            => PyStringOps.ExpandTabs(text, await RuntimeArgumentValidation.CoerceIndexInt32Async(width, context, span, true).ConfigureAwait(false), context, span);

        // These declarations are created once. Only a Python-visible attribute
        // allocates a receiver wrapper; immediate calls borrow existing operands.
        private static ReceiverMethod CreateStripMethod(string methodName, Func<PyString, PyString> whitespaceOperation,
            LythonCallableSignature signature)
            => new(signature, (receiver, arguments, span, context) =>
            {
                var text = (PyString)receiver;
                using var ambient = PyStructuralGuard.PushAmbient(context, span);
                PyString result;
                if (arguments.Length == 0 || arguments.Span[0] is PyNone)
                    result = whitespaceOperation(text);
                else if (!PyStringOps.TryAsString(arguments.Span[0]!, out var chars))
                    throw RuntimeErrors.Type($"{methodName} arg must be None or str", span);
                else
                {
                    using var working = context.MemoryGovernor.ReserveTemporary(
                        chars.Utf8Span.IsEmpty ? 0 : checked(256L + 64L * chars.ByteLength), span);
                    result = methodName switch
                    {
                        "strip" => PyStringOps.Strip(text, chars),
                        "lstrip" => PyStringOps.LStrip(text, chars),
                        _ => PyStringOps.RStrip(text, chars),
                    };
                    return OwnMethodResult(result, text, context.MemoryGovernor, span);
                }
                return OwnMethodResult(result, text, context.MemoryGovernor, span);
            });
    }
}
