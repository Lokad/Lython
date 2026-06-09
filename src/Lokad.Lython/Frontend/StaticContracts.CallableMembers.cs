namespace Lokad.Lython.Frontend;

internal static partial class StaticContracts
{
    private static readonly StaticCallableContract[] CallableContracts = CreateCallableContracts();

    public static bool TryGetCallableContract(AbstractValue receiver, string memberName, out StaticCallableContract contract)
    {
        foreach (var candidate in CallableContracts)
        {
            if (candidate.ReceiverKind == receiver.Kind &&
                candidate.MemberName == memberName)
            {
                contract = candidate;
                return true;
            }
        }

        contract = default;
        return false;
    }

    public static bool IsMutatingMember(AbstractValue receiver, string memberName)
        => TryGetCallableContract(receiver, memberName, out var contract) &&
           contract.Mutation == StaticMutationKind.MutatesReceiver;

    private static StaticCallableContract[] CreateCallableContracts()
    {
        var contracts = new List<StaticCallableContract>
        {
            new(AbstractValueKind.Path, "is_absolute", 0, 0, "LA3114", "Path.is_absolute() expects no arguments."),
            new(AbstractValueKind.Path, "as_posix", 0, 0, "LA3114", "Path.as_posix() expects no arguments."),
            new(AbstractValueKind.Path, "resolve", 0, 0, "LA3114", "Path.resolve() expects no arguments."),
            new(AbstractValueKind.Path, "exists", 0, 0, "LA3114", "Path.exists() expects no arguments."),
            new(AbstractValueKind.Path, "is_file", 0, 0, "LA3114", "Path.is_file() expects no arguments."),
            new(AbstractValueKind.Path, "is_dir", 0, 0, "LA3114", "Path.is_dir() expects no arguments."),
            new(AbstractValueKind.Path, "is_symlink", 0, 0, "LA3114", "Path.is_symlink() expects no arguments."),
            new(AbstractValueKind.Path, "stat", 0, 0, "LA3114", "Path.stat() expects no arguments."),
            new(AbstractValueKind.Path, "iterdir", 0, 0, "LA3114", "Path.iterdir() expects no arguments."),
            new(AbstractValueKind.Path, "absolute", 0, 0, "LA3114", "Path.absolute() expects no arguments."),
            new(AbstractValueKind.Path, "unlink", 0, 1, "LA3114", "Path.unlink([missing_ok]) expects zero or one argument.", ParameterNames: ["missing_ok"]),
            new(AbstractValueKind.Path, "rmdir", 0, 0, "LA3114", "Path.rmdir() expects no arguments."),
            new(AbstractValueKind.Path, "touch", 0, 2, "LA3114", "Path.touch([mode][, exist_ok]) expects zero to two arguments.", ParameterNames: ["mode", "exist_ok"]),
            new(AbstractValueKind.Path, "mkdir", 0, 3, "LA3114", "Path.mkdir([mode][, parents][, exist_ok]) expects zero to three arguments.", ParameterNames: ["mode", "parents", "exist_ok"]),
            new(AbstractValueKind.Path, "read_text", 0, 2, "LA3114", "Path.read_text([encoding][, errors]) expects zero to two arguments.", ParameterNames: ["encoding", "errors"]),
            new(AbstractValueKind.Path, "open", 0, 4, "LA3114", "Path.open([mode][, encoding][, errors][, newline]) expects zero to four arguments.", ParameterNames: ["mode", "encoding", "errors", "newline"]),
            new(AbstractValueKind.Path, "write_text", 1, 4, "LA3114", "Path.write_text(text[, encoding][, errors][, newline]) expects one to four arguments.", ParameterNames: ["text", "encoding", "errors", "newline"]),
            new(AbstractValueKind.Path, "match", 1, 1, "LA3114", "Path.match(pattern) expects one argument.", ParameterNames: ["pattern"]),
            new(AbstractValueKind.Path, "relative_to", 1, 1, "LA3114", "Path.relative_to(other) expects one argument.", ParameterNames: ["other"]),
            new(AbstractValueKind.Path, "with_suffix", 1, 1, "LA3114", "Path.with_suffix(suffix) expects one argument.", ParameterNames: ["suffix"]),
            new(AbstractValueKind.Path, "with_name", 1, 1, "LA3114", "Path.with_name(name) expects one argument.", ParameterNames: ["name"]),
            new(AbstractValueKind.Path, "with_stem", 1, 1, "LA3114", "Path.with_stem(stem) expects one argument.", ParameterNames: ["stem"]),
            new(AbstractValueKind.Path, "is_relative_to", 1, 1, "LA3114", "Path.is_relative_to(other) expects one argument.", ParameterNames: ["other"]),
            new(AbstractValueKind.Path, "samefile", 1, 1, "LA3114", "Path.samefile(other_path) expects one argument.", ParameterNames: ["other_path"]),
            new(AbstractValueKind.Path, "rename", 1, 1, "LA3114", "Path.rename(target) expects one argument.", ParameterNames: ["target"]),
            new(AbstractValueKind.Path, "replace", 1, 1, "LA3114", "Path.replace(target) expects one argument.", ParameterNames: ["target"]),
            new(AbstractValueKind.Path, "glob", 1, 1, "LA3114", "Path.glob(pattern) expects one argument.", ParameterNames: ["pattern"]),
            new(AbstractValueKind.Path, "rglob", 1, 1, "LA3114", "Path.rglob(pattern) expects one argument.", ParameterNames: ["pattern"]),
            new(AbstractValueKind.Path, "joinpath", 1, null, "LA3114", "Path.joinpath(*other) expects at least one argument."),
            new(AbstractValueKind.TextFileHandle, "close", 0, 0, "LA3108", "file.close() expects no arguments."),
            new(AbstractValueKind.TextFileHandle, "read", 0, 0, "LA3108", "file.read() expects no arguments."),
            new(AbstractValueKind.TextFileHandle, "readline", 0, 0, "LA3108", "file.readline() expects no arguments."),
            new(AbstractValueKind.TextFileHandle, "readlines", 0, 0, "LA3108", "file.readlines() expects no arguments."),
            new(AbstractValueKind.TextFileHandle, "write", 1, 1, "LA3111", "file.write(text) expects one string argument.", ParameterNames: ["text"]),
            new(AbstractValueKind.TextFileHandle, "writelines", 1, 1, "LA3112", "file.writelines(lines) expects one iterable of strings argument.", ParameterNames: ["lines"]),
            new(AbstractValueKind.TextFileHandle, "flush", 0, 0, "LA3108", "file.flush() expects no arguments."),
        };

        AddStringCallableContracts(contracts);
        AddListCallableContracts(contracts);

        contracts.AddRange(
        [
            new(AbstractValueKind.Dict, "get", 1, 2, "LA3126", "dict.get(key[, default]) expects one key and an optional default.", ParameterNames: ["key", "default"]),
            new(AbstractValueKind.Dict, "keys", 0, 0, "LA3127", "dict.keys() expects no arguments."),
            new(AbstractValueKind.Dict, "values", 0, 0, "LA3128", "dict.values() expects no arguments."),
            new(AbstractValueKind.Dict, "items", 0, 0, "LA3129", "dict.items() expects no arguments."),
            new(AbstractValueKind.Dict, "update", 1, 1, "LA3130", "dict.update(mapping) expects one dictionary argument.", StaticMutationKind.MutatesReceiver, ["mapping"]),
            new(AbstractValueKind.Dict, "pop", 1, 1, "LA3131", "dict.pop(key) expects one key.", StaticMutationKind.MutatesReceiver, ["key"]),
            new(AbstractValueKind.Dict, "copy", 0, 0, "LA3132", "dict.copy() expects no arguments."),
            new(AbstractValueKind.Dict, "clear", 0, 0, "LA3133", "dict.clear() expects no arguments.", StaticMutationKind.MutatesReceiver),
            new(AbstractValueKind.Dict, "setdefault", 1, 2, "LA3134", "dict.setdefault(key[, default]) expects one key and an optional default.", StaticMutationKind.MutatesReceiver, ["key", "default"]),
            new(AbstractValueKind.Set, "add", 1, 1, "LA3135", "set.add(value) expects one argument.", StaticMutationKind.MutatesReceiver, ["value"]),
            new(AbstractValueKind.Set, "discard", 1, 1, "LA3136", "set.discard(value) expects one argument.", StaticMutationKind.MutatesReceiver, ["value"]),
            new(AbstractValueKind.Set, "remove", 1, 1, "LA3137", "set.remove(value) expects one argument.", StaticMutationKind.MutatesReceiver, ["value"]),
            new(AbstractValueKind.Set, "copy", 0, 0, "LA3138", "set.copy() expects no arguments."),
            new(AbstractValueKind.Set, "clear", 0, 0, "LA3139", "set.clear() expects no arguments.", StaticMutationKind.MutatesReceiver),
            new(AbstractValueKind.RegexPattern, "search", 1, 1, "LA3152", "pattern.search(string) expects one argument.", ParameterNames: ["string"]),
            new(AbstractValueKind.RegexPattern, "match", 1, 1, "LA3152", "pattern.match(string) expects one argument.", ParameterNames: ["string"]),
            new(AbstractValueKind.RegexPattern, "fullmatch", 1, 1, "LA3152", "pattern.fullmatch(string) expects one argument.", ParameterNames: ["string"]),
            new(AbstractValueKind.RegexPattern, "findall", 1, 1, "LA3152", "pattern.findall(string) expects one argument.", ParameterNames: ["string"]),
            new(AbstractValueKind.RegexPattern, "finditer", 1, 1, "LA3152", "pattern.finditer(string) expects one argument.", ParameterNames: ["string"]),
            new(AbstractValueKind.RegexPattern, "sub", 2, 3, "LA3152", "pattern.sub(repl, string[, count]) expects two or three arguments.", ParameterNames: ["repl", "string", "count"]),
            new(AbstractValueKind.RegexPattern, "subn", 2, 3, "LA3152", "pattern.subn(repl, string[, count]) expects two or three arguments.", ParameterNames: ["repl", "string", "count"]),
            new(AbstractValueKind.RegexPattern, "split", 1, 2, "LA3152", "pattern.split(string[, maxsplit]) expects one or two arguments.", ParameterNames: ["string", "maxsplit"]),
            new(AbstractValueKind.RegexMatch, "group", 0, null, "LA3152", "match.group([index...]) expects zero or more positional group identifiers."),
            new(AbstractValueKind.RegexMatch, "start", 0, 0, "LA3152", "match.start() expects no arguments."),
            new(AbstractValueKind.RegexMatch, "end", 0, 0, "LA3152", "match.end() expects no arguments."),
            new(AbstractValueKind.RegexMatch, "span", 0, 0, "LA3152", "match.span() expects no arguments."),
            new(AbstractValueKind.ArgparseParser, "add_mutually_exclusive_group", 0, 1, "LA3153", "argparse.ArgumentParser.add_mutually_exclusive_group([required]) expects zero or one argument.", ParameterNames: ["required"]),
            new(AbstractValueKind.ArgparseParser, "parse_args", 0, 1, "LA3154", "argparse.ArgumentParser.parse_args([args]) expects zero or one argument.", ParameterNames: ["args"]),
            new(AbstractValueKind.CsvWriter, "writerow", 1, 1, "LA3155", "csv.writerow(row) expects one argument.", ParameterNames: ["row"]),
            new(AbstractValueKind.CsvWriter, "writerows", 1, 1, "LA3155", "csv.writerows(rows) expects one argument.", ParameterNames: ["rows"]),
            new(AbstractValueKind.CsvWriter, "getvalue", 0, 0, "LA3155", "csv.getvalue() expects no arguments."),
            new(AbstractValueKind.CsvDictWriter, "writeheader", 0, 0, "LA3155", "csv.DictWriter.writeheader() expects no arguments."),
            new(AbstractValueKind.CsvDictWriter, "writerow", 1, 1, "LA3155", "csv.DictWriter.writerow(rowdict) expects one argument.", ParameterNames: ["rowdict"]),
            new(AbstractValueKind.CsvDictWriter, "writerows", 1, 1, "LA3155", "csv.DictWriter.writerows(rowdicts) expects one argument.", ParameterNames: ["rowdicts"]),
            new(AbstractValueKind.DifflibDiffer, "compare", 2, 2, "LA3156", "Differ.compare(a, b) expects two arguments.", ParameterNames: ["a", "b"]),
            new(AbstractValueKind.DifflibHtmlDiff, "make_table", 2, 6, "LA3156", "HtmlDiff.make_table(fromlines, tolines[, fromdesc][, todesc][, context][, numlines]) expects two to six arguments.", ParameterNames: ["fromlines", "tolines", "fromdesc", "todesc", "context", "numlines"]),
            new(AbstractValueKind.DifflibHtmlDiff, "make_file", 2, 7, "LA3156", "HtmlDiff.make_file(fromlines, tolines[, fromdesc][, todesc][, context][, numlines][, charset]) expects two to seven arguments.", ParameterNames: ["fromlines", "tolines", "fromdesc", "todesc", "context", "numlines", "charset"]),
            new(AbstractValueKind.DifflibSequenceMatcher, "set_seqs", 2, 2, "LA3156", "SequenceMatcher.set_seqs(a, b) expects two arguments.", StaticMutationKind.MutatesReceiver, ["a", "b"]),
            new(AbstractValueKind.DifflibSequenceMatcher, "set_seq1", 1, 1, "LA3156", "SequenceMatcher.set_seq1(a) expects one argument.", StaticMutationKind.MutatesReceiver, ["a"]),
            new(AbstractValueKind.DifflibSequenceMatcher, "set_seq2", 1, 1, "LA3156", "SequenceMatcher.set_seq2(b) expects one argument.", StaticMutationKind.MutatesReceiver, ["b"]),
            new(AbstractValueKind.DifflibSequenceMatcher, "find_longest_match", 0, 4, "LA3156", "SequenceMatcher.find_longest_match([alo][, ahi][, blo][, bhi]) expects zero to four arguments.", ParameterNames: ["alo", "ahi", "blo", "bhi"]),
            new(AbstractValueKind.DifflibSequenceMatcher, "get_matching_blocks", 0, 0, "LA3156", "SequenceMatcher.get_matching_blocks() expects no arguments."),
            new(AbstractValueKind.DifflibSequenceMatcher, "get_opcodes", 0, 0, "LA3156", "SequenceMatcher.get_opcodes() expects no arguments."),
            new(AbstractValueKind.DifflibSequenceMatcher, "get_grouped_opcodes", 0, 1, "LA3156", "SequenceMatcher.get_grouped_opcodes([n]) expects zero or one argument.", ParameterNames: ["n"]),
            new(AbstractValueKind.DifflibSequenceMatcher, "ratio", 0, 0, "LA3156", "SequenceMatcher.ratio() expects no arguments."),
            new(AbstractValueKind.DifflibSequenceMatcher, "quick_ratio", 0, 0, "LA3156", "SequenceMatcher.quick_ratio() expects no arguments."),
            new(AbstractValueKind.DifflibSequenceMatcher, "real_quick_ratio", 0, 0, "LA3156", "SequenceMatcher.real_quick_ratio() expects no arguments."),
            new(AbstractValueKind.SubprocessCompletedProcess, "check_returncode", 0, 0, "LA3160", "CompletedProcess.check_returncode() expects no arguments."),
            new(AbstractValueKind.OpenPyxlWorkbook, "add_named_style", 1, 1, "LA3161", "Workbook.add_named_style(style) expects one argument.", StaticMutationKind.MutatesReceiver, ["style"]),
            new(AbstractValueKind.OpenPyxlWorkbook, "create_sheet", 0, 2, "LA3161", "Workbook.create_sheet([title][, index]) expects zero to two arguments.", StaticMutationKind.MutatesReceiver, ["title", "index"]),
            new(AbstractValueKind.OpenPyxlWorkbook, "remove", 1, 1, "LA3161", "Workbook.remove(worksheet) expects one worksheet argument.", StaticMutationKind.MutatesReceiver, ["worksheet"]),
            new(AbstractValueKind.OpenPyxlWorkbook, "remove_sheet", 1, 1, "LA3161", "Workbook.remove_sheet(worksheet) expects one worksheet argument.", StaticMutationKind.MutatesReceiver, ["worksheet"]),
            new(AbstractValueKind.OpenPyxlWorkbook, "copy_worksheet", 1, 1, "LA3161", "Workbook.copy_worksheet(from_worksheet) expects one worksheet argument.", StaticMutationKind.MutatesReceiver, ["from_worksheet"]),
            new(AbstractValueKind.OpenPyxlWorkbook, "index", 1, 1, "LA3161", "Workbook.index(worksheet) expects one worksheet argument.", ParameterNames: ["worksheet"]),
            new(AbstractValueKind.OpenPyxlWorkbook, "move_sheet", 1, 2, "LA3161", "Workbook.move_sheet(sheet[, offset]) expects one or two arguments.", StaticMutationKind.MutatesReceiver, ["sheet", "offset"]),
            new(AbstractValueKind.OpenPyxlWorkbook, "get_sheet_names", 0, 0, "LA3161", "Workbook.get_sheet_names() expects no arguments."),
            new(AbstractValueKind.OpenPyxlWorkbook, "save", 1, 1, "LA3161", "Workbook.save(filename) expects one argument.", StaticMutationKind.MutatesReceiver, ["filename"]),
            new(AbstractValueKind.OpenPyxlWorkbook, "close", 0, 0, "LA3161", "Workbook.close() expects no arguments."),
            new(AbstractValueKind.OpenPyxlWorksheet, "set_printer_settings", 2, 2, "LA3161", "Worksheet.set_printer_settings(paper_size, orientation) expects two arguments.", StaticMutationKind.MutatesReceiver, ["paper_size", "orientation"]),
            new(AbstractValueKind.OpenPyxlWorksheet, "add_table", 1, 1, "LA3161", "Worksheet.add_table(table) expects one table argument.", StaticMutationKind.MutatesReceiver, ["table"]),
            new(AbstractValueKind.OpenPyxlWorksheet, "add_data_validation", 1, 1, "LA3161", "Worksheet.add_data_validation(data_validation) expects one data validation argument.", StaticMutationKind.MutatesReceiver, ["data_validation"]),
            new(AbstractValueKind.OpenPyxlWorksheet, "add_chart", 1, 2, "LA3161", "Worksheet.add_chart(chart[, anchor]) expects one or two arguments.", StaticMutationKind.MutatesReceiver, ["chart", "anchor"]),
            new(AbstractValueKind.OpenPyxlWorksheet, "add_image", 1, 2, "LA3161", "Worksheet.add_image(img[, anchor]) expects one or two arguments.", StaticMutationKind.MutatesReceiver, ["img", "anchor"]),
            new(AbstractValueKind.OpenPyxlWorksheet, "cell", 2, 3, "LA3161", "Worksheet.cell(row, column[, value]) expects two or three arguments.", StaticMutationKind.MutatesReceiver, ["row", "column", "value"]),
            new(AbstractValueKind.OpenPyxlWorksheet, "append", 1, 1, "LA3161", "Worksheet.append(iterable) expects one argument.", StaticMutationKind.MutatesReceiver, ["iterable"]),
            new(AbstractValueKind.OpenPyxlWorksheet, "iter_rows", 0, 5, "LA3161", "Worksheet.iter_rows(...) expects up to five supported arguments.", ParameterNames: ["min_row", "max_row", "min_col", "max_col", "values_only"]),
            new(AbstractValueKind.OpenPyxlWorksheet, "iter_cols", 0, 5, "LA3161", "Worksheet.iter_cols(...) expects up to five supported arguments.", ParameterNames: ["min_row", "max_row", "min_col", "max_col", "values_only"]),
            new(AbstractValueKind.OpenPyxlWorksheet, "calculate_dimension", 0, 0, "LA3161", "Worksheet.calculate_dimension() expects no arguments."),
            new(AbstractValueKind.OpenPyxlWorksheet, "merge_cells", 0, 5, "LA3161", "Worksheet.merge_cells(...) expects supported range arguments.", StaticMutationKind.MutatesReceiver, ["range_string", "start_row", "start_column", "end_row", "end_column"]),
            new(AbstractValueKind.OpenPyxlWorksheet, "unmerge_cells", 0, 5, "LA3161", "Worksheet.unmerge_cells(...) expects supported range arguments.", StaticMutationKind.MutatesReceiver, ["range_string", "start_row", "start_column", "end_row", "end_column"]),
            new(AbstractValueKind.OpenPyxlWorksheet, "insert_rows", 1, 2, "LA3161", "Worksheet.insert_rows(idx[, amount]) expects one or two arguments.", StaticMutationKind.MutatesReceiver, ["idx", "amount"]),
            new(AbstractValueKind.OpenPyxlWorksheet, "delete_rows", 1, 2, "LA3161", "Worksheet.delete_rows(idx[, amount]) expects one or two arguments.", StaticMutationKind.MutatesReceiver, ["idx", "amount"]),
            new(AbstractValueKind.OpenPyxlWorksheet, "insert_cols", 1, 2, "LA3161", "Worksheet.insert_cols(idx[, amount]) expects one or two arguments.", StaticMutationKind.MutatesReceiver, ["idx", "amount"]),
            new(AbstractValueKind.OpenPyxlWorksheet, "delete_cols", 1, 2, "LA3161", "Worksheet.delete_cols(idx[, amount]) expects one or two arguments.", StaticMutationKind.MutatesReceiver, ["idx", "amount"]),
            new(AbstractValueKind.OpenPyxlWorksheet, "move_range", 1, 4, "LA3161", "Worksheet.move_range(cell_range[, rows][, cols][, translate]) expects one to four arguments.", StaticMutationKind.MutatesReceiver, ["cell_range", "rows", "cols", "translate"]),
            new(AbstractValueKind.OpenPyxlCell, "offset", 0, 2, "LA3161", "Cell.offset([row][, column]) expects zero to two arguments.", ParameterNames: ["row", "column"]),
            new(AbstractValueKind.OpenPyxlTableCollection, "keys", 0, 0, "LA3161", "TableList.keys() expects no arguments."),
            new(AbstractValueKind.OpenPyxlTableCollection, "values", 0, 0, "LA3161", "TableList.values() expects no arguments."),
            new(AbstractValueKind.OpenPyxlTableCollection, "items", 0, 0, "LA3161", "TableList.items() expects no arguments."),
            new(AbstractValueKind.OpenPyxlDataValidation, "add", 1, 1, "LA3161", "DataValidation.add(cell_range) expects one argument.", StaticMutationKind.MutatesReceiver, ["cell_range"]),
            new(AbstractValueKind.OpenPyxlDataValidationList, "append", 1, 1, "LA3161", "DataValidationList.append(data_validation) expects one data validation argument.", StaticMutationKind.MutatesReceiver, ["data_validation"]),
            new(AbstractValueKind.OpenPyxlConditionalFormattingCollection, "items", 0, 0, "LA3161", "ConditionalFormattingList.items() expects no arguments."),
            new(AbstractValueKind.OpenPyxlConditionalFormattingCollection, "add", 2, 2, "LA3161", "ConditionalFormattingList.add(range_string, rule) expects two arguments.", StaticMutationKind.MutatesReceiver, ["range_string", "rule"]),
            new(AbstractValueKind.OpenPyxlChart, "add_data", 1, 3, "LA3161", "Chart.add_data(data[, titles_from_data][, from_rows]) expects one to three arguments.", StaticMutationKind.MutatesReceiver, ["data", "titles_from_data", "from_rows"]),
            new(AbstractValueKind.OpenPyxlChart, "set_categories", 1, 1, "LA3161", "Chart.set_categories(labels) expects one argument.", StaticMutationKind.MutatesReceiver, ["labels"]),
            new(AbstractValueKind.OpenPyxlChart, "append", 1, 1, "LA3161", "Chart.append(value) expects one argument.", StaticMutationKind.MutatesReceiver, ["value"]),
            new(AbstractValueKind.OpenPyxlSheetProtection, "enable", 0, 0, "LA3161", "SheetProtection.enable() expects no arguments.", StaticMutationKind.MutatesReceiver),
            new(AbstractValueKind.OpenPyxlSheetProtection, "disable", 0, 0, "LA3161", "SheetProtection.disable() expects no arguments.", StaticMutationKind.MutatesReceiver),
            new(AbstractValueKind.OpenPyxlWorkbookProtection, "set_workbook_password", 1, 2, "LA3161", "WorkbookProtection.set_workbook_password(value[, already_hashed]) expects one or two arguments.", StaticMutationKind.MutatesReceiver, ["value", "already_hashed"]),
            new(AbstractValueKind.OpenPyxlWorkbookProtection, "set_revisions_password", 1, 2, "LA3161", "WorkbookProtection.set_revisions_password(value[, already_hashed]) expects one or two arguments.", StaticMutationKind.MutatesReceiver, ["value", "already_hashed"]),
        ]);

        return contracts.ToArray();
    }

    private static void AddStringCallableContracts(List<StaticCallableContract> contracts)
    {
        AddStringCallableContract(contracts, "lower", 0, 0, "str.lower() expects no arguments.");
        AddStringCallableContract(contracts, "capitalize", 0, 0, "str.capitalize() expects no arguments.");
        AddStringCallableContract(contracts, "islower", 0, 0, "str.islower() expects no arguments.");
        AddStringCallableContract(contracts, "upper", 0, 0, "str.upper() expects no arguments.");
        AddStringCallableContract(contracts, "swapcase", 0, 0, "str.swapcase() expects no arguments.");
        AddStringCallableContract(contracts, "title", 0, 0, "str.title() expects no arguments.");
        AddStringCallableContract(contracts, "isupper", 0, 0, "str.isupper() expects no arguments.");
        AddStringCallableContract(contracts, "isalpha", 0, 0, "str.isalpha() expects no arguments.");
        AddStringCallableContract(contracts, "isdigit", 0, 0, "str.isdigit() expects no arguments.");
        AddStringCallableContract(contracts, "isalnum", 0, 0, "str.isalnum() expects no arguments.");
        AddStringCallableContract(contracts, "isspace", 0, 0, "str.isspace() expects no arguments.");
        AddStringCallableContract(contracts, "strip", 0, 1, "str.strip([chars]) expects zero or one argument.", "chars");
        AddStringCallableContract(contracts, "lstrip", 0, 1, "str.lstrip([chars]) expects zero or one argument.", "chars");
        AddStringCallableContract(contracts, "rstrip", 0, 1, "str.rstrip([chars]) expects zero or one argument.", "chars");
        AddStringCallableContract(contracts, "center", 1, 2, "str.center(width[, fillchar]) expects one or two arguments.", "width", "fillchar");
        AddStringCallableContract(contracts, "ljust", 1, 2, "str.ljust(width[, fillchar]) expects one or two arguments.", "width", "fillchar");
        AddStringCallableContract(contracts, "rjust", 1, 2, "str.rjust(width[, fillchar]) expects one or two arguments.", "width", "fillchar");
        AddStringCallableContract(contracts, "zfill", 1, 1, "str.zfill(width) expects one argument.", "width");
        AddStringCallableContract(contracts, "expandtabs", 0, 1, "str.expandtabs([tabsize]) expects zero or one argument.", "tabsize");
        AddStringCallableContract(contracts, "replace", 2, 3, "str.replace(old, new[, count]) expects two or three arguments.", "old", "new", "count");
        AddStringCallableContract(contracts, "removeprefix", 1, 1, "str.removeprefix(prefix) expects one argument.", "prefix");
        AddStringCallableContract(contracts, "removesuffix", 1, 1, "str.removesuffix(suffix) expects one argument.", "suffix");
        AddStringCallableContract(contracts, "split", 0, 2, "str.split([separator[, maxsplit]]) expects zero to two arguments.", "separator", "maxsplit");
        AddStringCallableContract(contracts, "rsplit", 0, 2, "str.rsplit([separator[, maxsplit]]) expects zero to two arguments.", "separator", "maxsplit");
        AddStringCallableContract(contracts, "splitlines", 0, 1, "str.splitlines([keepends]) expects zero or one argument.", "keepends");
        AddStringCallableContract(contracts, "startswith", 1, 3, "str.startswith(prefix[, start[, end]]) expects one to three arguments.", "prefix", "start", "end");
        AddStringCallableContract(contracts, "endswith", 1, 3, "str.endswith(suffix[, start[, end]]) expects one to three arguments.", "suffix", "start", "end");
        AddStringCallableContract(contracts, "find", 1, 3, "str.find(sub[, start[, end]]) expects one to three arguments.", "sub", "start", "end");
        AddStringCallableContract(contracts, "index", 1, 3, "str.index(sub[, start[, end]]) expects one to three arguments.", "sub", "start", "end");
        AddStringCallableContract(contracts, "rfind", 1, 3, "str.rfind(sub[, start[, end]]) expects one to three arguments.", "sub", "start", "end");
        AddStringCallableContract(contracts, "rindex", 1, 3, "str.rindex(sub[, start[, end]]) expects one to three arguments.", "sub", "start", "end");
        AddStringCallableContract(contracts, "count", 1, 3, "str.count(sub[, start[, end]]) expects one to three arguments.", "sub", "start", "end");
        AddStringCallableContract(contracts, "partition", 1, 1, "str.partition(sep) expects one argument.", "sep");
        AddStringCallableContract(contracts, "rpartition", 1, 1, "str.rpartition(sep) expects one argument.", "sep");
        AddStringCallableContract(contracts, "join", 1, 1, "str.join(iterable) expects one argument.", "iterable");
        AddStringCallableContract(contracts, "format_map", 1, 1, "str.format_map(mapping) expects one argument.", "mapping");
    }

    private static void AddStringCallableContract(
        List<StaticCallableContract> contracts,
        string memberName,
        int minimumArgumentCount,
        int? maximumArgumentCount,
        string message,
        params string[] parameterNames)
    {
        var parameters = parameterNames.Length == 0 ? null : parameterNames;
        contracts.Add(new StaticCallableContract(AbstractValueKind.String, memberName, minimumArgumentCount, maximumArgumentCount, "LA3147", message, ParameterNames: parameters));
        contracts.Add(new StaticCallableContract(AbstractValueKind.StringType, memberName, minimumArgumentCount, maximumArgumentCount, "LA3147", message, ParameterNames: parameters));
    }

    private static void AddListCallableContracts(List<StaticCallableContract> contracts)
    {
        AddListCallableContract(contracts, "append", 1, 1, "LA3121", "list.append(value) expects one argument.", StaticMutationKind.MutatesReceiver, "value");
        AddListCallableContract(contracts, "extend", 1, 1, "LA3122", "list.extend(iterable) expects one argument.", StaticMutationKind.MutatesReceiver, "iterable");
        AddListCallableContract(contracts, "index", 1, 3, "LA3123", "list.index(value[, start[, stop]]) expects one to three arguments.", parameterNames: ["value", "start", "stop"]);
        AddListCallableContract(contracts, "count", 1, 1, "LA3123", "list.count(value) expects one argument.", parameterNames: ["value"]);
        AddListCallableContract(contracts, "insert", 2, 2, "LA3123", "list.insert(index, value) expects two arguments.", StaticMutationKind.MutatesReceiver, "index", "value");
        AddListCallableContract(contracts, "remove", 1, 1, "LA3123", "list.remove(value) expects one argument.", StaticMutationKind.MutatesReceiver, "value");
        AddListCallableContract(contracts, "pop", 0, 1, "LA3123", "list.pop([index]) expects zero or one argument.", StaticMutationKind.MutatesReceiver, "index");
        AddListCallableContract(contracts, "reverse", 0, 0, "LA3123", "list.reverse() expects no arguments.", StaticMutationKind.MutatesReceiver);
        AddListCallableContract(contracts, "sort", 0, 2, "LA3123", "list.sort(*, key=None, reverse=False) expects optional key/reverse keyword arguments.", StaticMutationKind.MutatesReceiver, "key", "reverse");
        AddListCallableContract(contracts, "copy", 0, 0, "LA3124", "list.copy() expects no arguments.");
        AddListCallableContract(contracts, "clear", 0, 0, "LA3125", "list.clear() expects no arguments.", StaticMutationKind.MutatesReceiver);
    }

    private static void AddListCallableContract(
        List<StaticCallableContract> contracts,
        string memberName,
        int minimumArgumentCount,
        int? maximumArgumentCount,
        string diagnosticCode,
        string message,
        StaticMutationKind mutation = StaticMutationKind.None,
        params string[] parameterNames)
    {
        var parameters = parameterNames.Length == 0 ? null : parameterNames;
        contracts.Add(new StaticCallableContract(AbstractValueKind.List, memberName, minimumArgumentCount, maximumArgumentCount, diagnosticCode, message, mutation, parameters));
        contracts.Add(new StaticCallableContract(AbstractValueKind.ListType, memberName, minimumArgumentCount, maximumArgumentCount, diagnosticCode, message, mutation, parameters));
    }
}
