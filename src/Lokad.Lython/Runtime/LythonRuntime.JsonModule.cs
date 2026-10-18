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
                "loads" => new JsonModuleCallable(LythonKnownCallableSignatures.JsonLoads, Loads, LoadsAsync),
                "dump" => new JsonModuleCallable(LythonKnownCallableSignatures.JsonDump, Dump, DumpAsync),
                "dumps" => new JsonModuleCallable(LythonKnownCallableSignatures.JsonDumps, Dumps, DumpsAsync),
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
            private readonly Func<BoundCallArguments, IReadOnlyList<KeyValuePair<string, object>>, LythonSourceSpan, ExecutionContext, object> _implementation;
            private readonly Func<BoundCallArguments, IReadOnlyList<KeyValuePair<string, object>>, LythonSourceSpan, ExecutionContext, ValueTask<object>>? _asyncImplementation;

            public JsonModuleCallable(
                LythonCallableSignature signature,
                Func<BoundCallArguments, IReadOnlyList<KeyValuePair<string, object>>, LythonSourceSpan, ExecutionContext, object> implementation)
                : this(signature, implementation, null)
            {
            }

            public JsonModuleCallable(
                LythonCallableSignature signature,
                Func<BoundCallArguments, IReadOnlyList<KeyValuePair<string, object>>, LythonSourceSpan, ExecutionContext, object> implementation,
                Func<BoundCallArguments, IReadOnlyList<KeyValuePair<string, object>>, LythonSourceSpan, ExecutionContext, ValueTask<object>>? asyncImplementation)
            {
                _signature = signature;
                _implementation = implementation;
                _asyncImplementation = asyncImplementation;
            }

            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecution(span);
                if (IsDefaultCall(arguments, LythonKnownCallableSignatures.JsonLoads))
                {
                    return Instance.LoadsDefault(arguments[0].Value, span, context, asynchronous: false).GetAwaiter().GetResult();
                }
                if (IsDefaultCall(arguments, LythonKnownCallableSignatures.JsonDumps))
                {
                    return Instance.DumpsDefault(arguments[0].Value, span, context, asynchronous: false).GetAwaiter().GetResult();
                }
                var extras = CollectExtraArguments(arguments);
                var bound = CallBinder.BindNamedArgumentsWithPresence(arguments, span, _signature, PythonCallableKind.Builtin);
                var result = _implementation(bound, extras, span, context);
                return result;
            }

            public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecution(span);
                if (IsDefaultCall(arguments, LythonKnownCallableSignatures.JsonLoads))
                {
                    return await Instance.LoadsDefault(arguments[0].Value, span, context, asynchronous: true).ConfigureAwait(false);
                }
                if (IsDefaultCall(arguments, LythonKnownCallableSignatures.JsonDumps))
                {
                    return await Instance.DumpsDefault(arguments[0].Value, span, context, asynchronous: true).ConfigureAwait(false);
                }
                var extras = CollectExtraArguments(arguments);
                var bound = CallBinder.BindNamedArgumentsWithPresence(arguments, span, _signature, PythonCallableKind.Builtin);
                var result = _asyncImplementation is null
                    ? _implementation(bound, extras, span, context)
                    : await _asyncImplementation(bound, extras, span, context).ConfigureAwait(false);
                return result;
            }

            // This complete call shape has no keyword/default slots to bind.
            // Other signatures and every explicit option retain general binding.
            private bool IsDefaultCall(CallArgumentValue[] arguments, LythonCallableSignature signature)
                => ReferenceEquals(_signature, signature) &&
                    arguments.Length == 1 && arguments[0].IsPositional;

            // All four signatures allow extra keywords. The ordinary binder
            // snapshots known values before any suspension; only constructor
            // forwarding needs a separate collection of unknown keywords.
            private IReadOnlyList<KeyValuePair<string, object>> CollectExtraArguments(CallArgumentValue[] arguments)
            {
                var known = (NamedCallableParameterLayout)_signature.Parameters;
                List<KeyValuePair<string, object>>? extras = null;
                foreach (var argument in arguments)
                {
                    if (argument.IsPositional || (argument.KeywordName is not null && known.ParameterIndices.ContainsKey(argument.KeywordName)))
                    {
                        continue;
                    }

                    (extras ??= []).Add(new KeyValuePair<string, object>(argument.KeywordName ?? string.Empty, argument.Value));
                }

                return extras is null ? Array.Empty<KeyValuePair<string, object>>() : extras;
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
            BoundCallArguments bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            context.CheckExecution(span);
            var text = PyTextStream.ReadAll(bound.Values[0], span, context);
            return DecodeWithClass(text, ResolveDecoderClass(GetOptional(bound.Values, 1), span), bound, extras, span, context);
        }

        private async ValueTask<object> LoadAsync(
            BoundCallArguments bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            context.CheckExecution(span);
            var text = await PyTextStream.ReadAllAsync(bound.Values[0], span, context).ConfigureAwait(false);
            return await DecodeWithClassAsync(text, ResolveDecoderClass(GetOptional(bound.Values, 1), span), bound, extras, span, context).ConfigureAwait(false);
        }

        private object Loads(
            BoundCallArguments bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            context.CheckExecution(span);
            var text = RequireLoadsText(bound.Values[0], span);

            return DecodeWithClass(text, ResolveDecoderClass(GetOptional(bound.Values, 1), span), bound, extras, span, context);
        }

        private async ValueTask<object> LoadsAsync(
            BoundCallArguments bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            context.CheckExecution(span);
            var text = RequireLoadsText(bound.Values[0], span);

            return await DecodeWithClassAsync(text, ResolveDecoderClass(GetOptional(bound.Values, 1), span), bound, extras, span, context).ConfigureAwait(false);
        }

        private ValueTask<object> LoadsDefault(object value, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            var text = RequireLoadsText(value, span);
            // Preserve the default decoder's checkpoint before entering the
            // same parser; only binding storage and forwarding are omitted.
            context.CheckExecution(span);
            return ParseJsonTextCoreAsync(text, JsonLoadOptions.Default, context, span, asynchronous);
        }

        private static PyString RequireLoadsText(object value, LythonSourceSpan span)
        {
            if (!PyStringOps.TryAsString(value, out var text))
                throw new LythonRuntimeException("TypeError", "json.loads(s, *, ...) expects a string argument.", span);
            return text;
        }

        private object Dump(
            BoundCallArguments bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            context.CheckExecution(span);
            var chunks = IterateWithClass(bound, extras, span, context);
            foreach (var chunk in PyIteration.ToSequence(chunks, span, context))
            {
                context.CheckExecution(span);
                PyTextStream.Write(bound.Values[1], chunk, span, context);
            }
            return PyNone.Instance;
        }

        private async ValueTask<object> DumpAsync(
            BoundCallArguments bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            context.CheckExecution(span);
            var cls = ResolveEncoderClass(GetOptional(bound.Values, 6), span);
            var forwarded = BuildEncoderForwardArgs(bound, extras, JsonDumpCallForm.Dump).ToArray();
            var instance = await ((ICallable)cls).InvokeAsync(forwarded, span, context).ConfigureAwait(false);
            var method = await PyTextStream.ResolveMemberAsync(instance, "iterencode", span, context).ConfigureAwait(false);
            var chunks = await InvokeCallableTargetAsync(method, span, span, context,
                () => ValueTask.FromResult<CallArgumentValue[]>([CallArgumentValue.Positional(bound.Values[0])])).ConfigureAwait(false);
            await foreach (var chunk in PyIteration.ToSequenceAsync(chunks, span, context).ConfigureAwait(false))
            {
                context.CheckExecution(span);
                await PyTextStream.WriteAsync(bound.Values[1], chunk, span, context).ConfigureAwait(false);
            }
            return PyNone.Instance;
        }

        private static object IterateWithClass(BoundCallArguments bound, IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span, ExecutionContext context)
        {
            var cls = ResolveEncoderClass(GetOptional(bound.Values, 6), span);
            var instance = ConstructEncoderClass(cls, bound, extras, JsonDumpCallForm.Dump, span, context);
            var method = PyTextStream.ResolveMember(instance, "iterencode", span, context);
            return InvokeCallableTarget(method, span, span, context, () => [CallArgumentValue.Positional(bound.Values[0])]);
        }

        private object Dumps(
            BoundCallArguments bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            context.CheckExecution(span);
            if (bound.Values.Length < 1)
            {
                throw new LythonRuntimeException("TypeError", "json.dumps(obj, *, ...) expects one object argument.", span);
            }

            return EncodeWithClass(bound.Values[0], ResolveEncoderClass(GetOptional(bound.Values, 5), span), bound, extras, JsonDumpCallForm.Dumps, span, context);
        }

        private async ValueTask<object> DumpsDefault(object value, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            // Keep the module body, builtin constructor, bound encode and encoder
            // body checkpoints while omitting their unobservable temporary storage.
            context.CheckExecution(span);
            context.CheckExecution(span);
            context.CheckExecution(span);
            context.CheckExecution(span);
            return await SerializeJsonTextCoreAsync(value, JsonDumpOptions.Default, context, span, asynchronous).ConfigureAwait(false);
        }

        private async ValueTask<object> DumpsAsync(
            BoundCallArguments bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            context.CheckExecution(span);
            if (bound.Values.Length < 1)
            {
                throw new LythonRuntimeException("TypeError", "json.dumps(obj, *, ...) expects one object argument.", span);
            }

            return await EncodeWithClassAsync(bound.Values[0], ResolveEncoderClass(GetOptional(bound.Values, 5), span), bound, extras, JsonDumpCallForm.Dumps, span, context).ConfigureAwait(false);
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
        // cls(**kw).decode(s): guest subclasses retain construction and method
        // override dispatch. The exact builtin binds the same scanner options
        // without creating an unobservable temporary decoder and public attributes.
        private object DecodeWithClass(
            PyString text,
            object clsValue,
            BoundCallArguments bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            if (ReferenceEquals(clsValue, JsonDecoderClass.Instance))
            {
                context.CheckExecution(span);
                var options = JsonDecoderClass.BindModuleLoadOptions(bound, extras, span);
                return ParseJsonText(text, options, context, span);
            }
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
            BoundCallArguments bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            if (ReferenceEquals(clsValue, JsonDecoderClass.Instance))
            {
                context.CheckExecution(span);
                var options = JsonDecoderClass.BindModuleLoadOptions(bound, extras, span);
                return await ParseJsonTextAsync(text, options, context, span).ConfigureAwait(false);
            }
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
            BoundCallArguments bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var forwarded = BuildDecoderForwardArgs(bound, extras).ToArray();
            return ((ICallable)clsValue).Invoke(forwarded, span, context);
        }

        private static async ValueTask<object> ConstructDecoderClassAsync(
            object clsValue,
            BoundCallArguments bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var forwarded = BuildDecoderForwardArgs(bound, extras).ToArray();
            return await ((ICallable)clsValue).InvokeAsync(forwarded, span, context).ConfigureAwait(false);
        }

        private static List<CallArgumentValue> BuildDecoderForwardArgs(
            BoundCallArguments bound,
            IReadOnlyList<KeyValuePair<string, object>> extras)
        {
            var forwarded = new List<CallArgumentValue>();
            AddDecoderOption(forwarded, "object_hook", GetOptional(bound.Values, 2));
            AddDecoderOption(forwarded, "parse_float", GetOptional(bound.Values, 3));
            AddDecoderOption(forwarded, "parse_int", GetOptional(bound.Values, 4));
            AddDecoderOption(forwarded, "parse_constant", GetOptional(bound.Values, 5));
            AddDecoderOption(forwarded, "object_pairs_hook", GetOptional(bound.Values, 6));
            if (bound.Assigned[7])
            {
                forwarded.Add(CallArgumentValue.Keyword("strict", bound.Values[7]));
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
            BoundCallArguments bound,
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

        private async ValueTask<object> EncodeWithClassAsync(
            object value,
            object clsValue,
            BoundCallArguments bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            JsonDumpCallForm callForm,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var forwarded = BuildEncoderForwardArgs(bound, extras, callForm).ToArray();
            var instance = await ((ICallable)clsValue).InvokeAsync(forwarded, span, context).ConfigureAwait(false);
            if (!PyMemberAccess.TryResolve(instance, "encode", context, span, out var method))
            {
                throw PyMemberAccess.CreateMissingMemberError(instance, "encode", span, context);
            }

            if (method is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "'" + UnboundTypeMethod.PythonTypeName(method, context) + "' object is not callable", span);
            }

            return await callable.InvokeAsync([CallArgumentValue.Positional(value)], span, context).ConfigureAwait(false);
        }

        private static object ConstructEncoderClass(
            object clsValue,
            BoundCallArguments bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            JsonDumpCallForm callForm,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var forwarded = BuildEncoderForwardArgs(bound, extras, callForm).ToArray();
            return ((ICallable)clsValue).Invoke(forwarded, span, context);
        }

        private static List<CallArgumentValue> BuildEncoderForwardArgs(
            BoundCallArguments bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            JsonDumpCallForm callForm)
        {
            var offset = callForm == JsonDumpCallForm.Dumps ? 0 : 1;
            var forwarded = new List<CallArgumentValue>
            {
                CallArgumentValue.Keyword("skipkeys", bound.Assigned[offset + 1] ? bound.Values[offset + 1] : false),
                CallArgumentValue.Keyword("ensure_ascii", bound.Assigned[offset + 2] ? bound.Values[offset + 2] : true),
                CallArgumentValue.Keyword("check_circular", bound.Assigned[offset + 3] ? bound.Values[offset + 3] : true),
                CallArgumentValue.Keyword("allow_nan", bound.Assigned[offset + 4] ? bound.Values[offset + 4] : true),
                CallArgumentValue.Keyword("indent", GetOptional(bound.Values, offset + 6)),
                CallArgumentValue.Keyword("separators", GetOptional(bound.Values, offset + 7)),
                CallArgumentValue.Keyword("default", GetOptional(bound.Values, offset + 8)),
                CallArgumentValue.Keyword("sort_keys", bound.Assigned[offset + 9] ? bound.Values[offset + 9] : false),
            };

            foreach (var extra in extras)
            {
                forwarded.Add(CallArgumentValue.Keyword(extra.Key, extra.Value));
            }

            return forwarded;
        }
    }
}
