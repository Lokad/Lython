using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // One pull path for synchronous and asynchronous discovery. No host operation
    // is pending while a record is exposed; retained traversal scratch stays
    // funded until the iterator relinquishes it (or its execution owner ends).
    private sealed class ModuleDiscoveryIterator : PyIteratorBase
    {
        private object? _paths, _prefix, _onerror;
        private ExecutionContext? _context;
        private readonly LythonSourceSpan _span;
        private readonly bool _recursive;
        private bool _asynchronous;
        private IAsyncEnumerator<object>? _enumerator;

        internal ModuleDiscoveryIterator(object paths, object prefix, object onerror, bool recursive,
            ExecutionContext context, LythonSourceSpan span)
        {
            _paths = paths; _prefix = prefix; _onerror = onerror;
            _recursive = recursive; _context = context; _span = span;
        }

        public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
        {
            var result = MoveNextAsync(false).GetAwaiter().GetResult();
            value = result.HasValue ? result.Value : PyNone.Instance;
            return result.HasValue;
        }

        public override ValueTask<PyIterationResult> TryMoveNextAsync() => MoveNextAsync(true);

        private async ValueTask<PyIterationResult> MoveNextAsync(bool asynchronous)
        {
            if (_context is null) return PyIterationResult.End;
            _asynchronous = asynchronous;
            _context.CheckExecution(_span);
            _enumerator ??= Walk().GetAsyncEnumerator();
            try
            {
                if (await _enumerator.MoveNextAsync().ConfigureAwait(false))
                    return PyIterationResult.Yield(_enumerator.Current);
            }
            catch
            {
                await EndAsync().ConfigureAwait(false);
                throw;
            }
            await EndAsync().ConfigureAwait(false);
            return PyIterationResult.End;
        }

        private async ValueTask EndAsync()
        {
            var enumerator = _enumerator;
            _enumerator = null;
            try { if (enumerator is not null) await enumerator.DisposeAsync().ConfigureAwait(false); }
            finally { _paths = _prefix = _onerror = null; _context = null; }
        }

        private async IAsyncEnumerable<object> Walk()
        {
            var context = _context!;
            using var scratch = context.MemoryGovernor.ReserveTemporary(512, _span);
            var pending = new Stack<IAsyncEnumerator<object>>();
            var seenPaths = new HashSet<string>(StringComparer.Ordinal);
            pending.Push(Level(_paths!, _prefix!, null, seenPaths, scratch).GetAsyncEnumerator());
            try
            {
                while (pending.Count != 0)
                {
                    context.CheckExecution(_span);
                    var current = pending.Peek();
                    if (!await current.MoveNextAsync().ConfigureAwait(false))
                    {
                        pending.Pop();
                        await current.DisposeAsync().ConfigureAwait(false);
                        continue;
                    }
                    var info = (PyNamedTupleObject)current.Current;
                    yield return info;
                    if (!_recursive || !(bool)info[2]) continue;

                    var name = (PyString)info[1];
                    using var key = context.MemoryGovernor.ReserveTemporary(checked(256L + 8L * name.Utf8Bytes.Length), _span);
                    object module;
                    try
                    {
                        var moduleName = name.AsString();
                        module = _asynchronous
                            ? await ResolveImportedModuleHierarchyAsync(moduleName, context, _span).ConfigureAwait(false)
                            : ResolveImportedModuleHierarchy(moduleName, context, _span);
                    }
                    catch (LythonRuntimeException error) when (MatchesExceptionType(BuiltinException("Exception"), error.Identity))
                    {
                        // Refusal/cancellation cannot become a recoverable package error.
                        context.CheckExecution(_span);
                        if (_onerror is not PyNone)
                        {
                            if (_asynchronous) await CallableInvocation.InvokeUnaryAsync(_onerror!, name, _span, context).ConfigureAwait(false);
                            else CallableInvocation.InvokeUnary(_onerror!, name, _span, context);
                        }
                        else if (!MatchesExceptionType(BuiltinException("ImportError"), error.Identity)) throw;
                        continue;
                    }
                    var locations = await TryReadWrapperAttributeAsync(module, "__path__", context, _span, _asynchronous).ConfigureAwait(false);
                    if (!locations.Found) continue;
                    var prefix = AppendName(name, ".", context, _span);
                    scratch.Grow(128, _span); // stack capacity and replacement overlap
                    pending.Push(Level(locations.Value, prefix,
                        module is PyModule and not ScriptPyModule ? ((PyModule)module).Name : null,
                        seenPaths, scratch).GetAsyncEnumerator());
                }
            }
            finally
            {
                while (pending.Count != 0) await pending.Pop().DisposeAsync().ConfigureAwait(false);
                seenPaths.Clear();
            }
        }

        private async IAsyncEnumerable<object> Level(object paths, object prefix, string? builtinParent,
            HashSet<string> seenPaths, MemoryGovernor.TemporaryMemoryReservation traversal)
        {
            var context = _context!;
            using var scratch = context.MemoryGovernor.ReserveTemporary(256, _span);
            var yielded = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                if (paths is PyNone || builtinParent is not null)
                {
                    foreach (var name in EnumerateDiscoverableBuiltinModuleNames(context))
                    {
                        context.CheckExecution(_span);
                        var dot = name.LastIndexOf('.');
                        if (builtinParent is null ? dot >= 0 : dot < 0 || !name.AsSpan(0, dot).SequenceEqual(builtinParent)) continue;
                        var leaf = name[(dot + 1)..];
                        var isPackage = EnumerateDiscoverableBuiltinModuleNames(context).Any(candidate => candidate.StartsWith(name + ".", StringComparison.Ordinal));
                        yield return PkgutilModule.MakeModuleInfo(AppendName(prefix, leaf, context, _span), isPackage, context, _span);
                    }
                }
                if (paths is PyNone)
                {
                    if (context.State.DisableLocalModuleImports || context.State.AllowedLocalModules is null) yield break;
                    foreach (var entry in context.State.AllowedLocalModules)
                    {
                        context.CheckExecution(_span);
                        using var work = context.MemoryGovernor.ReserveTemporary(checked(256L + 32L * entry.Length), _span);
                        var name = PkgutilModule.TryGetLocalModuleName(entry);
                        if (name is null || yielded.Contains(name)) continue;
                        scratch.Grow(checked(256L + 8L * name.Length), _span);
                        yielded.Add(name);
                        var found = _asynchronous ? await TryResolveLocalImportPathAsync(name, context, _span).ConfigureAwait(false)
                            : TryResolveLocalImportPath(name, context, _span, out var path, out var package) ? new LocalImportCandidate(path, package) : (LocalImportCandidate?)null;
                        if (found is { } local)
                            yield return PkgutilModule.MakeModuleInfo(AppendName(prefix, name, context, _span), local.IsPackage, context, _span);
                    }
                    yield break;
                }
                if (paths is PyString) throw new LythonRuntimeException("ValueError", "path must be None or an iterable of paths, not a string", _span);
                await using var cursor = _asynchronous ? await PyIteration.Cursor.CreateAsync(paths, _span, context).ConfigureAwait(false)
                    : PyIteration.Cursor.Create(paths, _span, context);
                while (true)
                {
                    context.CheckExecution(_span);
                    var pulled = _asynchronous ? await cursor.TryMoveNextAsync().ConfigureAwait(false)
                        : cursor.TryMoveNext(out var item) ? PyIterationResult.Yield(item) : PyIterationResult.End;
                    if (!pulled.HasValue) break;
                    var text = pulled.Value is PyPath pyPath ? pyPath.Value : pulled.Value as PyString
                        ?? throw RuntimeErrors.Type("pkgutil discovery expects an iterable of path strings", _span);
                    using var pathStorage = context.MemoryGovernor.ReserveTemporary(
                        checked(256L + 128L * (text.Utf8Bytes.Length + (long)context.Host.Cwd.Length)), _span);
                    var directory = text.Utf8Bytes.IsEmpty ? "." : text.AsString();
                    IReadOnlyList<string> entries;
                    try { entries = _asynchronous ? await context.HostListDirAsync(directory, _span).ConfigureAwait(false)
                        : context.HostListDir(directory, _span); }
                    catch (HostOperationException error) when (IsFileSystemFailure(error)) { continue; }
                    if (_recursive)
                    {
                        // Canonicalize only the deduplication key after the actual
                        // request has validated every original component.
                        var key = PathOps.Normalize(directory, context.Host.Cwd);
                        if (seenPaths.Contains(key)) continue;
                        traversal.Grow(checked(256L + 8L * key.Length), _span);
                        seenPaths.Add(key);
                    }
                    using var listing = context.MemoryGovernor.ReserveTemporary(checked(128L + 32L * entries.Count), _span);
                    var ordered = new List<string>(entries.Count);
                    try
                    {
                        foreach (var entry in entries) { context.CheckExecution(_span); ordered.Add(entry); }
                        ExecutionSort.Sort(ordered, context, _span, StringComparer.Ordinal.Compare);
                        foreach (var entry in ordered)
                        {
                            context.CheckExecution(_span);
                            if (entry.StartsWith('.')) continue;
                            var package = !entry.EndsWith(".py", StringComparison.Ordinal);
                            using var work = context.MemoryGovernor.ReserveTemporary(
                                checked(256L + 32L * (directory.Length + (long)entry.Length)), _span);
                            var leaf = package ? entry : entry[..^3];
                            if (leaf == "__init__" || !PkgutilModule.IsSimpleModuleName(leaf) || yielded.Contains(leaf)) continue;
                            var child = PathOps.Join(directory, entry);
                            var source = package ? PathOps.Join(child, "__init__.py") : child;
                            if (package && !(_asynchronous ? await context.HostExistsAsync(source, _span).ConfigureAwait(false) : context.HostExists(source, _span))) continue;
                            if (prefix is not PyString prefixText) throw RuntimeErrors.Type("pkgutil discovery prefix must be a string", _span);
                            using var named = context.MemoryGovernor.ReserveTemporary(checked(128L + 8L * (prefixText.Utf8Bytes.Length + (long)leaf.Length)), _span);
                            var fullname = prefixText.AsString() + leaf;
                            if (!PkgutilModule.IsDiscoveredLocalModuleAllowed(fullname, source, context)) continue;
                            scratch.Grow(checked(256L + 8L * leaf.Length), _span);
                            yielded.Add(leaf);
                            yield return PkgutilModule.MakeModuleInfo(AppendName(prefix, leaf, context, _span), package, context, _span);
                        }
                        // Drop borrowed listing references before refunding its working copy.
                    }
                    finally { ordered.Clear(); ordered = null!; entries = null!; }
                }
            }
            finally { yielded.Clear(); }
        }

        private static PyString AppendName(object prefix, string leaf, ExecutionContext context, LythonSourceSpan span)
        {
            if (prefix is not PyString text) throw RuntimeErrors.Type("pkgutil discovery prefix must be a string", span);
            var builder = new GovernedByteBuilder(context.MemoryGovernor);
            try
            {
                builder.Append(text);
                builder.AppendString(leaf);
                return builder.ToPyStringAndRelease();
            }
            finally { builder.Release(); }
        }

        public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<generator object pkgutil discovery>");
    }
}
