using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Ordinary JSONEncoder instances (N38): reusable and independent, each
    // carrying its parsed dump options plus the raw values behind the
    // readable attributes below. Subclasses (N39) and custom cls=...
    // classes fail explicitly through the dump options gate.
    internal sealed record JsonEncoderOptions(
        object SkipKeys,
        object EnsureAscii,
        object CheckCircular,
        object AllowNan,
        object SortKeys,
        object Indent,
        JsonDumpOptions Dump);

    internal sealed class JsonEncoderObject : IPyTruthyValue, IPyRenderableValue
    {
        internal JsonEncoderObject(JsonEncoderOptions options, PyString itemSeparator, PyString keySeparator, ICallable? defaultHook)
        {
            Options = options;
            ItemSeparator = itemSeparator;
            KeySeparator = keySeparator;
            DefaultHook = defaultHook;
        }

        internal JsonEncoderOptions Options { get; }

        internal PyString ItemSeparator { get; }

        internal PyString KeySeparator { get; }

        internal ICallable? DefaultHook { get; }

        public bool IsTruthy() => true;

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<json.encoder.JSONEncoder object>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public object Encode(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "JSONEncoder.encode() takes exactly one argument (" + arguments.Length + " given).", span);
            }

            context.CheckExecutionBudget(span);
            return JsonModule.SerializeJsonText(arguments[0], Options.Dump, context, span);
        }

        // The base default() is not a fallback call to encode(): even an
        // otherwise encodable value fails here unless a default= hook (or an
        // N39 override) supplies the behavior.
        public object Default(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "JSONEncoder.default() takes exactly one argument (" + arguments.Length + " given).", span);
            }

            throw new LythonRuntimeException("TypeError", "Object of type " + JsonDefaultTypeName(arguments[0], context) + " is not JSON serializable", span);
        }

        private static string JsonDefaultTypeName(object value, ExecutionContext context)
            => value switch
            {
                PyString => "str",
                PyBytes => "bytes",
                PyList => "list",
                PyDict => "dict",
                PyTuple => "tuple",
                PySet => "set",
                bool => "bool",
                BigInteger or int => "int",
                double => "float",
                PyDecimal => "Decimal",
                PyNone or null => "NoneType",
                PyInstance instance => instance.Type.Name,
                _ => UnboundTypeMethod.PythonTypeName(value, context),
            };
    }

    // The stable json.JSONEncoder class identity: construction binds the
    // keyword-only dump options while module-function layouts stay
    // positional-first on their own signatures. Cached by the module, so
    // identity and isinstance agree across direct, imported and aliased
    // references.
    internal sealed class JsonEncoderClass : ICallable, INamedRuntimeCallable, IPyRenderableValue, IPyDynamicAttributes, IPyContextualDynamicAttributes
    {
        public static readonly JsonEncoderClass Instance = new();

        private JsonEncoderClass()
        {
        }

        public string Name => "json.JSONEncoder";

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name == "__name__" || name == "__qualname__")
            {
                value = PyString.FromString("JSONEncoder");
                return true;
            }

            if (name == "__module__")
            {
                value = PyString.FromString("json.encoder");
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
            context.CheckExecutionBudget(span);
            object skipKeys = false;
            object ensureAscii = true;
            object checkCircular = true;
            object allowNan = true;
            object sortKeys = false;
            object indent = PyNone.Instance;
            object separators = PyNone.Instance;
            var hasSeparators = false;
            ICallable? defaultHook = null;
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
                    case "skipkeys":
                        skipKeys = argument.Value;
                        break;
                    case "ensure_ascii":
                        ensureAscii = argument.Value;
                        break;
                    case "check_circular":
                        checkCircular = argument.Value;
                        break;
                    case "allow_nan":
                        allowNan = argument.Value;
                        break;
                    case "sort_keys":
                        sortKeys = argument.Value;
                        break;
                    case "indent":
                        indent = argument.Value;
                        break;
                    case "separators":
                        separators = argument.Value;
                        hasSeparators = true;
                        break;
                    case "default":
                        defaultHook = JsonModule.OptionalJsonCallable(argument.Value, "default", span);
                        break;
                    default:
                        throw new LythonRuntimeException("TypeError", "JSONEncoder.__init__() got an unexpected keyword argument '" + argument.KeywordName + "'", span);
                }
            }

            if (positional > 0)
            {
                throw new LythonRuntimeException("TypeError", "JSONEncoder.__init__() takes 1 positional argument but " + (positional + 1) + " were given.", span);
            }

            var indentUnit = JsonModule.ParseJsonIndent(indent, span);
            var parsedSeparators = JsonModule.ParseJsonSeparators(hasSeparators ? separators : PyNone.Instance, !ReferenceEquals(indent, PyNone.Instance), span, context);
            var dump = new JsonDumpOptions(
                JsonModule.ParseJsonBoolOption(skipKeys, defaultValue: false),
                JsonModule.ParseJsonBoolOption(ensureAscii, defaultValue: true),
                JsonModule.ParseJsonBoolOption(checkCircular, defaultValue: true),
                JsonModule.ParseJsonBoolOption(allowNan, defaultValue: true),
                indentUnit,
                parsedSeparators.ItemSeparator,
                parsedSeparators.KeySeparator,
                defaultHook,
                JsonModule.ParseJsonBoolOption(sortKeys, defaultValue: false));
            return new JsonEncoderObject(
                new JsonEncoderOptions(skipKeys, ensureAscii, checkCircular, allowNan, sortKeys, indent, dump),
                PyString.FromString(parsedSeparators.ItemSeparator),
                PyString.FromString(parsedSeparators.KeySeparator),
                defaultHook);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<class 'json.JSONEncoder'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    internal static class JsonEncoderMembers
    {
        public static bool TryGetMember(JsonEncoderObject encoder, string name, [MaybeNullWhen(false)] out object value)
        {
            switch (name)
            {
                case "encode":
                    value = BoundCallable.Create((arguments, span, context) => encoder.Encode(arguments, span, context), LythonKnownCallableSignatures.JsonEncoderEncode);
                    return true;
                case "iterencode":
                    // Incremental streaming lands in the follow-up commit;
                    // until then the boundary fails explicitly instead of
                    // faking laziness by splitting a complete document.
                    value = BoundCallable.Create((arguments, span, context) => throw new LythonRuntimeException("NotImplementedError", "json.JSONEncoder.iterencode incremental streaming is not supported by Lython yet.", span), LythonKnownCallableSignatures.JsonEncoderIterencode);
                    return true;
                case "default":
                    value = encoder.DefaultHook is not null
                        ? encoder.DefaultHook
                        : BoundCallable.Create((arguments, span, context) => encoder.Default(arguments, span, context), LythonKnownCallableSignatures.JsonEncoderDefault);
                    return true;
                case "skipkeys":
                    value = encoder.Options.SkipKeys;
                    return true;
                case "ensure_ascii":
                    value = encoder.Options.EnsureAscii;
                    return true;
                case "check_circular":
                    value = encoder.Options.CheckCircular;
                    return true;
                case "allow_nan":
                    value = encoder.Options.AllowNan;
                    return true;
                case "sort_keys":
                    value = encoder.Options.SortKeys;
                    return true;
                case "indent":
                    value = encoder.Options.Indent;
                    return true;
                case "item_separator":
                    value = encoder.ItemSeparator;
                    return true;
                case "key_separator":
                    value = encoder.KeySeparator;
                    return true;
                default:
                    value = MissingMemberValue.Instance;
                    return false;
            }
        }
    }
}
