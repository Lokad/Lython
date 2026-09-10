namespace Lokad.Lython.Runtime;

internal sealed class ExecutionState
{
    private Dictionary<object, RuntimeMemberCacheEntry>? _runtimeMemberCaches;

    public static readonly HashSet<string> BuiltinNames =
    [
        "object", "type", "open", "print", "input", "str", "repr", "ascii", "format",
        "len", "sorted", "any", "all", "min", "max", "sum", "abs", "pow", "round", "divmod",
        "bin", "oct", "hex", "chr", "ord", "callable", "hash",
        "range", "enumerate", "zip", "iter", "next", "reversed", "map", "filter", "slice",
        "BaseException", "Exception", "ArithmeticError", "LookupError", "UnicodeError", "Warning",
        "TypeError", "ValueError", "KeyError", "IndexError", "RuntimeError",
        "AssertionError", "ImportError", "ModuleNotFoundError", "NameError", "AttributeError", "SyntaxError",
        "FileNotFoundError", "FileExistsError", "IsADirectoryError", "NotADirectoryError", "PermissionError",
        "TimeoutError", "IOError", "EnvironmentError", "OSError", "StopIteration",
        "ZeroDivisionError", "NotImplementedError", "RecursionError", "MemoryError",
        "UnicodeEncodeError", "UnicodeDecodeError", "UnicodeTranslateError", "OverflowError", "SystemExit",
        "GeneratorExit", "KeyboardInterrupt",
        "bool", "int", "float", "bytes",
        "staticmethod", "classmethod", "property", "super", "isinstance", "issubclass",
        "getattr", "hasattr", "setattr", "delattr", "dir", "vars",
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
