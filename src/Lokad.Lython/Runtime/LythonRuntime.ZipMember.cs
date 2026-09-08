using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    /// <summary>
    /// Sequential read handle for one ZIP member. Content is inflated and
    /// validated once at <c>open()</c> time into owned governed bytes, so the
    /// handle survives the parent archive closing within its own budget.
    /// Binary line scanning reuses the shared text line operation in
    /// line-feed mode; seeking is explicitly unsupported.
    /// </summary>
    private sealed class PyZipMemberReader : IPyDynamicAttributes, IPyAsyncContextManager, IPyIteratorValue, IPyRenderableValue, IPyHashableValue
    {
        private readonly PyBytes _content;
        private readonly PyString _name;
        private readonly ExecutionContext _context;
        private int _cursor;

        public PyZipMemberReader(PyBytes content, PyString name, ExecutionContext context)
        {
            _content = content;
            _name = name;
            _context = context;
        }

        public bool IsClosed { get; private set; }

        public int GetPyHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "closed" => IsClosed,
                "name" => _name,
                "mode" => PyString.FromString("rb"),
                "read" => BoundCallable.Create((arguments, span, context) => Read(ParseReadSize(arguments, "zip member.read([size])", span), span), "zip member.read", ["size"], 0),
                "readline" => BoundCallable.Create((arguments, span, context) => ReadLine(ParseReadSize(arguments, "zip member.readline([size])", span), span), "zip member.readline", ["size"], 0),
                "readlines" => BoundCallable.Create((arguments, span, context) => ReadLines(ParseReadSize(arguments, "zip member.readlines([hint])", span), span), "zip member.readlines", ["hint"], 0),
                "tell" => BoundCallable.CreateNoArguments(this, "zip member.tell", static (receiver, span, _) => receiver.Tell(span)),
                "readable" => BoundCallable.CreateNoArguments(this, "zip member.readable", static (receiver, span, _) =>
                {
                    receiver.EnsureOpen(span);
                    return true;
                }),
                "writable" => BoundCallable.CreateNoArguments(this, "zip member.writable", static (receiver, span, _) =>
                {
                    receiver.EnsureOpen(span);
                    return false;
                }),
                "seekable" => BoundCallable.CreateNoArguments(this, "zip member.seekable", static (receiver, span, _) =>
                {
                    receiver.EnsureOpen(span);
                    return false;
                }),
                "seek" => BoundCallable.Create((_, span, _) => throw new LythonRuntimeException("NotImplementedError", "zip member seek/random access is unsupported by Lython.", span), "zip member.seek", ["offset", "whence"], 1),
                "close" => BoundCallable.CreateNoArguments(this, "zip member.close", static (receiver, span, _) =>
                {
                    receiver.Close(span);
                    return PyNone.Instance;
                }),
                "__enter__" => BoundCallable.CreateNoArguments(this, "zip member.__enter__", static (receiver, span, _) => receiver.Enter(span)),
                "__exit__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 3)
                    {
                        throw new LythonRuntimeException("TypeError", "zip member __exit__(exc_type, exc, tb) expects three arguments", span);
                    }

                    ExitWithOutcome(arguments[0] is PyNone, span);
                    return false;
                }, "zip member.__exit__", ["exc_type", "exc_value", "traceback"], 3),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString($"<zipfile.ZipExtFile name='{_name.AsString()}' mode='rb'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public object Enter(LythonSourceSpan? span)
        {
            EnsureOpen(span);
            return this;
        }

        object IPyContextManager.Enter() => Enter(null);

        ValueTask<object> IPyAsyncContextManager.EnterAsync() => ValueTask.FromResult<object>(Enter(null));

        private void ExitWithOutcome(bool completedNormally, LythonSourceSpan? span)
        {
            _ = completedNormally;
            Close(span);
        }

        bool IPyContextManager.Exit(object exceptionType, object exceptionValue, object traceback)
        {
            ExitWithOutcome(exceptionType is PyNone, null);
            return false;
        }

        ValueTask<bool> IPyAsyncContextManager.ExitAsync(object exceptionType, object exceptionValue, object traceback)
        {
            ExitWithOutcome(exceptionType is PyNone, null);
            return ValueTask.FromResult(false);
        }

        public IEnumerable<object> Iterate() => PyIteration.EnumerateIterator(this);

        public bool TryMoveNext([MaybeNullWhen(false)] out object value)
        {
            if (ReadLine(-1, null) is not PyBytes line || line.Length == 0)
            {
                value = PyNone.Instance;
                return false;
            }

            value = line;
            return true;
        }

        public void Close(LythonSourceSpan? span)
        {
            _ = span;
            IsClosed = true;
        }

        private void EnsureOpen(LythonSourceSpan? span)
        {
            if (IsClosed)
            {
                throw new LythonRuntimeException("ValueError", "I/O operation on closed member.", span);
            }
        }

        private object Read(int size, LythonSourceSpan? span)
        {
            EnsureOpen(span);
            if (_cursor >= _content.Length)
            {
                return CreateBytes([], _context, span);
            }

            var remaining = _content.Length - _cursor;
            var take = size < 0 ? remaining : Math.Min(remaining, size);
            var end = _cursor + take;
            var length = end - _cursor;
            using (_context.MemoryGovernor.ReserveTemporary(PyBytes.EstimateApproximateBytes(length), span))
            {
                var binary = _content.Bytes.Slice(_cursor, length).ToArray();
                _cursor = end;
                return CreateBytes(binary, _context, span);
            }
        }

        private object ReadLine(int size, LythonSourceSpan? span)
        {
            EnsureOpen(span);
            if (_cursor >= _content.Length)
            {
                return CreateBytes([], _context, span);
            }

            var source = _content.Bytes;
            var end = TextLineScanning.FindLineEndByte(source, _cursor, TextNewlineMode.PreserveLineFeed);
            if (size >= 0)
            {
                end = Math.Min(end, _cursor + size);
            }

            var lineLength = end - _cursor;
            using (_context.MemoryGovernor.ReserveTemporary(PyBytes.EstimateApproximateBytes(lineLength), span))
            {
                var lineBytes = source.Slice(_cursor, lineLength).ToArray();
                _cursor = end;
                return CreateBytes(lineBytes, _context, span);
            }
        }

        private object ReadLines(int hint, LythonSourceSpan? span)
        {
            EnsureOpen(span);
            var lines = new PyList([], _context.MemoryGovernor, span);
            var total = 0;
            while (true)
            {
                var line = (PyBytes)ReadLine(-1, span);
                if (line.Length == 0)
                {
                    break;
                }

                lines.Add(line);
                total += line.Length;
                _context.ObserveCollectionCount(lines.Count, span);
                if ((lines.Count & 63) == 0)
                {
                    _context.CheckExecutionBudget(span);
                }

                if (hint > 0 && total > hint)
                {
                    break;
                }
            }

            return lines;
        }

        private object Tell(LythonSourceSpan? span)
        {
            EnsureOpen(span);
            return new BigInteger(_cursor);
        }

        private static int ParseReadSize(object[] arguments, string owner, LythonSourceSpan span)
        {
            if (arguments.Length == 0 || arguments[0] is null or PyNone)
            {
                return -1;
            }

            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", owner + " expects an integer", span);
            }

            if (!PyNumberOps.TryAsInteger(arguments[0], out var integer) || integer < int.MinValue || integer > int.MaxValue)
            {
                throw new LythonRuntimeException("TypeError", owner + " expects an integer", span);
            }

            return (int)integer;
        }
    }

    /// <summary>
    /// Sequential write handle for one ZIP member. Uncompressed bytes buffer
    /// in a governed builder; the entry commits to the archive staged state
    /// exactly once at the first close, even when a context body fails. The
    /// archive releases single-writer ownership on that close whether the
    /// commit succeeds or not, so a dead writer can never wedge later writes.
    /// A failed close is terminal for the handle while the archive stays
    /// usable; uncommitted bytes are dropped with the run governor.
    /// </summary>
    private sealed class PyZipMemberWriter : IPyDynamicAttributes, IPyAsyncContextManager, IPyRenderableValue, IPyHashableValue
    {
        private readonly PyZipFile _archive;
        private readonly PyString _name;
        private readonly ushort _dosTime;
        private readonly ushort _dosDate;
        private readonly ushort _method;
        private readonly int _level;
        private readonly byte[] _comment;
        private readonly byte[] _extra;
        private readonly int _createSystem;
        private readonly uint _externalAttributes;
        private readonly bool _forceZip64;
        private readonly PyZipInfo? _info;
        private readonly GovernedByteBuilder _buffer;
        private readonly ExecutionContext _context;
        private long _writeCalls;

        public PyZipMemberWriter(
            PyZipFile archive,
            PyString name,
            ushort dosTime,
            ushort dosDate,
            ushort method,
            int level,
            byte[] comment,
            byte[] extra,
            int createSystem,
            uint externalAttributes,
            bool forceZip64,
            PyZipInfo? info,
            GovernedByteBuilder buffer,
            ExecutionContext context)
        {
            _archive = archive;
            _name = name;
            _dosTime = dosTime;
            _dosDate = dosDate;
            _method = method;
            _level = level;
            _comment = comment;
            _extra = extra;
            _createSystem = createSystem;
            _externalAttributes = externalAttributes;
            _forceZip64 = forceZip64;
            _info = info;
            _buffer = buffer;
            _context = context;
        }

        public bool IsClosed { get; private set; }

        internal string EntryName => _name.AsString();

        internal ushort DosTime => _dosTime;

        internal ushort DosDate => _dosDate;

        internal ushort Method => _method;

        internal int Level => _level;

        internal byte[] Comment => _comment;

        internal byte[] Extra => _extra;

        internal int CreateSystem => _createSystem;

        internal uint ExternalAttributes => _externalAttributes;

        internal bool ForceZip64 => _forceZip64;

        internal PyZipInfo? Info => _info;

        public int GetPyHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "closed" => IsClosed,
                "name" => _name,
                "mode" => PyString.FromString("wb"),
                "write" => BoundCallable.Create((arguments, span, context) => Write(arguments, span), "zip member.write", ["data"], 1),
                "writelines" => BoundCallable.Create((arguments, span, context) => WriteLines(arguments, span, context), "zip member.writelines", ["lines"], 1),
                "read" => BoundCallable.Create((_, span, _) => throw new LythonRuntimeException("NotImplementedError", "zip member read is unsupported on write handles.", span), "zip member.read", ["size"], 0),
                "readline" => BoundCallable.Create((_, span, _) => throw new LythonRuntimeException("NotImplementedError", "zip member read is unsupported on write handles.", span), "zip member.readline", ["size"], 0),
                "readlines" => BoundCallable.Create((_, span, _) => throw new LythonRuntimeException("NotImplementedError", "zip member read is unsupported on write handles.", span), "zip member.readlines", ["hint"], 0),
                "tell" => BoundCallable.CreateNoArguments(this, "zip member.tell", static (_, span, _) => throw new LythonRuntimeException("NotImplementedError", "zip member seek/random access is unsupported by Lython.", span)),
                "readable" => BoundCallable.CreateNoArguments(this, "zip member.readable", static (receiver, span, _) =>
                {
                    receiver.EnsureOpen(span);
                    return false;
                }),
                "writable" => BoundCallable.CreateNoArguments(this, "zip member.writable", static (receiver, span, _) =>
                {
                    receiver.EnsureOpen(span);
                    return true;
                }),
                "seekable" => BoundCallable.CreateNoArguments(this, "zip member.seekable", static (receiver, span, _) =>
                {
                    receiver.EnsureOpen(span);
                    return false;
                }),
                "seek" => BoundCallable.Create((_, span, _) => throw new LythonRuntimeException("NotImplementedError", "zip member seek/random access is unsupported by Lython.", span), "zip member.seek", ["offset", "whence"], 1),
                "flush" => BoundCallable.CreateNoArguments(this, "zip member.flush", static (receiver, span, _) =>
                {
                    receiver.EnsureOpen(span);
                    return PyNone.Instance;
                }),
                "close" => BoundCallable.CreateNoArguments(this, "zip member.close", static (receiver, span, _) =>
                {
                    receiver.Close(span);
                    return PyNone.Instance;
                }),
                "__enter__" => BoundCallable.CreateNoArguments(this, "zip member.__enter__", static (receiver, span, _) => receiver.Enter(span)),
                "__exit__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 3)
                    {
                        throw new LythonRuntimeException("TypeError", "zip member __exit__(exc_type, exc, tb) expects three arguments", span);
                    }

                    ExitWithOutcome(arguments[0] is PyNone, span);
                    return false;
                }, "zip member.__exit__", ["exc_type", "exc_value", "traceback"], 3),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString($"<zipfile._ZipWriteFile name='{_name.AsString()}' mode='wb'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public object Enter(LythonSourceSpan? span)
        {
            EnsureOpen(span);
            return this;
        }

        object IPyContextManager.Enter() => Enter(null);

        ValueTask<object> IPyAsyncContextManager.EnterAsync() => ValueTask.FromResult<object>(Enter(null));

        private void ExitWithOutcome(bool completedNormally, LythonSourceSpan? span)
        {
            _ = completedNormally;
            Close(span);
        }

        bool IPyContextManager.Exit(object exceptionType, object exceptionValue, object traceback)
        {
            ExitWithOutcome(exceptionType is PyNone, null);
            return false;
        }

        ValueTask<bool> IPyAsyncContextManager.ExitAsync(object exceptionType, object exceptionValue, object traceback)
        {
            ExitWithOutcome(exceptionType is PyNone, null);
            return ValueTask.FromResult(false);
        }

        public void Close(LythonSourceSpan? span)
        {
            if (IsClosed)
            {
                return;
            }

            IsClosed = true;
            byte[] data;
            try
            {
                data = _buffer.ToArrayAndRelease();
            }
            catch
            {
                _archive.ReleaseWriter(this);
                throw;
            }

            _archive.CommitWriterEntry(this, data, span);
        }

        private void EnsureOpen(LythonSourceSpan? span)
        {
            if (IsClosed)
            {
                throw new LythonRuntimeException("ValueError", "I/O operation on closed member.", span);
            }
        }

        private object Write(object[] arguments, LythonSourceSpan span)
        {
            EnsureOpen(span);
            if (arguments[0] is not PyBytes bytes)
            {
                throw new LythonRuntimeException("TypeError", "write() data must be bytes.", span);
            }

            AppendBuffer(bytes.Bytes, span);
            return new BigInteger(bytes.Bytes.Length);
        }

        private object WriteLines(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            EnsureOpen(span);
            var count = 0;
            foreach (var item in ToSequence(arguments[0], span, context))
            {
                if (item is not PyBytes bytes)
                {
                    throw new LythonRuntimeException("TypeError", "writelines() items must be bytes.", span);
                }

                AppendBuffer(bytes.Bytes, span);
                if ((++count & 63) == 0)
                {
                    context.CheckExecutionBudget(span);
                }
            }

            return PyNone.Instance;
        }

        private void AppendBuffer(ReadOnlySpan<byte> data, LythonSourceSpan span)
        {
            if ((_writeCalls++ & 63) == 0)
            {
                _context.CheckExecutionBudget(span);
            }

            if (data.Length == 0)
            {
                return;
            }

            _buffer.Append(data);
        }
    }
}
