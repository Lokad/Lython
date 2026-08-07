using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class ImportlibModule : PyModule
    {
        public static readonly ImportlibModule Instance = new();

        private static readonly string[] Members = ["import_module", "invalidate_caches", "util"];

        private ImportlibModule() : base("importlib")
        {
        }

        public override IReadOnlyList<string> ExportedNames => Members;

        public override IReadOnlyList<string> MemberNames => Members;

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "import_module" => new BuiltinCallable(
                    LythonKnownCallableSignatures.ImportlibImportModule,
                    ImportModule,
                    ImportModuleAsync),
                "invalidate_caches" => new BuiltinCallable(
                    LythonKnownCallableSignatures.ImportlibInvalidateCaches,
                    InvalidateCaches),
                "util" => ImportlibUtilModule.Instance,
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static object ImportModule(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var moduleName = ResolveImportlibName(arguments, "importlib.import_module", span);
            return ResolveImportedModuleHierarchy(moduleName, context, span);
        }

        private static async ValueTask<object> ImportModuleAsync(
            object[] arguments,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var moduleName = ResolveImportlibName(arguments, "importlib.import_module", span);
            return await ResolveImportedModuleHierarchyAsync(moduleName, context, span).ConfigureAwait(false);
        }

        private static object InvalidateCaches(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = arguments;
            _ = span;
            _ = context;
            return PyNone.Instance;
        }
    }

    private sealed class ImportlibUtilModule : PyModule
    {
        public static readonly ImportlibUtilModule Instance = new();

        private static readonly string[] Members =
        [
            "find_spec",
            "resolve_name",
            "module_from_spec",
            "spec_from_file_location",
            "spec_from_loader",
        ];

        private ImportlibUtilModule() : base("importlib.util")
        {
        }

        public override IReadOnlyList<string> ExportedNames => Members;

        public override IReadOnlyList<string> MemberNames => Members;

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "find_spec" => new BuiltinCallable(
                    LythonKnownCallableSignatures.ImportlibUtilFindSpec,
                    FindSpec,
                    FindSpecAsync),
                "resolve_name" => new BuiltinCallable(
                    LythonKnownCallableSignatures.ImportlibUtilResolveName,
                    ResolveName),
                "module_from_spec" => UnsupportedImportlibCallable(
                    LythonKnownCallableSignatures.ImportlibUtilModuleFromSpec,
                    "importlib.util.module_from_spec() is unsupported because Lython does not expose ambient loader execution."),
                "spec_from_file_location" => UnsupportedImportlibCallable(
                    LythonKnownCallableSignatures.ImportlibUtilSpecFromFileLocation,
                    "importlib.util.spec_from_file_location() is unsupported because arbitrary file-backed module loading is outside the contained import model."),
                "spec_from_loader" => UnsupportedImportlibCallable(
                    LythonKnownCallableSignatures.ImportlibUtilSpecFromLoader,
                    "importlib.util.spec_from_loader() is unsupported because Lython module specs are created only by contained discovery."),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static object FindSpec(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var moduleName = ResolveImportlibName(arguments, "importlib.util.find_spec", span);
            return TryCreateContainedModuleSpec(moduleName, context, span, out var spec)
                ? spec
                : PyNone.Instance;
        }

        private static async ValueTask<object> FindSpecAsync(
            object[] arguments,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var moduleName = ResolveImportlibName(arguments, "importlib.util.find_spec", span);
            var spec = await TryCreateContainedModuleSpecAsync(moduleName, context, span).ConfigureAwait(false);
            return spec is null ? PyNone.Instance : spec;
        }

        private static object ResolveName(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            return PyString.FromString(ResolveImportlibName(arguments, "importlib.util.resolve_name", span));
        }
    }

    private static BuiltinCallable UnsupportedImportlibCallable(
        LythonCallableSignature signature,
        string message)
        => new(signature, (_, span, _) => throw new LythonRuntimeException("NotImplementedError", message, span));

    private static string ResolveImportlibName(object[] arguments, string owner, LythonSourceSpan span)
    {
        if (arguments.Length == 0 || !PyStringOps.TryAsString(arguments[0], out var nameValue))
        {
            throw new LythonRuntimeException("TypeError", $"{owner}(name[, package]) expects name to be a string.", span);
        }

        var name = nameValue.AsString();
        if (name.Length == 0)
        {
            throw new LythonRuntimeException("ValueError", "Empty module name", span);
        }

        if (name[0] != '.')
        {
            return name;
        }

        if (arguments.Length < 2 ||
            arguments[1] is null or PyNone ||
            !PyStringOps.TryAsString(arguments[1], out var packageValue) ||
            packageValue.Length == 0)
        {
            throw new LythonRuntimeException(
                "TypeError",
                $"the 'package' argument is required to perform a relative import for '{name}'",
                span);
        }

        var package = packageValue.AsString();
        var level = 0;
        while (level < name.Length && name[level] == '.')
        {
            level++;
        }

        var packageParts = package.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (packageParts.Length < level)
        {
            throw new LythonRuntimeException("ImportError", "attempted relative import beyond top-level package", span);
        }

        var prefix = string.Join('.', packageParts.Take(packageParts.Length - level + 1));
        var suffix = name[level..];
        return suffix.Length == 0 ? prefix : prefix + "." + suffix;
    }

    private static bool TryCreateContainedModuleSpec(
        string moduleName,
        ExecutionContext context,
        LythonSourceSpan span,
        [MaybeNullWhen(false)] out ImportlibModuleSpecObject spec)
    {
        if (IsDiscoverableBuiltinModuleName(moduleName, context))
        {
            spec = CreateBuiltinModuleSpec(moduleName, context, span);
            return true;
        }

        if (TryResolveLocalImportPath(moduleName, context, span, out var path, out var isPackage))
        {
            spec = CreateLocalModuleSpec(moduleName, path, isPackage, context, span);
            return true;
        }

        spec = null;
        return false;
    }

    private static async ValueTask<ImportlibModuleSpecObject?> TryCreateContainedModuleSpecAsync(
        string moduleName,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        if (IsDiscoverableBuiltinModuleName(moduleName, context))
        {
            return CreateBuiltinModuleSpec(moduleName, context, span);
        }

        var local = await TryResolveLocalImportPathAsync(moduleName, context, span).ConfigureAwait(false);
        return local is { } candidate
            ? CreateLocalModuleSpec(moduleName, candidate.Path, candidate.IsPackage, context, span)
            : null;
    }

    private static ImportlibModuleSpecObject CreateBuiltinModuleSpec(
        string moduleName,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        var isPackage = EnumerateDiscoverableBuiltinModuleNames(context)
            .Any(candidate => candidate.StartsWith(moduleName + ".", StringComparison.Ordinal));
        var loader = new PkgutilLoaderObject(PyString.FromString(moduleName), isPackage, sourcePath: null);
        object locations = isPackage
            ? new PyList([], context.MemoryGovernor, span)
            : PyNone.Instance;
        return new ImportlibModuleSpecObject(moduleName, loader, PyString.FromString("built-in"), isPackage, locations);
    }

    private static ImportlibModuleSpecObject CreateLocalModuleSpec(
        string moduleName,
        string path,
        bool isPackage,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        var loader = new PkgutilLoaderObject(PyString.FromString(moduleName), isPackage, path);
        object locations = isPackage
            ? new PyList([PyString.FromString(PathOps.Parent(path))], context.MemoryGovernor, span)
            : PyNone.Instance;
        return new ImportlibModuleSpecObject(moduleName, loader, PyString.FromString(path), isPackage, locations);
    }

    internal sealed class ImportlibModuleSpecObject : IPyDynamicAttributes, IPyRenderableValue
    {
        private readonly Dictionary<string, object> _members;

        public ImportlibModuleSpecObject(string name, object loader, object origin, bool isPackage, object locations)
        {
            var parent = isPackage
                ? name
                : name.Contains('.', StringComparison.Ordinal)
                    ? name[..name.LastIndexOf('.')]
                    : string.Empty;
            _members = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["name"] = PyString.FromString(name),
                ["loader"] = loader,
                ["origin"] = origin,
                ["loader_state"] = PyNone.Instance,
                ["submodule_search_locations"] = locations,
                ["parent"] = PyString.FromString(parent),
                ["has_location"] = origin is PyString originText && originText.AsString() != "built-in",
                ["cached"] = PyNone.Instance,
                ["_cached"] = PyNone.Instance,
                ["_initializing"] = false,
                ["_set_fileattr"] = origin is PyString sourceOrigin && sourceOrigin.AsString() != "built-in",
            };
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value) => _members.TryGetValue(name, out value);

        public bool TrySetMember(string name, object value)
        {
            if (!_members.ContainsKey(name))
            {
                return false;
            }

            _members[name] = value;
            return true;
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            var name = PyRendering.ToReprPyString(_members["name"], context).AsString();
            var loader = PyRendering.ToReprPyString(_members["loader"], context).AsString();
            var origin = _members["origin"] is PyNone
                ? string.Empty
                : $", origin={PyRendering.ToReprPyString(_members["origin"], context).AsString()}";
            return PyString.FromString($"ModuleSpec(name={name}, loader={loader}{origin})");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }
}
