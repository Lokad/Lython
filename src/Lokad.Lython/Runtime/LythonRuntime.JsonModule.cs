using System.Globalization;
using System.Runtime.CompilerServices;
using System.Numerics;
using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed partial class JsonModule : PyModule
    {
        public static readonly JsonModule Instance = new();

        private JsonModule() : base("json")
        {
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "load" => new JsonModuleCallable(LythonKnownCallableSignatures.JsonLoad, Load, LoadAsync),
                "loads" => new JsonModuleCallable(LythonKnownCallableSignatures.JsonLoads, Loads),
                "dump" => new JsonModuleCallable(LythonKnownCallableSignatures.JsonDump, Dump),
                "dumps" => new JsonModuleCallable(LythonKnownCallableSignatures.JsonDumps, Dumps),
                "JSONDecodeError" => new ExceptionTypeValue(ModuleException("json", "JSONDecodeError")),
                "JSONEncoder" => JsonEncoderClass.Instance,
                "JSONDecoder" => JsonDecoderClass.Instance,
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        // Module entry points accept extra keywords at binding so custom
        // cls=... constructors receive them; standard shapes bind exactly
        // like BuiltinCallable, and every introspection answer matches it.
        private sealed class JsonModuleCallable : ICallable, IPyRenderableValue, IPyDynamicAttributes, IPyContextualDynamicAttributes, IPyBoundEngineMethod, IPyHashableValue
        {
            private readonly LythonCallableSignature _signature;
            private readonly Func<object[], IReadOnlyList<KeyValuePair<string, object>>, LythonSourceSpan, ExecutionContext, object> _implementation;
            private readonly Func<object[], IReadOnlyList<KeyValuePair<string, object>>, LythonSourceSpan, ExecutionContext, ValueTask<object>>? _asyncImplementation;

            public JsonModuleCallable(
                LythonCallableSignature signature,
                Func<object[], IReadOnlyList<KeyValuePair<string, object>>, LythonSourceSpan, ExecutionContext, object> implementation)
                : this(signature, implementation, null)
            {
            }

            public JsonModuleCallable(
                LythonCallableSignature signature,
                Func<object[], IReadOnlyList<KeyValuePair<string, object>>, LythonSourceSpan, ExecutionContext, object> implementation,
                Func<object[], IReadOnlyList<KeyValuePair<string, object>>, LythonSourceSpan, ExecutionContext, ValueTask<object>>? asyncImplementation)
            {
                _signature = signature;
                _implementation = implementation;
                _asyncImplementation = asyncImplementation;
            }

            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecutionBudget(span);
                SplitArguments(arguments, out var standard, out var extras);
                var bound = CallBinder.BindNamedArguments(standard, span, _signature, PythonCallableKind.Builtin);
                var result = _implementation(bound, extras, span, context);
                context.Services.State.CallTemporaries.TrackCallResult(result, span);
                return result;
            }

            public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecutionBudget(span);
                SplitArguments(arguments, out var standard, out var extras);
                var bound = CallBinder.BindNamedArguments(standard, span, _signature, PythonCallableKind.Builtin);
                var result = _asyncImplementation is null
                    ? _implementation(bound, extras, span, context)
                    : await _asyncImplementation(bound, extras, span, context).ConfigureAwait(false);
                context.Services.State.CallTemporaries.TrackCallResult(result, span);
                return result;
            }

            private void SplitArguments(CallArgumentValue[] arguments, out CallArgumentValue[] standard, out List<KeyValuePair<string, object>> extras)
            {
                var known = (NamedCallableParameterLayout)_signature.Parameters;
                var standardList = new List<CallArgumentValue>(arguments.Length);
                extras = new List<KeyValuePair<string, object>>();
                foreach (var argument in arguments)
                {
                    if (argument.IsPositional || (argument.KeywordName is not null && known.ParameterIndices.ContainsKey(argument.KeywordName)))
                    {
                        standardList.Add(argument);
                        continue;
                    }

                    extras.Add(new KeyValuePair<string, object>(argument.KeywordName ?? string.Empty, argument.Value));
                }

                standard = standardList.ToArray();
            }

            public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            {
                var shortName = ShortName(_signature.Name);
                if (name is "__name__" or "__qualname__")
                {
                    value = PyString.FromString(shortName);
                    return true;
                }

                if (name == "__module__")
                {
                    value = ExceptionTypeValue.SharedModuleLabel(CallableModuleName(_signature.Name));
                    return true;
                }

                if (name == "__new__" && TryGetTypeNewSlot(this, out value))
                {
                    return true;
                }

                value = PyNone.Instance;
                return false;
            }

            public bool TryGetMember(string memberName, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
            {
                if (memberName == "__self__" && TryGetCallableModule(_signature.Name, context, out var module))
                {
                    value = module;
                    return true;
                }

                return TryGetMember(memberName, out value);
            }

            public PyString RenderPython(PyRenderingContext context)
            {
                _ = context;
                return PyString.FromString("<built-in function " + ShortName(_signature.Name) + ">");
            }

            public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

            public int GetPyHashCode() => RuntimeHelpers.GetHashCode(this);

            private static string ShortName(string name)
            {
                var dot = name.LastIndexOf('.');
                return dot < 0 ? name : name.Substring(dot + 1);
            }

            private static string CallableModuleName(string name)
            {
                var dot = name.LastIndexOf('.');
                return dot < 0 ? "builtins" : name.Substring(0, dot);
            }
        }

        private object Load(
            object[] bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (bound.Length < 1 || bound[0] is not ExecutionContext.TextFileHandle file)
            {
                throw new LythonRuntimeException("TypeError", "json.load(fp, *, ...) expects a readable text file handle.", span);
            }

            return DecodeWithClass(file.Read(), ResolveDecoderClass(GetOptional(bound, 1), span), bound, extras, span, context);
        }

        private async ValueTask<object> LoadAsync(
            object[] bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (bound.Length < 1 || bound[0] is not ExecutionContext.TextFileHandle file)
            {
                throw new LythonRuntimeException("TypeError", "json.load(fp, *, ...) expects a readable text file handle.", span);
            }

            return await DecodeWithClassAsync(await file.ReadAsync(-1).ConfigureAwait(false), ResolveDecoderClass(GetOptional(bound, 1), span), bound, extras, span, context).ConfigureAwait(false);
        }

        private object Loads(
            object[] bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (bound.Length < 1 || !PyStringOps.TryAsString(bound[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "json.loads(s, *, ...) expects a string argument.", span);
            }

            return DecodeWithClass(text, ResolveDecoderClass(GetOptional(bound, 1), span), bound, extras, span, context);
        }

        private object Dump(
            object[] bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (bound.Length < 2 || bound[1] is not ExecutionContext.TextFileHandle file)
            {
                throw new LythonRuntimeException("TypeError", "json.dump(obj, fp, *, ...) expects an object and writable text file handle.", span);
            }

            var encoded = EncodeWithClass(bound[0], ResolveEncoderClass(GetOptional(bound, 6), span), bound, extras, JsonDumpCallForm.Dump, span, context);
            if (encoded is not PyString text)
            {
                throw new LythonRuntimeException("TypeError", "json.dump() encoder returned a non-string value.", span);
            }

            _ = file.Write(text);
            return PyNone.Instance;
        }

        private object Dumps(
            object[] bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (bound.Length < 1)
            {
                throw new LythonRuntimeException("TypeError", "json.dumps(obj, *, ...) expects one object argument.", span);
            }

            return EncodeWithClass(bound[0], ResolveEncoderClass(GetOptional(bound, 5), span), bound, extras, JsonDumpCallForm.Dumps, span, context);
        }

        // Every entry point dispatches through the selected class like
        // CPython cls(**kw).decode(s): None selects the matching default
        // class, engine defaults and JSON guest subclasses construct through
        // their own binding, and anything outside the JSON family fails
        // explicitly below.
        private static object ResolveDecoderClass(object clsValue, LythonSourceSpan span)
        {
            if (ReferenceEquals(clsValue, PyNone.Instance))
            {
                return JsonDecoderClass.Instance;
            }

            return ResolveJsonClass(clsValue, span);
        }

        private static object ResolveEncoderClass(object clsValue, LythonSourceSpan span)
        {
            if (ReferenceEquals(clsValue, PyNone.Instance))
            {
                return JsonEncoderClass.Instance;
            }

            return ResolveJsonClass(clsValue, span);
        }

        private static object ResolveJsonClass(object clsValue, LythonSourceSpan span)
        {
            if (clsValue is JsonDecoderClass or JsonEncoderClass)
            {
                return clsValue;
            }

            if (clsValue is PyType guest && guest.JsonBase != JsonBaseKind.None)
            {
                return clsValue;
            }

            throw new LythonRuntimeException("NotImplementedError", "json cls=... custom encoder/decoder classes are not supported by Lython.", span);
        }

        // Dispatches decoding through the selected class like CPython
        // cls(**kw).decode(s): engine defaults and guest subclasses construct
        // through their own binding, then the instance method (possibly an
        // override) runs. Anything outside the JSON family fails explicitly.
        private object DecodeWithClass(
            PyString text,
            object clsValue,
            object[] bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var instance = ConstructDecoderClass(clsValue, bound, extras, span, context);
            if (!PyMemberAccess.TryResolve(instance, "decode", context, span, out var method))
            {
                throw PyMemberAccess.CreateMissingMemberError(instance, "decode", span, context);
            }

            if (method is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "'" + UnboundTypeMethod.PythonTypeName(method, context) + "' object is not callable", span);
            }

            return callable.Invoke([CallArgumentValue.Positional(text)], span, context);
        }

        private async ValueTask<object> DecodeWithClassAsync(
            PyString text,
            object clsValue,
            object[] bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var instance = await ConstructDecoderClassAsync(clsValue, bound, extras, span, context).ConfigureAwait(false);
            if (!PyMemberAccess.TryResolve(instance, "decode", context, span, out var method))
            {
                throw PyMemberAccess.CreateMissingMemberError(instance, "decode", span, context);
            }

            if (method is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "'" + UnboundTypeMethod.PythonTypeName(method, context) + "' object is not callable", span);
            }

            return await callable.InvokeAsync([CallArgumentValue.Positional(text)], span, context).ConfigureAwait(false);
        }

        private static object ConstructDecoderClass(
            object clsValue,
            object[] bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var forwarded = BuildDecoderForwardArgs(bound, extras).ToArray();
            return ((ICallable)clsValue).Invoke(forwarded, span, context);
        }

        private static async ValueTask<object> ConstructDecoderClassAsync(
            object clsValue,
            object[] bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var forwarded = BuildDecoderForwardArgs(bound, extras).ToArray();
            return await ((ICallable)clsValue).InvokeAsync(forwarded, span, context).ConfigureAwait(false);
        }

        private static List<CallArgumentValue> BuildDecoderForwardArgs(
            object[] bound,
            IReadOnlyList<KeyValuePair<string, object>> extras)
        {
            var forwarded = new List<CallArgumentValue>();
            AddDecoderOption(forwarded, "object_hook", GetOptional(bound, 2));
            AddDecoderOption(forwarded, "parse_float", GetOptional(bound, 3));
            AddDecoderOption(forwarded, "parse_int", GetOptional(bound, 4));
            AddDecoderOption(forwarded, "parse_constant", GetOptional(bound, 5));
            AddDecoderOption(forwarded, "object_pairs_hook", GetOptional(bound, 6));
            var strict = GetOptional(bound, 7);
            if (!ReferenceEquals(strict, PyNone.Instance))
            {
                forwarded.Add(CallArgumentValue.Keyword("strict", strict));
            }

            foreach (var extra in extras)
            {
                forwarded.Add(CallArgumentValue.Keyword(extra.Key, extra.Value));
            }

            return forwarded;
        }

        private static void AddDecoderOption(List<CallArgumentValue> forwarded, string name, object value)
        {
            if (!ReferenceEquals(value, PyNone.Instance))
            {
                forwarded.Add(CallArgumentValue.Keyword(name, value));
            }
        }

        private object EncodeWithClass(
            object value,
            object clsValue,
            object[] bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            JsonDumpCallForm callForm,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var instance = ConstructEncoderClass(clsValue, bound, extras, callForm, span, context);
            if (!PyMemberAccess.TryResolve(instance, "encode", context, span, out var method))
            {
                throw PyMemberAccess.CreateMissingMemberError(instance, "encode", span, context);
            }

            if (method is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "'" + UnboundTypeMethod.PythonTypeName(method, context) + "' object is not callable", span);
            }

            return callable.Invoke([CallArgumentValue.Positional(value)], span, context);
        }

        private static object ConstructEncoderClass(
            object clsValue,
            object[] bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            JsonDumpCallForm callForm,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var forwarded = BuildEncoderForwardArgs(bound, extras, callForm).ToArray();
            return ((ICallable)clsValue).Invoke(forwarded, span, context);
        }

        private static List<CallArgumentValue> BuildEncoderForwardArgs(
            object[] bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            JsonDumpCallForm callForm)
        {
            var offset = callForm == JsonDumpCallForm.Dumps ? 0 : 1;
            var forwarded = new List<CallArgumentValue>
            {
                CallArgumentValue.Keyword("skipkeys", GetOptional(bound, offset + 1)),
                CallArgumentValue.Keyword("ensure_ascii", GetOptional(bound, offset + 2)),
                CallArgumentValue.Keyword("check_circular", GetOptional(bound, offset + 3)),
                CallArgumentValue.Keyword("allow_nan", GetOptional(bound, offset + 4)),
                CallArgumentValue.Keyword("indent", GetOptional(bound, offset + 6)),
                CallArgumentValue.Keyword("separators", GetOptional(bound, offset + 7)),
                CallArgumentValue.Keyword("default", GetOptional(bound, offset + 8)),
                CallArgumentValue.Keyword("sort_keys", GetOptional(bound, offset + 9)),
            };

            foreach (var extra in extras)
            {
                forwarded.Add(CallArgumentValue.Keyword(extra.Key, extra.Value));
            }

            return forwarded;
        }
    }
}
