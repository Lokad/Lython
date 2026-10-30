using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;
using System.Runtime.ExceptionServices;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class FutureModule : PyModule
    {
        public static readonly FutureModule Instance = new();

        private FutureModule() : base("__future__")
        {
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "annotations" => PyNone.Instance,
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    private static void ExecuteImport(ImportStatementSyntax statement, ExecutionContext context)
        => ExecuteImportCore(
                statement,
                context,
                name => ValueTask.FromResult(ResolveImportedModule(name, context, statement.Span)), false)
            .GetAwaiter()
            .GetResult();

    private static object ResolveImportedModule(string moduleName, ExecutionContext context, LythonSourceSpan span)
    {
        CheckBuiltinModuleGrant(moduleName, context, span);
        if (TryResolveKnownImportedModule(moduleName, context, out var known))
        {
            return known;
        }

        if (!TryResolveLocalImportPath(moduleName, context, span, out var path, out var isPackage))
        {
            throw MissingImportedModule(moduleName, context, span);
        }

        var prepared = PrepareImportedModule(moduleName, path, isPackage, ReadGovernedHostText(path, context, span).AsString(), context, span);
        var completed = false;
        try
        {
            object? returned = null;
            LythonRuntimeException? failure = null;
            try
            {
                returned = ExecuteExecutableCodeObject(prepared.Code, prepared.Context, default, null);
            }
            catch (LythonRuntimeException ex)
            {
                ex.SetSourcePathIfMissing(path);
                failure = ex;
            }
            // Like lowered block evaluation, propagate after leaving the catch.
            // Nested imports must not accumulate active native exception handlers.
            if (failure is not null) ExceptionDispatchInfo.Throw(failure);
            if (returned is not null) throw RuntimeErrors.ImportedModuleReturned(moduleName, span);
            context.CheckExecution(span);
            if (!context.State.ImportedModules.TryGetValue(prepared.RegistryKey, out var loaded, context, span))
                throw RuntimeErrors.MissingKey(prepared.RegistryKey, span);
            completed = true;
            return loaded;
        }
        finally
        {
            prepared.Spec.TrySetMember("_initializing", false);
            if (!completed) context.State.ImportedModules.RemoveStoredKey(prepared.RegistryKey);
        }
    }

    private static object ResolveImportedModuleHierarchy(string moduleName, ExecutionContext context, LythonSourceSpan span)
        => ResolveImportedModuleHierarchyCore(
                moduleName,
                span,
                name => ValueTask.FromResult(ResolveImportedModule(name, context, span)))
            .GetAwaiter()
            .GetResult();

    private static async ValueTask<object> ResolveImportedModuleAsync(string moduleName, ExecutionContext context, LythonSourceSpan span)
    {
        CheckBuiltinModuleGrant(moduleName, context, span);
        var cached = await context.State.ImportedModules.TryGetStringValueAsync(moduleName, context, span).ConfigureAwait(false);
        if (cached.Found) { RequireImportedValue(moduleName, cached.Value, span); return cached.Value; }
        if (ResolveBuiltinModule(moduleName, context) is { } builtin)
        {
            ChargeImportedModule(context, span);
            context.State.ImportedModules.SetStringItem(moduleName, builtin, context.MemoryGovernor, span);
            context.CheckExecution(span);
            return builtin;
        }

        var localImport = await TryResolveLocalImportPathAsync(moduleName, context, span).ConfigureAwait(false);
        if (localImport is null)
        {
            throw MissingImportedModule(moduleName, context, span);
        }

        var path = localImport.Value.Path;

        var prepared = PrepareImportedModule(moduleName, path, localImport.Value.IsPackage, (await ReadGovernedHostTextAsync(path, context, span).ConfigureAwait(false)).AsString(), context, span);
        var completed = false;
        try
        {
            object? returned = null;
            LythonRuntimeException? failure = null;
            try
            {
                returned = await ExecuteExecutableCodeObjectAsync(prepared.Code, prepared.Context).ConfigureAwait(false);
            }
            catch (LythonRuntimeException ex)
            {
                ex.SetSourcePathIfMissing(path);
                failure = ex;
            }
            if (failure is not null) ExceptionDispatchInfo.Throw(failure);
            if (returned is not null) throw RuntimeErrors.ImportedModuleReturned(moduleName, span);
            context.CheckExecution(span);
            var loaded = await context.State.ImportedModules.TryGetBuiltinStringAsync(prepared.RegistryKey, context, span).ConfigureAwait(false);
            if (!loaded.Found) throw RuntimeErrors.MissingKey(prepared.RegistryKey, span);
            completed = true;
            return loaded.Value;
        }
        finally
        {
            prepared.Spec.TrySetMember("_initializing", false);
            if (!completed) context.State.ImportedModules.RemoveStoredKey(prepared.RegistryKey);
        }
    }

    private static async ValueTask<object> ResolveImportedModuleHierarchyAsync(
        string moduleName,
        ExecutionContext context,
        LythonSourceSpan span)
        => await ResolveImportedModuleHierarchyCore(
                moduleName,
                span,
                name => ResolveImportedModuleAsync(name, context, span))
            .ConfigureAwait(false);

    private static void AttachImportedChild(object parent, string childName, object child, LythonSourceSpan span)
    {
        if (parent is PyModule parentModule && parentModule.TrySetMember(childName, child))
        {
            return;
        }

        if (parent is PyModule knownParent && knownParent.TryGetMember(childName, out var existing) &&
            existing is PyModule existingModule &&
            child is PyModule childModule && string.Equals(existingModule.Name, childModule.Name, StringComparison.Ordinal))
        {
            return;
        }

        throw new LythonRuntimeException(
            "ImportError",
            $"Cannot attach imported child '{childName}' to its parent package.",
            span);
    }

    private static async ValueTask ExecuteImportAsync(ImportStatementSyntax statement, ExecutionContext context)
        => await ExecuteImportCore(
                statement,
                context,
                name => ResolveImportedModuleAsync(name, context, statement.Span), true)
            .ConfigureAwait(false);

    private static async ValueTask ExecuteImportCore(
        ImportStatementSyntax statement,
        ExecutionContext context,
        Func<string, ValueTask<object>> resolveModule, bool asynchronous)
    {
        if (string.Equals(statement.ModuleName, "__future__", StringComparison.Ordinal) &&
            statement.ImportedMembers is not null && ImportsOnlyFutureAnnotations(statement.ImportedMembers))
        {
            context.PostponedAnnotations = true;
            return;
        }

        var module = await ResolveImportedModuleHierarchyCore(statement.ModuleName, statement.Span, resolveModule).ConfigureAwait(false);

        if (statement.ImportedMembers is null)
        {
            var boundModule = string.Equals(statement.BoundModuleName, statement.ModuleName, StringComparison.Ordinal)
                ? module
                : await resolveModule(statement.BoundModuleName).ConfigureAwait(false);
            StoreName(statement.BindingName, boundModule, context, statement.Span);
            return;
        }

        if (ImportSyntaxFacts.IsStarImport(statement.ImportedMembers))
        {
            if (!asynchronous) ExecuteStarImport(module, context, statement.Span);
            else
                await foreach (var name in StarImportNamesAsync(module, context, statement.Span, true).ConfigureAwait(false))
                {
                    var member = await TryResolveRuntimeMemberAsync(module, name, context, statement.Span).ConfigureAwait(false);
                    if (!member.Found) throw PyMemberAccess.CreateMissingMemberError(module, name, statement.Span, context);
                    StoreName(name, member.Value, context, statement.Span);
                }
            return;
        }

        foreach (var importedMember in statement.ImportedMembers)
        {
            if (!TryResolveImportedMember(module, importedMember.Name, context, statement.Span, out var value))
            {
                throw RuntimeErrors.CannotImportMember(statement.ModuleName, importedMember.Name, statement.Span);
            }

            StoreName(importedMember.BindingName, value, context, statement.Span);
        }
    }

    private static void ExecuteStarImport(object module, ExecutionContext context, LythonSourceSpan span)
    {
        foreach (var name in StarImportNames(module, context, span))
        {
            if (!TryResolveImportedMember(module, name, context, span, out var value))
            {
                throw PyMemberAccess.CreateMissingMemberError(module, name, span, context);
            }

            StoreName(name, value, context, span);
        }
    }

    private static IEnumerable<string> StarImportNames(object module, ExecutionContext context, LythonSourceSpan span)
    {
        var names = StarImportNamesAsync(module, context, span, false).GetAsyncEnumerator();
        try { while (names.MoveNextAsync().GetAwaiter().GetResult()) yield return names.Current; }
        finally { names.DisposeAsync().GetAwaiter().GetResult(); }
    }

    private static async IAsyncEnumerable<string> StarImportNamesAsync(object module, ExecutionContext context,
        LythonSourceSpan span, bool asynchronous)
    {
        var found = asynchronous
            ? await TryResolveRuntimeMemberAsync(module, "__all__", context, span).ConfigureAwait(false)
            : TryResolveRuntimeMember(module, "__all__", context, span, out var all) ? (true, all) : (false, (object)PyNone.Instance);
        if (!found.Item1)
        {
            if (module is PyModule native)
                foreach (var name in native.ExportedNames) { context.CheckExecution(span); yield return name; }
            else if (Vars([module], span, context) is PyDict dictionary)
                foreach (var key in dictionary.Keys)
                {
                    context.CheckExecution(span);
                    var name = RequireStarName(key, span);
                    if (!name.StartsWith('_')) yield return name;
                }
            yield break;
        }
        // IMPORT_STAR indexes __all__; it does not use its __iter__ protocol.
        var exports = found.Item2;
        if (exports is PyDict or PyDefaultDict or PyCounter or PyChainMap)
            throw RuntimeErrors.Type("__all__ must support sequence indexing", span);
        for (var index = System.Numerics.BigInteger.Zero; ; index++)
        {
            context.CheckExecution(span);
            object item = PyNone.Instance;
            var done = false;
            try { item = asynchronous ? await ReadLoweredSubscriptAsync(exports, index, span, context).ConfigureAwait(false)
                    : ReadSubscriptValue(exports, index, span, context); }
            catch (LythonRuntimeException error) when (error.Identity == PythonExceptionIdentity.Builtin("IndexError")) { done = true; }
            if (done) yield break;
            yield return RequireStarName(item, span);
        }
    }

    private static string RequireStarName(object value, LythonSourceSpan span)
        => value is PyString name ? name.AsString() : throw RuntimeErrors.Type("star import names must be strings", span);

    private static bool TryResolveImportedMember(
        object module,
        string name,
        ExecutionContext context,
        LythonSourceSpan span,
        [MaybeNullWhen(false)] out object value)
        => TryResolveRuntimeMember(module, name, context, span, out value);

    private static string ResolveLocalModulePath(string moduleName, ExecutionContext context)
    {
        var fileName = moduleName + ".py";
        var baseDirectory = context.SourcePath is null
            ? context.Host.Cwd
            : PathOps.Parent(context.SourcePath);

        return PathOps.Combine(baseDirectory, fileName);
    }

    private static bool TryResolveLocalImportPath(
        string moduleName,
        ExecutionContext context,
        LythonSourceSpan span,
        out string path,
        out bool isPackage)
    {
        var locations = GetLocalImportLocationsAsync(moduleName, context, span, false).GetAwaiter().GetResult();
        var candidate = TryResolveLocalImportPathCoreAsync(moduleName, context, span, locations, false).GetAwaiter().GetResult();
        path = candidate?.Path ?? string.Empty;
        isPackage = candidate?.IsPackage ?? false;
        return candidate is not null;
    }

    private static async ValueTask<LocalImportCandidate?> TryResolveLocalImportPathAsync(
        string moduleName, ExecutionContext context, LythonSourceSpan span)
    {
        var locations = await GetLocalImportLocationsAsync(moduleName, context, span, true).ConfigureAwait(false);
        return await TryResolveLocalImportPathCoreAsync(moduleName, context, span, locations, true).ConfigureAwait(false);
    }

    private static async ValueTask<LocalImportCandidate?> TryResolveLocalImportPathCoreAsync(
        string moduleName, ExecutionContext context, LythonSourceSpan span, IPySequenceValue? locations, bool asynchronous)
    {
        foreach (var candidate in EnumerateLocalImportCandidates(moduleName, context, span, locations))
        {
            context.CheckExecution(span);
            var allowlistPath = ResolveLocalModuleAllowlistPath(candidate.Path, context);
            if (!IsLocalModuleImportAllowed(moduleName, candidate.Path, allowlistPath, context)) continue;
            if (asynchronous ? await context.HostExistsAsync(candidate.Path, span).ConfigureAwait(false)
                : context.HostExists(candidate.Path, span))
            {
                var bytes = checked(64L + 2L * candidate.Path.Length);
                context.MemoryGovernor.Reserve(bytes, span);
                context.MemoryGovernor.Commit(bytes);
                return candidate;
            }
        }
        return null;
    }

    private static async ValueTask<IPySequenceValue?> GetLocalImportLocationsAsync(
        string moduleName, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        var dot = moduleName.LastIndexOf('.');
        if (dot < 0) return null;
        var name = moduleName[..dot];
        object? parent;
        if (asynchronous)
        {
            var found = await context.State.ImportedModules.TryGetStringValueAsync(name, context, span).ConfigureAwait(false);
            parent = found.Found ? found.Value : null;
        }
        else context.State.ImportedModules.TryGetStringValue(name, context.MemoryGovernor, span, context, out parent);
        if (parent is null) return null;
        var attribute = await TryReadWrapperAttributeAsync(parent, "__path__", context, span, asynchronous).ConfigureAwait(false);
        if (!attribute.Found)
            throw new LythonRuntimeException("ModuleNotFoundError", $"No module named '{moduleName}'; '{name}' is not a package", span);
        var locations = attribute.Value;
        if (locations is not (PyList or PyTuple))
            throw new LythonRuntimeException("NotImplementedError", "Contained package __path__ requires a list or tuple of strings.", span);
        return (IPySequenceValue)locations;
    }

    private static IEnumerable<LocalImportCandidate> EnumerateLocalImportCandidates(
        string moduleName, ExecutionContext context, LythonSourceSpan span, IPySequenceValue? locations)
    {
        if (locations is not null)
        {
            for (var i = 0; i < locations.Count; i++)
            {
                context.CheckExecution(span);
                if (!PyStringOps.TryAsString(locations[i], out var location))
                    throw new LythonRuntimeException("NotImplementedError", "Contained package __path__ requires string entries.", span);
                // Fund path construction through the host probe, including
                // original components and replacement overlap. A selected CLR path
                // gets batch funding before this private reservation ends.
                using var working = context.MemoryGovernor.ReserveTemporary(
                    checked(256L + 16L * (moduleName.Length + (long)location.Utf8Bytes.Length)), span);
                var child = moduleName[(moduleName.LastIndexOf('.') + 1)..];
                var root = location.Utf8Bytes.IsEmpty ? "." : location.AsString();
                yield return new LocalImportCandidate(PathOps.Combine(root, child + "/__init__.py"), IsPackage: true);
                yield return new LocalImportCandidate(PathOps.Combine(root, child + ".py"), IsPackage: false);
            }
            yield break;
        }
        var baseDirectory = context.SourcePath is null
            ? context.Host.Cwd
            : PathOps.Parent(context.SourcePath);
        var packageInitPath = PathOps.Combine(baseDirectory, moduleName.Replace('.', '/') + "/__init__.py");
        yield return new LocalImportCandidate(packageInitPath, IsPackage: true);

        var directModulePath = ResolveLocalModulePath(moduleName, context);
        yield return new LocalImportCandidate(directModulePath, IsPackage: false);
        var packageModulePath = PathOps.Combine(baseDirectory, moduleName.Replace('.', '/') + ".py");
        if (!string.Equals(packageModulePath, directModulePath, StringComparison.Ordinal))
            yield return new LocalImportCandidate(packageModulePath, IsPackage: false);
    }

    private readonly record struct LocalImportCandidate(string Path, bool IsPackage);

    private static string ResolveLocalModuleAllowlistPath(string modulePath, ExecutionContext context)
    {
        var baseDirectory = context.SourcePath is null
            ? context.Host.Cwd
            : PathOps.Parent(context.SourcePath);

        try
        {
            return PathOps.RelativeTo(modulePath, baseDirectory);
        }
        catch (InvalidOperationException)
        {
            return modulePath;
        }
    }

    private static bool IsLocalModuleImportAllowed(string moduleName, string modulePath, string allowlistPath, ExecutionContext context)
    {
        if (IsDeniedBuiltinModule(moduleName, context)) return false;
        if (context.State.DisableLocalModuleImports)
        {
            return false;
        }

        return context.State.AllowedLocalModules is { } allowedModules &&
            (allowedModules.Contains(allowlistPath) ||
             allowedModules.Contains(PathOps.Normalize(allowlistPath)) ||
             allowedModules.Contains(modulePath) ||
             allowedModules.Contains(moduleName));
    }

    internal static PyModule? ResolveBuiltinModule(string moduleName, ExecutionContext context)
        => BuiltinModuleCatalog.Resolve(moduleName, context);

    private static IReadOnlyList<string> EnumerateDiscoverableBuiltinModuleNames(ExecutionContext context)
        => BuiltinModuleCatalog.GetDiscoverableNames(context);

    private static async ValueTask<object> ResolveImportedModuleHierarchyCore(
        string moduleName,
        LythonSourceSpan span,
        Func<string, ValueTask<object>> resolveModule)
    {
        var parts = moduleName.Split('.');
        object? parent = null;
        object? resolved = null;
        for (var i = 0; i < parts.Length; i++)
        {
            var qualifiedName = string.Join('.', parts, 0, i + 1);
            resolved = await resolveModule(qualifiedName).ConfigureAwait(false);
            if (parent is not null)
            {
                AttachImportedChild(parent, parts[i], resolved, span);
            }

            parent = resolved;
        }

        return resolved.RequireNotNull();
    }

    // Each newly registered module retains a registry slot plus handle wrapper,
    // name strings, and exported-table infrastructure for the run. Each exported
    // entry additionally owns its retained dictionary slot; keys and values stay
    // aliased. Local compilation retains a coarse estimate in the existing batch
    // owner, including code that may escape a failed import. Registry hits reuse it.
    private const long ImportedModuleBytes = 512;

    private static void ChargeImportedModule(ExecutionContext context, LythonSourceSpan? span)
    {
        context.MemoryGovernor.Reserve(ImportedModuleBytes, span);
        context.MemoryGovernor.Commit(ImportedModuleBytes);
    }

    private static bool TryResolveKnownImportedModule(string moduleName, ExecutionContext context,
        [MaybeNullWhen(false)] out object module)
    {
        if (context.State.ImportedModules.TryGetStringValue(moduleName, context.MemoryGovernor, null, context, out module))
        {
            RequireImportedValue(moduleName, module, null);
            return true;
        }
        module = ResolveBuiltinModule(moduleName, context);
        if (module is null) return false;
        ChargeImportedModule(context, null);
        context.State.ImportedModules.SetStringItem(moduleName, module, context.MemoryGovernor, null);
        context.CheckExecution(null);
        return true;
    }

    private static void RequireImportedValue(string name, object value, LythonSourceSpan? span)
    {
        if (value is PyNone) throw new LythonRuntimeException("ModuleNotFoundError", $"import of {name} halted; None in sys.modules", span);
    }

    private static PreparedImportedModule PrepareImportedModule(
        string moduleName,
        string path,
        bool isPackage,
        string source,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        var estimate = RuntimeMemoryEstimates.EstimateCompilationBytes(source.Length);
        using var code = context.MemoryGovernor.ReserveTemporary(estimate, span);
        var frontend = LythonFrontend.Compile(source, context.MemoryGovernor, context.State.RegexProvider, context.Limits.CancellationToken);
        if (frontend.Script is null || frontend.Diagnostics.Count != 0)
        {
            var diagnostic = frontend.Diagnostics.FirstOrDefault();
            var message = diagnostic?.Message ?? "unknown syntax error";
            throw RuntimeErrors.CannotImportModule(moduleName, message, diagnostic?.Span ?? span);
        }

        var executable = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script));
        var moduleContext = ExecutionContext.CreateModule(context, path, moduleName);
        // Exports and escaped functions share this module dictionary after its
        // executable slots leave. Do not depend on incidental opcode fallbacks.
        moduleContext.MirrorLocalStores = true;
        InitializeModuleAnnotations(executable.AnnotationInitializationSpan, moduleContext);
        // Execution can expose code through callbacks before an import fails.
        // Retain the coarse estimate for this batch without tracing escapes.
        code.Commit(estimate);
        ChargeImportedModule(context, span);
        var loaded = new ScriptPyModule(moduleName, moduleContext, span);
        var spec = CreateLocalModuleSpec(moduleName, path, isPackage, context, span);
        spec.TryGetMember("parent", context, span, out var package);
        spec.TryGetMember("loader", out var loader);
        loaded.TrySetMember("__package__", package!);
        loaded.TrySetMember("__loader__", loader!);
        loaded.TrySetMember("__spec__", spec);
        if (isPackage)
        {
            spec.TryGetMember("submodule_search_locations", out var locations);
            loaded.TrySetMember("__path__", locations!);
        }
        var key = PyString.FromString(moduleName, context.MemoryGovernor, span);
        spec.TrySetMember("_initializing", true);
        try { context.State.ImportedModules.SetItem(key, loaded); }
        catch
        {
            spec.TrySetMember("_initializing", false);
            context.State.ImportedModules.RemoveStoredKey(key);
            throw;
        }
        return new PreparedImportedModule(
            executable.EntryPoint,
            moduleContext,
            key,
            spec);
    }

    private readonly record struct PreparedImportedModule(
        ExecutableCodeObject Code,
        ExecutionContext Context,
        PyString RegistryKey,
        ImportlibModuleSpecObject Spec);

    private static bool IsDiscoverableBuiltinModuleName(string moduleName, ExecutionContext context)
        => BuiltinModuleCatalog.IsDiscoverable(moduleName, context);

    private static LythonRuntimeException MissingImportedModule(string moduleName, ExecutionContext context, LythonSourceSpan span)
    {
        var failure = RuntimeErrors.NoModuleNamed(moduleName, span);
        if (BuiltinModuleCatalog.HasUnavailableCapability(moduleName, context))
            failure.Attribution = new(LythonFailureReason.HostCapabilityUnavailable);
        return failure;
    }

    private static bool ImportsOnlyFutureAnnotations(IReadOnlyList<ImportedMemberSyntax> importedMembers)
    {
        for (var i = 0; i < importedMembers.Count; i++)
        {
            if (!string.Equals(importedMembers[i].Name, "annotations", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
