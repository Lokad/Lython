using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static partial class StringMembers
    {
        internal static NativeReturnOperation? GetNoArgumentsKind(string name)
            => name switch
            {
                "strip" => NativeReturnOperation.Strip,
                "lower" => NativeReturnOperation.Lower,
                "upper" => NativeReturnOperation.Upper,
                "islower" => NativeReturnOperation.IsLower,
                "isupper" => NativeReturnOperation.IsUpper,
                "istitle" => NativeReturnOperation.IsTitle,
                "isalpha" => NativeReturnOperation.IsAlpha,
                "isdigit" => NativeReturnOperation.IsDigit,
                "isalnum" => NativeReturnOperation.IsAlnum,
                "isascii" => NativeReturnOperation.IsAscii,
                "isdecimal" => NativeReturnOperation.IsDecimal,
                "isidentifier" => NativeReturnOperation.IsIdentifier,
                "isnumeric" => NativeReturnOperation.IsNumeric,
                "isprintable" => NativeReturnOperation.IsPrintable,
                "isspace" => NativeReturnOperation.IsSpace,
                _ => null
            };

        internal static ReceiverMethod? GetMethod(string name)
            => name switch
            {
                "split" => ReceiverMethods.StringSplit,
                "rsplit" => RSplitMethod,
                "splitlines" => SplitLinesMethod,
                "expandtabs" => ExpandTabsMethod,
                "lstrip" => LStripMethod,
                "rstrip" => RStripMethod,
                "center" => CenterMethod,
                "ljust" => LJustMethod,
                "rjust" => RJustMethod,
                "zfill" => ZFillMethod,
                "find" => FindMethod,
                "index" => IndexMethod,
                "rfind" => RFindMethod,
                "rindex" => RIndexMethod,
                "count" => CountMethod,
                "join" => ReceiverMethods.StringJoin,
                "replace" => ReceiverMethods.StringReplace,
                "startswith" => ReceiverMethods.StringStartsWith,
                "endswith" => ReceiverMethods.StringEndsWith,
                "strip" => GetNoArgumentsMethod(NativeReturnOperation.Strip),
                "lower" => GetNoArgumentsMethod(NativeReturnOperation.Lower),
                "upper" => GetNoArgumentsMethod(NativeReturnOperation.Upper),
                "islower" => GetNoArgumentsMethod(NativeReturnOperation.IsLower),
                "isupper" => GetNoArgumentsMethod(NativeReturnOperation.IsUpper),
                "istitle" => GetNoArgumentsMethod(NativeReturnOperation.IsTitle),
                "isalpha" => GetNoArgumentsMethod(NativeReturnOperation.IsAlpha),
                "isdigit" => GetNoArgumentsMethod(NativeReturnOperation.IsDigit),
                "isalnum" => GetNoArgumentsMethod(NativeReturnOperation.IsAlnum),
                "isascii" => GetNoArgumentsMethod(NativeReturnOperation.IsAscii),
                "isdecimal" => GetNoArgumentsMethod(NativeReturnOperation.IsDecimal),
                "isidentifier" => GetNoArgumentsMethod(NativeReturnOperation.IsIdentifier),
                "isnumeric" => GetNoArgumentsMethod(NativeReturnOperation.IsNumeric),
                "isprintable" => GetNoArgumentsMethod(NativeReturnOperation.IsPrintable),
                "isspace" => GetNoArgumentsMethod(NativeReturnOperation.IsSpace),
                "capitalize" => CasingMethods.Capitalize,
                "swapcase" => CasingMethods.SwapCase,
                "title" => CasingMethods.Title,
                "casefold" => CasingMethods.CaseFold,
                "encode" => EncodeMethod,
                _ => null,
            };

        internal static PyString Replace(PyString receiver, object oldArgument, object newArgument, object? countArgument,
            LythonSourceSpan span, ExecutionContext context)
        {
            if (!PyStringOps.TryAsString(oldArgument, out var oldValue))
            {
                // CPython numbers the bad argument, spelling a None value bare.
                var oldType = oldArgument is PyNone ? "None" : RuntimeErrors.OperandTypeName(oldArgument);
                throw new LythonRuntimeException("TypeError", $"replace() argument 1 must be str, not {oldType}", span);
            }

            if (!PyStringOps.TryAsString(newArgument, out var newValue))
            {
                var newType = newArgument is PyNone ? "None" : RuntimeErrors.OperandTypeName(newArgument);
                throw new LythonRuntimeException("TypeError", $"replace() argument 2 must be str, not {newType}", span);
            }

            // Null is internal omission; an explicitly supplied Python None
            // still reaches the existing count validation, after both strings.
            var count = countArgument is null ? -1 : NormalizeSequenceCount(CoerceIoIntegerAsync(countArgument, span, context, false).GetAwaiter().GetResult());
            return receiver.Replace(oldValue, newValue, count, context, span);
        }

        private static readonly ReceiverMethod EncodeMethod = new(
            LythonCallableSignature.Create("str.encode", ["encoding", "errors"], 0),
            static (receiver, arguments, span, context) => Encode((PyString)receiver, arguments, span, context));

        private static object Encode(PyString text, ReadOnlyMemory<object?> arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length >= 1) RequireCodecNameType(arguments.Span[0]!, "str.encode()", "encoding", span);
            if (arguments.Length == 2) RequireCodecNameType(arguments.Span[1]!, "str.encode()", "errors", span);
            var encoding = arguments.Length >= 1 ? ParseTextEncoding(arguments.Span[0]!, "str.encode()", span) : TextEncodingMode.Utf8;
            var errors = arguments.Length == 2 ? ParseTextErrors(arguments.Span[1]!, "str.encode()", span) : TextErrorMode.Strict;
            return EncodeGuestBytes(text, encoding, errors, context, span);
        }
    }
}
