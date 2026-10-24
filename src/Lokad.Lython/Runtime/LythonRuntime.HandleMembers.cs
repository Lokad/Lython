using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static class TextFileHandleMembers
    {
        internal static object Write(ExecutionContext.TextFileHandle handle, object value, LythonSourceSpan span)
        {
            if (!PyStringOps.TryAsString(value, out var text))
                throw new LythonRuntimeException("TypeError", "file.write(text) expects one string argument.", span);

            return handle.Write(text);
        }

        public static bool TryGetMember(ExecutionContext.TextFileHandle handle, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "closed" => handle.IsClosed,
                "name" => PyString.FromString(handle.Path),
                "mode" => PyString.FromString(handle.Mode),
                "encoding" => PyString.FromString(handle.EncodingName),
                "errors" => PyString.FromString(handle.ErrorsName),
                "__enter__" => BoundCallable.CreateNoArguments(handle, "file.__enter__", static (receiver, _, _) => receiver.Enter()),
                "__exit__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 3)
                    {
                        throw new LythonRuntimeException("TypeError", "__exit__(exc_type, exc, tb) expects three arguments.", span);
                    }

                    return handle.Exit();
                },
                async (arguments, span, _) =>
                {
                    if (arguments.Length != 3)
                    {
                        throw new LythonRuntimeException("TypeError", "__exit__(exc_type, exc, tb) expects three arguments.", span);
                    }

                    return await handle.ExitAsync().ConfigureAwait(false);
                }),
                "close" => BoundCallable.CreateNoArguments(handle, "file.close", static (receiver, _, _) =>
                {
                    receiver.Exit();
                    return PyNone.Instance;
                },
                static async (receiver, _, _) =>
                {
                    await receiver.ExitAsync().ConfigureAwait(false);
                    return PyNone.Instance;
                }),
                "readable" => BoundCallable.CreateNoArguments(handle, "file.readable", static (receiver, _, _) => receiver.IsReadable()),
                "writable" => BoundCallable.CreateNoArguments(handle, "file.writable", static (receiver, _, _) => receiver.IsWritable()),
                "seekable" => BoundCallable.CreateNoArguments(handle, "file.seekable", static (receiver, _, _) => receiver.IsSeekable()),
                "tell" => BoundCallable.CreateNoArguments(handle, "file.tell", static (receiver, _, _) => receiver.Tell()),
                "seek" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "file.seek(offset[, whence]) expects one or two arguments.", span);
                    }

                    return handle.Seek(span);
                }, "file.seek", ["offset", "whence"], 1),
                "read" or "readline" or "readlines" => BoundCallable.Create(
                    (arguments, span, context) => ReadAsync(handle, name, arguments, span, context, false).GetAwaiter().GetResult(),
                    LythonCallableSignature.Create("file." + name, [name == "readlines" ? "hint" : "size"], 0),
                    (arguments, span, context) => ReadAsync(handle, name, arguments, span, context, true)),
                "write" => BoundCallable.Create((arguments, span, _) => Write(handle, arguments[0], span), "file.write", ["text"]),
                "writelines" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "file.writelines(lines) expects one argument.", span);
                    }

                    return handle.WriteLines(arguments[0], span, context);
                }, "file.writelines", ["lines"]),
                "flush" => BoundCallable.CreateNoArguments(
                    handle,
                    "file.flush",
                    static (receiver, _, _) => receiver.Flush(),
                    static async (receiver, _, _) => await receiver.FlushAsync().ConfigureAwait(false)),
                "__iter__" => BoundCallable.CreateNoArguments(handle, "file.__iter__", static (receiver, _, _) =>
                {
                    receiver.EnsureOpen();
                    return receiver;
                }),
                "__next__" => BoundCallable.CreateNoArguments(handle, "file.__next__", static (receiver, span, _) =>
                {
                    if (receiver.TryMoveNext(out var item))
                    {
                        return LythonRuntime.RuntimeValue(item);
                    }

                    throw new LythonRuntimeException("StopIteration", "", span);
                },
                static async (receiver, span, _) =>
                {
                    var advanced = await receiver.TryMoveNextAsync().ConfigureAwait(false);
                    if (advanced.HasValue)
                    {
                        return LythonRuntime.RuntimeValue(advanced.Value);
                    }

                    throw new LythonRuntimeException("StopIteration", "", span);
                }),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static async ValueTask<object> ReadAsync(ExecutionContext.TextFileHandle handle, string name,
            object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var size = arguments.Length == 0 || arguments[0] is PyNone && name != "readline" ? -1
                : await CoerceIoIntegerAsync(arguments[0], span, context, asynchronous).ConfigureAwait(false);
            if (name == "readlines")
                return asynchronous ? await handle.ReadLinesAsync(size).ConfigureAwait(false) : handle.ReadLines(size);
            // Requests can exceed one string's representation; grow only for
            // actual input, with the same admitted builder as an unbounded read.
            var count = size < 0 ? -1 : (int)Math.Min(size, int.MaxValue);
            if (name == "read")
                return asynchronous ? await handle.ReadAsync(count).ConfigureAwait(false) : handle.Read(count);
            return asynchronous ? await handle.ReadLineAsync(count).ConfigureAwait(false) : handle.ReadLine(count);
        }
    }

    internal static class HostTextInputMembers
    {
        public static bool TryGetMember(HostTextInputHandle handle, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "read" => BoundCallable.CreateNoArguments(
                    handle,
                    "stream.read",
                    static (receiver, span, _) => receiver.ReadAll(span),
                    static async (receiver, span, _) => await receiver.ReadAllAsync(span).ConfigureAwait(false)),
                "readline" => BoundCallable.CreateNoArguments(
                    handle,
                    "stream.readline",
                    static (receiver, span, _) => receiver.ReadLine(span),
                    static async (receiver, span, _) => await receiver.ReadLineAsync(span).ConfigureAwait(false)),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    internal static class HostTextOutputMembers
    {
        public static bool TryGetMember(HostTextOutputHandle handle, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "write" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var text))
                    {
                        throw new LythonRuntimeException("TypeError", "stream.write(text) expects one string argument.", span);
                    }

                    return handle.Write(text, span);
                },
                async (arguments, span, _) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var text))
                    {
                        throw new LythonRuntimeException("TypeError", "stream.write(text) expects one string argument.", span);
                    }

                    return await handle.WriteAsync(text, span).ConfigureAwait(false);
                }, "stream.write", ["text"]),
                "flush" => BoundCallable.CreateNoArguments(
                    handle,
                    "stream.flush",
                    static (receiver, span, _) => receiver.Flush(span),
                    static async (receiver, span, _) => await receiver.FlushAsync(span).ConfigureAwait(false)),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    internal static class CompletedProcessMembers
    {
        public static bool TryGetMember(PyCompletedProcess process, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "args" => process.Args,
                "returncode" => process.ReturnCode,
                "stdout" => process.Stdout,
                "stderr" => process.Stderr,
                "check_returncode" => BoundCallable.CreateNoArguments(process, "CompletedProcess.check_returncode", static (receiver, span, context) =>
                {
                    if (receiver.ReturnCode != BigInteger.Zero)
                    {
                        throw CreateCalledProcessError(
                            receiver.ReturnCode,
                            receiver.Args,
                            receiver.Stdout,
                            receiver.Stderr,
                            $"subprocess.CompletedProcess failed with return code {receiver.ReturnCode}.",
                            context,
                            span);
                    }

                    return PyNone.Instance;
                }),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }
}
