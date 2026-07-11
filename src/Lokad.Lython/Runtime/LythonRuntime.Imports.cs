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
        "collections.abc",
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
        "openpyxl",
        "openpyxl.reader",
        "openpyxl.reader.excel",
        "openpyxl.utils",
        "openpyxl.utils.cell",
        "openpyxl.utils.exceptions",
        "openpyxl.workbook",
        "openpyxl.cell",
        "openpyxl.cell.cell",
        "openpyxl.styles",
        "openpyxl.styles.colors",
        "openpyxl.comments",
        "openpyxl.chart",
        "openpyxl.worksheet",
        "openpyxl.worksheet.worksheet",
        "openpyxl.worksheet.table",
        "openpyxl.worksheet.datavalidation",
        "openpyxl.drawing",
        "openpyxl.drawing.image",
        "os",
        "os.path",
        "pathlib",
        "pkgutil",
        "random",
        "re",
        "shutil",
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
            StoreName(statement.BindingName, module, context, statement.Span);
            return;
        }

        if (IsStarImport(statement.ImportedMembers))
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

        if (!TryResolveLocalImportPath(moduleName, context, span, out var path, out _))
        {
            throw RuntimeErrors.NoModuleNamed(moduleName, span);
        }

        if (!context.State.LoadingModules.Add(moduleName))
        {
            throw RuntimeErrors.CircularImport(moduleName, span);
        }

        try
        {
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

            var moduleContext = new ExecutionContext(context, moduleScope: true, sourcePath: path, moduleName: moduleName);
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

        var localImport = await TryResolveLocalImportPathAsync(moduleName, context, span).ConfigureAwait(false);
        if (localImport is null)
        {
            throw RuntimeErrors.NoModuleNamed(moduleName, span);
        }

        var path = localImport.Value.Path;

        if (!context.State.LoadingModules.Add(moduleName))
        {
            throw RuntimeErrors.CircularImport(moduleName, span);
        }

        try
        {
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

            var moduleContext = new ExecutionContext(context, moduleScope: true, sourcePath: path, moduleName: moduleName);
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
            StoreName(statement.BindingName, module, context, statement.Span);
            return;
        }

        if (IsStarImport(statement.ImportedMembers))
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

    private static bool IsStarImport(IReadOnlyList<ImportedMemberSyntax> members)
        => members.Count == 1 && members[0].Name == "*";

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
        out object value)
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
            if (IsLocalModuleImportAllowed(moduleName, candidate.Path, allowlistPath, context) &&
                context.HostExists(candidate.Path, span))
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
            if (IsLocalModuleImportAllowed(moduleName, candidate.Path, allowlistPath, context) &&
                await context.HostExistsAsync(candidate.Path, span).ConfigureAwait(false))
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

    private static PyModule? ResolveBuiltinModule(string moduleName, ExecutionContext context)
    {
        return moduleName switch
        {
            "__future__" => FutureModule.Instance,
            "sys" => new SysModule(context),
            "argparse" => ArgparseModule.Instance,
            "dataclasses" => DataclassesModule.Instance,
            "typing" => TypingModule.Instance,
            "pathlib" => PathlibModule.Instance,
            "pkgutil" => PkgutilModule.Instance,
            "collections" => CollectionsModule.Instance,
            "collections.abc" => CollectionsAbcModule.Instance,
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
            "openpyxl" => OpenPyxlModule.Instance,
            "openpyxl.reader" => OpenPyxlReaderModule.Instance,
            "openpyxl.reader.excel" => OpenPyxlReaderExcelModule.Instance,
            "openpyxl.utils" => OpenPyxlUtilsModule.Instance,
            "openpyxl.utils.cell" => OpenPyxlUtilsCellModule.Instance,
            "openpyxl.utils.exceptions" => OpenPyxlUtilsExceptionsModule.Instance,
            "openpyxl.workbook" => OpenPyxlWorkbookModule.Instance,
            "openpyxl.cell" => OpenPyxlCellModule.Instance,
            "openpyxl.cell.cell" => OpenPyxlCellCellModule.Instance,
            "openpyxl.styles" => OpenPyxlStylesModule.Instance,
            "openpyxl.styles.colors" => OpenPyxlStylesColorsModule.Instance,
            "openpyxl.comments" => OpenPyxlCommentsModule.Instance,
            "openpyxl.chart" => OpenPyxlChartModule.Instance,
            "openpyxl.worksheet" => OpenPyxlWorksheetModule.Instance,
            "openpyxl.worksheet.worksheet" => OpenPyxlWorksheetWorksheetModule.Instance,
            "openpyxl.worksheet.table" => OpenPyxlWorksheetTableModule.Instance,
            "openpyxl.worksheet.datavalidation" => OpenPyxlWorksheetDataValidationModule.Instance,
            "openpyxl.drawing" => OpenPyxlDrawingModule.Instance,
            "openpyxl.drawing.image" => OpenPyxlDrawingImageModule.Instance,
            "functools" => FunctoolsModule.Instance,
            "re" => ReModule.Instance,
            "shutil" => ShutilModule.Instance,
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
