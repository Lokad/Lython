using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // GzipFile is a Python ABC in CPython. This metadata value preserves its
    // observed metaclass identity; constructing metaclasses remains unsupported.
    private sealed class GzipFileMetaType : ICallable, INamedRuntimeCallable, IPyDynamicAttributes,
        IPyRenderableValue, IPyContextualDynamicAttributes
    {
        internal static readonly GzipFileMetaType Instance = new();
        public string Name => "abc.ABCMeta";
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => throw new LythonRuntimeException("NotImplementedError", "metaclass construction is unsupported", span);
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "__name__" or "__qualname__" => PyString.FromString("ABCMeta"),
                "__module__" => ExceptionTypeValue.SharedModuleLabel("abc"),
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span,
            [MaybeNullWhen(false)] out object value)
        {
            if (name is "__bases__" or "__mro__")
            {
                var tuple = name == "__bases__" ? context.State.GzipFileMetaBases : context.State.GzipFileMetaMro;
                if (tuple is null)
                {
                    tuple = new PyTuple(name == "__bases__"
                        ? [TryGetBuiltinOrNull(context, "type")!]
                        : [this, TryGetBuiltinOrNull(context, "type")!, TryGetBuiltinOrNull(context, "object")!], context.MemoryGovernor, span);
                    context.State.CallTemporaries.TrackFreshMutable(tuple, tuple.CommittedStorageBytes, span);
                    if (name == "__bases__") context.State.GzipFileMetaBases = tuple;
                    else context.State.GzipFileMetaMro = tuple;
                }
                value = tuple;
                return true;
            }
            return TryGetMember(name, out value);
        }
        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<class 'abc.ABCMeta'>");
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class GzipFileClass : BoundArgumentsCallable, INamedRuntimeCallable,
        IPyDynamicAttributes, IPyRenderableValue
    {
        internal static readonly GzipFileClass Instance = new();
        private GzipFileClass() : base(LythonKnownCallableSignatures.GzipFile, PythonCallableKind.Builtin) { }
        public string Name => "gzip.GzipFile";
        protected override bool PreservePresence => true;
        protected override object InvokeBound(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => throw new InvalidOperationException("GzipFile requires argument presence");
        protected override object InvokeBoundWithPresence(BoundCallArguments arguments, LythonSourceSpan span, ExecutionContext context)
            => CreateAsync(arguments, span, context, false).GetAwaiter().GetResult();
        protected override ValueTask<object> InvokeBoundWithPresenceAsync(BoundCallArguments arguments,
            LythonSourceSpan span, ExecutionContext context) => CreateAsync(arguments, span, context, true);

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "__name__" or "__qualname__" => PyString.FromString("GzipFile"),
                "__module__" => ExceptionTypeValue.SharedModuleLabel("gzip"),
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<class 'gzip.GzipFile'>");
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        private static async ValueTask<object> CreateAsync(BoundCallArguments bound, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous)
        {
            var values = bound.Values;
            var filename = values[0];
            var modeValue = values[1];
            var stream = values[3];
            if (modeValue is not PyNone && !PyStringOps.TryAsString(modeValue, out _))
                throw RuntimeErrors.Type("GzipFile mode must be str or None", span);
            var mode = modeValue is PyNone ? null : ((PyString)modeValue).AsString();
            if (mode is not null && (mode.Contains('t') || mode.Contains('U')))
                throw RuntimeErrors.Value("Invalid mode: " + mode, span);
            var ownsStream = stream is PyNone;
            // builtins.open uses the first fspath result, then GzipFile separately
            // obtains the name from the original filename object.
            if (ownsStream)
            {
                var path = await GzipFilePathAsync(filename, context, span, asynchronous).ConfigureAwait(false);
                var operation = GzipFileOperation(mode ?? "rb", span, ownsStream: true);
                stream = await ExecutionContext.BinaryFileHandle.OpenAsync(
                    PathOps.Normalize(path.AsString(), context.Host.Cwd), operation, context, span, asynchronous).ConfigureAwait(false);
            }
            GzipFileObject? result = null;
            try
            {
                PyString name;
                if (filename is PyNone)
                {
                    var named = await GzipFileMemberAsync(stream, "name", context, span, asynchronous).ConfigureAwait(false);
                    if (named is PyBytes)
                        throw new LythonRuntimeException("NotImplementedError", "GzipFile byte filenames are unsupported", span);
                    name = PyStringOps.TryAsString(named, out var label) ? label : PyString.Empty;
                }
                else name = await GzipFilePathAsync(filename, context, span, asynchronous).ConfigureAwait(false);
                if (mode is null)
                {
                    var streamMode = await GzipFileMemberAsync(stream, "mode", context, span, asynchronous).ConfigureAwait(false);
                    mode = streamMode is PyNone ? "rb" : PyStringOps.TryAsString(streamMode, out var text)
                        ? text.AsString() : throw RuntimeErrors.Type("GzipFile stream mode must be str", span);
                }
                var operation = GzipFileOperation(mode, span, ownsStream);
                if (modeValue is PyNone && operation != TextFileOperation.Read)
                    await EmitDefaultWarningAsync("FutureWarning",
                        "GzipFile was opened for writing, but this will change in future Python releases.  "
                        + "Specify the mode argument for opening it for writing.",
                        context, span, asynchronous).ConfigureAwait(false);
                result = GzipFileObject.Create(stream, ownsStream, name, operation == TextFileOperation.Read, context, span);
                if (operation != TextFileOperation.Read)
                {
                    var originalLevel = bound.Assigned[2] ? values[2] : new BigInteger(9);
                    var level = await CoerceIoIntegerAsync(originalLevel, span, context, asynchronous).ConfigureAwait(false);
                    if (level < int.MinValue || level > int.MaxValue)
                        throw new LythonRuntimeException("OverflowError", "Python int too large to convert to C int", span);
                    if (level is < -1 or > 9) throw RuntimeErrors.Value("Invalid initialization option", span);
                    result.InitializeCompressor((int)level, span);
                    await result.WriteHeaderAsync(originalLevel, values[4], span, asynchronous).ConfigureAwait(false);
                    context.State.TrackOpenFileWriter(result);
                }
                return result;
            }
            catch
            {
                if (result is not null) result.AbortConstruction();
                if (ownsStream)
                {
                    try { await PyBinaryStream.CallAsync(stream, "close", [], span, context, asynchronous).ConfigureAwait(false); }
                    catch (Exception) { /* Preserve the constructor's original error. */ }
                }
                throw;
            }
        }
    }

    private static TextFileOperation GzipFileOperation(string mode, LythonSourceSpan span, bool ownsStream)
    {
        if (mode is "r" or "rb") return TextFileOperation.Read;
        if (mode is "w" or "wb") return TextFileOperation.Write;
        if (mode is "a" or "ab") return TextFileOperation.Append;
        if (mode is "x" or "xb" && !ownsStream) return TextFileOperation.Write;
        if (mode.Contains('+') || mode is "x" or "xb" || mode.StartsWith('r') || mode.StartsWith('w') || mode.StartsWith('a'))
            throw new LythonRuntimeException("NotImplementedError", "GzipFile supports sequential r/rb, w/wb and a/ab modes; exclusive host creation is unsupported", span);
        throw RuntimeErrors.Value("Invalid mode: " + mode, span);
    }

    private static async ValueTask<object> GzipFileMemberAsync(object target, string member, ExecutionContext context,
        LythonSourceSpan span, bool asynchronous)
    {
        var result = asynchronous ? await TryResolveRuntimeMemberAsync(target, member, context, span).ConfigureAwait(false)
            : TryResolveRuntimeMember(target, member, context, span, out var value) ? (true, value) : (false, PyNone.Instance);
        return result.Item1 ? result.Item2 : PyNone.Instance;
    }

    private static async ValueTask<PyString> GzipFilePathAsync(object value, ExecutionContext context,
        LythonSourceSpan span, bool asynchronous)
    {
        if (value is PyPath path) return path.Value;
        if (PyStringOps.TryAsString(value, out var text)) return text;
        if (value is PyBytes)
            throw new LythonRuntimeException("NotImplementedError", "GzipFile byte filenames are unsupported", span);
        if (value is PyInstance instance && instance.Type.TryLookupInMro("__fspath__", 0, out var raw, out _))
        {
            var method = asynchronous ? await PyAttributeLookup.BindForInstanceAsync(instance, raw, context, span).ConfigureAwait(false)
                : PyAttributeLookup.BindForInstance(instance, raw, context, span);
            var result = asynchronous
                ? await InvokeCallableTargetAsync(method, span, span, context, () => ValueTask.FromResult<CallArgumentValue[]>([])).ConfigureAwait(false)
                : InvokeCallableTarget(method, span, span, context, static () => []);
            if (PyStringOps.TryAsString(result, out text)) return text;
            if (result is PyBytes)
                throw new LythonRuntimeException("NotImplementedError", "GzipFile byte filenames are unsupported", span);
            throw RuntimeErrors.Type("__fspath__ must return str", span);
        }
        throw RuntimeErrors.Type("GzipFile filename must be a path-like object", span);
    }
}
