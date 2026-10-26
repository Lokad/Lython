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
                "import_module" => BuiltinCallable.Create(
                    LythonKnownCallableSignatures.ImportlibImportModule,
                    ImportModule,
                    ImportModuleAsync),
                "invalidate_caches" => BuiltinCallable.Create(
                    LythonKnownCallableSignatures.ImportlibInvalidateCaches,
                    InvalidateCaches),
                "util" => ImportlibUtilModule.Instance,
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static object ImportModule(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var moduleName = ResolveImportlibName(arguments, "importlib.import_module", span, context);
            return ResolveImportedModuleHierarchy(moduleName, context, span);
        }

        private static async ValueTask<object> ImportModuleAsync(
            object[] arguments,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var moduleName = ResolveImportlibName(arguments, "importlib.import_module", span, context);
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
                "find_spec" => BuiltinCallable.Create(
                    LythonKnownCallableSignatures.ImportlibUtilFindSpec,
                    FindSpec,
                    FindSpecAsync),
                "resolve_name" => BuiltinCallable.Create(
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
            var moduleName = ResolveImportlibName(arguments, "importlib.util.find_spec", span, context);
            return TryCreateContainedModuleSpec(moduleName, context, span, out var spec)
                ? spec
                : PyNone.Instance;
        }

        private static async ValueTask<object> FindSpecAsync(
            object[] arguments,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var moduleName = ResolveImportlibName(arguments, "importlib.util.find_spec", span, context);
            var spec = await TryCreateContainedModuleSpecAsync(moduleName, context, span).ConfigureAwait(false);
            return spec is null ? PyNone.Instance : spec;
        }

        private static object ResolveName(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 2 || arguments[0] is not PyString name)
                throw RuntimeErrors.Type("resolve_name(name, package) expects a string name and package", span);
            return ResolveRelativeImportName(name, arguments[1], context, span);
        }
    }

    private static BuiltinCallable UnsupportedImportlibCallable(
        LythonCallableSignature signature,
        string message)
        => BuiltinCallable.Create(signature, (_, span, _) => throw new LythonRuntimeException("NotImplementedError", message, span));

    private static string ResolveImportlibName(object[] arguments, string owner, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 0 || arguments[0] is not PyString name)
            throw RuntimeErrors.Type(owner + " expects a string name", span);
        if (name.Utf8Bytes.IsEmpty) throw new LythonRuntimeException("ValueError", "Empty module name", span);
        var package = arguments.Length > 1 ? arguments[1] : PyNone.Instance;
        if (name.Utf8Bytes.Span[0] == '.' && (package is not PyString text || text.Utf8Bytes.IsEmpty))
            throw RuntimeErrors.Type("the 'package' argument is required for a relative import", span);
        var absolute = ResolveRelativeImportName(name, package, context, span);
        // Native registry/host keys can outlive this call. Keep their decoded
        // storage conservatively funded by the execution before allocating it.
        var bytes = checked(64L + 2L * absolute.GetUtf16CodeUnitCount());
        using var storage = context.MemoryGovernor.ReserveTemporary(bytes, span);
        var result = absolute.AsString();
        storage.Commit(bytes);
        return result;
    }

    private static PyString ResolveRelativeImportName(PyString name, object packageValue, ExecutionContext context, LythonSourceSpan span)
    {
        var bytes = name.Utf8Bytes.Span;
        if (bytes.IsEmpty || bytes[0] != '.') return name;
        if (packageValue is not PyString package || package.Utf8Bytes.IsEmpty)
            throw new LythonRuntimeException("ImportError", "no package specified for relative module name", span);
        var level = 0;
        while (level < bytes.Length && bytes[level] == '.')
        {
            if ((level & 4095) == 0) context.CheckExecution(span);
            level++;
        }
        var prefix = package.Utf8Bytes.Span;
        var remaining = level - 1;
        for (var i = prefix.Length - 1; remaining > 0 && i >= 0; i--)
        {
            if ((i & 4095) == 0) context.CheckExecution(span);
            if (prefix[i] == '.') { prefix = prefix[..i]; remaining--; }
        }
        if (remaining != 0) throw new LythonRuntimeException("ImportError", "attempted relative import beyond top-level package", span);
        if (prefix.Length == package.Utf8Bytes.Length && level == bytes.Length) return package;
        var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
        try
        {
            builder.Append(prefix);
            if (level < bytes.Length) { builder.Append((byte)'.'); builder.Append(bytes[level..]); }
            return builder.ToPyStringAndRelease();
        }
        finally { builder.Release(); }
    }

    private static bool TryCreateContainedModuleSpec(
        string moduleName,
        ExecutionContext context,
        LythonSourceSpan span,
        [MaybeNullWhen(false)] out object spec)
    {
        if (IsDeniedBuiltinModule(moduleName, context)) { spec = null; return false; }
        if (context.State.ImportedModules.TryGetStringValue(moduleName, context.MemoryGovernor, span, context, out var loaded) &&
            loaded is ScriptPyModule scriptModule)
        {
            spec = RequireLoadedModuleSpec(moduleName, scriptModule, span);
            return true;
        }
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

    private static async ValueTask<object?> TryCreateContainedModuleSpecAsync(
        string moduleName,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        if (IsDeniedBuiltinModule(moduleName, context)) return null;
        var loaded = await context.State.ImportedModules.TryGetStringValueAsync(moduleName, context, span).ConfigureAwait(false);
        if (loaded.Found && loaded.Value is ScriptPyModule scriptModule)
            return RequireLoadedModuleSpec(moduleName, scriptModule, span);
        if (IsDiscoverableBuiltinModuleName(moduleName, context))
        {
            return CreateBuiltinModuleSpec(moduleName, context, span);
        }

        var local = await TryResolveLocalImportPathAsync(moduleName, context, span).ConfigureAwait(false);
        return local is { } candidate
            ? CreateLocalModuleSpec(moduleName, candidate.Path, candidate.IsPackage, context, span)
            : null;
    }

    private static object RequireLoadedModuleSpec(string moduleName, ScriptPyModule module, LythonSourceSpan span)
    {
        if (!module.TryGetMember("__spec__", out var spec) || spec is PyNone)
            throw new LythonRuntimeException("ValueError", $"{moduleName}.__spec__ is not set", span);
        return spec;
    }

    private static ImportlibModuleSpecObject CreateBuiltinModuleSpec(
        string moduleName,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        var isPackage = EnumerateDiscoverableBuiltinModuleNames(context)
            .Any(candidate => candidate.StartsWith(moduleName + ".", StringComparison.Ordinal));
        PkgutilModule.ChargePkgutilValue(context.MemoryGovernor, span);
        var loader = new PkgutilLoaderObject(PyString.FromString(moduleName, context.MemoryGovernor, span), isPackage, sourcePath: null);
        object locations = isPackage
            ? new PyList([], context.MemoryGovernor, span)
            : PyNone.Instance;
        return new ImportlibModuleSpecObject(moduleName, loader, BuiltInOrigin, isPackage, locations, context.MemoryGovernor, span);
    }

    private static ImportlibModuleSpecObject CreateLocalModuleSpec(
        string moduleName,
        string path,
        bool isPackage,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        PkgutilModule.ChargePkgutilValue(context.MemoryGovernor, span);
        var loader = new PkgutilLoaderObject(PyString.FromString(moduleName, context.MemoryGovernor, span), isPackage, PyString.FromString(path, context.MemoryGovernor, span));
        object locations = isPackage
            ? new PyList([PyString.FromString(PathOps.Parent(path), context.MemoryGovernor, span)], context.MemoryGovernor, span)
            : PyNone.Instance;
        return new ImportlibModuleSpecObject(moduleName, loader, PyString.FromString(path, context.MemoryGovernor, span), isPackage, locations, context.MemoryGovernor, span);
    }

    private static readonly PyString BuiltInOrigin = PyString.FromString("built-in");

    internal sealed class ImportlibModuleSpecObject : IPyMutableDynamicAttributes, IPyRenderableValue
    {
        // Specs retain a fixed 11-member table beside governed payloads; charge the
        // shell plus one attribute slot per member at construction. Reassignment
        // overwrites fixed slots, so nothing is released.
        internal const long SpecValueBytes = 64L + 11L * 64L;

        private readonly Dictionary<string, object> _members;

        public ImportlibModuleSpecObject(string name, object loader, object origin, bool isPackage, object locations, MemoryGovernor governor, LythonSourceSpan? span)
        {
            governor.Reserve(SpecValueBytes, span);
            governor.Commit(SpecValueBytes);
            var parent = isPackage
                ? name
                : name.Contains('.', StringComparison.Ordinal)
                    ? name[..name.LastIndexOf('.')]
                    : string.Empty;
            _members = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["name"] = PyString.FromString(name, governor, span),
                ["loader"] = loader,
                ["origin"] = origin,
                ["loader_state"] = PyNone.Instance,
                ["submodule_search_locations"] = locations,
                ["parent"] = PyString.FromString(parent, governor, span),
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
            var builder = new GovernedByteBuilder(context.Context.MemoryGovernor);
            try
            {
                builder.AppendAscii("ModuleSpec(name=");
                builder.Append(PyRendering.ToReprPyString(_members["name"], context));
                builder.AppendAscii(", loader=");
                builder.Append(PyRendering.ToReprPyString(_members["loader"], context));
                if (_members["origin"] is not PyNone)
                {
                    builder.AppendAscii(", origin=");
                    builder.Append(PyRendering.ToReprPyString(_members["origin"], context));
                }
                if (_members["submodule_search_locations"] is not PyNone)
                {
                    builder.AppendAscii(", submodule_search_locations=");
                    builder.Append(PyRendering.ToReprPyString(_members["submodule_search_locations"], context));
                }
                builder.Append((byte)')');
                return builder.ToPyStringAndRelease();
            }
            finally { builder.Release(); }
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }
}
