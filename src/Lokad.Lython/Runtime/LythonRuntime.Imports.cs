using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static readonly string[] AlwaysDiscoverableBuiltinModuleNames =
    [
        "__future__",
        "argparse",
        "collections",
        "copy",
        "csv",
        "dataclasses",
        "datetime",
        "decimal",
        "difflib",
        "fnmatch",
        "functools",
        "glob",
        "itertools",
        "json",
        "math",
        "operator",
        "os",
        "os.path",
        "pathlib",
        "pkgutil",
        "random",
        "re",
        "statistics",
        "sys",
        "typing",
    ];

    private sealed class FutureModule : PyModule
    {
        public static readonly FutureModule Instance = new();

        private FutureModule() : base("__future__")
        {
        }

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "annotations" => PyNone.Instance,
                _ => null!
            };

            return value is not null;
        }
    }

    private static void ExecuteImport(ImportStatementSyntax statement, ExecutionContext context)
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

        var module = ResolveImportedModule(statement.ModuleName, context, statement.Span);

        if (statement.ImportedMembers is null)
        {
            context.Variables[statement.BindingName] = module;
            return;
        }

        foreach (var importedMember in statement.ImportedMembers)
        {
            if (!module.TryGetMember(importedMember.Name, out var value))
            {
                throw RuntimeErrors.CannotImportMember(statement.ModuleName, importedMember.Name, statement.Span);
            }

            context.Variables[importedMember.BindingName] = value;
        }
    }

    private static PyModule ResolveImportedModule(string moduleName, ExecutionContext context, LythonSourceSpan span)
    {
        var builtin = ResolveBuiltinModule(moduleName, context);
        if (builtin is not null)
        {
            return builtin;
        }

        if (context.State.ImportedModules.TryGetValue(moduleName, out var cached))
        {
            return cached;
        }

        var path = ResolveLocalModulePath(moduleName, context);
        var allowlistPath = ResolveLocalModuleAllowlistPath(path, context);
        if (!IsLocalModuleImportAllowed(moduleName, path, allowlistPath, context))
        {
            throw RuntimeErrors.NoModuleNamed(moduleName, span);
        }

        if (!context.State.LoadingModules.Add(moduleName))
        {
            throw RuntimeErrors.CircularImport(moduleName, span);
        }

        try
        {
            context.RegisterHostCall(span);
            if (!context.HostExists(path, span))
            {
                throw RuntimeErrors.NoModuleNamed(moduleName, span);
            }

            var source = ReadGovernedHostText(path, context, span);
            var frontend = LythonFrontend.Compile(source.AsString());
            if (frontend.Script is null || frontend.Diagnostics.Count != 0)
            {
                var diagnostic = frontend.Diagnostics.FirstOrDefault();
                var message = diagnostic is null
                    ? "unknown syntax error"
                    : diagnostic.Message;
                throw RuntimeErrors.CannotImportModule(moduleName, message, diagnostic?.Span ?? span);
            }

            var moduleContext = new ExecutionContext(context, moduleScope: true, sourcePath: path);
            var signal = ExecuteStatements(LoweredScript.Lower(frontend.Script).Statements, moduleContext);
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
            context.State.ImportedModules[moduleName] = loaded;
            return loaded;
        }
        catch (ReturnSignal)
        {
            throw RuntimeErrors.ImportedModuleReturned(moduleName, span);
        }
        finally
        {
            context.State.LoadingModules.Remove(moduleName);
        }
    }

    private static async ValueTask<PyModule> ResolveImportedModuleAsync(string moduleName, ExecutionContext context, LythonSourceSpan span)
    {
        var builtin = ResolveBuiltinModule(moduleName, context);
        if (builtin is not null)
        {
            return builtin;
        }

        if (context.State.ImportedModules.TryGetValue(moduleName, out var cached))
        {
            return cached;
        }

        var path = ResolveLocalModulePath(moduleName, context);
        var allowlistPath = ResolveLocalModuleAllowlistPath(path, context);
        if (!IsLocalModuleImportAllowed(moduleName, path, allowlistPath, context))
        {
            throw RuntimeErrors.NoModuleNamed(moduleName, span);
        }

        if (!context.State.LoadingModules.Add(moduleName))
        {
            throw RuntimeErrors.CircularImport(moduleName, span);
        }

        try
        {
            context.RegisterHostCall(span);
            if (!await context.HostExistsAsync(path, span).ConfigureAwait(false))
            {
                throw RuntimeErrors.NoModuleNamed(moduleName, span);
            }

            var source = await ReadGovernedHostTextAsync(path, context, span).ConfigureAwait(false);
            var frontend = LythonFrontend.Compile(source.AsString());
            if (frontend.Script is null || frontend.Diagnostics.Count != 0)
            {
                var diagnostic = frontend.Diagnostics.FirstOrDefault();
                var message = diagnostic is null
                    ? "unknown syntax error"
                    : diagnostic.Message;
                throw RuntimeErrors.CannotImportModule(moduleName, message, diagnostic?.Span ?? span);
            }

            var moduleContext = new ExecutionContext(context, moduleScope: true, sourcePath: path);
            var signal = await ExecuteStatementsAsync(LoweredScript.Lower(frontend.Script).Statements, moduleContext).ConfigureAwait(false);
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
            context.State.ImportedModules[moduleName] = loaded;
            return loaded;
        }
        catch (ReturnSignal)
        {
            throw RuntimeErrors.ImportedModuleReturned(moduleName, span);
        }
        finally
        {
            context.State.LoadingModules.Remove(moduleName);
        }
    }

    private static async ValueTask ExecuteImportAsync(ImportStatementSyntax statement, ExecutionContext context)
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

        var module = await ResolveImportedModuleAsync(statement.ModuleName, context, statement.Span).ConfigureAwait(false);

        if (statement.ImportedMembers is null)
        {
            context.Variables[statement.BindingName] = module;
            return;
        }

        foreach (var importedMember in statement.ImportedMembers)
        {
            if (!module.TryGetMember(importedMember.Name, out var value))
            {
                throw RuntimeErrors.CannotImportMember(statement.ModuleName, importedMember.Name, statement.Span);
            }

            context.Variables[importedMember.BindingName] = value;
        }
    }

    private static string ResolveLocalModulePath(string moduleName, ExecutionContext context)
    {
        var fileName = moduleName + ".py";
        var baseDirectory = context.SourcePath is null
            ? context.Host.Cwd
            : PathOps.Parent(context.SourcePath);

        return PathOps.Normalize(fileName, baseDirectory);
    }

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

    private static PyModule? ResolveBuiltinModule(string moduleName, ExecutionContext context)
    {
        return moduleName switch
        {
            "__future__" => FutureModule.Instance,
            "sys" => new SysModule(context.State),
            "argparse" => ArgparseModule.Instance,
            "dataclasses" => DataclassesModule.Instance,
            "typing" => TypingModule.Instance,
            "pathlib" => PathlibModule.Instance,
            "pkgutil" => PkgutilModule.Instance,
            "collections" => CollectionsModule.Instance,
            "itertools" => ItertoolsModule.Instance,
            "os" => OsModule.Instance,
            "os.path" => OsPathModule.Instance,
            "glob" => GlobModule.Instance,
            "decimal" => DecimalModule.Instance,
            "math" => MathModule.Instance,
            "datetime" => DatetimeModule.Instance,
            "statistics" => StatisticsModule.Instance,
            "random" => new RandomModule(context.State.RandomState),
            "copy" => CopyModule.Instance,
            "operator" => OperatorModule.Instance,
            "functools" => FunctoolsModule.Instance,
            "re" => ReModule.Instance,
            "fnmatch" => FnMatchModule.Instance,
            "difflib" => DifflibModule.Instance,
            "json" => JsonModule.Instance,
            "csv" => CsvModule.Instance,
            "subprocess" when context.Host.SubprocessRunner is not null => SubprocessModule.Instance,
            _ => null,
        };
    }

    private static IEnumerable<string> EnumerateDiscoverableBuiltinModuleNames(ExecutionContext context)
    {
        foreach (var moduleName in AlwaysDiscoverableBuiltinModuleNames)
        {
            yield return moduleName;
        }

        if (context.Host.SubprocessRunner is not null)
        {
            yield return "subprocess";
        }
    }

    private static bool IsDiscoverableBuiltinModuleName(string moduleName, ExecutionContext context)
    {
        if (AlwaysDiscoverableBuiltinModuleNames.Contains(moduleName, StringComparer.Ordinal))
        {
            return true;
        }

        return string.Equals(moduleName, "subprocess", StringComparison.Ordinal) &&
            context.Host.SubprocessRunner is not null;
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
