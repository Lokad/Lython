using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        internal sealed partial class BinaryFileHandle
        {
            public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            {
                if (name == "closed") { value = IsClosed; return true; }
                if (name == "name" || name == "mode")
                {
                    value = PyString.FromString(name == "name" ? Path : Mode); return true;
                }
                var arity = name switch
                {
                    "read" or "readline" or "readlines" => (0, 1),
                    "write" or "writelines" => (1, 1),
                    "seek" => (1, 2),
                    "__exit__" => (3, 3),
                    "tell" or "close" or "flush" or "readable" or "writable" or "seekable" or
                        "__iter__" or "__next__" or "__enter__" => (0, 0),
                    _ => (-1, -1),
                };
                if (arity.Item1 < 0) { value = MissingMemberValue.Instance; return false; }
                var parameters = name switch
                {
                    "seek" => new[] { "offset", "whence" },
                    "__exit__" => new[] { "type", "value", "traceback" },
                    _ => arity.Item2 == 1 ? new[] { "value" } : Array.Empty<string>(),
                };
                var signature = LythonCallableSignature.Create("file." + name, parameters, arity.Item1, parameters.Length, LythonVariadicParameters.None,
                    positionalOnlyCount: parameters.Length);
                value = BoundCallable.Create(
                    (args, span, context) => InvokeMemberAsync(name, args, span, context, false).GetAwaiter().GetResult(),
                    signature,
                    (args, span, context) => InvokeMemberAsync(name, args, span, context, true));
                return true;
            }

            private async ValueTask<object> InvokeMemberAsync(string name, object[] args, LythonSourceSpan span,
                ExecutionContext context, bool asynchronous)
            {
                context.CheckExecution(span);
                switch (name)
                {
                    case "close": case "__exit__":
                        await CloseAsync(span, asynchronous).ConfigureAwait(false);
                        return name == "close" ? PyNone.Instance : false;
                    case "write": return Write(args[0], span);
                    case "read": case "readline":
                    {
                        var size = args.Length == 0 || args[0] is PyNone ? -1
                            : await CoerceIoIntegerAsync(args[0], span, context, asynchronous).ConfigureAwait(false);
                        return await ReadAsync(size, name == "readline", span, asynchronous).ConfigureAwait(false);
                    }
                    case "readlines":
                    {
                        EnsureReadable(span);
                        var hint = args.Length == 0 || args[0] is PyNone ? -1
                            : await CoerceIoIntegerAsync(args[0], span, context, asynchronous).ConfigureAwait(false);
                        var result = new PyList([], context.MemoryGovernor, span);
                        context.State.CallTemporaries.TrackFreshMutable(result, result.CommittedStorageBytes, span);
                        long consumed = 0;
                        while (true)
                        {
                            var line = await ReadAsync(-1, true, span, asynchronous).ConfigureAwait(false);
                            if (line.Length == 0) break;
                            result.Add(line);
                            context.ObserveCollectionCount(result.Count, span);
                            consumed += line.Length;
                            if (hint > 0 && consumed > hint) break;
                        }
                        return result;
                    }
                    case "writelines":
                        EnsureWritable(span);
                        if (asynchronous)
                        {
                            await foreach (var item in PyIteration.ToSequenceAsync(args[0], span, context).ConfigureAwait(false))
                                Write(item, span);
                        }
                        else
                            foreach (var item in PyIteration.ToSequence(args[0], span, context)) Write(item, span);
                        return PyNone.Instance;
                    case "flush":
                        await FlushAsync(span, asynchronous).ConfigureAwait(false); return PyNone.Instance;
                    case "tell": EnsureOpen(span); return _position;
                    case "readable": EnsureOpen(span); return _operation == TextFileOperation.Read;
                    case "writable": EnsureOpen(span); return _operation != TextFileOperation.Read;
                    case "seekable": EnsureOpen(span); return false;
                    case "seek":
                        throw new LythonRuntimeException("NotImplementedError", "file.seek(...) is not supported by Lython sequential binary handles", span);
                    case "__iter__": case "__enter__": EnsureOpen(span); return this;
                    case "__next__":
                    {
                        var line = await ReadAsync(-1, true, span, asynchronous).ConfigureAwait(false);
                        if (line.Length != 0) return line;
                        throw new LythonRuntimeException("StopIteration", "", span);
                    }
                    default: throw new InvalidOperationException("Unknown binary file member");
                }
            }
        }
    }
}
