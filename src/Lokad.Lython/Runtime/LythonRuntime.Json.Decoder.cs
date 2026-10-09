using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Ordinary JSONDecoder instances (N37): reusable and independent, each
    // carrying constructor-captured scanner options plus writable public
    // attributes, matching CPython when those attributes are later replaced.
    internal sealed class JsonDecoderObject : IPyTruthyValue, IPyRenderableValue, IPyMutableDynamicAttributes
    {
        internal JsonDecoderObject(JsonLoadOptions options, ExecutionContext context)
        {
            Options = options;
            context.TryGetBuiltin("int", out var integer);
            context.TryGetBuiltin("float", out var floating);
            Attributes = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["strict"] = options.Strict,
                ["object_hook"] = (object?)options.ObjectHook ?? PyNone.Instance,
                ["object_pairs_hook"] = (object?)options.ObjectPairsHook ?? PyNone.Instance,
                ["parse_int"] = (object?)options.ParseInt ?? integer!,
                ["parse_float"] = (object?)options.ParseFloat ?? floating!,
                ["parse_constant"] = (object?)options.ParseConstant ?? BoundCallable.Create((args, span, ctx) =>
                    (args[0] is PyString text ? text.ToString() : throw new LythonRuntimeException("TypeError", "JSON constant must be a string", span)) switch
                    {
                        "NaN" => (object)PythonNaN,
                        "Infinity" => double.PositiveInfinity,
                        "-Infinity" => double.NegativeInfinity,
                        _ => throw new LythonRuntimeException("KeyError", "Unknown JSON constant", span)
                    }, LythonCallableSignature.Create("json.parse_constant", ["s"]))
            };
        }

        internal JsonLoadOptions Options { get; }

        internal Dictionary<string, object> Attributes { get; }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value) => JsonDecoderMembers.TryGetMember(this, name, out value);

        public bool TrySetMember(string name, object value)
        {
            if (!Attributes.ContainsKey(name)) return false;
            Attributes[name] = value;
            return true;
        }

        public bool IsTruthy() => true;

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<json.decoder.JSONDecoder object>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public object Decode(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => DecodeCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();

        public ValueTask<object> DecodeAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => DecodeCoreAsync(arguments, span, context, true);

        private async ValueTask<object> DecodeCoreAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "JSONDecoder.decode() takes exactly one argument (" + arguments.Length + " given).", span);
            }

            if (!PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "expected string or bytes-like object, got '" + UnboundTypeMethod.PythonTypeName(arguments[0], context) + "'", span);
            }

            context.CheckExecution(span);
            return asynchronous
                ? await JsonModule.Instance.ParseJsonTextAsync(text, Options, context, span).ConfigureAwait(false)
                : JsonModule.Instance.ParseJsonText(text, Options, context, span);
        }

        public object RawDecode(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => RawDecodeCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();

        public ValueTask<object> RawDecodeAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => RawDecodeCoreAsync(arguments, span, context, true);

        private async ValueTask<object> RawDecodeCoreAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            if (arguments.Length is < 1 or > 2)
            {
                throw new LythonRuntimeException("TypeError", "JSONDecoder.raw_decode() takes from 1 to 2 arguments (" + arguments.Length + " given).", span);
            }

            if (!PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "first argument must be a string, not " + UnboundTypeMethod.PythonTypeName(arguments[0], context), span);
            }

            var index = arguments.Length == 2 ? CoerceJsonRawIndex(arguments[1], context, span) : 0L;
            context.CheckExecution(span);
            var (value, end) = asynchronous
                ? await JsonModule.Instance.DecodeJsonRawValueAsync(text, index, Options, context, span).ConfigureAwait(false)
                : JsonModule.Instance.DecodeJsonRawValue(text, index, Options, context, span);
            return new PyTuple(new object[] { value, new BigInteger(end) }, context.MemoryGovernor, span);
        }

        // idx follows the __index__ protocol like CPython sequence indexing:
        // huge magnitudes overflow before the negativity check, and only
        // in-range values convert to byte positions (past-the-end indices
        // report themselves from the prefix reader).
        private static long CoerceJsonRawIndex(object value, ExecutionContext context, LythonSourceSpan span)
        {
            var coerced = CoerceIndexProtocol(value, context, span);
            BigInteger integer = coerced switch
            {
                _ when Numbers.PyNumberOps.TryAsInteger(coerced, out var integerValue) => integerValue,
                _ => throw new LythonRuntimeException("TypeError", "'" + RuntimeErrors.DatetimeQualifiedTypeName(value, context) + "' object cannot be interpreted as an integer", span),
            };

            if (integer > long.MaxValue || integer < long.MinValue)
            {
                throw new LythonRuntimeException("OverflowError", "Python int too large to convert to C ssize_t", span);
            }

            var index = (long)integer;
            if (index < 0)
            {
                throw new LythonRuntimeException("ValueError", "idx cannot be negative", span);
            }

            return index;
        }
    }

    // The stable json.JSONDecoder class identity: construction binds the
    // keyword-only load options (matching the loads() bundle plus strict)
    // while module-function layouts stay positional-first on their own
    // signatures. Cached by the module, so identity and isinstance agree
    // across direct, imported and aliased references.
    internal sealed class JsonDecoderClass : ICallable, INamedRuntimeCallable, IPyRenderableValue, IPyDynamicAttributes, IPyContextualDynamicAttributes
    {
        public static readonly JsonDecoderClass Instance = new();

        private JsonDecoderClass()
        {
        }

        public string Name => "json.JSONDecoder";

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name == "__name__" || name == "__qualname__")
            {
                value = PyString.FromString("JSONDecoder");
                return true;
            }

            if (name == "__module__")
            {
                value = PyString.FromString("json.decoder");
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public bool TryGetMember(string memberName, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            _ = context;
            _ = span;
            return TryGetMember(memberName, out value);
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            return BindDecoderOptions(arguments, span, context);
        }

        internal static JsonDecoderObject BindDecoderOptions(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            ICallable? objectHook = null;
            ICallable? parseFloat = null;
            ICallable? parseInt = null;
            ICallable? parseConstant = null;
            ICallable? objectPairsHook = null;
            var strict = true;
            var positional = 0;
            foreach (var argument in arguments)
            {
                if (argument.IsPositional)
                {
                    positional++;
                    continue;
                }

                switch (argument.KeywordName)
                {
                    case "object_hook":
                        objectHook = JsonModule.OptionalJsonCallable(argument.Value, "object_hook", span);
                        break;
                    case "parse_float":
                        parseFloat = JsonModule.OptionalJsonCallable(argument.Value, "parse_float", span);
                        break;
                    case "parse_int":
                        parseInt = JsonModule.OptionalJsonCallable(argument.Value, "parse_int", span);
                        break;
                    case "parse_constant":
                        parseConstant = JsonModule.OptionalJsonCallable(argument.Value, "parse_constant", span);
                        break;
                    case "strict":
                        strict = JsonModule.ParseJsonBoolOption(argument.Value, defaultValue: true);
                        break;
                    case "object_pairs_hook":
                        objectPairsHook = JsonModule.OptionalJsonCallable(argument.Value, "object_pairs_hook", span);
                        break;
                    default:
                        throw new LythonRuntimeException("TypeError", "JSONDecoder.__init__() got an unexpected keyword argument '" + argument.KeywordName + "'", span);
                }
            }

            if (positional > 0)
            {
                throw new LythonRuntimeException("TypeError", "JSONDecoder.__init__() takes 1 positional argument but " + (positional + 1) + " were given.", span);
            }

            return new JsonDecoderObject(new JsonLoadOptions(objectHook, parseFloat, parseInt, parseConstant, objectPairsHook, strict), context);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<class 'json.JSONDecoder'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    internal static class JsonDecoderMembers
    {
        public static bool TryGetMember(JsonDecoderObject decoder, string name, [MaybeNullWhen(false)] out object value)
        {
            if (decoder.Attributes.TryGetValue(name, out value!)) return true;
            value = name switch
            {
                "decode" => BoundCallable.Create((arguments, span, context) => decoder.Decode(arguments, span, context), LythonKnownCallableSignatures.JsonDecoderDecode, (arguments, span, context) => decoder.DecodeAsync(arguments, span, context)),
                "raw_decode" => BoundCallable.Create((arguments, span, context) => decoder.RawDecode(arguments, span, context), LythonKnownCallableSignatures.JsonDecoderRawDecode, (arguments, span, context) => decoder.RawDecodeAsync(arguments, span, context)),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }
}
