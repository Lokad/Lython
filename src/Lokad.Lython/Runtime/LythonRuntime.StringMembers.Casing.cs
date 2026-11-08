using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static partial class StringMembers
    {
        // Shared immutable declarations; native preparation reuses these same
        // methods and the frontend's existing return facts, without an enum index.
        private static class CasingMethods
        {
            internal static readonly ReceiverMethod Lower = new(
                LythonCallableSignature.Create("str.lower", []),
                static (receiver, span, context) => PyStringOps.Lower((PyString)receiver, context, span));

            internal static readonly ReceiverMethod Upper = new(
                LythonCallableSignature.Create("str.upper", []),
                static (receiver, span, context) => PyStringOps.Upper((PyString)receiver, context, span));

            internal static readonly ReceiverMethod IsLower = new(
                LythonCallableSignature.Create("str.islower", []),
                static (receiver, span, context) => PyStringOps.IsLower((PyString)receiver, context, span));

            internal static readonly ReceiverMethod IsUpper = new(
                LythonCallableSignature.Create("str.isupper", []),
                static (receiver, span, context) => PyStringOps.IsUpper((PyString)receiver, context, span));

            internal static readonly ReceiverMethod IsTitle = new(
                LythonCallableSignature.Create("str.istitle", []),
                static (receiver, span, context) => PyStringOps.IsTitle((PyString)receiver, context, span));

            internal static readonly ReceiverMethod IsAlpha = new(
                LythonCallableSignature.Create("str.isalpha", []),
                static (receiver, span, context) => PyStringOps.IsAlpha((PyString)receiver, context, span));

            internal static readonly ReceiverMethod IsDigit = new(
                LythonCallableSignature.Create("str.isdigit", []),
                static (receiver, span, context) => PyStringOps.IsDigit((PyString)receiver, context, span));

            internal static readonly ReceiverMethod IsAlnum = new(
                LythonCallableSignature.Create("str.isalnum", []),
                static (receiver, span, context) => PyStringOps.IsAlnum((PyString)receiver, context, span));

            internal static readonly ReceiverMethod IsAscii = new(
                LythonCallableSignature.Create("str.isascii", []),
                static (receiver, span, context) => PyStringOps.IsAscii((PyString)receiver, context, span));

            internal static readonly ReceiverMethod IsDecimal = new(
                LythonCallableSignature.Create("str.isdecimal", []),
                static (receiver, span, context) => PyStringOps.IsDecimal((PyString)receiver, context, span));

            internal static readonly ReceiverMethod IsIdentifier = new(
                LythonCallableSignature.Create("str.isidentifier", []),
                static (receiver, span, context) => PyStringOps.IsIdentifier((PyString)receiver, context, span));

            internal static readonly ReceiverMethod IsNumeric = new(
                LythonCallableSignature.Create("str.isnumeric", []),
                static (receiver, span, context) => PyStringOps.IsNumeric((PyString)receiver, context, span));

            internal static readonly ReceiverMethod IsPrintable = new(
                LythonCallableSignature.Create("str.isprintable", []),
                static (receiver, span, context) => PyStringOps.IsPrintable((PyString)receiver, context, span));

            internal static readonly ReceiverMethod IsSpace = new(
                LythonCallableSignature.Create("str.isspace", []),
                static (receiver, span, context) => PyStringOps.IsSpace((PyString)receiver, context, span));

            internal static readonly ReceiverMethod Capitalize = new(
                LythonCallableSignature.Create("str.capitalize", []),
                static (receiver, span, context) => PyStringOps.Capitalize((PyString)receiver, context, span));

            internal static readonly ReceiverMethod SwapCase = new(
                LythonCallableSignature.Create("str.swapcase", []),
                static (receiver, span, context) => PyStringOps.SwapCase((PyString)receiver, context, span));

            internal static readonly ReceiverMethod Title = new(
                LythonCallableSignature.Create("str.title", []),
                static (receiver, span, context) => PyStringOps.Title((PyString)receiver, context, span));

            internal static readonly ReceiverMethod CaseFold = new(
                LythonCallableSignature.Create("str.casefold", []),
                static (receiver, span, context) => PyStringOps.CaseFold((PyString)receiver, context, span));

        }
    }
}
