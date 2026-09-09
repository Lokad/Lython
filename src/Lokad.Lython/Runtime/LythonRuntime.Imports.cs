using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

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
                name => ValueTask.FromResult(ResolveImportedModule(name, context, statement.Span)))
            .GetAwaiter()
            .GetResult();

    private static PyModule ResolveImportedModule(string moduleName, ExecutionContext context, LythonSourceSpan span)
    {
        if (TryResolveKnownImportedModule(moduleName, context, out var known))
        {
            return known;
        }

        if (!TryResolveLocalImportPath(moduleName, context, span, out var path, out _))
        {
            throw RuntimeErrors.NoModuleNamed(moduleName, span);
        }

        using var loadingScope = EnterModuleLoading(moduleName, context, span);
        try
        {
            var source = ReadGovernedHostText(path, context, span);
            var prepared = PrepareImportedModule(moduleName, path, source.AsString(), context, span);
            var signal = ExecuteStatements(prepared.Statements, prepared.Context);
            return CompleteImportedModule(moduleName, prepared.Context, signal, context, span);
        }
        catch (ReturnSignal)
        {
            throw RuntimeErrors.ImportedModuleReturned(moduleName, span);
        }
    }

    private static PyModule ResolveImportedModuleHierarchy(string moduleName, ExecutionContext context, LythonSourceSpan span)
        => ResolveImportedModuleHierarchyCore(
                moduleName,
                span,
                name => ValueTask.FromResult(ResolveImportedModule(name, context, span)))
            .GetAwaiter()
            .GetResult();

    private static async ValueTask<PyModule> ResolveImportedModuleAsync(string moduleName, ExecutionContext context, LythonSourceSpan span)
    {
        if (TryResolveKnownImportedModule(moduleName, context, out var known))
        {
            return known;
        }

        var localImport = await TryResolveLocalImportPathAsync(moduleName, context, span).ConfigureAwait(false);
        if (localImport is null)
        {
            throw RuntimeErrors.NoModuleNamed(moduleName, span);
        }

        var path = localImport.Value.Path;

        using var loadingScope = EnterModuleLoading(moduleName, context, span);
        try
        {
            var source = await ReadGovernedHostTextAsync(path, context, span).ConfigureAwait(false);
            var prepared = PrepareImportedModule(moduleName, path, source.AsString(), context, span);
            var signal = await ExecuteStatementsAsync(prepared.Statements, prepared.Context).ConfigureAwait(false);
            return CompleteImportedModule(moduleName, prepared.Context, signal, context, span);
        }
        catch (ReturnSignal)
        {
            throw RuntimeErrors.ImportedModuleReturned(moduleName, span);
        }
    }

    private static async ValueTask<PyModule> ResolveImportedModuleHierarchyAsync(
        string moduleName,
        ExecutionContext context,
        LythonSourceSpan span)
        => await ResolveImportedModuleHierarchyCore(
                moduleName,
                span,
                name => ResolveImportedModuleAsync(name, context, span))
            .ConfigureAwait(false);

    private static void AttachImportedChild(PyModule parent, string childName, PyModule child, LythonSourceSpan span)
    {
        if (parent.TrySetMember(childName, child))
        {
            return;
        }

        if (parent.TryGetMember(childName, out var existing) &&
            existing is PyModule existingModule &&
            string.Equals(existingModule.Name, child.Name, StringComparison.Ordinal))
        {
            return;
        }

        throw new LythonRuntimeException(
            "ImportError",
            $"Cannot attach imported module '{child.Name}' to parent package '{parent.Name}'.",
            span);
    }

    private static async ValueTask ExecuteImportAsync(ImportStatementSyntax statement, ExecutionContext context)
        => await ExecuteImportCore(
                statement,
                context,
                name => ResolveImportedModuleAsync(name, context, statement.Span))
            .ConfigureAwait(false);

    private static async ValueTask ExecuteImportCore(
        ImportStatementSyntax statement,
        ExecutionContext context,
        Func<string, ValueTask<PyModule>> resolveModule)
    {
        if (string.Equals(statement.ModuleName, "__future__", StringComparison.Ordinal))
        {
            if (statement.ImportedMembers is not null &&
                ImportsOnlyFutureAnnotations(statement.ImportedMembers))
            {
                return;
            }

            throw RuntimeErrors.NoModuleNamed(statement.ModuleName, statement.Span);
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
            ExecuteStarImport(module, context, statement.Span);
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

    private static void ExecuteStarImport(PyModule module, ExecutionContext context, LythonSourceSpan span)
    {
        foreach (var name in module.ExportedNames)
        {
            if (!TryResolveImportedMember(module, name, context, span, out var value))
            {
                throw RuntimeErrors.CannotImportMember(module.Name, name, span);
            }

            StoreName(name, value, context, span);
        }
    }

    private static bool TryResolveImportedMember(
        PyModule module,
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

        return PathOps.Normalize(fileName, baseDirectory);
    }

    private static bool TryResolveLocalImportPath(
        string moduleName,
        ExecutionContext context,
        LythonSourceSpan span,
        out string path,
        out bool isPackage)
    {
        foreach (var candidate in EnumerateLocalImportCandidates(moduleName, context))
        {
            var allowlistPath = ResolveLocalModuleAllowlistPath(candidate.Path, context);
            if (!IsLocalModuleImportAllowed(moduleName, candidate.Path, allowlistPath, context))
            {
                continue;
            }

            context.RegisterHostCall(span);
            if (context.HostExists(candidate.Path, span))
            {
                path = candidate.Path;
                isPackage = candidate.IsPackage;
                return true;
            }
        }

        path = string.Empty;
        isPackage = false;
        return false;
    }

    private static async ValueTask<LocalImportCandidate?> TryResolveLocalImportPathAsync(
        string moduleName,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        foreach (var candidate in EnumerateLocalImportCandidates(moduleName, context))
        {
            var allowlistPath = ResolveLocalModuleAllowlistPath(candidate.Path, context);
            if (!IsLocalModuleImportAllowed(moduleName, candidate.Path, allowlistPath, context))
            {
                continue;
            }

            context.RegisterHostCall(span);
            if (await context.HostExistsAsync(candidate.Path, span).ConfigureAwait(false))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<LocalImportCandidate> EnumerateLocalImportCandidates(string moduleName, ExecutionContext context)
    {
        var yielded = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in CreateLocalImportCandidates(moduleName, context))
        {
            if (yielded.Add(candidate.Path))
            {
                yield return candidate;
            }
        }
    }

    private static IEnumerable<LocalImportCandidate> CreateLocalImportCandidates(string moduleName, ExecutionContext context)
    {
        yield return new LocalImportCandidate(ResolveLocalModulePath(moduleName, context), IsPackage: false);

        var baseDirectory = context.SourcePath is null
            ? context.Host.Cwd
            : PathOps.Parent(context.SourcePath);
        var packageModulePath = PathOps.Normalize(moduleName.Replace('.', '/') + ".py", baseDirectory);
        yield return new LocalImportCandidate(packageModulePath, IsPackage: false);

        var packageInitPath = PathOps.Normalize(moduleName.Replace('.', '/') + "/__init__.py", baseDirectory);
        yield return new LocalImportCandidate(packageInitPath, IsPackage: true);
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

    private static async ValueTask<PyModule> ResolveImportedModuleHierarchyCore(
        string moduleName,
        LythonSourceSpan span,
        Func<string, ValueTask<PyModule>> resolveModule)
    {
        var parts = moduleName.Split('.');
        PyModule? parent = null;
        PyModule? resolved = null;
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
    // name strings, and exported-table infrastructure for the run. Per-variable
    // exported entries stay open (module-table ownership belongs to the
    // frontend/import envelope); re-imports hit the registry and pay nothing.
    private const long ImportedModuleBytes = 512;

    private static void ChargeImportedModule(ExecutionContext context, LythonSourceSpan? span)
    {
        context.MemoryGovernor.Reserve(ImportedModuleBytes, span);
        context.MemoryGovernor.Commit(ImportedModuleBytes);
    }

    private static bool TryResolveKnownImportedModule(
        string moduleName,
        ExecutionContext context,
        [MaybeNullWhen(false)] out PyModule module)
    {
        if (context.State.ImportedModules.TryGetValue(moduleName, out module))
        {
            return true;
        }

        module = ResolveBuiltinModule(moduleName, context);
        if (module is null)
        {
            return false;
        }

        ChargeImportedModule(context, null);
        context.State.ImportedModules[moduleName] = module;
        return true;
    }

    private static PreparedImportedModule PrepareImportedModule(
        string moduleName,
        string path,
        string source,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        var frontend = LythonFrontend.Compile(source);
        if (frontend.Script is null || frontend.Diagnostics.Count != 0)
        {
            var diagnostic = frontend.Diagnostics.FirstOrDefault();
            var message = diagnostic?.Message ?? "unknown syntax error";
            throw RuntimeErrors.CannotImportModule(moduleName, message, diagnostic?.Span ?? span);
        }

        return new PreparedImportedModule(
            LoweredScript.Lower(frontend.Script).Statements,
            ExecutionContext.CreateModule(context, path, moduleName));
    }

    private static PyModule CompleteImportedModule(
        string moduleName,
        ExecutionContext moduleContext,
        object? signal,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        if (signal is BreakSignal or ContinueSignal)
        {
            throw RuntimeErrors.TopLevelLoopControl(span);
        }

        var exported = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var pair in moduleContext.Variables)
        {
            if (!ExecutionState.BuiltinNames.Contains(pair.Key))
            {
                exported[pair.Key] = pair.Value;
            }
        }

        var loaded = new ScriptPyModule(moduleName, exported);
        ChargeImportedModule(context, span);
        context.State.ImportedModules[moduleName] = loaded;
        return loaded;
    }

    private static ModuleLoadingScope EnterModuleLoading(
        string moduleName,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        if (!context.State.LoadingModules.Add(moduleName))
        {
            throw RuntimeErrors.CircularImport(moduleName, span);
        }

        return new ModuleLoadingScope(context.State.LoadingModules, moduleName);
    }

    private readonly record struct PreparedImportedModule(
        IReadOnlyList<LoweredStatement> Statements,
        ExecutionContext Context);

    private readonly struct ModuleLoadingScope : IDisposable
    {
        private readonly HashSet<string> _loadingModules;
        private readonly string _moduleName;

        public ModuleLoadingScope(HashSet<string> loadingModules, string moduleName)
        {
            _loadingModules = loadingModules;
            _moduleName = moduleName;
        }

        public void Dispose() => _loadingModules.Remove(_moduleName);
    }

    private static bool IsDiscoverableBuiltinModuleName(string moduleName, ExecutionContext context)
        => BuiltinModuleCatalog.IsDiscoverable(moduleName, context);

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
