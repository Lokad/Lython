namespace Lokad.Lython.Runtime;

internal sealed class ExecutionState
{
    public static readonly HashSet<string> BuiltinNames =
    [
        "object", "type", "read_text", "write_text", "append_text", "open", "print", "input", "str", "repr",
        "len", "sorted", "any", "all", "min", "max", "sum",
        "range", "enumerate", "zip", "next", "exists", "listdir", "mkdir", "remove", "copy", "move", "cwd", "join_path",
        "dirname", "basename", "stat", "Exception", "TypeError", "ValueError", "KeyError", "IndexError", "RuntimeError",
        "AssertionError", "ImportError", "NameError", "AttributeError", "FileNotFoundError", "OSError", "StopIteration",
        "ZeroDivisionError", "NotImplementedError", "OverflowError", "SystemExit", "bool", "int", "float", "bytes",
        "staticmethod", "classmethod", "property", "super", "isinstance", "issubclass", "list", "tuple", "dict", "set"
    ];

    public ExecutionState(ILythonHost host, LythonRunOptions? options)
    {
        Host = host;
        Limits = LythonRuntime.ExecutionLimits.FromOptions(options);
        MemoryGovernor = new MemoryGovernor(Limits.MaxExecutionMemoryBytes);
        LegacyApproximateMemoryDiagnostics = new LegacyApproximateMemoryDiagnostics(Limits.MaxExecutionMemoryBytes);
        RandomState = new PyRandomState();
        DisableLocalModuleImports = options?.DisableLocalModuleImports ?? false;
        AllowedLocalModules = options?.AllowedLocalModules;
        Args = (options?.Args ?? Array.Empty<string>())
            .Select(Text.PyString.FromString)
            .ToArray();
        ImportedModules = new Dictionary<string, PyModule>(StringComparer.Ordinal);
        LoadingModules = new HashSet<string>(StringComparer.Ordinal);
        StandardOutput = new Text.Utf8ValueBuilder(
            MemoryGovernor,
            maxLengthBytes: Limits.MaxStandardOutputBytes,
            maxLengthOwner: "standard output");
        StandardError = new Text.Utf8ValueBuilder(
            MemoryGovernor,
            maxLengthBytes: Limits.MaxStandardErrorBytes,
            maxLengthOwner: "standard error");
        Stdin = new HostTextInputHandle(host.StandardInput, this);
        Stdout = new HostTextOutputHandle(host.StandardOutput, StandardOutput, "<stdout>", this);
        Stderr = new HostTextOutputHandle(host.StandardError, StandardError, "<stderr>", this);
    }

    public ILythonHost Host { get; }

    public LythonRuntime.ExecutionLimits Limits { get; }

    public MemoryGovernor MemoryGovernor { get; }

    public LegacyApproximateMemoryDiagnostics LegacyApproximateMemoryDiagnostics { get; }

    public PyRandomState RandomState { get; }

    public bool DisableLocalModuleImports { get; }

    public IReadOnlySet<string>? AllowedLocalModules { get; }

    public IReadOnlyList<Text.PyString> Args { get; }

    public Dictionary<string, PyModule> ImportedModules { get; }

    public HashSet<string> LoadingModules { get; }

    public Text.Utf8ValueBuilder StandardOutput { get; }

    public Text.Utf8ValueBuilder StandardError { get; }

    public HostTextInputHandle Stdin { get; }

    public HostTextOutputHandle Stdout { get; }

    public HostTextOutputHandle Stderr { get; }
}
