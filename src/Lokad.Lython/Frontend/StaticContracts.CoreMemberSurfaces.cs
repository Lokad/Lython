namespace Lokad.Lython.Frontend;

internal static partial class StaticContracts
{
    private static readonly HashSet<string> StringMembers = new(StringComparer.Ordinal)
    {
        "encode",
        "replace",
        "startswith",
        "endswith",
        "lower",
        "capitalize",
        "islower",
        "upper",
        "swapcase",
        "title",
        "isupper",
        "isalpha",
        "isdigit",
        "isalnum",
        "isspace",
        "split",
        "rsplit",
        "splitlines",
        "expandtabs",
        "strip",
        "lstrip",
        "rstrip",
        "join",
        "center",
        "ljust",
        "rjust",
        "zfill",
        "find",
        "index",
        "rfind",
        "rindex",
        "count",
        "removeprefix",
        "removesuffix",
        "partition",
        "rpartition",
        "format",
        "format_map",
    };

    private static readonly HashSet<string> BytesMembers = new(StringComparer.Ordinal)
    {
        "decode",
    };

    private static readonly HashSet<string> PathMembers = new(StringComparer.Ordinal)
    {
        "name",
        "suffix",
        "suffixes",
        "stem",
        "parent",
        "parents",
        "parts",
        "drive",
        "root",
        "anchor",
        "__fspath__",
        "is_absolute",
        "is_mount",
        "is_reserved",
        "joinpath",
        "match",
        "as_posix",
        "resolve",
        "absolute",
        "expanduser",
        "relative_to",
        "is_relative_to",
        "with_suffix",
        "with_name",
        "with_stem",
        "exists",
        "is_file",
        "is_dir",
        "is_symlink",
        "samefile",
        "stat",
        "lstat",
        "unlink",
        "rmdir",
        "rename",
        "replace",
        "mkdir",
        "touch",
        "open",
        "iterdir",
        "glob",
        "read_text",
        "write_text",
        "rglob",
        "read_bytes",
        "write_bytes",
        "readlink",
        "symlink_to",
        "hardlink_to",
        "chmod",
        "owner",
        "group",
    };

    private static readonly HashSet<string> TextFileHandleMembers = new(StringComparer.Ordinal)
    {
        "closed",
        "name",
        "mode",
        "encoding",
        "errors",
        "__enter__",
        "__exit__",
        "close",
        "readable",
        "writable",
        "seekable",
        "tell",
        "seek",
        "read",
        "readline",
        "readlines",
        "write",
        "writelines",
        "flush",
    };

    private static readonly HashSet<string> ListMembers = new(StringComparer.Ordinal)
    {
        "append",
        "extend",
        "index",
        "count",
        "insert",
        "remove",
        "pop",
        "reverse",
        "sort",
        "copy",
        "clear",
    };

    private static readonly HashSet<string> DictMembers = new(StringComparer.Ordinal)
    {
        "get",
        "keys",
        "values",
        "items",
        "update",
        "pop",
        "copy",
        "clear",
        "setdefault",
    };

    private static readonly HashSet<string> SetMembers = new(StringComparer.Ordinal)
    {
        "add",
        "clear",
        "copy",
        "difference",
        "difference_update",
        "discard",
        "intersection",
        "intersection_update",
        "isdisjoint",
        "issubset",
        "issuperset",
        "pop",
        "remove",
        "symmetric_difference",
        "symmetric_difference_update",
        "union",
        "update",
    };

    private static readonly HashSet<string> PopenMembers = new(StringComparer.Ordinal)
    {
        "args",
        "stdin",
        "stdout",
        "stderr",
        "returncode",
        "encoding",
        "errors",
        "universal_newlines",
        "communicate",
        "wait",
        "poll",
        "send_signal",
        "terminate",
        "kill",
        "pid",
    };

    private static readonly HashSet<string> CollectionsDefaultDictMembers = new(StringComparer.Ordinal)
    {
        "default_factory",
        "get",
        "keys",
        "values",
        "items",
        "setdefault",
        "copy",
        "clear",
    };

    private static readonly HashSet<string> CollectionsCounterMembers = new(StringComparer.Ordinal)
    {
        "get",
        "update",
        "subtract",
        "total",
        "most_common",
        "elements",
        "copy",
        "clear",
        "keys",
        "values",
        "items",
    };

    private static readonly HashSet<string> CollectionsDequeMembers = new(StringComparer.Ordinal)
    {
        "maxlen",
        "append",
        "appendleft",
        "pop",
        "popleft",
        "extend",
        "extendleft",
        "clear",
        "copy",
        "count",
        "index",
        "insert",
        "remove",
        "reverse",
        "rotate",
    };

    private static readonly HashSet<string> CollectionsChainMapMembers = new(StringComparer.Ordinal)
    {
        "maps",
        "parents",
        "get",
        "keys",
        "values",
        "items",
        "new_child",
        "copy",
    };

    private static readonly HashSet<string> DecimalMembers = new(StringComparer.Ordinal)
    {
        "quantize",
        "normalize",
        "sqrt",
        "exp",
        "ln",
        "log10",
        "copy_abs",
        "copy_negate",
        "copy_sign",
        "to_integral_value",
        "to_integral_exact",
        "to_integral",
        "as_tuple",
        "adjusted",
        "compare",
        "compare_total",
        "is_nan",
        "is_infinite",
        "is_finite",
        "is_zero",
        "is_signed",
        "to_eng_string",
        "scaleb",
        "shift",
        "rotate",
        "same_quantum",
        "remainder_near",
        "min",
        "max",
        "min_mag",
        "max_mag",
    };

    private static readonly HashSet<string> DecimalContextMembers = new(StringComparer.Ordinal)
    {
        "prec",
        "rounding",
        "Emin",
        "Emax",
        "capitals",
        "clamp",
        "flags",
        "traps",
        "copy",
        "clear_flags",
        "create_decimal",
        "create_decimal_from_float",
    };

    private static readonly HashSet<string> DecimalTupleMembers = new(StringComparer.Ordinal)
    {
        "sign",
        "digits",
        "exponent",
    };

    private static readonly HashSet<string> DateTimeTimedeltaMembers = new(StringComparer.Ordinal)
    {
        "days",
        "seconds",
        "microseconds",
        "total_seconds",
    };

    private static readonly HashSet<string> DateTimeDateMembers = new(StringComparer.Ordinal)
    {
        "year",
        "month",
        "day",
        "weekday",
        "isoweekday",
        "isocalendar",
        "toordinal",
        "timetuple",
        "ctime",
        "isoformat",
        "__format__",
        "strftime",
        "replace",
    };

    private static readonly HashSet<string> DateTimeTimeMembers = new(StringComparer.Ordinal)
    {
        "hour",
        "minute",
        "second",
        "microsecond",
        "tzinfo",
        "fold",
        "utcoffset",
        "tzname",
        "dst",
        "isoformat",
        "__format__",
        "strftime",
        "replace",
    };

    private static readonly HashSet<string> DateTimeDateTimeMembers = new(StringComparer.Ordinal)
    {
        "year",
        "month",
        "day",
        "hour",
        "minute",
        "second",
        "microsecond",
        "tzinfo",
        "fold",
        "date",
        "time",
        "timetz",
        "weekday",
        "isoweekday",
        "isocalendar",
        "toordinal",
        "timetuple",
        "utctimetuple",
        "ctime",
        "timestamp",
        "utcoffset",
        "tzname",
        "dst",
        "astimezone",
        "isoformat",
        "__format__",
        "strftime",
        "replace",
    };

    private static readonly HashSet<string> DateTimeTimezoneMembers = new(StringComparer.Ordinal)
    {
        "utcoffset",
        "tzname",
        "dst",
    };

    private static readonly HashSet<string> StatisticsLinearRegressionMembers = new(StringComparer.Ordinal)
    {
        "slope",
        "intercept",
        "_fields",
        "_asdict",
        "_replace",
        "count",
        "index",
    };

    private static readonly HashSet<string> StatisticsNormalDistMembers = new(StringComparer.Ordinal)
    {
        "mean",
        "median",
        "mode",
        "stdev",
        "variance",
        "zscore",
        "pdf",
        "cdf",
        "inv_cdf",
        "overlap",
        "quantiles",
        "samples",
    };

    private static readonly HashSet<string> RandomMembers = new(StringComparer.Ordinal)
    {
        "seed",
        "random",
        "getstate",
        "setstate",
        "randrange",
        "randint",
        "choice",
        "choices",
        "shuffle",
        "sample",
        "getrandbits",
        "randbytes",
        "uniform",
        "triangular",
        "betavariate",
        "expovariate",
        "gammavariate",
        "gauss",
        "normalvariate",
        "lognormvariate",
        "paretovariate",
        "vonmisesvariate",
        "weibullvariate",
    };

    public static bool IsKnownMissingMember(AbstractValue value, string memberName)
    {
        // Every object reports its class like CPython, so __class__ is never
        // a statically known missing member on any receiver.
        if (memberName == "__class__")
        {
            return false;
        }

        bool? hasMember = value.Kind switch
        {
            AbstractValueKind.Module => IsKnownBuiltinModule(value.RequireText())
                ? memberName == "__name__" || IsKnownBuiltinModuleMember(value.RequireText(), memberName)
                : null,
            AbstractValueKind.String or AbstractValueKind.StringType => StringMembers.Contains(memberName),
            AbstractValueKind.Bytes or AbstractValueKind.BytesType => BytesMembers.Contains(memberName),
            AbstractValueKind.Integer or
            AbstractValueKind.IntegerType or
            AbstractValueKind.Float or
            AbstractValueKind.FloatType or
            AbstractValueKind.Boolean or
            AbstractValueKind.BooleanType or
            AbstractValueKind.None => false,
            AbstractValueKind.Path => PathMembers.Contains(memberName),
            AbstractValueKind.TextFileHandle => TextFileHandleMembers.Contains(memberName),
            AbstractValueKind.List or AbstractValueKind.ListType => ListMembers.Contains(memberName),
            AbstractValueKind.Tuple => false,
            AbstractValueKind.Dict => DictMembers.Contains(memberName),
            AbstractValueKind.Set or AbstractValueKind.SetType => SetMembers.Contains(memberName),
            AbstractValueKind.CollectionsDefaultDict => CollectionsDefaultDictMembers.Contains(memberName),
            AbstractValueKind.CollectionsCounter => CollectionsCounterMembers.Contains(memberName),
            AbstractValueKind.CollectionsDeque => CollectionsDequeMembers.Contains(memberName),
            AbstractValueKind.CollectionsChainMap => CollectionsChainMapMembers.Contains(memberName),
            AbstractValueKind.Decimal => DecimalMembers.Contains(memberName),
            AbstractValueKind.DecimalContext => DecimalContextMembers.Contains(memberName),
            AbstractValueKind.DecimalTuple => DecimalTupleMembers.Contains(memberName),
            AbstractValueKind.DateTimeTimedelta => DateTimeTimedeltaMembers.Contains(memberName),
            AbstractValueKind.DateTimeDate => DateTimeDateMembers.Contains(memberName),
            AbstractValueKind.DateTimeTime => DateTimeTimeMembers.Contains(memberName),
            AbstractValueKind.DateTimeDateTime => DateTimeDateTimeMembers.Contains(memberName),
            AbstractValueKind.DateTimeTimezone => DateTimeTimezoneMembers.Contains(memberName),
            AbstractValueKind.StatisticsLinearRegression => StatisticsLinearRegressionMembers.Contains(memberName),
            AbstractValueKind.StatisticsNormalDist => StatisticsNormalDistMembers.Contains(memberName),
            AbstractValueKind.Random => RandomMembers.Contains(memberName),
            AbstractValueKind.RegexPattern => RegexPatternMembers.Contains(memberName),
            AbstractValueKind.RegexMatch => RegexMatchMembers.Contains(memberName),
            AbstractValueKind.ArgparseParser => ArgparseParserMembers.Contains(memberName),
            AbstractValueKind.ArgparseMutuallyExclusiveGroup => ArgparseGroupMembers.Contains(memberName),
            AbstractValueKind.ArgparseNamespace => (value.RequireArgparseNamespaceSummary()).IsSealed
                ? (value.RequireArgparseNamespaceSummary()).Members.ContainsKey(memberName)
                : null,
            AbstractValueKind.CsvReader => CsvReaderMembers.Contains(memberName),
            AbstractValueKind.CsvDictReader => CsvDictReaderMembers.Contains(memberName),
            AbstractValueKind.CsvWriter => CsvWriterMembers.Contains(memberName),
            AbstractValueKind.CsvDictWriter => CsvDictWriterMembers.Contains(memberName),
            AbstractValueKind.DifflibDiffer => DifflibDifferMembers.Contains(memberName),
            AbstractValueKind.DifflibHtmlDiff => DifflibHtmlDiffMembers.Contains(memberName),
            AbstractValueKind.DifflibMatch => DifflibMatchMembers.Contains(memberName),
            AbstractValueKind.DifflibSequenceMatcher => DifflibSequenceMatcherMembers.Contains(memberName),
            AbstractValueKind.PkgutilModuleInfo => PkgutilModuleInfoMembers.Contains(memberName),
            AbstractValueKind.PkgutilLoader => PkgutilLoaderMembers.Contains(memberName),
            AbstractValueKind.SubprocessCompletedProcess => CompletedProcessMembers.Contains(memberName),
            AbstractValueKind.SubprocessPopen => PopenMembers.Contains(memberName),
            AbstractValueKind.DataclassField => DataclassFieldMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlWorkbook => OpenPyxlWorkbookMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlWorksheet => OpenPyxlWorksheetMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlCell => OpenPyxlCellMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlHyperlink => OpenPyxlHyperlinkMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlComment => OpenPyxlCommentMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlFont => OpenPyxlFontMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlPatternFill => OpenPyxlPatternFillMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlBorder => OpenPyxlBorderMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlSide => OpenPyxlSideMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlAlignment => OpenPyxlAlignmentMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlProtection => OpenPyxlProtectionMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlNamedStyle => OpenPyxlNamedStyleMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlColor => OpenPyxlColorMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlTable => OpenPyxlTableMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlTableStyleInfo => OpenPyxlTableStyleInfoMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlDataValidation => OpenPyxlDataValidationMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlConditionalFormattingRule => OpenPyxlConditionalFormattingRuleMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlAutoFilter => OpenPyxlAutoFilterMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlSheetProtection => OpenPyxlSheetProtectionMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlWorkbookProtection => OpenPyxlWorkbookProtectionMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlDrawing => OpenPyxlDrawingMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlChart => OpenPyxlChartMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlImage => OpenPyxlImageMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlSheetView => OpenPyxlSheetViewMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlSelection => OpenPyxlSelectionMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlPageMargins => OpenPyxlPageMarginsMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlPageSetup => OpenPyxlPageSetupMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlTableCollection => OpenPyxlTableCollectionMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlDataValidationList => OpenPyxlDataValidationListMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlConditionalFormattingCollection => OpenPyxlConditionalFormattingCollectionMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlColumnDimension => OpenPyxlColumnDimensionMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlRowDimension => OpenPyxlRowDimensionMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlMergedCellSet => OpenPyxlMergedCellSetMembers.Contains(memberName),
            AbstractValueKind.UserInstance => (value.RequireInstanceSummary()).Class.IsDataclass
                ? HasKnownDataclassInstanceMember(value.RequireInstanceSummary(), memberName)
                : null,
            _ => null
        };

        return hasMember == false;
    }

    private static bool HasKnownDataclassInstanceMember(AbstractInstanceSummary instance, string memberName)
        => instance.Class.IsDataclass &&
           (instance.Class.FieldsByName.TryGetValue(memberName, out var field) && field.StoreOnInstance ||
            instance.Class.Methods.ContainsKey(memberName));
}
