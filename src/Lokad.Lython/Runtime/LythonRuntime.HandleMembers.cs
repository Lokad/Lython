using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static class TextFileHandleMembers
    {
        public static bool TryGetMember(ExecutionContext.TextFileHandle handle, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "closed" => handle.IsClosed,
                "name" => PyString.FromString(handle.Path),
                "mode" => PyString.FromString(handle.Mode),
                "encoding" => PyString.FromString(handle.EncodingName),
                "errors" => PyString.FromString(handle.ErrorsName),
                "__enter__" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "__enter__() expects no arguments.", span);
                    }

                    return handle.Enter();
                }),
                "__exit__" => new BoundCallable((arguments, span, _) =>
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
                "close" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "file.close() expects no arguments.", span);
                    }

                    handle.Exit();
                    return PyNone.Instance;
                },
                async (arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "file.close() expects no arguments.", span);
                    }

                    await handle.ExitAsync().ConfigureAwait(false);
                    return PyNone.Instance;
                }),
                "readable" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "file.readable() expects no arguments.", span);
                    }

                    return handle.IsReadable();
                }, "file.readable", []),
                "writable" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "file.writable() expects no arguments.", span);
                    }

                    return handle.IsWritable();
                }, "file.writable", []),
                "seekable" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "file.seekable() expects no arguments.", span);
                    }

                    return handle.IsSeekable();
                }, "file.seekable", []),
                "tell" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "file.tell() expects no arguments.", span);
                    }

                    return handle.Tell();
                }, "file.tell", []),
                "seek" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "file.seek(offset[, whence]) expects one or two arguments.", span);
                    }

                    return handle.Seek(span);
                }, "file.seek", ["offset", "whence"], 1),
                "read" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "file.read([size]) expects zero or one integer argument.", span);
                    }

                    return handle.Read(ParseOptionalSize(arguments, "file.read([size])", span));
                }, "file.read", ["size"], 0),
                "readline" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "file.readline([size]) expects zero or one integer argument.", span);
                    }

                    return handle.ReadLine(ParseOptionalSize(arguments, "file.readline([size])", span));
                }, "file.readline", ["size"], 0),
                "readlines" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "file.readlines([hint]) expects zero or one integer argument.", span);
                    }

                    return handle.ReadLines(ParseOptionalSize(arguments, "file.readlines([hint])", span));
                }, "file.readlines", ["hint"], 0),
                "write" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var text))
                    {
                        throw new LythonRuntimeException("TypeError", "file.write(text) expects one string argument.", span);
                    }

                    return handle.Write(text);
                }, "file.write", ["text"]),
                "writelines" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "file.writelines(lines) expects one argument.", span);
                    }

                    return handle.WriteLines(arguments[0], span);
                }, "file.writelines", ["lines"]),
                "flush" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "file.flush() expects no arguments.", span);
                    }

                    return handle.Flush();
                },
                async (arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "file.flush() expects no arguments.", span);
                    }

                    return await handle.FlushAsync().ConfigureAwait(false);
                }, "file.flush", []),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    internal static class HostTextInputMembers
    {
        public static bool TryGetMember(HostTextInputHandle handle, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "read" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "stream.read() expects no arguments.", span);
                    }

                    return handle.ReadAll(span);
                },
                async (arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "stream.read() expects no arguments.", span);
                    }

                    return await handle.ReadAllAsync(span).ConfigureAwait(false);
                }),
                "readline" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "stream.readline() expects no arguments.", span);
                    }

                    return handle.ReadLine(span);
                },
                async (arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "stream.readline() expects no arguments.", span);
                    }

                    return await handle.ReadLineAsync(span).ConfigureAwait(false);
                }),
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
                "write" => new BoundCallable((arguments, span, _) =>
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
                "flush" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "stream.flush() expects no arguments.", span);
                    }

                    return handle.Flush(span);
                },
                async (arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "stream.flush() expects no arguments.", span);
                    }

                    return await handle.FlushAsync(span).ConfigureAwait(false);
                }),
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
                "check_returncode" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "CompletedProcess.check_returncode() expects no arguments.", span);
                    }

                    if (process.ReturnCode != BigInteger.Zero)
                    {
                        throw CreateCalledProcessError(
                            process.ReturnCode,
                            process.Args,
                            process.Stdout,
                            process.Stderr,
                            $"subprocess.CompletedProcess failed with return code {process.ReturnCode}.",
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
