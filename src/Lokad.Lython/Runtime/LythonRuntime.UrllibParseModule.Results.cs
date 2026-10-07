using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class UrllibParseModule
    {
        private static readonly PyString AsciiName = PyString.FromString("ascii");
        private static readonly PyString StrictName = PyString.FromString("strict");

        internal sealed class UrlResultType : ICallable, INamedRuntimeCallable, IPyDynamicAttributes, IPyContextualDynamicAttributes, IPyRenderableValue, IPyHashableValue
        {
            private static readonly string[] SplitFields = ["scheme", "netloc", "path", "query", "fragment"];
            private static readonly string[] ParseFields = ["scheme", "netloc", "path", "params", "query", "fragment"];
            private static readonly PyTuple SplitFieldNames = new(SplitFields.Select(PyString.FromString).Cast<object>());
            private static readonly PyTuple ParseFieldNames = new(ParseFields.Select(PyString.FromString).Cast<object>());
            private static readonly UrlResultType Split = new("SplitResult", false, false);
            private static readonly UrlResultType Parse = new("ParseResult", true, false);
            private static readonly UrlResultType SplitBytes = new("SplitResultBytes", false, true);
            private static readonly UrlResultType ParseBytes = new("ParseResultBytes", true, true);
            private readonly LythonCallableSignature _signature;
            private readonly PyString _name;
            internal readonly string[] Fields;
            internal readonly PyTuple FieldNames;
            internal readonly bool Parameters, Binary;
            private UrlResultType(string name, bool parameters, bool binary)
            {
                _name = PyString.FromString(name); Name = "urllib.parse." + name;
                Parameters = parameters; Binary = binary;
                Fields = parameters ? ParseFields : SplitFields;
                FieldNames = parameters ? ParseFieldNames : SplitFieldNames;
                _signature = LythonCallableSignature.Create(Name, Fields);
            }
            public string Name { get; }
            internal string ShortName => _name.AsString();
            internal static UrlResultType Get(bool parameters, bool binary) => (parameters, binary) switch
            { (true, true) => ParseBytes, (true, false) => Parse, (false, true) => SplitBytes, _ => Split };
            internal static bool TryFind(string name, [MaybeNullWhen(false)] out object value)
            {
                value = name switch { "SplitResult" => Split, "ParseResult" => Parse, "SplitResultBytes" => SplitBytes, "ParseResultBytes" => ParseBytes, _ => MissingMemberValue.Instance };
                return !ReferenceEquals(value, MissingMemberValue.Instance);
            }
            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecutionBudget(span);
                var bound = CallBinder.BindNamedArguments(arguments, span, _signature, PythonCallableKind.Builtin);
                return UrlResult.Create(this, bound, context, span);
            }
            public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            {
                value = name switch
                {
                    "__name__" or "__qualname__" => _name,
                    "__module__" => ModuleName,
                    "_fields" or "__match_args__" => FieldNames,
                    "_make" => new UrlResultMethod(this, null, "_make"),
                    _ => MissingMemberValue.Instance,
                };
                return !ReferenceEquals(value, MissingMemberValue.Instance);
            }
            public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
            {
                if (name == "_field_defaults") { value = ResultFieldDefaults(Parameters, context, span); return true; }
                return TryGetMember(name, out value);
            }
            public int GetPyHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
            public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<class '" + Name + "'>");
            public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
        }

        internal sealed class UrlResult : IPySequenceValue, IPyIndexableValue, IPyTruthyValue, IPyIterableValue,
            IPyRenderableValue, IPyContextualDynamicAttributes, IPyHashableValue, IPyGovernedValue, IPyOwnershipSnapshot
        {
            private const long ShellBytes = 64;
            private readonly PyTuple _values;
            internal UrlResultType Type { get; }
            private UrlResult(UrlResultType type, PyTuple values, MemoryGovernor governor, LythonSourceSpan span)
            { Type = type; _values = values; OwnerMemoryGovernor = governor; AllocationSpan = span; }
            internal static UrlResult Create(UrlResultType type, IEnumerable<object> values, ExecutionContext context, LythonSourceSpan span)
            {
                context.MemoryGovernor.EnsureCanReserve(ShellBytes + PyTuple.EstimateApproximateBytes(type.Fields.Length), span);
                var tuple = new PyTuple(values, context.MemoryGovernor, span);
                context.State.CallTemporaries.TrackFreshMutable(tuple, tuple.CommittedStorageBytes);
                context.MemoryGovernor.Reserve(ShellBytes, span);
                context.MemoryGovernor.Commit(ShellBytes);
                var result = new UrlResult(type, tuple, context.MemoryGovernor, span);
                context.State.CallTemporaries.TrackFreshMutable(result, ShellBytes);
                return result;
            }
            public MemoryGovernor? OwnerMemoryGovernor { get; }
            public LythonSourceSpan? AllocationSpan { get; }
            public bool TrySnapshotOwnership(out long bytes) => OwnershipSnapshot.Owned(OwnerMemoryGovernor, ShellBytes, out bytes);
            public int Count => _values.Count;
            public int Length => Count;
            public object this[int index] => _values[index];
            public object GetItem(int index) => _values[index];
            public object GetIndex(int index) => _values[index];
            public object GetSlice(IEnumerable<int> indices) => new PyTuple(indices.Select(index => _values[index]), OwnerMemoryGovernor!, AllocationSpan);
            public object CreateSlice(IEnumerable<object> items) => new PyTuple(items, OwnerMemoryGovernor!, AllocationSpan);
            public bool IsTruthy() => true;
            public IEnumerable<object> Iterate() => _values;
            public IEnumerator<object> GetEnumerator() => _values.GetEnumerator();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
            public int GetPyHashCode() => PyTupleLike.ComputeHashCode(_values);
            public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
            {
                var index = Array.IndexOf(Type.Fields, name);
                if (index >= 0) { value = _values[index]; return true; }
                value = name switch
                {
                    "__class__" => Type,
                    "__module__" => ModuleName,
                    "_fields" or "__match_args__" => Type.FieldNames,
                    "_field_defaults" => ResultFieldDefaults(Type.Parameters, context, span),
                    "_make" => new UrlResultMethod(Type, null, "_make"),
                    "geturl" or "_replace" or "_asdict" => new UrlResultMethod(Type, this, name),
                    "encode" when !Type.Binary => new UrlResultMethod(Type, this, name),
                    "decode" when Type.Binary => new UrlResultMethod(Type, this, name),
                    "username" or "password" or "hostname" or "port" => AuthorityMember(name, context, span),
                    _ => MissingMemberValue.Instance,
                };
                return !ReferenceEquals(value, MissingMemberValue.Instance) || TupleMembers.TryGetMember(this, name, out value);
            }
            private object AuthorityMember(string name, ExecutionContext context, LythonSourceSpan span)
            {
                ReadOnlyMemory<byte> memory;
                if (Type.Binary && _values[1] is PyBytes bytes) memory = bytes.Memory;
                else if (!Type.Binary && _values[1] is PyString text) memory = text.Utf8Bytes;
                else throw new LythonRuntimeException(_values[1] is PyString or PyBytes ? "TypeError" : "AttributeError", "invalid netloc component", span);
                var source = memory.Span; var afterAt = 0;
                for (var i = 0; i < source.Length; i++)
                { if ((i & 1023) == 0) context.CheckExecutionBudget(span); if (source[i] == '@') afterAt = i + 1; }
                if (name is "username" or "password")
                {
                    if (afterAt == 0) return PyNone.Instance;
                    var colon = FindByte(source, (byte)':', 0, afterAt - 1, context, span);
                    if (name == "username") return Slice(0, colon < 0 ? afterAt - 1 : colon);
                    return colon < 0 ? PyNone.Instance : Slice(colon + 1, afterAt - colon - 2);
                }
                var open = FindByte(source, (byte)'[', afterAt, source.Length, context, span);
                int hostStart, hostEnd, portStart;
                if (open >= 0)
                {
                    hostStart = open + 1;
                    var close = FindByte(source, (byte)']', hostStart, source.Length, context, span);
                    hostEnd = close < 0 ? source.Length : close;
                    var colon = close < 0 ? -1 : FindByte(source, (byte)':', close + 1, source.Length, context, span);
                    portStart = colon < 0 ? source.Length : colon + 1;
                }
                else
                {
                    hostStart = afterAt;
                    var colon = FindByte(source, (byte)':', afterAt, source.Length, context, span);
                    hostEnd = colon < 0 ? source.Length : colon;
                    portStart = colon < 0 ? source.Length : colon + 1;
                }
                if (name == "port")
                {
                    if (portStart == source.Length) return PyNone.Instance;
                    var number = 0; var range = false;
                    for (var i = portStart; i < source.Length; i++)
                    {
                        if ((i & 1023) == 0) context.CheckExecutionBudget(span);
                        if (source[i] is < (byte)'0' or > (byte)'9') throw new LythonRuntimeException("ValueError", "Port could not be cast to integer", span);
                        if (!range) { number = number * 10 + source[i] - '0'; range = number > 65535; }
                    }
                    if (range) throw new LythonRuntimeException("ValueError", "Port out of range 0-65535", span);
                    return new BigInteger(number);
                }
                if (hostStart == hostEnd) return PyNone.Instance;
                var host = Slice(hostStart, hostEnd - hostStart);
                if (host is PyString hostname) return LowerHost(hostname, context, span, true);
                var raw = ((PyBytes)host).Bytes;
                var zone = FindByte(raw, (byte)'%', 0, raw.Length, context, span);
                context.MemoryGovernor.EnsureCanReserve(PyBytes.EstimateApproximateBytes(raw.Length), span);
                var lowered = raw.ToArray();
                for (var i = 0; i < (zone < 0 ? raw.Length : zone); i++)
                { if ((i & 1023) == 0) context.CheckExecutionBudget(span); if (lowered[i] is >= (byte)'A' and <= (byte)'Z') lowered[i] += 32; }
                var lowerBytes = OwnBytes(lowered, context, span);
                GC.KeepAlive(host);
                return lowerBytes;

                object Slice(int start, int length)
                {
                    if (!Type.Binary) return OwnedText(memory.Slice(start, length), context, span);
                    context.MemoryGovernor.EnsureCanReserve(PyBytes.EstimateApproximateBytes(length), span);
                    return OwnBytes(memory.Slice(start, length).ToArray(), context, span);
                }
            }
            public PyString RenderPython(PyRenderingContext context)
            {
                var builder = new GovernedByteBuilder(context.Context.MemoryGovernor);
                try
                {
                    builder.AppendAscii(Type.ShortName); builder.Append((byte)'(');
                    for (var i = 0; i < Count; i++)
                    {
                        if (i > 0) builder.AppendAscii(", ");
                        builder.AppendAscii(Type.Fields[i]); builder.Append((byte)'=');
                        builder.Append(PyRendering.ToReprPyString(_values[i], context));
                    }
                    builder.Append((byte)')');
                    return builder.ToPyStringAndRelease();
                }
                finally { builder.Release(); }
            }
            public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
        }

        private static PyDict ResultFieldDefaults(bool parameters, ExecutionContext context, LythonSourceSpan span)
        {
            var state = context.State;
            var existing = parameters ? state.UrlParseFieldDefaults : state.UrlSplitFieldDefaults;
            if (existing is not null) return existing;
            var defaults = new PyDict(context.MemoryGovernor, span);
            context.State.CallTemporaries.TrackFreshMutable(defaults, defaults.CommittedStorageBytes);
            if (parameters) state.UrlParseFieldDefaults = defaults;
            else state.UrlSplitFieldDefaults = defaults;
            return defaults;
        }

        private sealed class UrlResultMethod(UrlResultType type, UrlResult? receiver, string method) : ICallable, IPyDynamicAttributes, IPyRenderableValue
        {
            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
                => InvokeCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();
            public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
                => InvokeCoreAsync(arguments, span, context, true);
            private async ValueTask<object> InvokeCoreAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
            {
                context.CheckExecutionBudget(span);
                if (method == "_make")
                {
                    var bound = CallBinder.BindNamedArguments(arguments, span, LythonCallableSignature.Create(type.Name + "._make", ["iterable"]), PythonCallableKind.Method);
                    var values = await UrlComponentsAsync(bound[0], span, context, asynchronous).ConfigureAwait(false);
                    if (values.Count != type.Fields.Length) throw new LythonRuntimeException("TypeError", "wrong number of result fields", span);
                    return UrlResult.Create(type, values, context, span);
                }
                if (method == "_replace")
                {
                    context.MemoryGovernor.EnsureCanReserve(PyTuple.EstimateApproximateBytes(receiver!.Count) + 64, span);
                    var values = receiver.ToArray(); var seen = 0;
                    foreach (var arg in arguments)
                    {
                        if (arg.IsPositional) throw RuntimeErrors.Type("_replace() accepts keyword arguments only", span);
                        var index = Array.IndexOf(type.Fields, arg.KeywordName);
                        if (index < 0 || (seen & (1 << index)) != 0) throw RuntimeErrors.Type("unexpected or repeated result field", span);
                        seen |= 1 << index; values[index] = arg.Value;
                    }
                    return UrlResult.Create(type, values, context, span);
                }
                if (method is "encode" or "decode")
                {
                    var bound = CallBinder.BindNamedArgumentsWithPresence(arguments, span,
                        LythonCallableSignature.Create(type.Name + "." + method, ["encoding", "errors"], requiredCount: 0), PythonCallableKind.Method);
                    var encodingValue = bound.Assigned[0] ? bound.Values[0] : AsciiName;
                    var errorsValue = bound.Assigned[1] ? bound.Values[1] : StrictName;
                    context.MemoryGovernor.EnsureCanReserve(PyTuple.EstimateApproximateBytes(receiver!.Count) + 64, span);
                    var values = new object[receiver.Count];
                    for (var i = 0; i < values.Length; i++)
                    {
                        if (method == "encode" && receiver[i] is PyString || method == "decode" && receiver[i] is PyBytes)
                            values[i] = TranscodeUrlField(receiver[i], method, encodingValue, errorsValue, context, span);
                        else
                        {
                            var target = asynchronous ? await PyTextStream.ResolveMemberAsync(receiver[i], method, span, context).ConfigureAwait(false)
                                : PyTextStream.ResolveMember(receiver[i], method, span, context);
                            values[i] = await CallAsync(target, [CallArgumentValue.Positional(encodingValue), CallArgumentValue.Positional(errorsValue)], span, context, asynchronous).ConfigureAwait(false);
                        }
                    }
                    return UrlResult.Create(UrlResultType.Get(type.Parameters, !type.Binary), values, context, span);
                }
                if (arguments.Length != 0) throw RuntimeErrors.Type(method + "() accepts no arguments", span);
                if (method == "geturl") return await ReassembleValuesAsync(receiver!, span, context, asynchronous, type.Parameters).ConfigureAwait(false);
                var dict = new PyDict(context.MemoryGovernor, span);
                try
                {
                    for (var i = 0; i < receiver!.Count; i++) dict.SetItem(type.FieldNames[i], receiver[i]);
                }
                catch { context.MemoryGovernor.Release(dict.CommittedStorageBytes); throw; }
                context.State.CallTemporaries.TrackFreshMutable(dict, dict.CommittedStorageBytes);
                return dict;
            }
            public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            {
                value = name switch { "__self__" => receiver is null ? type : receiver, "__name__" => PyString.FromString(method), "__module__" => ModuleName, _ => MissingMemberValue.Instance };
                return !ReferenceEquals(value, MissingMemberValue.Instance);
            }
            public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<bound method " + type.ShortName + "." + method + ">");
            public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
        }

        private static object TranscodeUrlField(object field, string method, object encodingValue, object errorsValue, ExecutionContext context, LythonSourceSpan span)
        {
            RequireCodecNameType(encodingValue, method, "encoding", span);
            RequireCodecNameType(errorsValue, method, "errors", span);
            // Native bytes.decode returns empty text without a codec lookup;
            // both native operations resolve error handlers only on bad input.
            if (field is PyBytes { Length: 0 }) return PyString.Empty;
            var encoding = Codec(encodingValue, span);
            var unknownHandler = false;
            TextErrorMode errors;
            try { errors = Errors(errorsValue, TextErrorMode.Strict, span); }
            catch (LythonRuntimeException ex) when (ex.ExceptionType == "LookupError")
            { errors = TextErrorMode.Strict; unknownHandler = true; }
            try
            {
                return field is PyString text
                    ? OwnBytes(EncodeText(text, encoding, errors, TextNewlineMode.PreserveUniversal, context, span), context, span)
                    : DecodeOwned(((PyBytes)field).Memory, encoding, errors, context, span);
            }
            catch (LythonRuntimeException ex) when (unknownHandler && ex.ExceptionType is "UnicodeEncodeError" or "UnicodeDecodeError")
            { throw new LythonRuntimeException("LookupError", "unknown error handler", span); }
        }
    }
}
