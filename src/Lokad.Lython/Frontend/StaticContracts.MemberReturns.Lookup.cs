namespace Lokad.Lython.Frontend;

internal static partial class StaticContracts
{
    private static StaticReturnShape? LookupFallbackMemberReturn(AbstractValueKind kind, string name) => kind switch
    {
        AbstractValueKind.ArgparseParser => GetArgparseParserMemberReturn(name),
        AbstractValueKind.BinaryFileHandle => GetBinaryFileHandleMemberReturn(name),
        AbstractValueKind.BytesIO => GetBytesIOMemberReturn(name),
        AbstractValueKind.CollectionsChainMap => GetCollectionsChainMapMemberReturn(name),
        AbstractValueKind.CollectionsCounter => GetCollectionsCounterMemberReturn(name),
        AbstractValueKind.CollectionsDefaultDict => GetCollectionsDefaultDictMemberReturn(name),
        AbstractValueKind.CollectionsDeque => GetCollectionsDequeMemberReturn(name),
        AbstractValueKind.CsvDictReader => GetCsvDictReaderMemberReturn(name),
        AbstractValueKind.CsvDictWriter => GetCsvDictWriterMemberReturn(name),
        AbstractValueKind.CsvReader when name == "line_num" => StaticReturnShape.Integer,
        AbstractValueKind.CsvWriter => GetCsvWriterMemberReturn(name),
        AbstractValueKind.DateTimeDate => GetDateTimeDateMemberReturn(name),
        AbstractValueKind.DateTimeDateTime => GetDateTimeDateTimeMemberReturn(name),
        AbstractValueKind.DateTimeTime => GetDateTimeTimeMemberReturn(name),
        AbstractValueKind.DateTimeTimedelta when name == "total_seconds" => StaticReturnShape.Float,
        AbstractValueKind.DateTimeTimezone => GetDateTimeTimezoneMemberReturn(name),
        AbstractValueKind.Decimal => GetDecimalMemberReturn(name),
        AbstractValueKind.DecimalContext => GetDecimalContextMemberReturn(name),
        AbstractValueKind.Dict => GetDictMemberReturn(name),
        AbstractValueKind.DifflibDiffer when name == "compare" => StaticReturnShape.ListOfString,
        AbstractValueKind.DifflibHtmlDiff => GetDifflibHtmlDiffMemberReturn(name),
        AbstractValueKind.DifflibSequenceMatcher => GetDifflibSequenceMatcherMemberReturn(name),
        AbstractValueKind.OpenPyxlCell when name == "offset" => StaticReturnShape.OpenPyxlCell,
        AbstractValueKind.OpenPyxlChart => GetOpenPyxlChartMemberReturn(name),
        AbstractValueKind.OpenPyxlConditionalFormattingCollection => GetOpenPyxlConditionalFormattingCollectionMemberReturn(name),
        AbstractValueKind.OpenPyxlDataValidation when name == "add" => StaticReturnShape.None,
        AbstractValueKind.OpenPyxlDataValidationList when name == "append" => StaticReturnShape.None,
        AbstractValueKind.OpenPyxlSheetProtection => GetOpenPyxlSheetProtectionMemberReturn(name),
        AbstractValueKind.OpenPyxlTableCollection => GetOpenPyxlTableCollectionMemberReturn(name),
        AbstractValueKind.OpenPyxlWorkbook => GetOpenPyxlWorkbookMemberReturn(name),
        AbstractValueKind.OpenPyxlWorkbookProtection => GetOpenPyxlWorkbookProtectionMemberReturn(name),
        AbstractValueKind.OpenPyxlWorksheet => GetOpenPyxlWorksheetMemberReturn(name),
        AbstractValueKind.Path => GetPathMemberReturn(name),
        AbstractValueKind.PkgutilLoader => GetPkgutilLoaderMemberReturn(name),
        AbstractValueKind.PkgutilModuleInfo => GetPkgutilModuleInfoMemberReturn(name),
        AbstractValueKind.Random => GetRandomMemberReturn(name),
        AbstractValueKind.RegexPattern => GetRegexPatternMemberReturn(name),
        AbstractValueKind.Set => GetSetMemberReturn(name),
        AbstractValueKind.SetType => GetSetTypeMemberReturn(name),
        AbstractValueKind.StatisticsNormalDist => GetStatisticsNormalDistMemberReturn(name),
        AbstractValueKind.StringIO => GetStringIOMemberReturn(name),
        AbstractValueKind.SubprocessCompletedProcess when name == "check_returncode" => StaticReturnShape.None,
        AbstractValueKind.SubprocessPopen => GetSubprocessPopenMemberReturn(name),
        AbstractValueKind.TextFileHandle => GetTextFileHandleMemberReturn(name),
        _ => null,
    };

    private static StaticReturnShape? GetArgparseParserMemberReturn(string name) => name switch
    {
        "add_mutually_exclusive_group" => StaticReturnShape.ArgparseMutuallyExclusiveGroup,
        "parse_args" => StaticReturnShape.ArgparseNamespace,
        "parse_known_args" or "get_default" or "add_subparsers" => StaticReturnShape.Unknown,
        "format_usage" or "format_help" => StaticReturnShape.String,
        "print_usage" or "print_help" or "error" or "exit" or "set_defaults" => StaticReturnShape.None,
        _ => null,
    };

    private static StaticReturnShape? GetBinaryFileHandleMemberReturn(string name) => name switch
    {
        "close" or "flush" or "writelines" => StaticReturnShape.None,
        "readable" or "writable" or "seekable" => StaticReturnShape.Boolean,
        "tell" or "write" => StaticReturnShape.Integer,
        "seek" => StaticReturnShape.Unknown,
        "read" or "readline" or "__next__" => StaticReturnShape.Bytes,
        "readlines" => StaticReturnShape.ListOfBytes,
        _ => null,
    };

    private static StaticReturnShape? GetBytesIOMemberReturn(string name) => name switch
    {
        "read" or "read1" or "readline" or "getvalue" or "__next__" => StaticReturnShape.Bytes,
        "readlines" => StaticReturnShape.ListOfBytes,
        "write" or "seek" or "tell" or "truncate" => StaticReturnShape.Integer,
        "writelines" or "close" or "flush" or "__exit__" => StaticReturnShape.None,
        "readable" or "writable" or "seekable" => StaticReturnShape.Boolean,
        "__iter__" or "__enter__" => StaticReturnShape.BytesIO,
        _ => null,
    };

    private static StaticReturnShape? GetCollectionsChainMapMemberReturn(string name) => name switch
    {
        "get" or "keys" or "values" or "items" => StaticReturnShape.Unknown,
        "new_child" or "copy" => StaticReturnShape.CollectionsChainMap,
        "update" or "__setitem__" or "__delitem__" => StaticReturnShape.None,
        "__len__" => StaticReturnShape.Integer,
        _ => null,
    };

    private static StaticReturnShape? GetCollectionsCounterMemberReturn(string name) => name switch
    {
        "get" or "elements" or "keys" or "values" or "items" => StaticReturnShape.Unknown,
        "update" or "subtract" or "__setitem__" or "__delitem__" or "clear" => StaticReturnShape.None,
        "total" or "__len__" => StaticReturnShape.Integer,
        "most_common" => StaticReturnShape.ListOfUnknown,
        "copy" => StaticReturnShape.CollectionsCounter,
        _ => null,
    };

    private static StaticReturnShape? GetCollectionsDefaultDictMemberReturn(string name) => name switch
    {
        "get" or "keys" or "values" or "items" or "setdefault" => StaticReturnShape.Unknown,
        "copy" => StaticReturnShape.CollectionsDefaultDict,
        "__len__" => StaticReturnShape.Integer,
        "__setitem__" or "__delitem__" or "clear" => StaticReturnShape.None,
        _ => null,
    };

    private static StaticReturnShape? GetCollectionsDequeMemberReturn(string name) => name switch
    {
        "append" or "appendleft" or "extend" or "extendleft" or "clear" or "__setitem__" or "__delitem__" or
            "insert" or "remove" or "reverse" or "rotate"
            => StaticReturnShape.None,
        "pop" or "popleft" => StaticReturnShape.Unknown,
        "copy" => StaticReturnShape.CollectionsDeque,
        "__len__" or "count" or "index" => StaticReturnShape.Integer,
        _ => null,
    };

    private static StaticReturnShape? GetCsvDictReaderMemberReturn(string name) => name switch
    {
        "fieldnames" or "line_num" => StaticReturnShape.Unknown,
        _ => null,
    };

    private static StaticReturnShape? GetCsvDictWriterMemberReturn(string name) => name switch
    {
        "writeheader" or "writerow" => StaticReturnShape.Unknown,
        "writerows" => StaticReturnShape.None,
        _ => null,
    };

    private static StaticReturnShape? GetCsvWriterMemberReturn(string name) => name switch
    {
        "writerow" => StaticReturnShape.Unknown,
        "writerows" => StaticReturnShape.None,
        "getvalue" => StaticReturnShape.String,
        _ => null,
    };

    private static StaticReturnShape? GetDateTimeDateMemberReturn(string name) => name switch
    {
        "weekday" or "isoweekday" or "toordinal" => StaticReturnShape.Integer,
        "isocalendar" or "timetuple" => StaticReturnShape.Unknown,
        "ctime" or "isoformat" or "__format__" or "strftime" => StaticReturnShape.String,
        "replace" => StaticReturnShape.DateTimeDate,
        _ => null,
    };

    private static StaticReturnShape? GetDateTimeDateTimeMemberReturn(string name) => name switch
    {
        "date" => StaticReturnShape.DateTimeDate,
        "time" or "timetz" => StaticReturnShape.DateTimeTime,
        "weekday" or "isoweekday" or "toordinal" => StaticReturnShape.Integer,
        "isocalendar" or "timetuple" or "utctimetuple" or "utcoffset" or "tzname" => StaticReturnShape.Unknown,
        "ctime" or "isoformat" or "__format__" or "strftime" => StaticReturnShape.String,
        "timestamp" => StaticReturnShape.Float,
        "dst" => StaticReturnShape.None,
        "astimezone" or "replace" => StaticReturnShape.DateTimeDateTime,
        _ => null,
    };

    private static StaticReturnShape? GetDateTimeTimeMemberReturn(string name) => name switch
    {
        "utcoffset" or "tzname" => StaticReturnShape.Unknown,
        "dst" => StaticReturnShape.None,
        "isoformat" or "__format__" or "strftime" => StaticReturnShape.String,
        "replace" => StaticReturnShape.DateTimeTime,
        _ => null,
    };

    private static StaticReturnShape? GetDateTimeTimezoneMemberReturn(string name) => name switch
    {
        "utcoffset" => StaticReturnShape.DateTimeTimedelta,
        "tzname" => StaticReturnShape.String,
        "dst" => StaticReturnShape.None,
        _ => null,
    };

    private static StaticReturnShape? GetDecimalMemberReturn(string name) => name switch
    {
        "quantize" or "normalize" or "sqrt" or "exp" or "ln" or "log10" or "copy_abs" or "copy_negate" or
            "copy_sign" or "to_integral_value" or "to_integral_exact" or "to_integral" or "compare" or
            "compare_total" or "scaleb" or "shift" or "rotate" or "fma" or "logb" or "compare_signal" or
            "radix" or "canonical" or "conjugate" or "remainder_near" or "min" or "max" or "min_mag" or
            "max_mag"
            => StaticReturnShape.Decimal,
        "as_tuple" => StaticReturnShape.DecimalTuple,
        "adjusted" => StaticReturnShape.Integer,
        "is_nan" or "is_infinite" or "is_finite" or "is_zero" or "is_signed" or "same_quantum" or
            "is_qnan" or "is_snan" or "is_canonical"
            => StaticReturnShape.Boolean,
        "to_eng_string" => StaticReturnShape.String,
        _ => null,
    };

    private static StaticReturnShape? GetDecimalContextMemberReturn(string name) => name switch
    {
        "copy" => StaticReturnShape.DecimalContext,
        "clear_flags" => StaticReturnShape.None,
        "create_decimal" or "create_decimal_from_float" => StaticReturnShape.Decimal,
        _ => null,
    };

    private static StaticReturnShape? GetDictMemberReturn(string name) => name switch
    {
        "copy" => StaticReturnShape.DictSame,
        "__len__" => StaticReturnShape.Integer,
        "__setitem__" or "__delitem__" or "clear" or "update" => StaticReturnShape.None,
        _ => null,
    };

    private static StaticReturnShape? GetDifflibHtmlDiffMemberReturn(string name) => name switch
    {
        "make_table" or "make_file" => StaticReturnShape.String,
        _ => null,
    };

    private static StaticReturnShape? GetDifflibSequenceMatcherMemberReturn(string name) => name switch
    {
        "set_seqs" or "set_seq1" or "set_seq2" => StaticReturnShape.None,
        "find_longest_match" or "get_grouped_opcodes" => StaticReturnShape.Unknown,
        "get_matching_blocks" or "get_opcodes" => StaticReturnShape.ListOfUnknown,
        "ratio" or "quick_ratio" or "real_quick_ratio" => StaticReturnShape.Float,
        _ => null,
    };

    private static StaticReturnShape? GetOpenPyxlChartMemberReturn(string name) => name switch
    {
        "add_data" or "set_categories" or "append" => StaticReturnShape.None,
        _ => null,
    };

    private static StaticReturnShape? GetOpenPyxlConditionalFormattingCollectionMemberReturn(string name) => name switch
    {
        "items" => StaticReturnShape.ListOfUnknown,
        "add" => StaticReturnShape.None,
        _ => null,
    };

    private static StaticReturnShape? GetOpenPyxlSheetProtectionMemberReturn(string name) => name switch
    {
        "enable" or "disable" => StaticReturnShape.None,
        _ => null,
    };

    private static StaticReturnShape? GetOpenPyxlTableCollectionMemberReturn(string name) => name switch
    {
        "keys" => StaticReturnShape.ListOfString,
        "values" or "items" => StaticReturnShape.ListOfUnknown,
        _ => null,
    };

    private static StaticReturnShape? GetOpenPyxlWorkbookMemberReturn(string name) => name switch
    {
        "add_named_style" or "remove" or "remove_sheet" or "move_sheet" or "save" or "close"
            => StaticReturnShape.None,
        "create_sheet" or "copy_worksheet" => StaticReturnShape.OpenPyxlWorksheet,
        "index" => StaticReturnShape.Integer,
        "get_sheet_names" => StaticReturnShape.ListOfString,
        _ => null,
    };

    private static StaticReturnShape? GetOpenPyxlWorkbookProtectionMemberReturn(string name) => name switch
    {
        "set_workbook_password" or "set_revisions_password" => StaticReturnShape.None,
        _ => null,
    };

    private static StaticReturnShape? GetOpenPyxlWorksheetMemberReturn(string name) => name switch
    {
        "set_printer_settings" or "add_table" or "add_data_validation" or "add_chart" or "add_image" or
            "append" or "merge_cells" or "unmerge_cells" or "insert_rows" or "delete_rows" or
            "insert_cols" or "delete_cols" or "move_range"
            => StaticReturnShape.None,
        "cell" => StaticReturnShape.OpenPyxlCell,
        "iter_rows" or "iter_cols" => StaticReturnShape.ListOfUnknown,
        "calculate_dimension" => StaticReturnShape.String,
        _ => null,
    };

    private static StaticReturnShape? GetPathMemberReturn(string name) => name switch
    {
        "__fspath__" or "read_text" or "as_posix" or "owner" or "group" => StaticReturnShape.String,
        "resolve" or "absolute" or "expanduser" or "joinpath" or "relative_to" or "with_suffix" or
            "with_name" or "with_stem" or "rename" or "replace"
            => StaticReturnShape.Path,
        "exists" or "is_file" or "is_dir" or "is_symlink" or "is_mount" or "is_reserved" or
            "is_relative_to" or "samefile"
            => StaticReturnShape.Boolean,
        "stat" or "lstat" or "readlink" => StaticReturnShape.Unknown,
        "unlink" or "rmdir" or "touch" or "mkdir" or "symlink_to" or "hardlink_to" or "chmod"
            => StaticReturnShape.None,
        "read_bytes" => StaticReturnShape.Bytes,
        "write_bytes" => StaticReturnShape.Integer,
        _ => null,
    };

    private static StaticReturnShape? GetPkgutilLoaderMemberReturn(string name) => name switch
    {
        "is_package" => StaticReturnShape.Boolean,
        "get_source" => StaticReturnShape.Unknown,
        _ => null,
    };

    private static StaticReturnShape? GetPkgutilModuleInfoMemberReturn(string name) => name switch
    {
        "_asdict" => StaticReturnShape.Unknown,
        "_replace" => StaticReturnShape.PkgutilModuleInfo,
        "count" or "index" => StaticReturnShape.Integer,
        _ => null,
    };

    private static StaticReturnShape? GetRandomMemberReturn(string name) => name switch
    {
        "seed" or "setstate" or "shuffle" => StaticReturnShape.None,
        "random" or "uniform" or "triangular" or "betavariate" or "expovariate" or "gammavariate" or
            "gauss" or "normalvariate" or "lognormvariate" or "paretovariate" or "vonmisesvariate" or
            "weibullvariate"
            => StaticReturnShape.Float,
        "getstate" or "choice" => StaticReturnShape.Unknown,
        "randrange" or "randint" or "getrandbits" => StaticReturnShape.Integer,
        "choices" or "sample" => StaticReturnShape.ListOfUnknown,
        "randbytes" => StaticReturnShape.Bytes,
        _ => null,
    };

    private static StaticReturnShape? GetRegexPatternMemberReturn(string name) => name switch
    {
        "sub" => StaticReturnShape.String,
        "split" => StaticReturnShape.ListOfString,
        "search" or "match" or "fullmatch" => StaticReturnShape.MaybeRegexMatch,
        _ => null,
    };

    private static StaticReturnShape? GetSetMemberReturn(string name) => name switch
    {
        "copy" or "__or__" or "__ror__" or "__and__" or "__rand__" or "__sub__" or "__rsub__" or
            "__xor__" or "__rxor__" or "__ior__" or "__iand__" or "__isub__" or "__ixor__" or "union" or
            "intersection" or "difference" or "symmetric_difference"
            => StaticReturnShape.SetSame,
        "__len__" => StaticReturnShape.Integer,
        "clear" or "add" or "discard" or "remove" or "update" or "intersection_update" or
            "difference_update" or "symmetric_difference_update"
            => StaticReturnShape.None,
        "isdisjoint" or "issubset" or "issuperset" => StaticReturnShape.Boolean,
        _ => null,
    };

    private static StaticReturnShape? GetSetTypeMemberReturn(string name) => name switch
    {
        "copy" or "union" or "intersection" or "difference" or "symmetric_difference" => StaticReturnShape.SetSame,
        "clear" or "add" or "discard" or "remove" or "update" or "intersection_update" or
            "difference_update" or "symmetric_difference_update"
            => StaticReturnShape.None,
        "isdisjoint" or "issubset" or "issuperset" => StaticReturnShape.Boolean,
        _ => null,
    };

    private static StaticReturnShape? GetStatisticsNormalDistMemberReturn(string name) => name switch
    {
        "zscore" or "pdf" or "cdf" or "inv_cdf" or "overlap" => StaticReturnShape.Float,
        "quantiles" or "samples" => StaticReturnShape.ListOfFloat,
        _ => null,
    };

    private static StaticReturnShape? GetStringIOMemberReturn(string name) => name switch
    {
        "read" or "readline" or "getvalue" or "__next__" => StaticReturnShape.String,
        "readlines" => StaticReturnShape.ListOfString,
        "write" or "seek" or "tell" or "truncate" => StaticReturnShape.Integer,
        "writelines" or "close" or "flush" or "__exit__" => StaticReturnShape.None,
        "readable" or "writable" or "seekable" => StaticReturnShape.Boolean,
        "__iter__" or "__enter__" => StaticReturnShape.StringIO,
        _ => null,
    };

    private static StaticReturnShape? GetSubprocessPopenMemberReturn(string name) => name switch
    {
        "communicate" or "poll" => StaticReturnShape.Unknown,
        "wait" => StaticReturnShape.Integer,
        "send_signal" or "terminate" or "kill" => StaticReturnShape.None,
        _ => null,
    };

    private static StaticReturnShape? GetTextFileHandleMemberReturn(string name) => name switch
    {
        "close" or "flush" => StaticReturnShape.None,
        "readable" or "writable" or "seekable" => StaticReturnShape.Boolean,
        "tell" => StaticReturnShape.Integer,
        "seek" => StaticReturnShape.Unknown,
        "read" or "readline" or "__next__" => StaticReturnShape.String,
        "readlines" => StaticReturnShape.ListOfString,
        _ => null,
    };
}
