using System.Collections.Frozen;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private enum BuiltinModuleCapability
    {
        None,
        Subprocess,
    }

    private sealed record BuiltinModuleRegistration(
        Func<ExecutionContext, PyModule> Resolve,
        BuiltinModuleCapability RequiredCapability);

    private static class BuiltinModuleCatalog
    {
        private static readonly FrozenDictionary<string, BuiltinModuleRegistration> Registrations =
            new Dictionary<string, BuiltinModuleRegistration>(StringComparer.Ordinal)
            {
                ["__future__"] = Module(static _ => FutureModule.Instance),
                ["builtins"] = Module(static context => new BuiltinsModule(context)),
                ["sys"] = Module(static context => new SysModule(context)),
                ["argparse"] = Module(static _ => ArgparseModule.Instance),
                ["dataclasses"] = Module(static _ => DataclassesModule.Instance),
                ["typing"] = Module(static _ => TypingModule.Instance),
                ["pathlib"] = Module(static _ => PathlibModule.Instance),
                ["pkgutil"] = Module(static _ => PkgutilModule.Instance),
                ["collections"] = Module(static _ => CollectionsModule.Instance),
                ["collections.abc"] = Module(static _ => CollectionsAbcModule.Instance),
                ["itertools"] = Module(static _ => ItertoolsModule.Instance),
                ["os"] = Module(static _ => OsModule.Instance),
                ["os.path"] = Module(static _ => OsPathModule.Instance),
                ["glob"] = Module(static _ => GlobModule.Instance),
                ["gzip"] = Module(static _ => GzipModule.Instance),
                ["hashlib"] = Module(static _ => new HashlibModule()),
                ["importlib"] = Module(static _ => ImportlibModule.Instance),
                ["importlib.util"] = Module(static _ => ImportlibUtilModule.Instance),
                ["decimal"] = Module(static _ => DecimalModule.Instance),
                ["math"] = Module(static _ => MathModule.Instance),
                ["datetime"] = Module(static _ => DatetimeModule.Instance),
                ["statistics"] = Module(static _ => StatisticsModule.Instance),
                ["time"] = Module(static _ => TimeModule.Instance),
                ["random"] = Module(static context => new RandomModule(context.State.RandomState)),
                ["copy"] = Module(static _ => new CopyModule()),
                ["operator"] = Module(static _ => OperatorModule.Instance),
                ["openpyxl"] = Module(static _ => OpenPyxlModule.Instance),
                ["openpyxl.reader"] = Module(static _ => OpenPyxlReaderModule.Instance),
                ["openpyxl.reader.excel"] = Module(static _ => OpenPyxlReaderExcelModule.Instance),
                ["openpyxl.utils"] = Module(static _ => OpenPyxlUtilsModule.Instance),
                ["openpyxl.utils.cell"] = Module(static _ => OpenPyxlUtilsCellModule.Instance),
                ["openpyxl.utils.exceptions"] = Module(static _ => OpenPyxlUtilsExceptionsModule.Instance),
                ["openpyxl.workbook"] = Module(static _ => OpenPyxlWorkbookModule.Instance),
                ["openpyxl.cell"] = Module(static _ => OpenPyxlCellModule.Instance),
                ["openpyxl.cell.cell"] = Module(static _ => OpenPyxlCellCellModule.Instance),
                ["openpyxl.styles"] = Module(static _ => OpenPyxlStylesModule.Instance),
                ["openpyxl.styles.colors"] = Module(static _ => OpenPyxlStylesColorsModule.Instance),
                ["openpyxl.comments"] = Module(static _ => OpenPyxlCommentsModule.Instance),
                ["openpyxl.chart"] = Module(static _ => OpenPyxlChartModule.Instance),
                ["openpyxl.worksheet"] = Module(static _ => OpenPyxlWorksheetModule.Instance),
                ["openpyxl.worksheet.worksheet"] = Module(static _ => OpenPyxlWorksheetWorksheetModule.Instance),
                ["openpyxl.worksheet.table"] = Module(static _ => OpenPyxlWorksheetTableModule.Instance),
                ["openpyxl.worksheet.datavalidation"] = Module(static _ => OpenPyxlWorksheetDataValidationModule.Instance),
                ["openpyxl.drawing"] = Module(static _ => OpenPyxlDrawingModule.Instance),
                ["openpyxl.drawing.image"] = Module(static _ => OpenPyxlDrawingImageModule.Instance),
                ["functools"] = Module(static _ => FunctoolsModule.Instance),
                ["re"] = Module(static _ => ReModule.Instance),
                ["shlex"] = Module(static _ => ShlexModule.Instance),
                ["shutil"] = Module(static _ => ShutilModule.Instance),
                ["filecmp"] = Module(static _ => FilecmpModule.Instance),
                ["fnmatch"] = Module(static _ => FnMatchModule.Instance),
                ["difflib"] = Module(static _ => DifflibModule.Instance),
                ["json"] = Module(static _ => JsonModule.Instance),
                ["csv"] = Module(static _ => CsvModule.Instance),
                ["subprocess"] = Module(static _ => SubprocessModule.Instance, BuiltinModuleCapability.Subprocess),
            }.ToFrozenDictionary(StringComparer.Ordinal);

        private static readonly string[] KnownNames = Registrations.Keys.Order(StringComparer.Ordinal).ToArray();
        private static readonly string[] CapabilityFreeNames = KnownNames
            .Where(static name => Registrations[name].RequiredCapability == BuiltinModuleCapability.None)
            .ToArray();

        public static IReadOnlyList<string> Names => KnownNames;

        public static IReadOnlyList<string> GetDiscoverableNames(ExecutionContext context)
            => context.Host.SubprocessRunner is null ? CapabilityFreeNames : KnownNames;

        public static bool IsDiscoverable(string moduleName, ExecutionContext context)
            => Registrations.TryGetValue(moduleName, out var registration) &&
                (registration.RequiredCapability == BuiltinModuleCapability.None || context.Host.SubprocessRunner is not null);

        public static PyModule? Resolve(string moduleName, ExecutionContext context)
            => IsDiscoverable(moduleName, context) ? Registrations[moduleName].Resolve(context) : null;

        private static BuiltinModuleRegistration Module(
            Func<ExecutionContext, PyModule> resolve,
            BuiltinModuleCapability requiredCapability)
            => new(resolve, requiredCapability);

        private static BuiltinModuleRegistration Module(Func<ExecutionContext, PyModule> resolve)
            => Module(resolve, BuiltinModuleCapability.None);
    }

    internal static IReadOnlyList<string> GetKnownBuiltinModuleNames()
        => BuiltinModuleCatalog.Names;
}
