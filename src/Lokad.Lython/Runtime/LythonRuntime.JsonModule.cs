using System.Globalization;
using System.Runtime.CompilerServices;
using System.Numerics;
using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

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
        internal sealed class JsonModuleCallable : IOperandBoundCallable, IPyRenderableValue, IPyDynamicAttributes, IPyContextualDynamicAttributes, IPyBoundEngineMethod, IPyHashableValue
        {
            private readonly LythonCallableSignature _signature;
            private readonly Func<BoundBuiltinArguments, IReadOnlyList<KeyValuePair<string, object>>, LythonSourceSpan, ExecutionContext, object> _implementation;
            private readonly Func<BoundBuiltinArguments, IReadOnlyList<KeyValuePair<string, object>>, LythonSourceSpan, ExecutionContext, ValueTask<object>>? _asyncImplementation;

            public JsonModuleCallable(
                LythonCallableSignature signature,
                Func<BoundBuiltinArguments, IReadOnlyList<KeyValuePair<string, object>>, LythonSourceSpan, ExecutionContext, object> implementation)
                : this(signature, implementation, null)
            {
            }

            public JsonModuleCallable(
                LythonCallableSignature signature,
                Func<BoundBuiltinArguments, IReadOnlyList<KeyValuePair<string, object>>, LythonSourceSpan, ExecutionContext, object> implementation,
                Func<BoundBuiltinArguments, IReadOnlyList<KeyValuePair<string, object>>, LythonSourceSpan, ExecutionContext, ValueTask<object>>? asyncImplementation)
            {
                _signature = signature;
                _implementation = implementation;
                _asyncImplementation = asyncImplementation;
            }

            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
                => InvokeBorrowed(arguments, span, context);

            public object InvokeBorrowed(ReadOnlySpan<CallArgumentValue> arguments, LythonSourceSpan span, ExecutionContext context)
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
                return InvokeOptions(new CallBinder.PacketInputs(arguments), span, context, asynchronous: false)
                    .GetAwaiter().GetResult();
            }

            public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
                => InvokeBorrowedAsync(arguments, span, context);

            public ValueTask<object> InvokeBorrowedAsync(ReadOnlySpan<CallArgumentValue> arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecution(span);
                if (IsDefaultCall(arguments, LythonKnownCallableSignatures.JsonLoads))
                {
                    return Instance.LoadsDefault(arguments[0].Value, span, context, asynchronous: true);
                }
                if (IsDefaultCall(arguments, LythonKnownCallableSignatures.JsonDumps))
                {
                    return Instance.DumpsDefault(arguments[0].Value, span, context, asynchronous: true);
                }
                return InvokeOptions(new CallBinder.PacketInputs(arguments), span, context, asynchronous: true);
            }

            LythonCallableSignature IOperandBoundCallable.BindingSignature => _signature;

            ValueTask<object> IOperandBoundCallable.InvokeFromOperands(Span<ExecutableSlot> operands,
                ExecutableCallSite call, ExecutionContext context, bool asynchronous)
            {
                return InvokeOptions(new CallBinder.OperandInputs(operands, call.Arguments, context),
                    call.CallSpan, context, asynchronous);
            }

            private ValueTask<object> InvokeOptions<T>(T arguments, LythonSourceSpan span,
                ExecutionContext context, bool asynchronous)
                where T : CallBinder.IBindingInputs, allows ref struct
            {
                var bound = CallBinder.BindNamedArgumentsWithExtras(arguments, span, _signature,
                    PythonCallableKind.Builtin, out var extras);
                return InvokeBoundOptions(bound, extras, span, context, asynchronous);
            }

            internal bool AcceptsMappingOperands(int positionalCount)
                => _signature.Parameters is NamedCallableParameterLayout
                {
                    PositionalOnlyCount: 0, AllowsExtraPositional: false, AllowsExtraKeywords: true,
                } parameters && positionalCount <= parameters.ParameterNames.Length &&
                    parameters.MaximumPositionalArgumentCount.Accepts(positionalCount);

            internal async ValueTask<object> InvokeFromMapping(Memory<ExecutableSlot> positional, PyDict dictionary,
                LythonSourceSpan mappingSpan, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
            {
                var parameters = (NamedCallableParameterLayout)_signature.Parameters;
                var size = parameters.ParameterNames.Length;
                var bytes = 64L + 8L * size + (size > 64 ? 32L + size - 64 : 0);
                using var storage = context.MemoryGovernor.ReserveTemporary(bytes, mappingSpan);
                context.CheckExecution(mappingSpan);
                var bound = CallBinder.BindExactKeywordOptions(positional.Span, dictionary, parameters, context,
                    mappingSpan, storage, out var extras, out var duplicate);
                context.EnterInterpreterFrame(span);
                try
                {
                    context.CheckExecution(span);
                    if (duplicate >= 0)
                        throw CallErrors.MultipleValues(PythonCallableKind.Builtin, _signature.Name, parameters.ParameterNames[duplicate], span);
                    CallBinder.ValidateRequiredNamedArguments(bound.Assigned, parameters, PythonCallableKind.Builtin, _signature.Name, span);
                    return RuntimeValue(await InvokeBoundOptions(bound, extras, span, context, asynchronous).ConfigureAwait(false));
                }
                catch (Exception error) when (TryTranslateCallableException(error, span, out var translated)) { throw translated; }
                finally { context.LeaveInterpreterFrame(); }
            }

            private ValueTask<object> InvokeBoundOptions(BoundBuiltinArguments bound,
                IReadOnlyList<KeyValuePair<string, object>> extras, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
                => !asynchronous || _asyncImplementation is null
                    ? new(_implementation(bound, extras, span, context))
                    : _asyncImplementation(bound, extras, span, context);

            // This complete call shape has no keyword/default slots to bind.
            // Other signatures and every explicit option retain general binding.
            private bool IsDefaultCall(ReadOnlySpan<CallArgumentValue> arguments, LythonCallableSignature signature)
                => ReferenceEquals(_signature, signature) &&
                    arguments.Length == 1 && arguments[0].IsPositional;

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
            BoundBuiltinArguments bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            context.CheckExecution(span);
            var text = ReadDocumentAsync(bound.Values[0], span, context, false).GetAwaiter().GetResult();
            return DecodeWithClass(text, ResolveDecoderClass(GetOptional(bound.Values, 1), span), bound, extras, span, context);
        }

        private async ValueTask<object> LoadAsync(
            BoundBuiltinArguments bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            context.CheckExecution(span);
            var text = await ReadDocumentAsync(bound.Values[0], span, context, true).ConfigureAwait(false);
            return await DecodeWithClassAsync(text, ResolveDecoderClass(GetOptional(bound.Values, 1), span), bound, extras, span, context).ConfigureAwait(false);
        }

        private object Loads(
            BoundBuiltinArguments bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            context.CheckExecution(span);
            var text = RequireLoadsText(bound.Values[0], context, span);

            return DecodeWithClass(text, ResolveDecoderClass(GetOptional(bound.Values, 1), span), bound, extras, span, context);
        }

        private async ValueTask<object> LoadsAsync(
            BoundBuiltinArguments bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            context.CheckExecution(span);
            var text = RequireLoadsText(bound.Values[0], context, span);

            return await DecodeWithClassAsync(text, ResolveDecoderClass(GetOptional(bound.Values, 1), span), bound, extras, span, context).ConfigureAwait(false);
        }

        private ValueTask<object> LoadsDefault(object value, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            var text = RequireLoadsText(value, context, span);
            return ParseJsonTextCoreAsync(text, JsonLoadOptions.Default, context, span, asynchronous);
        }

        private object Dump(
            BoundBuiltinArguments bound, IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span, ExecutionContext context)
            => DumpCoreAsync(bound, extras, span, context, false).GetAwaiter().GetResult();

        private ValueTask<object> DumpAsync(
            BoundBuiltinArguments bound, IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span, ExecutionContext context)
            => DumpCoreAsync(bound, extras, span, context, true);

        private async ValueTask<object> DumpCoreAsync(
            BoundBuiltinArguments bound, IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            object chunks;
            if (await CanUseDefaultEncoderAsync(bound, extras, 1, span, context, asynchronous).ConfigureAwait(false))
                chunks = JsonEncodeIterator.Create(bound.Values[0], JsonDumpOptions.Default, context, span);
            else
            {
                var cls = ResolveEncoderClass(GetOptional(bound.Values, 6), span);
                if (cls is JsonEncoderClass && extras.Count == 0)
                    chunks = await InvokeBoundEncoderAsync(bound, 1, span, context, asynchronous).ConfigureAwait(false);
                else
                {
                    var instance = await ConstructEncoderClassAsync(cls, bound, extras, JsonDumpCallForm.Dump, span, context, asynchronous).ConfigureAwait(false);
                    var arguments = new[] { CallArgumentValue.Positional(bound.Values[0]) };
                    chunks = asynchronous
                        ? await InvokeCallableTargetAsync(await PyTextStream.ResolveMemberAsync(instance, "iterencode", span, context).ConfigureAwait(false),
                            span, span, context, arguments).ConfigureAwait(false)
                        : InvokeCallableTarget(PyTextStream.ResolveMember(instance, "iterencode", span, context),
                            span, span, context, arguments);
                }
            }

            if (chunks is JsonEncodeIterator native && bound.Values[1] is ExecutionContext.TextFileHandle textFile)
            {
                await native.WriteToAsync(textFile, span, asynchronous).ConfigureAwait(false);
                return PyNone.Instance;
            }
            if (asynchronous)
                await foreach (var chunk in PyIteration.Cursor.ForAsyncEnumeration(chunks, span, context).ConfigureAwait(false))
                {
                    context.CheckExecution(span);
                    await PyTextStream.WriteAsync(bound.Values[1], chunk, span, context).ConfigureAwait(false);
                }
            else
                foreach (var chunk in PyIteration.Cursor.Create(chunks, span, context))
                {
                    context.CheckExecution(span);
                    PyTextStream.Write(bound.Values[1], chunk, span, context);
                }
            return PyNone.Instance;
        }

        private object Dumps(
            BoundBuiltinArguments bound, IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span, ExecutionContext context)
            => DumpsCoreAsync(bound, extras, span, context, false).GetAwaiter().GetResult();

        private ValueTask<object> DumpsAsync(
            BoundBuiltinArguments bound, IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span, ExecutionContext context)
            => DumpsCoreAsync(bound, extras, span, context, true);

        private async ValueTask<object> DumpsCoreAsync(
            BoundBuiltinArguments bound, IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            if (bound.Values.Length < 1)
                throw new LythonRuntimeException("TypeError", "json.dumps(obj, *, ...) expects one object argument.", span);

            var value = bound.Values[0];
            if (await CanUseDefaultEncoderAsync(bound, extras, 0, span, context, asynchronous).ConfigureAwait(false))
                return await DumpsDefault(value, span, context, asynchronous).ConfigureAwait(false);

            var cls = ResolveEncoderClass(GetOptional(bound.Values, 5), span);
            if (cls is JsonEncoderClass && extras.Count == 0)
                return await InvokeBoundEncoderAsync(bound, 0, span, context, asynchronous).ConfigureAwait(false);

            var instance = await ConstructEncoderClassAsync(cls, bound, extras, JsonDumpCallForm.Dumps, span, context, asynchronous).ConfigureAwait(false);
            if (instance is JsonEncoderObject native)
                return await InvokeNativeEncoderAsync(native, value, span, context, asynchronous).ConfigureAwait(false);
            var arguments = new[] { CallArgumentValue.Positional(value) };
            return asynchronous
                ? await InvokeCallableTargetAsync(await PyTextStream.ResolveMemberAsync(instance, "encode", span, context).ConfigureAwait(false),
                    span, span, context, arguments).ConfigureAwait(false)
                : InvokeCallableTarget(PyTextStream.ResolveMember(instance, "encode", span, context),
                    span, span, context, arguments);
        }

        private async ValueTask<object> DumpsDefault(object value, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            return await SerializeJsonTextCoreAsync(value, JsonDumpOptions.Default, context, span, asynchronous).ConfigureAwait(false);
        }

        private static object EncodingFlag(BoundBuiltinArguments bound, int index, bool fallback)
            => bound.Assigned[index] ? bound.Values[index] : fallback;

        private static async ValueTask<object> InvokeBoundEncoderAsync(BoundBuiltinArguments bound, int offset,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            // Construction enumerates a supplied separators iterable before it
            // invokes either encoder method. Keep that order even for strings.
            context.CheckExecution(span);
            var indent = GetOptional(bound.Values, offset + 6);
            var separatorValue = GetOptional(bound.Values, offset + 7);
            var separators = separatorValue is PyNone
                ? new JsonSeparators(indent is PyNone ? JsonDumpOptions.Default.ItemSeparator : PrettyItemSeparator,
                    JsonDumpOptions.Default.KeySeparator)
                : await ParseJsonSeparatorsAsync(separatorValue, span, context, asynchronous).ConfigureAwait(false);

            var value = bound.Values[0];
            var oneShot = offset == 0;
            context.EnterInterpreterFrame(span);
            try
            {
                context.CheckExecution(span);
                var asciiFlag = EncodingFlag(bound, offset + 2, true);
                if (oneShot && value is PyString text)
                {
                    var ascii = await JsonOptionTruthAsync(asciiFlag, context, span, asynchronous).ConfigureAwait(false);
                    return await SerializeJsonTextCoreAsync(text, JsonDumpOptions.Default with { EnsureAscii = ascii },
                        context, span, asynchronous).ConfigureAwait(false);
                }

                var circular = await JsonOptionTruthAsync(EncodingFlag(bound, offset + 3, true), context, span, asynchronous).ConfigureAwait(false);
                var ensureAscii = await JsonOptionTruthAsync(asciiFlag, context, span, asynchronous).ConfigureAwait(false);
                var indentText = ParseJsonIndent(indent, span, context);
                var key = RequireJsonSeparator(separators.KeySeparator, span);
                var item = RequireJsonSeparator(separators.ItemSeparator, span);
                var callback = GetOptional(bound.Values, offset + 8);
                var sort = EncodingFlag(bound, offset + 9, false);
                var skip = EncodingFlag(bound, offset + 1, false);
                var allowNan = EncodingFlag(bound, offset + 4, true);
                if (oneShot)
                {
                    sort = await JsonOptionTruthAsync(sort, context, span, asynchronous).ConfigureAwait(false);
                    skip = await JsonOptionTruthAsync(skip, context, span, asynchronous).ConfigureAwait(false);
                    allowNan = await JsonOptionTruthAsync(allowNan, context, span, asynchronous).ConfigureAwait(false);
                }

                var options = new JsonDumpOptions(skip, ensureAscii, circular, allowNan, indentText, item, key,
                    callback is PyNone ? JsonDumpOptions.Default.DefaultCallable : callback, sort);
                return oneShot
                    ? await SerializeJsonTextCoreAsync(value, options, context, span, asynchronous).ConfigureAwait(false)
                    : JsonEncodeIterator.Create(value, options, context, span);
            }
            catch (Exception error) when (TryTranslateCallableException(error, span, out var translated)) { throw translated; }
            finally { context.LeaveInterpreterFrame(); }
        }

        // Preserve Python's short-circuit option effects before selecting the
        // immutable default options. No instance or caller token is cached.
        private static async ValueTask<bool> CanUseDefaultEncoderAsync(BoundBuiltinArguments bound,
            IReadOnlyList<KeyValuePair<string, object>> extras, int offset, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous)
        {
            object Flag(int index, bool fallback) => bound.Assigned[offset + index] ? bound.Values[offset + index] : fallback;
            return !await JsonOptionTruthAsync(Flag(1, false), context, span, asynchronous).ConfigureAwait(false)
                && await JsonOptionTruthAsync(Flag(2, true), context, span, asynchronous).ConfigureAwait(false)
                && await JsonOptionTruthAsync(Flag(3, true), context, span, asynchronous).ConfigureAwait(false)
                && await JsonOptionTruthAsync(Flag(4, true), context, span, asynchronous).ConfigureAwait(false)
                && GetOptional(bound.Values, offset + 5) is PyNone
                && GetOptional(bound.Values, offset + 6) is PyNone
                && GetOptional(bound.Values, offset + 7) is PyNone
                && GetOptional(bound.Values, offset + 8) is PyNone
                && !await JsonOptionTruthAsync(Flag(9, false), context, span, asynchronous).ConfigureAwait(false)
                && extras.Count == 0;
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
            BoundBuiltinArguments bound,
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
            BoundBuiltinArguments bound,
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
            BoundBuiltinArguments bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var forwarded = BuildDecoderForwardArgs(bound, extras).ToArray();
            return ((ICallable)clsValue).Invoke(forwarded, span, context);
        }

        private static async ValueTask<object> ConstructDecoderClassAsync(
            object clsValue,
            BoundBuiltinArguments bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var forwarded = BuildDecoderForwardArgs(bound, extras).ToArray();
            return await ((ICallable)clsValue).InvokeAsync(forwarded, span, context).ConfigureAwait(false);
        }

        private static List<CallArgumentValue> BuildDecoderForwardArgs(
            BoundBuiltinArguments bound,
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

        private static async ValueTask<object> InvokeNativeEncoderAsync(JsonEncoderObject encoder,
            object value, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            // Keep the same managed frame and exception policy as ordinary
            // callable invocation while borrowing the already-evaluated value.
            context.EnterInterpreterFrame(span);
            try { return RuntimeValue(await encoder.EncodeValueAsync(value, span, context, asynchronous).ConfigureAwait(false)); }
            catch (Exception error) when (TryTranslateCallableException(error, span, out var translated)) { throw translated; }
            finally { context.LeaveInterpreterFrame(); }
        }

        private static ValueTask<object> ConstructEncoderClassAsync(
            object clsValue,
            BoundBuiltinArguments bound,
            IReadOnlyList<KeyValuePair<string, object>> extras,
            JsonDumpCallForm callForm,
            LythonSourceSpan span,
            ExecutionContext context,
            bool asynchronous)
        {
            var forwarded = BuildEncoderForwardArgs(bound, extras, callForm).ToArray();
            var callable = (ICallable)clsValue;
            return asynchronous ? callable.InvokeAsync(forwarded, span, context)
                : ValueTask.FromResult(callable.Invoke(forwarded, span, context));
        }

        private static List<CallArgumentValue> BuildEncoderForwardArgs(
            BoundBuiltinArguments bound,
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
