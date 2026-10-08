using System.Numerics;
using System.Runtime.CompilerServices;

namespace Lokad.Lython.Runtime;

internal sealed class ExecutionState
{
    private Dictionary<object, RuntimeMemberCacheEntry>? _runtimeMemberCaches;
    private Dictionary<PythonExceptionIdentity, PyTuple>? _exceptionBaseTuples;
    private readonly List<PoolRegistration> _poolRegistrations = new();
    internal PyType? TypingGeneric { get; set; }
    internal PyDict? UrlSplitFieldDefaults { get; set; }
    internal PyDict? UrlParseFieldDefaults { get; set; }
    internal PyTuple? GzipFileMetaBases { get; set; }
    internal PyTuple? GzipFileMetaMro { get; set; }
    internal LythonRuntime.UrllibParseModule.UrlSplitCache? UrlSplitCache { get; set; }
    private long _csvPulls;
    private long _boundCalls;
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
        "AssertionError", "ImportError", "ModuleNotFoundError", "NameError", "AttributeError", "SyntaxError",
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
        Dictionary<string, object> builtinVariables)
    {
        Host = host;
        Limits = LythonRuntime.ExecutionLimits.FromOptions(options);
        BudgetGuards = new ExecutionBudgetGuards(this);
        MemoryGovernor = new MemoryGovernor(Limits.MaxExecutionMemoryBytes);
        CallTemporaries = new ChargeReclamationPool(MemoryGovernor);
        MemoryGovernor.LivePoolProvider = LiveReclamationPools;
        RandomState = new PyRandomState();
        DecimalContext = PyDecimalContext.Default();
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
        ImportedModules = new Dictionary<string, PyModule>(StringComparer.Ordinal);
        LoadingModules = new HashSet<string>(StringComparer.Ordinal);
        StandardOutput = new Text.GovernedByteBuilder(
            MemoryGovernor,
            allocationSpan: null,
            capacity: 0,
            maxLengthBytes: Limits.MaxStandardOutputBytes,
            maxLengthOwner: "standard output");
        StandardError = new Text.GovernedByteBuilder(
            MemoryGovernor,
            allocationSpan: null,
            capacity: 0,
            maxLengthBytes: Limits.MaxStandardErrorBytes,
            maxLengthOwner: "standard error");
        Stdin = new HostTextInputHandle(host.StandardInput, this);
        Stdout = new HostTextOutputHandle(host.StandardOutput, StandardOutput, "<stdout>", this);
        Stderr = new HostTextOutputHandle(host.StandardError, StandardError, "<stderr>", this);
        BuiltinVariables = builtinVariables;
    }

    public ILythonHost Host { get; }

    public LythonRuntime.ExecutionLimits Limits { get; }

    public ExecutionBudgetGuards BudgetGuards { get; }

    public MemoryGovernor MemoryGovernor { get; }

    // Per-call variadic materializations (overflow lists and tuples, keyword
    // dicts and key strings) register here instead of leaking durable commits
    // when dropped; sweeps release whatever the collector reclaimed while
    // retained aliases stay charged.
    internal ChargeReclamationPool CallTemporaries { get; }

    // N19: bounded per-execution regex compilation cache. Lazy so regex-free runs
    // pay nothing; shared by every context of the run, never across runs.
    internal RegexPatternCache? RegexCache { get; set; }

    // N01: execution-local structural traversal state (cycle pairs, depth,
    // cooperative work ticks). One per run, carried explicitly through async
    // recursion via ExecutionContext so continuations hopping pool threads
    // dispose the same state they entered. Never shared across runs.
    internal StructuralGuardState StructuralTraversal { get; } = new StructuralGuardState();

    public PyRandomState RandomState { get; }

    public PyDecimalContext DecimalContext { get; set; }

    public bool DisableLocalModuleImports { get; }

    public IReadOnlySet<string>? AllowedLocalModules { get; }

    public IReadOnlyList<Text.PyString> Args { get; }

    public Dictionary<string, string> Environment { get; }

    // os.environ reads share one mapping per run over the live table above,
    // so identity holds like CPython while contents stay current.
    public LythonRuntime.PyEnvironmentMapping? OsEnvironMapping { get; set; }

    public Dictionary<string, PyModule> ImportedModules { get; }

    public Dictionary<string, object> BuiltinVariables { get; }

    public HashSet<string> LoadingModules { get; }

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
    // id() exposes stable per-run object identity: the same live object
    // keeps its number while distinct live objects get distinct numbers.
    // Numbers are opaque like CPython, but only shared boxes (such as
    // repeated literals) share numbers; separately computed integers box
    // fresh, so id(a) == id(b) may be False for equal ints.
    // MG11: each distinct live identity owns one registry entry (table node,
    // weak handle and identity box): reserve before publishing so a denied
    // insertion strands nothing, share the entry across repeated lookups,
    // and track the box in the reclamation pool so dropped identities
    // release. The runtime is single-threaded, so one lookup-then-add sequence
    // cannot race; governor accounting is per-run and never shared across threads.
    private const long IdentityEntryBytes = 64;

    public BigInteger GetObjectId(object? value)
    {
        var key = value ?? PyNone.Instance;
        if (_objectIds.TryGetValue(key, out var existing))
        {
            return new BigInteger(existing.Value);
        }

        // Commit first, publish last: a denied pool entry rolls the identity commit
        // back exactly (no reservation holder can outlive the commit it guards
        // without double-releasing shared reserved bytes), so a second-boundary
        // denial strands neither the commit nor an untracked box.
        MemoryGovernor.Reserve(IdentityEntryBytes, null);
        MemoryGovernor.Commit(IdentityEntryBytes);
        var box = new StrongBox<long>(Interlocked.Increment(ref _nextObjectId));
        try
        {
            CallTemporaries.Track(box, IdentityEntryBytes);
        }
        catch (Exception)
        {
            MemoryGovernor.Release(IdentityEntryBytes);
            throw;
        }

        _objectIds.Add(key, box);
        return new BigInteger(box.Value);
    }

    // Tracks every pool-owning source (CSV readers, text readers) for
    // abandonment reclamation: entries hold the pool and scratch strongly
    // but the owner weakly. A dropped source releases its scratch; its pool
    // stays registered while returned values still need reclamation tracking.
    // The governor also enumerates this registry for exhaustion
    // relief, so registered pools participate without a second registry.
    internal void RegisterCsvSource(
        LythonRuntime.CsvRecordSource source,
        ChargeReclamationPool pool,
        MemoryGovernor.TemporaryMemoryReservation scratch)
    {
        RegisterPool(source, pool, scratch);
    }

    internal void RegisterPool(
        object owner,
        ChargeReclamationPool pool,
        MemoryGovernor.TemporaryMemoryReservation? scratch = null)
    {
        // Registrations retain a weak handle plus the pool/scratch references for
        // the pool lifetime: one entry charge at the shared rate, released with
        // the registration on abandonment below.
        MemoryGovernor.Reserve(ChargeReclamationPool.EntryChargeBytes, null);
        MemoryGovernor.Commit(ChargeReclamationPool.EntryChargeBytes);
        _poolRegistrations.Add(new PoolRegistration(new WeakReference<object>(owner), pool, scratch));
    }

    // Yields every live reclamation pool, reclaiming abandoned registrations
    // on the way: dropped owners release scratch, while returned values keep
    // their tracking pool until collection. Fully enumerating also serves the
    // pull cadence, so there is a single reconciliation path.
    internal IEnumerable<ChargeReclamationPool> LiveReclamationPools()
    {
        for (var i = _poolRegistrations.Count - 1; i >= 0; i--)
        {
            // Promotion funding can reenter this registry and retire other
            // sources, so an index from before the sweep may no longer exist.
            i = Math.Min(i, _poolRegistrations.Count - 1);
            if (i < 0) break;
            var entry = _poolRegistrations[i];
            if (!entry.Owner.TryGetTarget(out _) && !entry.Pool.IsSweeping)
            {
                var firstAbandonment = !entry.Abandoned;
                if (firstAbandonment)
                {
                    // Scratch belongs to the source, so free it before a sweep
                    // might need promotion capacity under a tight budget.
                    entry.Scratch?.Dispose();
                    entry = entry with { Scratch = null, Abandoned = true };
                    _poolRegistrations[i] = entry;
                }

                entry.Pool.Sweep(full: firstAbandonment);
                if (entry.Pool.Count == 0)
                {
                    var removeIndex = i < _poolRegistrations.Count
                        && ReferenceEquals(_poolRegistrations[i].Owner, entry.Owner)
                        ? i : _poolRegistrations.IndexOf(entry);
                    if (removeIndex < 0) continue;
                    entry.Pool.ReleaseTierBacking();
                    MemoryGovernor.Release(ChargeReclamationPool.EntryChargeBytes);
                    _poolRegistrations[removeIndex] = _poolRegistrations[_poolRegistrations.Count - 1];
                    _poolRegistrations.RemoveAt(_poolRegistrations.Count - 1);
                    continue;
                }
            }

            yield return entry.Pool;
        }

        yield return CallTemporaries;
    }

    internal void NoteCsvPull()
    {
        if ((++_csvPulls & 255) == 0)
        {
            foreach (var _ in LiveReclamationPools())
            {
            }
        }
    }

    // Bound calls sweep the same pool as execution steps, and deliberately so:
    // user calls tick both cadences, so either alone keeps peaks flat on the
    // current tree (removing this sweep leaves the kwargs peak byte-identical).
    // An earlier tree doubled kwargs peaks without it, before entry calibration
    // and refund-on-deny changed peak dynamics; keep both cadences since steps
    // also cover call-free loops while bound calls re-sweep right after calls
    // drop their temporaries. Consolidation would save ~microseconds per 256
    // calls while risking call-heavy shapes, so it stays rejected.
    internal void NoteBoundCall()
    {
        if ((++_boundCalls & 255) == 0)
        {
            CallTemporaries.Sweep();
        }
    }

    // Outstanding file writers opened for writing or appending and not yet
    // closed. Successful execution publishes them (flush + close) instead of
    // silently discarding accepted writes; the registry lives on the shared
    // run state so handles opened under any child scope are covered. Handles
    // unregister on explicit close; a failed close keeps its registration so
    // end-of-run publication can retry it.
    private readonly List<IExecutionFileWriter> _openFileWriters = new();

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
    // host-call and memory budgets like explicit closes.
    internal void CloseOpenFileWriters()
    {
        while (_openFileWriters.Count > 0)
        {
            var handle = _openFileWriters[^1];
            handle.Exit();
        }
    }

    internal async ValueTask CloseOpenFileWritersAsync()
    {
        while (_openFileWriters.Count > 0)
        {
            var handle = _openFileWriters[^1];
            await handle.ExitAsync().ConfigureAwait(false);
        }
    }

    // Best-effort variants for the failure path: publication is attempted
    // (CPython publishes buffered writes even when execution fails) but any
    // publication error is swallowed so the original failure is preserved.
    // Handles dequeue before closing so a persistently failing handle cannot
    // stall the sweep. Cancellation skips publication entirely (checked by
    // the run entries): a cancelled run leaves no file behind, and host calls
    // against a canceled token would fail anyway.
    internal void TryCloseOpenFileWriters()
    {
        while (_openFileWriters.Count > 0)
        {
            var handle = _openFileWriters[^1];
            _openFileWriters.RemoveAt(_openFileWriters.Count - 1);
            try
            {
                handle.Exit();
            }
            catch (Exception)
            {
            }
        }
    }

    internal async ValueTask TryCloseOpenFileWritersAsync()
    {
        while (_openFileWriters.Count > 0)
        {
            var handle = _openFileWriters[^1];
            _openFileWriters.RemoveAt(_openFileWriters.Count - 1);
            try
            {
                await handle.ExitAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }
    }

    private readonly record struct PoolRegistration(
        WeakReference<object> Owner,
        ChargeReclamationPool Pool,
        MemoryGovernor.TemporaryMemoryReservation? Scratch,
        bool Abandoned = false);

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
