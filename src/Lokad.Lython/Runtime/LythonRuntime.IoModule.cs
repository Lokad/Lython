using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class IoModule : PyModule
    {
        internal static readonly IoModule Instance = new();
        internal static readonly ExceptionTypeValue UnsupportedOperationType = new(ModuleException("io", "UnsupportedOperation"));
        private IoModule() : base("io") { }
        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "BytesIO" => BytesIOClass.Instance,
                "UnsupportedOperation" => UnsupportedOperationType,
                "StringIO" => StringIOClass.Instance,
                "SEEK_SET" => BigInteger.Zero,
                "SEEK_CUR" => BigInteger.One,
                "SEEK_END" => new BigInteger(2),
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    internal sealed class StringIOClass : ICallable, INamedRuntimeCallable, IPyDynamicAttributes, IPyRenderableValue
    {
        internal static readonly StringIOClass Instance = new();
        private StringIOClass() { }
        public string Name => "io.StringIO";
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var bound = CallBinder.BindNamedArgumentsWithPresence(arguments, span, LythonKnownCallableSignatures.IoStringIO, PythonCallableKind.Builtin);
            var initial = bound.Values[0];
            var newline = bound.Assigned[1] ? bound.Values[1] : PyString.FromString("\n");
            if (initial is not PyNone and not PyString)
                throw new LythonRuntimeException("TypeError", "initial_value must be str or None", span);
            string? policy;
            if (newline is PyNone) policy = null;
            else if (newline is PyString text) policy = text.ToString();
            else throw new LythonRuntimeException("TypeError", "newline must be str or None", span);
            if (policy is not (null or "" or "\n" or "\r" or "\r\n"))
                throw new LythonRuntimeException("ValueError", "illegal newline value", span);
            var result = new StringIOObject(context.Services, span, policy);
            try
            {
                if (initial is PyString initialText) result.Write(initialText, span);
                result.RewindInitialValue();
            }
            catch { result.ReleaseFailedConstruction(); throw; }
            context.Services.State.CallTemporaries.TrackFreshMutable(result, result.OwnedBytes, span);
            return result;
        }
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "__name__" or "__qualname__" => PyString.FromString("StringIO"),
                "__module__" => PyString.FromString("_io"),
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<class '_io.StringIO'>");
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    internal sealed partial class StringIOObject
    {
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            switch (name)
            {
                case "closed": value = _closed; return true;
                case "encoding": case "errors": value = PyNone.Instance; return true;
                case "line_buffering": EnsureOpen(_creationSpan); value = false; return true;
                case "newlines": value = GetNewlines(); return true;
            }
            var arity = name switch
            {
                "read" or "readline" or "readlines" or "truncate" => (0, 1),
                "write" or "writelines" => (1, 1),
                "seek" => (1, 2),
                "__exit__" => (3, 3),
                "getvalue" or "tell" or "close" or "flush" or "readable" or "writable" or "seekable" or "__iter__" or "__next__" or "__enter__" => (0, 0),
                _ => (-1, -1),
            };
            if (arity.Item1 < 0) { value = PyNone.Instance; return false; }
            var parameters = name switch
            {
                "seek" => new[] { "pos", "whence" },
                "__exit__" => new[] { "type", "value", "traceback" },
                _ => arity.Item2 == 1 ? new[] { "value" } : Array.Empty<string>(),
            };
            var signature = LythonCallableSignature.Create("StringIO." + name, parameters, arity.Item1, arity.Item2, LythonVariadicParameters.None, arity.Item2);
            value = BoundCallable.Create(
                (args, span, context) => InvokeMemberAsync(name, args, span, context, false).GetAwaiter().GetResult(),
                signature,
                (args, span, context) => InvokeMemberAsync(name, args, span, context, true));
            return true;
        }

        private object GetNewlines()
        {
            EnsureOpen(_creationSpan);
            if (_newlines == 0) return PyNone.Instance;
            if (_newlines is 1 or 2 or 4) return PyString.FromString(_newlines switch { 1 => "\r", 2 => "\n", _ => "\r\n" });
            var items = new object[System.Numerics.BitOperations.PopCount((uint)_newlines)];
            var count = 0;
            if ((_newlines & 1) != 0) items[count++] = PyString.FromString("\r");
            if ((_newlines & 2) != 0) items[count++] = PyString.FromString("\n");
            if ((_newlines & 4) != 0) items[count] = PyString.FromString("\r\n");
            var result = new PyTuple(items, _services.MemoryGovernor, _creationSpan);
            _services.State.CallTemporaries.TrackCallResult(result, _creationSpan);
            return result;
        }

        private async ValueTask<object> InvokeMemberAsync(string name, object[] args, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            context.CheckExecutionBudget(span);
            switch (name)
            {
                case "close": case "__exit__": return Close();
                case "write": return Write(args[0], span);
                case "read": case "readline":
                {
                    var size = args.Length == 0 || args[0] is PyNone ? -1 : await CoerceIoIntegerAsync(args[0], span, context, asynchronous).ConfigureAwait(false);
                    return Read(size, name == "readline", span);
                }
                case "truncate":
                {
                    var size = args.Length == 0 || args[0] is PyNone ? _position : await CoerceIoIntegerAsync(args[0], span, context, asynchronous).ConfigureAwait(false);
                    return Truncate(size, span);
                }
                case "seek":
                {
                    var offset = await CoerceIoIntegerAsync(args[0], span, context, asynchronous).ConfigureAwait(false);
                    var whence = args.Length < 2 ? 0 : await CoerceIoIntegerAsync(args[1], span, context, asynchronous).ConfigureAwait(false);
                    if (whence is < int.MinValue or > int.MaxValue)
                        throw new LythonRuntimeException("OverflowError", "Python int too large to convert to C int", span);
                    return Seek(offset, whence, span);
                }
                case "readlines":
                {
                    var hint = args.Length == 0 || args[0] is PyNone ? -1 : await CoerceIoIntegerAsync(args[0], span, context, asynchronous).ConfigureAwait(false);
                    EnsureOpen(span);
                    var result = new PyList([], context.MemoryGovernor, span);
                    context.Services.State.CallTemporaries.TrackFreshMutable(result, result.CommittedStorageBytes, span);
                    long consumed = 0;
                    while (_position < _length)
                    {
                        var before = _position;
                        var line = Read(-1, true, span);
                        result.Add(line);
                        consumed += _position - before;
                        if (hint > 0 && consumed > hint) break;
                    }
                    return result;
                }
                case "writelines":
                    EnsureOpen(span);
                    if (asynchronous)
                    {
                        await foreach (var item in PyIteration.ToSequenceAsync(args[0], span, context).ConfigureAwait(false)) Write(item, span);
                    }
                    else foreach (var item in PyIteration.ToSequence(args[0], span, context)) Write(item, span);
                    return PyNone.Instance;
                case "getvalue": return GetValue(span);
                case "tell": EnsureOpen(span); return new BigInteger(_position);
                case "flush": return PyNone.Instance;
                case "readable": case "writable": case "seekable": EnsureOpen(span); return true;
                case "__enter__": case "__iter__": EnsureOpen(span); return this;
                case "__next__":
                    if (TryMoveNext(out var next)) return next;
                    throw new LythonRuntimeException("StopIteration", string.Empty, span);
                default: throw new InvalidOperationException("Unknown StringIO member");
            }
        }
    }

    private static async ValueTask<long> CoerceIoIntegerAsync(object value, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        var original = value;
        if (value is PyInstance instance && instance.Type.TryLookupInMro("__index__", 0, out var raw, out _))
        {
            var member = asynchronous
                ? await PyAttributeLookup.BindForInstanceAsync(instance, raw, context, span).ConfigureAwait(false)
                : PyAttributeLookup.BindForInstance(instance, raw, context, span);
            if (member is not ICallable callable) throw new LythonRuntimeException("TypeError", "__index__ is not callable", span);
            value = asynchronous ? await callable.InvokeAsync([], span, context).ConfigureAwait(false) : callable.Invoke([], span, context);
            if (!PyNumberOps.TryAsInteger(value, out _)) throw new LythonRuntimeException("TypeError", "__index__ returned non-int", span);
        }
        if (!PyNumberOps.TryAsInteger(value, out var integer))
            throw new LythonRuntimeException("TypeError", "'" + UnboundTypeMethod.PythonTypeName(original, context) + "' object cannot be interpreted as an integer", span);
        if (integer < long.MinValue || integer > long.MaxValue)
            throw new LythonRuntimeException("OverflowError", "Python int too large to convert to C ssize_t", span);
        return (long)integer;
    }
}
