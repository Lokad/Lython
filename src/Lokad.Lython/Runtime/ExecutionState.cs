using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace Lokad.Lython.Runtime;

internal sealed class ExecutionState
{

    private Dictionary<object, RuntimeMemberCacheEntry>? _runtimeMemberCaches;
    private Dictionary<PythonExceptionIdentity, PyTuple>? _exceptionBaseTuples;
    internal PyType? TypingGeneric { get; set; }
    internal LythonRuntime.DecimalModule? DecimalModule { get; set; }
    internal PyNamedTupleType? FunctoolsCacheInfoType { get; set; }
    internal PyNamedTupleType? StatisticsRegressionType { get; set; }
    internal PyDict? UrlSplitFieldDefaults { get; set; }
    internal PyDict? UrlParseFieldDefaults { get; set; }
    internal PyTuple? GzipFileMetaBases { get; set; }
    internal PyTuple? GzipFileMetaMro { get; set; }
    internal LythonRuntime.UrllibParseModule.UrlSplitCache? UrlSplitCache { get; set; }
    private readonly ConditionalWeakTable<object, StrongBox<long>> _objectIds = new();
    private long _nextObjectId;

    internal bool TryReadExceptionBases(PythonExceptionIdentity identity, [MaybeNullWhen(false)] out PyTuple value)
    {
        value = null;
        return _exceptionBaseTuples is not null && _exceptionBaseTuples.TryGetValue(identity, out value);
    }

    // Immutable exception metadata has stable tuple identity within a run.
    // The finite builtin/module exception inventory bounds this cache; entries
    // own their dictionary slots while the ordinary pool owns tuple backing.
    internal void CacheExceptionBases(PythonExceptionIdentity identity, PyTuple value, LythonSourceSpan span)
    {
        var bytes = _exceptionBaseTuples is null ? 192L : 64L;
        MemoryGovernor.Reserve(bytes, span);
        try
        {
            _exceptionBaseTuples ??= new();
            _exceptionBaseTuples.Add(identity, value);
            MemoryGovernor.Commit(bytes);
        }
        catch
        {
            MemoryGovernor.ReleaseReserved(bytes);
            throw;
        }
    }
    public static readonly HashSet<string> BuiltinNames =
    [
        "object", "type", "open", "print", "input", "str", "repr", "ascii", "format",
        "len", "sorted", "any", "all", "min", "max", "sum", "abs", "pow", "round", "divmod",
        "bin", "oct", "hex", "chr", "ord", "callable", "hash", "id",
        "range", "enumerate", "zip", "iter", "next", "reversed", "map", "filter", "slice",
        "BaseException", "Exception", "ArithmeticError", "LookupError", "UnicodeError", "Warning", "FutureWarning",
        "TypeError", "ValueError", "KeyError", "IndexError", "RuntimeError", "EOFError",
        "AssertionError", "ImportError", "ModuleNotFoundError", "NameError", "UnboundLocalError", "AttributeError", "SyntaxError",
        "FileNotFoundError", "FileExistsError", "IsADirectoryError", "NotADirectoryError", "PermissionError",
        "TimeoutError", "IOError", "EnvironmentError", "OSError", "StopIteration",
        "ZeroDivisionError", "NotImplementedError", "RecursionError", "MemoryError",
        "UnicodeEncodeError", "UnicodeDecodeError", "UnicodeTranslateError", "OverflowError", "SystemExit",
        "GeneratorExit", "KeyboardInterrupt",
        "bool", "int", "float", "complex", "bytes",
        "staticmethod", "classmethod", "property", "super", "isinstance", "issubclass",
        "getattr", "hasattr", "setattr", "delattr", "dir", "vars", "globals", "locals",
        "list", "tuple", "dict", "set", "Ellipsis", "NotImplemented"
    ];

    public ExecutionState(ILythonHost host, LythonRunOptions? options)
        : this(host, options, new Dictionary<string, object>(StringComparer.Ordinal))
    {
    }

    public ExecutionState(
        ILythonHost host,
        LythonRunOptions? options,
        Dictionary<string, object> builtinVariables,
        ILythonRegexProvider? regexProvider = null)
    {
        Host = host;
        RegexProvider = regexProvider;
        AllowedBuiltinModules = LythonRuntime.SnapshotBuiltinModuleGrants(options?.AllowedBuiltinModules);
        Limits = LythonRuntime.ExecutionLimits.FromOptions(options);
        Guards = new ExecutionGuards(this);
        MemoryGovernor = new MemoryGovernor(options?.ExecutionMemoryOwner ?? options?.MemoryBudget);

        RandomState = new PyRandomState();
        DisableLocalModuleImports = options?.DisableLocalModuleImports ?? false;
        AllowedLocalModules = options?.AllowedLocalModules;
        // Host-provided argv is guest-retained through sys.argv, so its string
        // payload and backing array charge the execution budget up front like
        // any other retained collection. Empty argv stays free.
        var argvSource = options?.Args ?? Array.Empty<string>();
        var argvGovernor = MemoryGovernor;
        if (argvSource.Count > 0)
        {
            var argvArrayBytes = PyTuple.EstimateApproximateBytes(argvSource.Count);
            argvGovernor.Reserve(argvArrayBytes, null);
            argvGovernor.Commit(argvArrayBytes);
        }

        Args = argvSource
            .Select(arg => Text.PyString.FromString(arg, argvGovernor, allocationSpan: null))
            .ToArray();
        // Host-provided environment is guest-visible through os.environ, so own
        // the copy table up front at the dictionary slot rate; keys and values
        // stay host-owned references. Absent or empty environments stay free.
        var providedEnvironment = options?.Environment;
        if (providedEnvironment is null || providedEnvironment.Count == 0)
        {
            Environment = new Dictionary<string, string>(StringComparer.Ordinal);
        }
        else
        {
            var environmentTableBytes = 80L + (32L * providedEnvironment.Count);
            MemoryGovernor.Reserve(environmentTableBytes, null);
            MemoryGovernor.Commit(environmentTableBytes);
            Environment = new Dictionary<string, string>(providedEnvironment, StringComparer.Ordinal);
        }
        StandardOutput = new Text.GovernedByteBuilder(
            MemoryGovernor,
            allocationSpan: null,
            capacity: 0);
        StandardError = new Text.GovernedByteBuilder(
            MemoryGovernor,
            allocationSpan: null,
            capacity: 0);
        Stdin = new HostTextInputHandle(host.StandardInput, this);
        Stdout = new HostTextOutputHandle(host.StandardOutput, StandardOutput, "<stdout>", this);
        Stderr = new HostTextOutputHandle(host.StandardError, StandardError, "<stderr>", this);
        BuiltinVariables = builtinVariables;
    }

    public ILythonHost Host { get; }

    internal ILythonRegexProvider? RegexProvider { get; }

    public LythonRuntime.ExecutionLimits Limits { get; }

    public ExecutionGuards Guards { get; }

    public MemoryGovernor MemoryGovernor { get; }


    // N19: bounded per-execution regex compilation cache. Lazy so regex-free runs
    // pay nothing; shared by every context of the run, never across runs.
    internal RegexPatternCache? RegexCache { get; set; }

    // N01: execution-local structural traversal state (cycle pairs, depth,
    // cooperative work ticks). One per run, carried explicitly through async
    // recursion via ExecutionContext so continuations hopping pool threads
    // dispose the same state they entered. Never shared across runs.
    internal StructuralGuardState StructuralTraversal { get; } = new StructuralGuardState();

    public PyRandomState RandomState { get; }

    private PyDecimalContext? _decimalContext;

    public PyDecimalContext DecimalContext
    {
        get => _decimalContext ??= PyDecimalContext.Default(MemoryGovernor);
        set => _decimalContext = value;
    }

    public bool DisableLocalModuleImports { get; }

    public IReadOnlySet<string>? AllowedLocalModules { get; }

    public IReadOnlySet<string>? AllowedBuiltinModules { get; }

    public IReadOnlyList<Text.PyString> Args { get; }

    public Dictionary<string, string> Environment { get; }

    // os.environ reads share one mapping per run over the live table above,
    // so identity holds like CPython while contents stay current.
    public LythonRuntime.PyEnvironmentMapping? OsEnvironMapping { get; set; }

    internal Dictionary<PyModule, PyMappingProxy>? ModuleNamespaces;
    private PyDict? _importedModules;
    public PyDict ImportedModules => _importedModules ??= new PyDict(MemoryGovernor);

    public Dictionary<string, object> BuiltinVariables { get; }


    public Text.GovernedByteBuilder StandardOutput { get; }

    public Text.GovernedByteBuilder StandardError { get; }

    public HostTextInputHandle Stdin { get; }

    public HostTextOutputHandle Stdout { get; }

    public HostTextOutputHandle Stderr { get; }

    private HashSet<(string Filename, int Line, string Category, string Message)>? _shownDefaultWarnings;

    // Default warning action: one notice per message/category/module location.
    // Entries retain existing filename/message references; 256 B per entry
    // conservatively covers both old and replacement hash storage at growth.
    // The registry belongs to this run, so another execution starts afresh.
    internal bool MarkDefaultWarning(string filename, int line, string category, string message, LythonSourceSpan span)
    {
        var key = (filename, line, category, message);
        if (_shownDefaultWarnings?.Contains(key) == true) return false;
        var first = _shownDefaultWarnings is null;
        var charge = first ? 384L : 256L;
        MemoryGovernor.Reserve(charge, span);
        try
        {
            _shownDefaultWarnings ??= new();
            _shownDefaultWarnings.Add(key);
            MemoryGovernor.Commit(charge);
            return true;
        }
        catch
        {
            if (first) _shownDefaultWarnings = null;
            MemoryGovernor.ReleaseReserved(charge);
            throw;
        }
    }
    private const long IdentityEntryBytes = 64;

    // id() needs stable reference identity. Its entry estimates remain charged
    // for the batch even when the semantic weak table drops an unreachable key.

    public BigInteger GetObjectId(object? value)
    {
        var key = value ?? PyNone.Instance;
        if (_objectIds.TryGetValue(key, out var existing))
        {
            return new BigInteger(existing.Value);
        }

        MemoryGovernor.Reserve(IdentityEntryBytes, null);
        MemoryGovernor.Commit(IdentityEntryBytes);
        var box = new StrongBox<long>(Interlocked.Increment(ref _nextObjectId));

        _objectIds.Add(key, box);
        return new BigInteger(box.Value);
    }

    // Outstanding file writers opened for writing or appending and not yet
    // closed. Successful execution publishes them (flush + close) instead of
    // silently discarding accepted writes; the registry lives on the shared
    // run state so handles opened under any child scope are covered. Handles
    // unregister on explicit close. Ordinary host failures keep registration for
    // end-of-run publication; terminal admission refusal only releases storage.
    private readonly List<IExecutionFileWriter> _openFileWriters = new();

    // Active provider resources only: no guest-reference inventory or tracing.
    private LythonRuntime.PyRegexFindIterator? _regexIterators;

    // Hash state is execution-owned, including clones. It remains usable during
    // awaited writer cleanup and diagnostic projection; the outer run finally
    // disposes it before relinquishing execution ownership.
    private List<IncrementalHash>? _hashResources;

    internal void TrackHash(IncrementalHash hash) => (_hashResources ??= new()).Add(hash);

    internal void CloseHashes()
    {
        if (_hashResources is not { } hashes) return;
        foreach (var hash in hashes) hash.Dispose();
        _hashResources = null;
    }

    internal void TrackRegexIterator(LythonRuntime.PyRegexFindIterator iterator)
    {
        iterator.NextResource = _regexIterators;
        _regexIterators = iterator;
    }

    internal void UntrackRegexIterator(LythonRuntime.PyRegexFindIterator iterator)
    {
        LythonRuntime.PyRegexFindIterator? previous = null;
        for (var current = _regexIterators; current is not null; current = current.NextResource)
        {
            if (ReferenceEquals(current, iterator))
            {
                if (previous is null) _regexIterators = current.NextResource;
                else previous.NextResource = current.NextResource;
                current.NextResource = null;
                return;
            }
            previous = current;
        }
    }

    internal void CloseRegexIterators()
    {
        while (_regexIterators is { } iterator) iterator.Dispose();
    }

    internal void TrackOpenFileWriter(IExecutionFileWriter handle)
    {
        _openFileWriters.Add(handle);
    }

    internal void UntrackOpenFileWriter(IExecutionFileWriter handle)
    {
        _openFileWriters.Remove(handle);
    }

    internal bool CancellationRequested => Limits.CancellationToken.IsCancellationRequested;

    // End-of-run publication for outstanding writers, newest first: behaves
    // exactly like an implicit close() on each handle, so a publication failure
    // surfaces as a failure rather than silent success. Flushes consume
    // cancellation and memory admission like explicit closes.
    internal void CloseOpenFileWriters(LythonRuntime.ExecutionContext context)
    {
        CloseRegexIterators();
        while (_openFileWriters.Count > 0)
        {
            var handle = _openFileWriters[^1];
            handle.Exit(context);
        }
    }

    internal async ValueTask CloseOpenFileWritersAsync(LythonRuntime.ExecutionContext context)
    {
        CloseRegexIterators();
        while (_openFileWriters.Count > 0)
        {
            var handle = _openFileWriters[^1];
            await handle.ExitAsync(context).ConfigureAwait(false);
        }
    }

    internal void TryCloseOpenFileWriters(LythonRuntime.ExecutionContext context)
    {
        CloseRegexIterators();
        while (_openFileWriters.Count > 0)
        {
            var handle = _openFileWriters[^1];
            _openFileWriters.RemoveAt(_openFileWriters.Count - 1);
            try
            {
                handle.Exit(context);
            }
            catch (Exception)
            {
            }
        }
    }

    internal async ValueTask TryCloseOpenFileWritersAsync(LythonRuntime.ExecutionContext context)
    {
        CloseRegexIterators();
        while (_openFileWriters.Count > 0)
        {
            var handle = _openFileWriters[^1];
            _openFileWriters.RemoveAt(_openFileWriters.Count - 1);
            try
            {
                await handle.ExitAsync(context).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }
    }

    public bool TryReadRuntimeMemberCache(
        object cacheSite,
        object target,
        [MaybeNullWhen(false)] out object value)
    {
        if (_runtimeMemberCaches is not null &&
            _runtimeMemberCaches.TryGetValue(cacheSite, out var entry) &&
            ReferenceEquals(entry.Target, target))
        {
            value = entry.Value;
            return true;
        }

        value = null;
        return false;
    }

    public void WriteRuntimeMemberCache(object cacheSite, object target, object value)
    {
        _runtimeMemberCaches ??= new Dictionary<object, RuntimeMemberCacheEntry>(ReferenceEqualityComparer.Instance);
        _runtimeMemberCaches[cacheSite] = new RuntimeMemberCacheEntry(target, value);
    }

    private sealed record RuntimeMemberCacheEntry(object Target, object Value);
}
