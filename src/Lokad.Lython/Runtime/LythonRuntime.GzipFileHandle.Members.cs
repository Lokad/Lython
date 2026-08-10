using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed partial class GzipFileHandle
    {
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "closed" => IsClosed,
                "name" => PyString.FromString(_options.Path),
                "mode" => PyString.FromString(_options.Mode),
                "encoding" when _options.ContentKind == GzipContentKind.Text => PyString.FromString(EncodingName),
                "errors" when _options.ContentKind == GzipContentKind.Text => PyString.FromString(ErrorsName),
                "__enter__" => BoundCallable.CreateNoArguments(this, "gzip file.__enter__", static (receiver, span, _) =>
                {
                    receiver.EnsureOpen(span);
                    return receiver;
                }),
                "__exit__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 3)
                    {
                        throw new LythonRuntimeException("TypeError", "gzip file __exit__(exc_type, exc, tb) expects three arguments", span);
                    }

                    if (arguments[0] is PyNone)
                    {
                        Close(span);
                    }
                    else
                    {
                        AbortClose();
                    }
                    return false;
                },
                async (arguments, span, _) =>
                {
                    if (arguments.Length != 3)
                    {
                        throw new LythonRuntimeException("TypeError", "gzip file __exit__(exc_type, exc, tb) expects three arguments", span);
                    }

                    if (arguments[0] is PyNone)
                    {
                        await CloseAsync(span).ConfigureAwait(false);
                    }
                    else
                    {
                        AbortClose();
                    }
                    return false;
                }),
                "close" => BoundCallable.CreateNoArguments(this, "gzip file.close", static (receiver, span, _) =>
                {
                    receiver.Close(span);
                    return PyNone.Instance;
                },
                static async (receiver, span, _) =>
                {
                    await receiver.CloseAsync(span).ConfigureAwait(false);
                    return PyNone.Instance;
                }),
                "flush" => BoundCallable.CreateNoArguments(this, "gzip file.flush", static (receiver, span, _) =>
                {
                    receiver.Flush(span);
                    return PyNone.Instance;
                },
                static async (receiver, span, _) =>
                {
                    await receiver.FlushAsync(span).ConfigureAwait(false);
                    return PyNone.Instance;
                }),
                "readable" => BoundCallable.CreateNoArguments(this, "gzip file.readable", static (receiver, span, _) =>
                {
                    receiver.EnsureOpen(span);
                    return receiver._options.Operation == GzipOperation.Read;
                }),
                "writable" => BoundCallable.CreateNoArguments(this, "gzip file.writable", static (receiver, span, _) =>
                {
                    receiver.EnsureOpen(span);
                    return receiver._options.Operation is GzipOperation.Write or GzipOperation.Append;
                }),
                "seekable" => BoundCallable.CreateNoArguments(this, "gzip file.seekable", static (receiver, span, _) =>
                {
                    receiver.EnsureOpen(span);
                    return false;
                }),
                "tell" => BoundCallable.CreateNoArguments(this, "gzip file.tell", static (receiver, span, _) =>
                {
                    receiver.EnsureOpen(span);
                    return new BigInteger(receiver._options.Operation == GzipOperation.Read ? receiver._readCursor : receiver._writeBuffer.Length);
                }),
                "seek" => BoundCallable.Create((_, span, _) => throw new LythonRuntimeException("NotImplementedError", "gzip file seek/random access is unsupported by Lython.", span), "gzip file.seek", ["offset", "whence"], 1),
                "read" => BoundCallable.Create((arguments, span, _) => Read(ParseOptionalSize(arguments, "gzip file.read([size])", span), span), "gzip file.read", ["size"], 0),
                "readline" => BoundCallable.Create((arguments, span, _) => ReadLine(ParseOptionalSize(arguments, "gzip file.readline([size])", span), span), "gzip file.readline", ["size"], 0),
                "readlines" => BoundCallable.Create((arguments, span, _) => ReadLines(ParseOptionalSize(arguments, "gzip file.readlines([hint])", span), span), "gzip file.readlines", ["hint"], 0),
                "write" => BoundCallable.Create((arguments, span, _) => Write(arguments, span), "gzip file.write", ["data"]),
                "writelines" => BoundCallable.Create((arguments, span, _) => WriteLines(arguments, span), "gzip file.writelines", ["lines"]),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public object Enter() => this;

        object IPyContextManager.Enter() => Enter();

        ValueTask<object> IPyAsyncContextManager.EnterAsync() => ValueTask.FromResult<object>(Enter());

        bool IPyContextManager.Exit(object exceptionType, object exceptionValue, object traceback)
        {
            if (exceptionType is PyNone)
            {
                Close(null);
            }
            else
            {
                AbortClose();
            }
            return false;
        }

        async ValueTask<bool> IPyAsyncContextManager.ExitAsync(object exceptionType, object exceptionValue, object traceback)
        {
            if (exceptionType is PyNone)
            {
                await CloseAsync(null).ConfigureAwait(false);
            }
            else
            {
                AbortClose();
            }
            return false;
        }

        public IEnumerable<object> Iterate()
        {
            while (TryMoveNext(out var value))
            {
                yield return value;
            }
        }

        public bool TryMoveNext([MaybeNullWhen(false)] out object value)
        {
            var line = ReadLine(-1, null);
            if (IsEmptyReadValue(line))
            {
                value = PyNone.Instance;
                return false;
            }

            value = line;
            return true;
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString($"<gzip file '{_options.Path}' mode '{_options.Mode}'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }
}
