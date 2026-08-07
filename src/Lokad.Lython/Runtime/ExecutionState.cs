namespace Lokad.Lython.Runtime;

internal sealed class ExecutionState
{
    private Dictionary<string, object>? _builtinVariables;
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
        "bool", "int", "float", "bytes",
        "staticmethod", "classmethod", "property", "super", "isinstance", "issubclass",
        "getattr", "hasattr", "setattr", "delattr", "dir", "vars",
        "list", "tuple", "dict", "set"
    ];

    public ExecutionState(ILythonHost host, LythonRunOptions? options)
    {
        Host = host;
        Limits = LythonRuntime.ExecutionLimits.FromOptions(options);
        MemoryGovernor = new MemoryGovernor(Limits.MaxExecutionMemoryBytes);
        RandomState = new PyRandomState();
        DecimalContext = PyDecimalContext.Default();
        DisableLocalModuleImports = options?.DisableLocalModuleImports ?? false;
        AllowedLocalModules = options?.AllowedLocalModules;
        Args = (options?.Args ?? Array.Empty<string>())
            .Select(Text.PyString.FromString)
            .ToArray();
        Environment = options?.Environment is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(options.Environment, StringComparer.Ordinal);
        ImportedModules = new Dictionary<string, PyModule>(StringComparer.Ordinal);
        LoadingModules = new HashSet<string>(StringComparer.Ordinal);
        StandardOutput = new Text.Utf8ValueBuilder(
            MemoryGovernor,
            allocationSpan: null,
            capacity: 0,
            maxLengthBytes: Limits.MaxStandardOutputBytes,
            maxLengthOwner: "standard output");
        StandardError = new Text.Utf8ValueBuilder(
            MemoryGovernor,
            allocationSpan: null,
            capacity: 0,
            maxLengthBytes: Limits.MaxStandardErrorBytes,
            maxLengthOwner: "standard error");
        Stdin = new HostTextInputHandle(host.StandardInput, this);
        Stdout = new HostTextOutputHandle(host.StandardOutput, StandardOutput, "<stdout>", this);
        Stderr = new HostTextOutputHandle(host.StandardError, StandardError, "<stderr>", this);
    }

    public ILythonHost Host { get; }

    public LythonRuntime.ExecutionLimits Limits { get; }

    public MemoryGovernor MemoryGovernor { get; }

    public PyRandomState RandomState { get; }

    public PyDecimalContext DecimalContext { get; set; }

    public bool DisableLocalModuleImports { get; }

    public IReadOnlySet<string>? AllowedLocalModules { get; }

    public IReadOnlyList<Text.PyString> Args { get; }

    public Dictionary<string, string> Environment { get; }

    public Dictionary<string, PyModule> ImportedModules { get; }

    public Dictionary<string, object> BuiltinVariables
        => _builtinVariables ?? throw new InvalidOperationException("Builtin variables are not initialized.");

    public HashSet<string> LoadingModules { get; }

    public Text.Utf8ValueBuilder StandardOutput { get; }

    public Text.Utf8ValueBuilder StandardError { get; }

    public HostTextInputHandle Stdin { get; }

    public HostTextOutputHandle Stdout { get; }

    public HostTextOutputHandle Stderr { get; }

    public void InitializeBuiltinVariables(Dictionary<string, object> builtinVariables)
    {
        if (_builtinVariables is not null)
        {
            throw new InvalidOperationException("Builtin variables are already initialized.");
        }

        _builtinVariables = builtinVariables;
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
