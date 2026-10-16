using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static partial class StringMembers
    {
        private sealed class TextSemanticsMemberProvider : IStringMemberProvider
        {
            // N17: hot fixed signatures hoisted per family.
            private static readonly LythonCallableSignature StrEncodeSignature = LythonCallableSignature.Create("str.encode", ["encoding", "errors"], 0);
            private static readonly LythonCallableSignature StrReplaceSignature = LythonCallableSignature.Create("str.replace", ["old", "new", "count"], 2);
            private static readonly LythonCallableSignature StrStartsWithSignature = LythonCallableSignature.Create("str.startswith", 1);
            private static readonly LythonCallableSignature StrEndsWithSignature = LythonCallableSignature.Create("str.endswith", 1);
            public static readonly TextSemanticsMemberProvider Instance = new();

            public bool TryGetMember(PyString text, string name, [MaybeNullWhen(false)] out object value)
            {
                value = name switch
                {
                    "encode" => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length > 2)
                        {
                            throw new LythonRuntimeException("TypeError", "str.encode([encoding][, errors]) expects zero to two arguments.", span);
                        }

                        var encoding = arguments.Length >= 1
                            ? ParseTextEncoding(arguments[0], "str.encode()", span)
                            : TextEncodingMode.Utf8;
                        var errors = arguments.Length == 2
                            ? ParseTextErrors(arguments[1], "str.encode()", span)
                            : TextErrorMode.Strict;
                        return CreateBytes(
                            EncodeText(text, encoding, errors, TextNewlineMode.PreserveUniversal, context, span),
                            context,
                            span);
                    }, StrEncodeSignature),
                    "replace" => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length is < 2 or > 3)
                        {
                            throw new LythonRuntimeException("TypeError", "str.replace(old, new[, count]) expects two string arguments and an optional integer count.", span);
                        }

                        if (!PyStringOps.TryAsString(arguments[0], out var oldValue))
                        {
                            // CPython numbers the bad argument, spelling a None value bare.
                            var oldType = arguments[0] is PyNone ? "None" : RuntimeErrors.OperandTypeName(arguments[0]);
                            throw new LythonRuntimeException("TypeError", $"replace() argument 1 must be str, not {oldType}", span);
                        }

                        if (!PyStringOps.TryAsString(arguments[1], out var newValue))
                        {
                            var newType = arguments[1] is PyNone ? "None" : RuntimeErrors.OperandTypeName(arguments[1]);
                            throw new LythonRuntimeException("TypeError", $"replace() argument 2 must be str, not {newType}", span);
                        }

                        var count = arguments.Length == 3 ? ParseStringOptionalInt(arguments[2], "count", "str.replace(old, new[, count])", span, context) : -1;
                        return OwnMethodResult(PyStringOps.Replace(text, oldValue, newValue, count),text,context.MemoryGovernor,span);
                    }, StrReplaceSignature),
                    "startswith" => BoundCallable.Create(
                        (arguments, span, context) => InvokeStringAffix(text, arguments, span, context, isStart: true, asynchronous: false).GetAwaiter().GetResult(),
                        StrStartsWithSignature,
                        (arguments, span, context) => InvokeStringAffix(text, arguments, span, context, isStart: true, asynchronous: true)),
                    "endswith" => BoundCallable.Create(
                        (arguments, span, context) => InvokeStringAffix(text, arguments, span, context, isStart: false, asynchronous: false).GetAwaiter().GetResult(),
                        StrEndsWithSignature,
                        (arguments, span, context) => InvokeStringAffix(text, arguments, span, context, isStart: false, asynchronous: true)),
                    "lower" => BoundCallable.CreateNoArguments(text, "str.lower", static (receiver, span, context) => OwnMethodResult(receiver.ToLowerInvariant(),receiver,context.MemoryGovernor,span)),
                    "capitalize" => BoundCallable.CreateNoArguments(text, "str.capitalize", static (receiver, span, context) => OwnMethodResult(PyStringOps.Capitalize(receiver),receiver,context.MemoryGovernor,span)),
                    "islower" => BoundCallable.CreateNoArguments(text, "str.islower", static (receiver, _, _) => PyStringOps.IsLower(receiver)),
                    "upper" => BoundCallable.CreateNoArguments(text, "str.upper", static (receiver, span, context) => OwnMethodResult(receiver.ToUpperInvariant(),receiver,context.MemoryGovernor,span)),
                    "swapcase" => BoundCallable.CreateNoArguments(text, "str.swapcase", static (receiver, span, context) => OwnMethodResult(PyStringOps.SwapCase(receiver),receiver,context.MemoryGovernor,span)),
                    "title" => BoundCallable.CreateNoArguments(text, "str.title", static (receiver, span, context) => OwnMethodResult(PyStringOps.Title(receiver),receiver,context.MemoryGovernor,span)),
                    "casefold" => BoundCallable.CreateNoArguments(text, "str.casefold", static (receiver, span, context) => OwnMethodResult(PyStringOps.CaseFold(receiver),receiver,context.MemoryGovernor,span)),
                    "isupper" => BoundCallable.CreateNoArguments(text, "str.isupper", static (receiver, _, _) => PyStringOps.IsUpper(receiver)),
                    "isalpha" => BoundCallable.CreateNoArguments(text, "str.isalpha", static (receiver, _, _) => PyStringOps.IsAlpha(receiver)),
                    "isdigit" => BoundCallable.CreateNoArguments(text, "str.isdigit", static (receiver, _, _) => PyStringOps.IsDigit(receiver)),
                    "isalnum" => BoundCallable.CreateNoArguments(text, "str.isalnum", static (receiver, _, _) => PyStringOps.IsAlnum(receiver)),
                    "isascii" => BoundCallable.CreateNoArguments(text, "str.isascii", static (receiver, _, _) => PyStringOps.IsAscii(receiver)),
                    "isdecimal" => BoundCallable.CreateNoArguments(text, "str.isdecimal", static (receiver, _, _) => PyStringOps.IsDecimal(receiver)),
                    "isidentifier" => BoundCallable.CreateNoArguments(text, "str.isidentifier", static (receiver, _, _) => PyStringOps.IsIdentifier(receiver)),
                    "isnumeric" => BoundCallable.CreateNoArguments(text, "str.isnumeric", static (receiver, _, _) => PyStringOps.IsNumeric(receiver)),
                    "isprintable" => BoundCallable.CreateNoArguments(text, "str.isprintable", static (receiver, _, _) => PyStringOps.IsPrintable(receiver)),
                    "istitle" => BoundCallable.CreateNoArguments(text, "str.istitle", static (receiver, _, _) => PyStringOps.IsTitle(receiver)),
                    "isspace" => BoundCallable.CreateNoArguments(text, "str.isspace", static (receiver, _, _) => PyStringOps.IsSpace(receiver)),
                    _ => MissingMemberValue.Instance,
                };

                return !ReferenceEquals(value, MissingMemberValue.Instance);

            }
        }
    }
}
