using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static partial class StringMembers
    {
        private static readonly Func<PyString, LythonSourceSpan, ExecutionContext, object> StripWhitespaceOperation = StripWhitespace;
        private static readonly Func<PyString, LythonSourceSpan, ExecutionContext, object> SplitWhitespaceOperation =
            static (receiver, span, context) => PyStringOps.SplitWhitespace(receiver, context.MemoryGovernor, span);
        private static readonly Func<PyString, LythonSourceSpan, ExecutionContext, object> LowerOperation = Lower;

        internal static Func<PyString, LythonSourceSpan, ExecutionContext, object>? GetNoArgumentsOperation(string name)
            => name switch
            {
                "strip" => StripWhitespaceOperation,
                "split" => SplitWhitespaceOperation,
                "lower" => LowerOperation,
                "islower" => static (receiver, _, _) => PyStringOps.IsLower(receiver),
                "isupper" => static (receiver, _, _) => PyStringOps.IsUpper(receiver),
                "istitle" => static (receiver, _, _) => PyStringOps.IsTitle(receiver),
                "isalpha" => static (receiver, _, _) => PyStringOps.IsAlpha(receiver),
                "isdigit" => static (receiver, _, _) => PyStringOps.IsDigit(receiver),
                "isalnum" => static (receiver, _, _) => PyStringOps.IsAlnum(receiver),
                "isascii" => static (receiver, _, _) => PyStringOps.IsAscii(receiver),
                "isdecimal" => static (receiver, _, _) => PyStringOps.IsDecimal(receiver),
                "isidentifier" => static (receiver, _, _) => PyStringOps.IsIdentifier(receiver),
                "isnumeric" => static (receiver, _, _) => PyStringOps.IsNumeric(receiver),
                "isprintable" => static (receiver, _, _) => PyStringOps.IsPrintable(receiver),
                "isspace" => static (receiver, _, _) => PyStringOps.IsSpace(receiver),
                _ => null
            };

        internal static PyString Lower(PyString receiver, LythonSourceSpan span, ExecutionContext context)
            => OwnMethodResult(receiver.ToLowerInvariant(), receiver, context.MemoryGovernor, span);

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
            var count = countArgument is null ? -1 : ParseStringOptionalInt(countArgument, "count", "str.replace(old, new[, count])", span, context);
            return OwnMethodResult(receiver.Replace(oldValue, newValue, count), receiver, context.MemoryGovernor, span);
        }

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

                        return Replace(text, arguments[0], arguments[1], arguments.Length == 3 ? arguments[2] : null, span, context);
                    }, StrReplaceSignature),
                    "startswith" => BoundCallable.Create(
                        (arguments, span, context) => InvokeStringAffix(text, arguments, span, context, isStart: true, asynchronous: false).GetAwaiter().GetResult(),
                        StrStartsWithSignature,
                        (arguments, span, context) => InvokeStringAffix(text, arguments, span, context, isStart: true, asynchronous: true)),
                    "endswith" => BoundCallable.Create(
                        (arguments, span, context) => InvokeStringAffix(text, arguments, span, context, isStart: false, asynchronous: false).GetAwaiter().GetResult(),
                        StrEndsWithSignature,
                        (arguments, span, context) => InvokeStringAffix(text, arguments, span, context, isStart: false, asynchronous: true)),
                    "lower" => BoundCallable.CreateNoArguments(text, "str.lower", Lower),
                    "capitalize" => BoundCallable.CreateNoArguments(text, "str.capitalize", static (receiver, span, context) => OwnMethodResult(PyStringOps.Capitalize(receiver),receiver,context.MemoryGovernor,span)),
                    "islower" => BoundCallable.CreateNoArguments(text, "str.islower", GetNoArgumentsOperation(name)!),
                    "upper" => BoundCallable.CreateNoArguments(text, "str.upper", static (receiver, span, context) => OwnMethodResult(receiver.ToUpperInvariant(),receiver,context.MemoryGovernor,span)),
                    "swapcase" => BoundCallable.CreateNoArguments(text, "str.swapcase", static (receiver, span, context) => OwnMethodResult(PyStringOps.SwapCase(receiver),receiver,context.MemoryGovernor,span)),
                    "title" => BoundCallable.CreateNoArguments(text, "str.title", static (receiver, span, context) => OwnMethodResult(PyStringOps.Title(receiver),receiver,context.MemoryGovernor,span)),
                    "casefold" => BoundCallable.CreateNoArguments(text, "str.casefold", static (receiver, span, context) => OwnMethodResult(PyStringOps.CaseFold(receiver),receiver,context.MemoryGovernor,span)),
                    "isupper" => BoundCallable.CreateNoArguments(text, "str.isupper", GetNoArgumentsOperation(name)!),
                    "isalpha" => BoundCallable.CreateNoArguments(text, "str.isalpha", GetNoArgumentsOperation(name)!),
                    "isdigit" => BoundCallable.CreateNoArguments(text, "str.isdigit", GetNoArgumentsOperation(name)!),
                    "isalnum" => BoundCallable.CreateNoArguments(text, "str.isalnum", GetNoArgumentsOperation(name)!),
                    "isascii" => BoundCallable.CreateNoArguments(text, "str.isascii", GetNoArgumentsOperation(name)!),
                    "isdecimal" => BoundCallable.CreateNoArguments(text, "str.isdecimal", GetNoArgumentsOperation(name)!),
                    "isidentifier" => BoundCallable.CreateNoArguments(text, "str.isidentifier", GetNoArgumentsOperation(name)!),
                    "isnumeric" => BoundCallable.CreateNoArguments(text, "str.isnumeric", GetNoArgumentsOperation(name)!),
                    "isprintable" => BoundCallable.CreateNoArguments(text, "str.isprintable", GetNoArgumentsOperation(name)!),
                    "istitle" => BoundCallable.CreateNoArguments(text, "str.istitle", GetNoArgumentsOperation(name)!),
                    "isspace" => BoundCallable.CreateNoArguments(text, "str.isspace", GetNoArgumentsOperation(name)!),
                    _ => MissingMemberValue.Instance,
                };

                return !ReferenceEquals(value, MissingMemberValue.Instance);

            }
        }
    }
}
