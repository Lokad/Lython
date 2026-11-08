using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static partial class StringMembers
    {
        internal static object Join(PyString text, object iterable, LythonSourceSpan span, ExecutionContext context)
        {
            // Consume arbitrary input before item validation so later effects
            // and iteration errors retain Python precedence.
            using var parts = PyIteration.MaterializeLeased(iterable, span, context);
            return JoinStrings(text, EnumerateJoinParts(parts.Items, span), context.MemoryGovernor, span);
        }

        internal static async ValueTask<object> JoinAsync(PyString text, object iterable, LythonSourceSpan span, ExecutionContext context)
        {
            // Keep the drain funded through output admission, awaiting every
            // guest callback and its cleanup before this scope ends.
            using var parts = await PyIteration.MaterializeLeasedAsync(iterable, span, context).ConfigureAwait(false);
            return JoinStrings(text, EnumerateJoinParts(parts.Items, span), context.MemoryGovernor, span);
        }

        private static IEnumerable<PyString> EnumerateJoinParts(IEnumerable<object> parts, LythonSourceSpan span)
        {
            var index = 0;
            foreach (var part in parts)
            {
                if (!PyStringOps.TryAsString(part, out var text))
                    throw new LythonRuntimeException("TypeError", "sequence item " + index + ": expected str instance, " + RuntimeErrors.OperandTypeName(part) + " found", span);
                yield return text;
                index++;
            }
        }

        private static readonly ReceiverMethod CenterMethod = CreatePaddingMethod("center", PyStringOps.Center);
        private static readonly ReceiverMethod LJustMethod = CreatePaddingMethod("ljust", PyStringOps.LJust);
        private static readonly ReceiverMethod RJustMethod = CreatePaddingMethod("rjust", PyStringOps.RJust);
        private static readonly ReceiverMethod ZFillMethod = new(
            LythonCallableSignature.Create("str.zfill", ["width"], 1, 1, LythonVariadicParameters.None, 1),
            static (receiver, arguments, span, context) => ZFill((PyString)receiver, arguments.Span[0]!, span, context, false).GetAwaiter().GetResult(),
            static (receiver, arguments, span, context) => ZFill((PyString)receiver, arguments.Span[0]!, span, context, true));
        private static readonly ReceiverMethod FindMethod = CreateSearchMethod("find", PyStringOps.Find, false);
        private static readonly ReceiverMethod IndexMethod = CreateSearchMethod("index", PyStringOps.Find, true);
        private static readonly ReceiverMethod RFindMethod = CreateSearchMethod("rfind", PyStringOps.RFind, false);
        private static readonly ReceiverMethod RIndexMethod = CreateSearchMethod("rindex", PyStringOps.RFind, true);
        private static readonly ReceiverMethod CountMethod = CreateSearchMethod("count", PyStringOps.Count, false);

        private static ReceiverMethod? GetLayoutSearchMethod(string name) => name switch
        {
            "center" => CenterMethod,
            "ljust" => LJustMethod,
            "rjust" => RJustMethod,
            "zfill" => ZFillMethod,
            "find" => FindMethod,
            "index" => IndexMethod,
            "rfind" => RFindMethod,
            "rindex" => RIndexMethod,
            "count" => CountMethod,
            _ => null,
        };

        private static ReceiverMethod CreatePaddingMethod(string name,
            Func<PyString, int, PyString?, MemoryGovernor, LythonSourceSpan, CancellationToken, PyString> operation)
            => new(LythonCallableSignature.Create("str." + name, ["width", "fillchar"], 1, 2, LythonVariadicParameters.None, 2),
                (receiver, arguments, span, context) => Pad((PyString)receiver, arguments, operation, span, context, false).GetAwaiter().GetResult(),
                (receiver, arguments, span, context) => Pad((PyString)receiver, arguments, operation, span, context, true));

        private static ValueTask<object> Pad(PyString text, ReadOnlyMemory<object?> arguments,
            Func<PyString, int, PyString?, MemoryGovernor, LythonSourceSpan, CancellationToken, PyString> operation,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            if (arguments.Span[0] is PyInstance)
                return PadWithGuestWidthAsync(text, arguments, operation, span, context, asynchronous);
            var width = NormalizeTextWidth(CoerceIoIntegerAsync(arguments.Span[0]!, span, context, false).GetAwaiter().GetResult(), span);
            return new(PadWithWidth(text, width, arguments.Length == 2 ? arguments.Span[1] : null, operation, span, context));
        }

        private static async ValueTask<object> PadWithGuestWidthAsync(PyString text, ReadOnlyMemory<object?> arguments,
            Func<PyString, int, PyString?, MemoryGovernor, LythonSourceSpan, CancellationToken, PyString> operation,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var width = NormalizeTextWidth(await CoerceIoIntegerAsync(arguments.Span[0]!, span, context, asynchronous).ConfigureAwait(false), span);
            return PadWithWidth(text, width, arguments.Length == 2 ? arguments.Span[1] : null, operation, span, context);
        }

        private static object PadWithWidth(PyString text, int width, object? fillArgument,
            Func<PyString, int, PyString?, MemoryGovernor, LythonSourceSpan, CancellationToken, PyString> operation,
            LythonSourceSpan span, ExecutionContext context)
        {
            var fill = fillArgument is null ? null : RequireFillChar(fillArgument, span);
            try { return OwnMethodResult(operation(text, width, fill, context.MemoryGovernor, span, context.State.CancellationToken), text, context.MemoryGovernor, span); }
            catch (InvalidOperationException error) { throw new LythonRuntimeException("TypeError", error.Message, span); }
        }

        private static ValueTask<object> ZFill(PyString text, object width, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
            => width is PyInstance ? ZFillWithGuestWidthAsync(text, width, span, context, asynchronous)
                : new(OwnMethodResult(PyStringOps.ZFill(text,
                    NormalizeTextWidth(CoerceIoIntegerAsync(width, span, context, false).GetAwaiter().GetResult(), span),
                    context.MemoryGovernor, span, context.State.CancellationToken), text, context.MemoryGovernor, span));

        private static async ValueTask<object> ZFillWithGuestWidthAsync(PyString text, object width,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
            => OwnMethodResult(PyStringOps.ZFill(text,
                NormalizeTextWidth(await CoerceIoIntegerAsync(width, span, context, asynchronous).ConfigureAwait(false), span),
                context.MemoryGovernor, span, context.State.CancellationToken), text, context.MemoryGovernor, span);

        private static ReceiverMethod CreateSearchMethod(string name,
            Func<PyString, PyString, int, int, BigInteger> operation, bool throwWhenMissing)
            => new(LythonCallableSignature.Create("str." + name, ["sub", "start", "end"], 1, 3, LythonVariadicParameters.None, 3),
                (receiver, arguments, span, context) => Search((PyString)receiver, arguments, name, operation, throwWhenMissing, span, context, false).GetAwaiter().GetResult(),
                (receiver, arguments, span, context) => Search((PyString)receiver, arguments, name, operation, throwWhenMissing, span, context, true));

        private static ValueTask<object> Search(PyString text, ReadOnlyMemory<object?> arguments, string name,
            Func<PyString, PyString, int, int, BigInteger> operation, bool throwWhenMissing,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            if (!PyStringOps.TryAsString(arguments.Span[0]!, out var needle))
            {
                var needleType = arguments.Span[0] is PyNone ? "None" : RuntimeErrors.OperandTypeName(arguments.Span[0]!);
                throw RuntimeErrors.Type($"{name}() argument 1 must be str, not {needleType}", span);
            }
            if ((arguments.Length >= 2 && arguments.Span[1] is PyInstance) || (arguments.Length == 3 && arguments.Span[2] is PyInstance))
                return SearchWithGuestBoundsAsync(text, needle, arguments, name, operation, throwWhenMissing, span, context, asynchronous);
            return new(SearchWithBounds(text, needle, ParseStringBounds(text, arguments.Span, span, context), name, operation, throwWhenMissing, span));
        }

        private static async ValueTask<object> SearchWithGuestBoundsAsync(PyString text, PyString needle,
            ReadOnlyMemory<object?> arguments, string name, Func<PyString, PyString, int, int, BigInteger> operation,
            bool throwWhenMissing, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var start = await CoerceTypeIndexAsync(arguments.Span[1], context, span, asynchronous).ConfigureAwait(false);
            _ = PyIndexing.NormalizeSliceBound(start, span);
            var end = arguments.Length == 3
                ? await CoerceTypeIndexAsync(arguments.Span[2], context, span, asynchronous).ConfigureAwait(false) : null;
            return SearchWithBounds(text, needle, NormalizeStringBounds(text, start, end, span), name, operation, throwWhenMissing, span);
        }

        private static object SearchWithBounds(PyString text, PyString needle, StringSearchBounds bounds, string name,
            Func<PyString, PyString, int, int, BigInteger> operation, bool throwWhenMissing, LythonSourceSpan span)
        {
            if (!bounds.CanMatch)
            {
                if (name == "count") return BigInteger.Zero;
                if (throwWhenMissing) throw RuntimeErrors.Value("substring not found", span);
                return new BigInteger(-1);
            }
            var result = operation(text, needle, bounds.StartByte, bounds.EndByte);
            if (throwWhenMissing && result < 0) throw RuntimeErrors.Value("substring not found", span);
            return result;
        }
    }
}
