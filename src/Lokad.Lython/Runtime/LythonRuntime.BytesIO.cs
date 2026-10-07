using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed class BytesIOClass : ICallable, INamedRuntimeCallable, IPyDynamicAttributes, IPyRenderableValue
    {
        internal static readonly BytesIOClass Instance = new();
        private BytesIOClass() { }
        public string Name => "io.BytesIO";
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var bound = CallBinder.BindNamedArguments(arguments, span, LythonKnownCallableSignatures.IoBytesIO, PythonCallableKind.Builtin);
            var initial = bound.Length == 0 ? PyNone.Instance : bound[0];
            if (initial is not PyNone and not PyBytes)
                throw new LythonRuntimeException("TypeError", "a bytes-like object is required", span);
            var result = new BytesIOObject(context.Services, span);
            try
            {
                if (initial is PyBytes bytes) result.Write(bytes, span);
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
                "__name__" or "__qualname__" => PyString.FromString("BytesIO"),
                "__module__" => PyString.FromString("_io"),
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<class '_io.BytesIO'>");
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    internal sealed partial class BytesIOObject
    {
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name == "closed") { value = _closed; return true; }
            var arity = name switch
            {
                "read" or "read1" or "readline" or "readlines" or "truncate" => (0, 1),
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
            var signature = LythonCallableSignature.Create("BytesIO." + name, parameters, arity.Item1, arity.Item2, LythonVariadicParameters.None, arity.Item2);
            value = BoundCallable.Create(
                (args, span, context) => InvokeMemberAsync(name, args, span, context, false).GetAwaiter().GetResult(),
                signature,
                (args, span, context) => InvokeMemberAsync(name, args, span, context, true));
            return true;
        }

        private async ValueTask<object> InvokeMemberAsync(string name, object[] args, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            context.CheckExecutionBudget(span);
            switch (name)
            {
                case "close": case "__exit__": return Close();
                case "__iter__": return this; // BytesIO permits iter() after close.
                case "write": return Write(args[0], span);
                case "read": case "read1": case "readline":
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
                    EnsureOpen(span);
                    // BytesIO uses the IOBase integer parser here, which does
                    // not invoke __index__; its other size APIs do invoke it.
                    if (args.Length != 0 && args[0] is not PyNone && !PyNumberOps.TryAsInteger(args[0], out _))
                        throw new LythonRuntimeException("TypeError", "integer argument expected", span);
                    var hint = args.Length == 0 || args[0] is PyNone ? -1 : await CoerceIoIntegerAsync(args[0], span, context, asynchronous).ConfigureAwait(false);
                    var result = new PyList([], context.MemoryGovernor, span);
                    context.Services.State.CallTemporaries.TrackFreshMutable(result, result.CommittedStorageBytes, span);
                    long consumed = 0;
                    while (_position < _length)
                    {
                        var before = _position;
                        result.Add(Read(-1, true, span));
                        consumed += _position - before;
                        if (hint > 0 && consumed >= hint) break;
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
                case "flush": EnsureOpen(span); return PyNone.Instance;
                case "readable": case "writable": case "seekable": EnsureOpen(span); return true;
                case "__enter__": EnsureOpen(span); return this;
                case "__next__":
                    if (TryMoveNext(out var next)) return next;
                    throw new LythonRuntimeException("StopIteration", string.Empty, span);
                default: throw new InvalidOperationException("Unknown BytesIO member");
            }
        }
    }
}
