using System.Numerics;
using System.Text;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class UrllibModule : PyModule
    {
        internal static readonly UrllibModule Instance = new();
        private UrllibModule() : base("urllib") { }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name == "parse" ? UrllibParseModule.Instance : MissingMemberValue.Instance;
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    internal sealed partial class UrllibParseModule : PyModule
    {
        internal static readonly UrllibParseModule Instance = new();
        private UrllibParseModule() : base("urllib.parse") { }
        private static readonly PyString Slash = PyString.FromString("/");
        private static readonly PyString Plus = PyString.FromString("+");
        private static readonly PyString Space = PyString.FromString(" ");
        private static readonly PyString Percent = PyString.FromString("%");
        private static readonly PyString Utf8Name = PyString.FromString("utf-8");
        private static readonly PyString ReplaceName = PyString.FromString("replace");
        private static readonly PyBytes EmptyBytes = new([]);
        private static readonly PyBytes ByteSpace = new([32]);
        private static readonly PyString ModuleName = PyString.FromString("urllib.parse");
        private static readonly PyString Ampersand = PyString.FromString("&");
        private static readonly Dictionary<string, UrlMethod> Methods = new(StringComparer.Ordinal)
        {
            ["quote"] = new(LythonKnownCallableSignatures.UrlQuote, UrlOperation.Quote),
            ["quote_plus"] = new(LythonKnownCallableSignatures.UrlQuotePlus, UrlOperation.QuotePlus),
            ["quote_from_bytes"] = new(LythonKnownCallableSignatures.UrlQuoteFromBytes, UrlOperation.QuoteBytes),
            ["unquote"] = new(LythonKnownCallableSignatures.UrlUnquote, UrlOperation.Unquote),
            ["unquote_plus"] = new(LythonKnownCallableSignatures.UrlUnquotePlus, UrlOperation.UnquotePlus),
            ["unquote_to_bytes"] = new(LythonKnownCallableSignatures.UrlUnquoteToBytes, UrlOperation.UnquoteBytes),
            ["urlencode"] = new(LythonKnownCallableSignatures.UrlEncode, UrlOperation.Encode),
            ["parse_qs"] = new(LythonKnownCallableSignatures.UrlParseQs, UrlOperation.ParseQs),
            ["parse_qsl"] = new(LythonKnownCallableSignatures.UrlParseQsl, UrlOperation.ParseQsl),
        };

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = Methods.TryGetValue(name, out var method) ? method : MissingMemberValue.Instance;
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private enum UrlOperation { Quote, QuotePlus, QuoteBytes, Unquote, UnquotePlus, UnquoteBytes, Encode, ParseQs, ParseQsl }

        private sealed class UrlMethod : ICallable, INamedRuntimeCallable, IPyDynamicAttributes, IPyRenderableValue, IPyHashableValue
        {
            private readonly LythonCallableSignature _signature;
            private readonly UrlOperation _operation;
            private readonly PyString _name;
            internal UrlMethod(LythonCallableSignature signature, UrlOperation operation)
            {
                _signature = signature;
                _operation = operation;
                _name = PyString.FromString(signature.Name[(signature.Name.LastIndexOf('.') + 1)..]);
            }
            public string Name => _signature.Name;
            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
                => InvokeCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();
            public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
                => InvokeCoreAsync(arguments, span, context, true);
            private ValueTask<object> InvokeCoreAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
            {
                context.CheckExecutionBudget(span);
                var bound = CallBinder.BindNamedArgumentsWithPresence(arguments, span, _signature, PythonCallableKind.Builtin);
                var args = bound.Values;
                if (_operation is UrlOperation.Quote or UrlOperation.QuotePlus or UrlOperation.QuoteBytes && !bound.Assigned[1])
                    args[1] = _operation == UrlOperation.QuotePlus ? PyString.Empty : Slash;
                if (_operation is UrlOperation.Unquote or UrlOperation.UnquotePlus)
                {
                    if (!bound.Assigned[1]) args[1] = Utf8Name;
                    if (!bound.Assigned[2]) args[2] = ReplaceName;
                }
                if (_operation == UrlOperation.Encode)
                {
                    if (!bound.Assigned[1]) args[1] = false;
                    if (!bound.Assigned[2]) args[2] = PyString.Empty;
                    if (!bound.Assigned[5]) args[5] = Methods["quote_plus"];
                }
                if (_operation is UrlOperation.ParseQs or UrlOperation.ParseQsl)
                {
                    if (!bound.Assigned[1]) args[1] = false;
                    if (!bound.Assigned[2]) args[2] = false;
                    if (!bound.Assigned[3]) args[3] = Utf8Name;
                    if (!bound.Assigned[4]) args[4] = ReplaceName;
                    if (!bound.Assigned[6]) args[6] = Ampersand;
                }
                return _operation switch
                {
                    UrlOperation.Quote => QuoteAsync(args, span, context, asynchronous, false),
                    UrlOperation.QuotePlus => QuoteAsync(args, span, context, asynchronous, true),
                    UrlOperation.QuoteBytes => QuoteFromBytesAsync(args, span, context, asynchronous),
                    UrlOperation.Unquote => UnquoteAsync(args, span, context, asynchronous, false),
                    UrlOperation.UnquotePlus => UnquoteAsync(args, span, context, asynchronous, true),
                    UrlOperation.Encode => UrlEncodeAsync(args, span, context, asynchronous),
                    UrlOperation.ParseQs => ParseQueryAsync(args, span, context, asynchronous, true),
                    UrlOperation.ParseQsl => ParseQueryAsync(args, span, context, asynchronous, false),
                    _ => ValueTask.FromResult<object>(UnquoteToBytes(args[0], span, context)),
                };
            }
            public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            {
                value = name switch
                {
                    "__name__" or "__qualname__" => _name,
                    "__module__" => ModuleName,
                    "__doc__" => PyNone.Instance,
                    _ => MissingMemberValue.Instance,
                };
                return !ReferenceEquals(value, MissingMemberValue.Instance);
            }
            public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<function " + _name + ">");
            public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
            public int GetPyHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
        }

        private static async ValueTask<object> QuoteAsync(object[] args, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous, bool plus)
        {
            var input = args[0];
            var safe = args.Length > 1 ? args[1] : plus ? PyString.Empty : Slash;
            var addSpaceToSafe = plus && safe is PyString or PyBytes;
            // quote_plus first adds a space to a non-string safe value when
            // the input contains spaces. Guest addition may supply new safe
            // bytes, and its failure precedes encoding validation.
            if (plus && safe is not PyString && safe is not PyBytes &&
                !(input is PyString noSpaces && noSpaces.Utf8Bytes.Span.IndexOf((byte)' ') < 0) &&
                !(input is PyBytes noByteSpaces && noByteSpaces.Bytes.IndexOf((byte)' ') < 0))
            {
                safe = asynchronous
                    ? await EvaluateBinaryOperatorAsync(BinaryOperatorSyntax.Add, safe, ByteSpace, context, span).ConfigureAwait(false)
                    : EvaluateBinaryOperator(BinaryOperatorSyntax.Add, safe, ByteSpace, context, span);
            }
            // Python returns empty strings before validating either option.
            if (input is PyString { Length: 0 }) return input;
            var encodingValue = args.Length > 2 ? args[2] : PyNone.Instance;
            var errorsValue = args.Length > 3 ? args[3] : PyNone.Instance;
            PyBytes bytes;
            if (PyStringOps.TryAsString(input, out var text))
            {
                var encoding = Codec(encodingValue, span);
                var errors = Errors(errorsValue, TextErrorMode.Strict, span);
                bytes = OwnBytes(EncodeText(text, encoding, errors, TextNewlineMode.PreserveUniversal, context, span), context, span);
            }
            else
            {
                if (encodingValue is not PyNone || errorsValue is not PyNone)
                    throw new LythonRuntimeException("TypeError", "quote() does not support encoding or errors for bytes", span);
                bytes = input as PyBytes ?? throw new LythonRuntimeException("TypeError", "quote_from_bytes() expected bytes", span);
            }
            try
            {
                if (bytes.Length == 0) return PyString.Empty;
                var set = await SafeSetAsync(safe, span, context, asynchronous).ConfigureAwait(false);
                if (addSpaceToSafe) set.Add(32);
                return QuoteBytes(bytes.Bytes, set, plus, context, span);
            }
            finally { GC.KeepAlive(bytes); GC.KeepAlive(input); }
        }

        private static async ValueTask<object> QuoteFromBytesAsync(object[] args, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous)
        {
            var bytes = args[0] as PyBytes ?? throw new LythonRuntimeException("TypeError", "quote_from_bytes() expected bytes", span);
            if (bytes.Length == 0) return PyString.Empty;
            var safe = await SafeSetAsync(args.Length > 1 ? args[1] : Slash, span, context, asynchronous).ConfigureAwait(false);
            try { return QuoteBytes(bytes.Bytes, safe, false, context, span); }
            finally { GC.KeepAlive(bytes); }
        }

        internal struct SafeSet
        {
            private ulong _low, _high;
            internal void Add(int value)
            {
                if (value < 64) _low |= 1UL << value;
                else _high |= 1UL << (value - 64);
            }
            internal readonly bool Contains(byte value) => value < 64 ? (_low & (1UL << value)) != 0
                : value < 128 && (_high & (1UL << (value - 64))) != 0;
        }

        private static async ValueTask<SafeSet> SafeSetAsync(object value, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous)
        {
            var result = new SafeSet();
            foreach (var c in "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~") result.Add(c);
            if (PyStringOps.TryAsString(value, out var text))
            {
                var index = 0;
                foreach (var b in text.Utf8Bytes.Span)
                {
                    if ((index++ & 1023) == 0) context.CheckExecutionBudget(span);
                    if (b < 128) result.Add(b);
                }
            }
            else if (value is PyBytes bytes)
            {
                var index = 0;
                foreach (var b in bytes.Bytes)
                {
                    if ((index++ & 1023) == 0) context.CheckExecutionBudget(span);
                    if (b < 128) result.Add(b);
                }
            }
            else
            {
                if (asynchronous)
                {
                    await foreach (var item in PyIteration.ToSequenceAsync(value, span, context).ConfigureAwait(false)) Add(item);
                }
                else foreach (var item in PyIteration.ToSequence(value, span, context)) Add(item);
            }
            return result;

            void Add(object item)
            {
                context.CheckExecutionBudget(span);
                var number = item switch
                {
                    BigInteger integer => integer,
                    bool boolean => boolean ? BigInteger.One : BigInteger.Zero,
                    _ => throw new LythonRuntimeException("TypeError", "safe byte values must be integers", span),
                };
                if (number >= 128) return;
                if (number < 0) throw new LythonRuntimeException("ValueError", "bytes must be in range(0, 256)", span);
                result.Add((int)number);
            }
        }

        private static async ValueTask<object> UnquoteAsync(object[] args, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous, bool plus)
        {
            var input = args[0];
            if (plus)
            {
                var replace = asynchronous ? await PyTextStream.ResolveMemberAsync(input, "replace", span, context).ConfigureAwait(false)
                    : PyTextStream.ResolveMember(input, "replace", span, context);
                CallArgumentValue[] parameters = [CallArgumentValue.Positional(Plus), CallArgumentValue.Positional(Space)];
                input = asynchronous ? await InvokeCallableTargetAsync(replace, span, span, context, () => ValueTask.FromResult(parameters)).ConfigureAwait(false)
                    : InvokeCallableTarget(replace, span, span, context, () => parameters);
            }
            var encodingValue = args.Length > 1 ? args[1] : Utf8Name;
            var errorsValue = args.Length > 2 ? args[2] : ReplaceName;
            if (input is PyBytes bytes)
            {
                RequireCodecNameType(encodingValue, "decode", "encoding", span);
                RequireCodecNameType(errorsValue, "decode", "errors", span);
                if (bytes.Length == 0) return PyString.Empty;
                var encoding = Codec(encodingValue, span);
                var errors = Errors(errorsValue, TextErrorMode.Replace, span);
                var decodedBytes = PercentDecodeBytes(bytes.Bytes, context, span);
                try { return DecodeOwned(decodedBytes.Memory, encoding, errors, context, span); }
                finally { GC.KeepAlive(decodedBytes); GC.KeepAlive(bytes); }
            }
            if (PyStringOps.TryAsString(input, out var text))
            {
                if (text.Utf8Bytes.Span.IndexOf((byte)'%') < 0) return input;
                return UnquoteText(text, Codec(encodingValue, span), Errors(errorsValue, TextErrorMode.Replace, span), context, span);
            }
            var contains = asynchronous
                ? await ContainsAsync(input, Percent, context, span).ConfigureAwait(false)
                : Contains(input, Percent, context, span);
            if (!contains)
            {
                if (asynchronous) _ = await PyTextStream.ResolveMemberAsync(input, "split", span, context).ConfigureAwait(false);
                else _ = PyTextStream.ResolveMember(input, "split", span, context);
                return input;
            }
            throw new LythonRuntimeException("TypeError", "unquote() expected string text", span);
        }

        private static PyBytes UnquoteToBytes(object value, LythonSourceSpan span, ExecutionContext context)
        {
            if (value is PyBytes bytes)
            {
                try { return PercentDecodeBytes(bytes.Bytes, context, span); }
                finally { GC.KeepAlive(bytes); }
            }
            if (PyStringOps.TryAsString(value, out var text))
            {
                try { return PercentDecodeBytes(text.Utf8Bytes.Span, context, span); }
                finally { GC.KeepAlive(text); }
            }
            throw new LythonRuntimeException("AttributeError", "object has no attribute 'split'", span);
        }

        private static TextEncodingMode Codec(object value, LythonSourceSpan span)
        {
            if (value is not PyNone && !PyStringOps.TryAsString(value, out _))
                throw new LythonRuntimeException("TypeError", "encoding must be a string", span);
            try { return ParseTextEncoding(value, "urllib.parse", span); }
            catch (LythonRuntimeException ex) when (ex.ExceptionType == "ValueError")
            { throw new LythonRuntimeException("LookupError", "unknown encoding", span); }
        }

        private static TextErrorMode Errors(object value, TextErrorMode fallback, LythonSourceSpan span)
        {
            if (value is PyNone) return fallback;
            if (!PyStringOps.TryAsString(value, out _)) throw new LythonRuntimeException("TypeError", "errors must be a string", span);
            try { return ParseTextErrors(value, "urllib.parse", span); }
            catch (LythonRuntimeException ex) when (ex.ExceptionType == "ValueError")
            { throw new LythonRuntimeException("LookupError", "unknown error handler", span); }
        }
    }
}
