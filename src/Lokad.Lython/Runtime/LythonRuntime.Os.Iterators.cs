using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class PyScandirIterator : PyIteratorBase, IPyContextManager, IPyDynamicAttributes
    {
        private readonly PyDirEntryObject[] _entries;
        private int _index;
        private bool _closed;

        public PyScandirIterator(PyDirEntryObject[] entries)
        {
            _entries = entries;
        }

        public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
        {
            if (_closed || _index >= _entries.Length)
            {
                value = PyNone.Instance;
                return false;
            }

            value = _entries[_index++];
            return true;
        }

        public object Enter() => this;

        public bool Exit(object exceptionType, object exceptionValue, object traceback)
        {
            _ = exceptionType;
            _ = exceptionValue;
            _ = traceback;
            _closed = true;
            return false;
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "close" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "ScandirIterator.close() expects no arguments.", span);
                    }

                    _closed = true;
                    return PyNone.Instance;
                }, "ScandirIterator.close", []),
                "__enter__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "ScandirIterator.__enter__() expects no arguments.", span);
                    }

                    return this;
                }, "ScandirIterator.__enter__", []),
                "__exit__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 3)
                    {
                        throw new LythonRuntimeException("TypeError", "ScandirIterator.__exit__(exc_type, exc, tb) expects three arguments.", span);
                    }

                    _closed = true;
                    return false;
                }, "ScandirIterator.__exit__", ["exc_type", "exc", "tb"]),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
        public override PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<ScandirIterator>");
        }
    }

    internal sealed class PyDirEntryObject : IPyDynamicAttributes, IPyRenderableValue
    {
        private readonly string _name;
        private readonly string _path;
        private LythonPathStat? _stat;

        public PyDirEntryObject(string name, string path)
        {
            _name = name;
            _path = path;
        }

        public string Path => _path;

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "name" => PyString.FromString(_name),
                "path" => PyString.FromString(_path),
                "__fspath__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "DirEntry.__fspath__() expects no arguments.", span);
                    }

                    return PyString.FromString(_path);
                }, "DirEntry.__fspath__", []),
                "is_file" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "DirEntry.is_file() expects no arguments.", span);
                    }

                    return GetCachedStat(context, span).IsFile;
                },
                async (arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "DirEntry.is_file() expects no arguments.", span);
                    }

                    return (await GetCachedStatAsync(context, span).ConfigureAwait(false)).IsFile;
                },
                "DirEntry.is_file",
                []),
                "is_dir" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "DirEntry.is_dir() expects no arguments.", span);
                    }

                    return GetCachedStat(context, span).IsDir;
                },
                async (arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "DirEntry.is_dir() expects no arguments.", span);
                    }

                    return (await GetCachedStatAsync(context, span).ConfigureAwait(false)).IsDir;
                },
                "DirEntry.is_dir",
                []),
                "stat" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "DirEntry.stat() expects no arguments.", span);
                    }

                    return GetCachedStat(context, span);
                },
                async (arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "DirEntry.stat() expects no arguments.", span);
                    }

                    return await GetCachedStatAsync(context, span).ConfigureAwait(false);
                },
                "DirEntry.stat",
                []),
                "inode" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "DirEntry.inode() expects no arguments.", span);
                    }

                    throw new LythonRuntimeException("NotImplementedError", "DirEntry.inode() is not supported because Lython's host path model does not expose inode metadata.", span);
                }, "DirEntry.inode", []),
                "is_symlink" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "DirEntry.is_symlink() expects no arguments.", span);
                    }

                    throw new LythonRuntimeException("NotImplementedError", "DirEntry.is_symlink() is not supported because Lython's host path model does not expose symlinks.", span);
                }, "DirEntry.is_symlink", []),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString($"<DirEntry '{_name}'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        private LythonPathStat GetCachedStat(ExecutionContext context, LythonSourceSpan span)
            => _stat ??= HostStat(_path, context, span);

        private async ValueTask<LythonPathStat> GetCachedStatAsync(ExecutionContext context, LythonSourceSpan span)
            => _stat ??= await HostStatAsync(_path, context, span).ConfigureAwait(false);
    }

    private sealed class PyWalkIterator : PyIteratorBase
    {
        private readonly bool _topdown;
        private readonly ICallable? _onerror;
        private readonly ExecutionContext _context;
        private readonly MemoryGovernor _governor;
        private readonly LythonSourceSpan? _span;
        private readonly Stack<WalkFrame> _frames = [];

        public PyWalkIterator(string root, bool topdown, ICallable? onerror, ExecutionContext context, LythonSourceSpan? span)
        {
            PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
            _topdown = topdown;
            _onerror = onerror;
            _context = context;
            _governor = context.MemoryGovernor;
            _span = span;
            _frames.Push(new WalkFrame(root));
        }

        public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
        {
            while (_frames.Count != 0)
            {
                var frame = _frames.Peek();
                if (!frame.Scanned)
                {
                    if (!TryScan(frame))
                    {
                        _frames.Pop();
                        continue;
                    }

                    if (_topdown)
                    {
                        frame.Yielded = true;
                        value = CreateTuple(frame);
                        return true;
                    }
                }

                var childNames = frame.GetChildNames(_topdown, _span);
                while (frame.NextChildIndex < childNames.Count)
                {
                    var childName = childNames[frame.NextChildIndex++];
                    var childPath = JoinChild(frame.DirectoryPath, childName);
                    try
                    {
                        _context.RegisterHostCall(_span);
                        var stat = _context.HostStat(childPath, _span);
                        if (!stat.Exists || !stat.IsDir)
                        {
                            continue;
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        HandleWalkError(ex.Message);
                        continue;
                    }

                    _frames.Push(new WalkFrame(childPath));
                    goto ContinueTraversal;
                }

                if (!_topdown && !frame.Yielded)
                {
                    frame.Yielded = true;
                    value = CreateTuple(frame);
                    return true;
                }

                _frames.Pop();

            ContinueTraversal:
                continue;
            }

            value = PyNone.Instance;
            return false;
        }

        public override async ValueTask<PyIterationResult> TryMoveNextAsync()
        {
            while (_frames.Count != 0)
            {
                var frame = _frames.Peek();
                if (!frame.Scanned)
                {
                    if (!await TryScanAsync(frame).ConfigureAwait(false))
                    {
                        _frames.Pop();
                        continue;
                    }

                    if (_topdown)
                    {
                        frame.Yielded = true;
                        return PyIterationResult.Yield(CreateTuple(frame));
                    }
                }

                var childNames = frame.GetChildNames(_topdown, _span);
                while (frame.NextChildIndex < childNames.Count)
                {
                    var childName = childNames[frame.NextChildIndex++];
                    var childPath = JoinChild(frame.DirectoryPath, childName);
                    try
                    {
                        _context.RegisterHostCall(_span);
                        var stat = await _context.HostStatAsync(childPath, _span).ConfigureAwait(false);
                        if (!stat.Exists || !stat.IsDir)
                        {
                            continue;
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        await HandleWalkErrorAsync(ex.Message).ConfigureAwait(false);
                        continue;
                    }

                    _frames.Push(new WalkFrame(childPath));
                    goto ContinueTraversal;
                }

                if (!_topdown && !frame.Yielded)
                {
                    frame.Yielded = true;
                    return PyIterationResult.Yield(CreateTuple(frame));
                }

                _frames.Pop();

            ContinueTraversal:
                continue;
            }

            return PyIterationResult.End;
        }

        private bool TryScan(WalkFrame frame)
        {
            try
            {
                _context.RegisterHostCall(_span);
                var names = _context.HostListDir(frame.DirectoryPath, _span);
                var directories = new List<string>();
                var files = new List<string>();
                foreach (var name in names)
                {
                    var child = JoinChild(frame.DirectoryPath, name);
                    _context.RegisterHostCall(_span);
                    var stat = _context.HostStat(child, _span);
                    if (!stat.Exists)
                    {
                        continue;
                    }

                    if (stat.IsDir)
                    {
                        directories.Add(name);
                    }
                    else if (stat.IsFile)
                    {
                        files.Add(name);
                    }
                }

                directories.Sort(StringComparer.Ordinal);
                files.Sort(StringComparer.Ordinal);
                frame.SetEntries(CreateStringList(directories), CreateStringList(files));
                return true;
            }
            catch (InvalidOperationException ex)
            {
                HandleWalkError(ex.Message);
                return false;
            }
            catch (LythonRuntimeException ex) when (IsHostRuntimeFailure(ex))
            {
                HandleWalkError(ex.Message);
                return false;
            }
        }

        private async ValueTask<bool> TryScanAsync(WalkFrame frame)
        {
            try
            {
                _context.RegisterHostCall(_span);
                var names = await _context.HostListDirAsync(frame.DirectoryPath, _span).ConfigureAwait(false);
                var directories = new List<string>();
                var files = new List<string>();
                foreach (var name in names)
                {
                    var child = JoinChild(frame.DirectoryPath, name);
                    _context.RegisterHostCall(_span);
                    var stat = await _context.HostStatAsync(child, _span).ConfigureAwait(false);
                    if (!stat.Exists)
                    {
                        continue;
                    }

                    if (stat.IsDir)
                    {
                        directories.Add(name);
                    }
                    else if (stat.IsFile)
                    {
                        files.Add(name);
                    }
                }

                directories.Sort(StringComparer.Ordinal);
                files.Sort(StringComparer.Ordinal);
                frame.SetEntries(CreateStringList(directories), CreateStringList(files));
                return true;
            }
            catch (InvalidOperationException ex)
            {
                await HandleWalkErrorAsync(ex.Message).ConfigureAwait(false);
                return false;
            }
            catch (LythonRuntimeException ex) when (IsHostRuntimeFailure(ex))
            {
                await HandleWalkErrorAsync(ex.Message).ConfigureAwait(false);
                return false;
            }
        }

        private PyList CreateStringList(IReadOnlyList<string> items)
        {
            var values = new object[items.Count];
            for (var i = 0; i < items.Count; i++)
            {
                values[i] = PyString.FromString(items[i], _governor, _span);
            }

            return new PyList(values, _governor, _span);
        }

        private void HandleWalkError(string message)
        {
            if (_onerror is null)
            {
                return;
            }

            var payload = PyString.FromString(message, _governor, _span);
            var callbackSpan = _span ?? new LythonSourceSpan(0, 0, 0, 0);
            _ = InvokeCallableTarget(
                _onerror,
                callbackSpan,
                callbackSpan,
                _context,
                [CallArgumentValue.Positional(new PyException("RuntimeError", message, payload))]);
        }

        private async ValueTask HandleWalkErrorAsync(string message)
        {
            if (_onerror is null)
            {
                return;
            }

            var payload = PyString.FromString(message, _governor, _span);
            var callbackSpan = _span ?? new LythonSourceSpan(0, 0, 0, 0);
            _ = await InvokeCallableTargetAsync(
                _onerror,
                callbackSpan,
                callbackSpan,
                _context,
                () => ValueTask.FromResult(new[] { CallArgumentValue.Positional(new PyException("RuntimeError", message, payload)) })).ConfigureAwait(false);
        }

        private object CreateTuple(WalkFrame frame)
        {
            return new PyTuple(
            [
                PyString.FromString(frame.DirectoryPath, _governor, _span),
                frame.DirectoryNames.RequireNotNull(),
                frame.FileNames.RequireNotNull()
            ],
            _governor,
            _span);
        }

        public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<os.walk>");

        private sealed class WalkFrame
        {
            public WalkFrame(string directoryPath)
            {
                DirectoryPath = directoryPath;
            }

            public string DirectoryPath { get; }

            public bool Scanned { get; private set; }

            public bool Yielded { get; set; }

            public int NextChildIndex { get; set; }

            public PyList? DirectoryNames { get; private set; }

            public PyList? FileNames { get; private set; }

            public void SetEntries(PyList directoryNames, PyList fileNames)
            {
                DirectoryNames = directoryNames;
                FileNames = fileNames;
                Scanned = true;
            }

            public List<string> GetChildNames(bool topdown, LythonSourceSpan? span)
            {
                if (!topdown)
                {
                    var values = new List<string>(DirectoryNames?.Count ?? 0);
                    if (DirectoryNames is not null)
                    {
                        foreach (var item in DirectoryNames)
                        {
                            values.Add(RequireWalkDirName(item, span));
                        }
                    }

                    return values;
                }

                var result = new List<string>(DirectoryNames?.Count ?? 0);
                if (DirectoryNames is null)
                {
                    return result;
                }

                foreach (var item in DirectoryNames)
                {
                    result.Add(RequireWalkDirName(item, span));
                }

                return result;
            }

            private static string RequireWalkDirName(object value, LythonSourceSpan? span)
            {
                if (!PyStringOps.TryAsString(value, out var text))
                {
                    throw new LythonRuntimeException("TypeError", "os.walk(...) expects dirnames to remain a list of strings.", span);
                }

                return text.AsString();
            }
        }
    }
}
