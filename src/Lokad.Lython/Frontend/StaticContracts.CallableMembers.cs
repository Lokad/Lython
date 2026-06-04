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
            new(AbstractValueKind.SubprocessCompletedProcess, "check_returncode", 0, 0, "LA3160", "CompletedProcess.check_returncode() expects no arguments."),
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
        AddListCallableContract(contracts, "pop", 0, 0, "LA3123", "list.pop() expects no arguments.", StaticMutationKind.MutatesReceiver);
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
