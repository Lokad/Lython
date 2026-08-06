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
            new(AbstractValueKind.Path, "__fspath__", 0, 0, "LA3114", "Path.__fspath__() expects no arguments."),
            new(AbstractValueKind.Path, "is_absolute", 0, 0, "LA3114", "Path.is_absolute() expects no arguments."),
            new(AbstractValueKind.Path, "is_mount", 0, 0, "LA3114", "Path.is_mount() expects no arguments."),
            new(AbstractValueKind.Path, "is_reserved", 0, 0, "LA3114", "Path.is_reserved() expects no arguments."),
            new(AbstractValueKind.Path, "as_posix", 0, 0, "LA3114", "Path.as_posix() expects no arguments."),
            new(AbstractValueKind.Path, "resolve", 0, 0, "LA3114", "Path.resolve() expects no arguments."),
            new(AbstractValueKind.Path, "expanduser", 0, 0, "LA3114", "Path.expanduser() expects no arguments."),
            new(AbstractValueKind.Path, "exists", 0, 0, "LA3114", "Path.exists() expects no arguments."),
            new(AbstractValueKind.Path, "is_file", 0, 0, "LA3114", "Path.is_file() expects no arguments."),
            new(AbstractValueKind.Path, "is_dir", 0, 0, "LA3114", "Path.is_dir() expects no arguments."),
            new(AbstractValueKind.Path, "is_symlink", 0, 0, "LA3114", "Path.is_symlink() expects no arguments."),
            new(AbstractValueKind.Path, "stat", 0, 0, "LA3114", "Path.stat() expects no arguments."),
            new(AbstractValueKind.Path, "lstat", 0, 0, "LA3114", "Path.lstat() expects no arguments."),
            new(AbstractValueKind.Path, "iterdir", 0, 0, "LA3114", "Path.iterdir() expects no arguments."),
            new(AbstractValueKind.Path, "absolute", 0, 0, "LA3114", "Path.absolute() expects no arguments."),
            new(AbstractValueKind.Path, "unlink", 0, 1, "LA3114", "Path.unlink([missing_ok]) expects zero or one argument.", ParameterNames: ["missing_ok"]),
            new(AbstractValueKind.Path, "rmdir", 0, 0, "LA3114", "Path.rmdir() expects no arguments."),
            new(AbstractValueKind.Path, "touch", 0, 2, "LA3114", "Path.touch([mode][, exist_ok]) expects zero to two arguments.", ParameterNames: ["mode", "exist_ok"]),
            new(AbstractValueKind.Path, "mkdir", 0, 3, "LA3114", "Path.mkdir([mode][, parents][, exist_ok]) expects zero to three arguments.", ParameterNames: ["mode", "parents", "exist_ok"]),
            new(AbstractValueKind.Path, "read_text", 0, 3, "LA3114", "Path.read_text([encoding][, errors][, newline]) expects zero to three arguments.", ParameterNames: ["encoding", "errors", "newline"]),
            new(AbstractValueKind.Path, "open", 0, 5, "LA3114", "Path.open([mode][, buffering][, encoding][, errors][, newline]) expects zero to five arguments.", ParameterNames: ["mode", "buffering", "encoding", "errors", "newline"]),
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
            new(AbstractValueKind.Path, "glob", 1, 3, "LA3114", "Path.glob(pattern[, case_sensitive][, recurse_symlinks]) expects one to three arguments.", ParameterNames: ["pattern", "case_sensitive", "recurse_symlinks"]),
            new(AbstractValueKind.Path, "rglob", 1, 3, "LA3114", "Path.rglob(pattern[, case_sensitive][, recurse_symlinks]) expects one to three arguments.", ParameterNames: ["pattern", "case_sensitive", "recurse_symlinks"]),
            new(AbstractValueKind.Path, "joinpath", 1, null, "LA3114", "Path.joinpath(*other) expects at least one argument."),
            new(AbstractValueKind.Path, "read_bytes", 0, 0, "LA3114", "Path.read_bytes() expects no arguments."),
            new(AbstractValueKind.Path, "write_bytes", 1, 1, "LA3114", "Path.write_bytes(data) expects one argument.", ParameterNames: ["data"]),
            new(AbstractValueKind.Path, "readlink", 0, 0, "LA3114", "Path.readlink() expects no arguments."),
            new(AbstractValueKind.Path, "symlink_to", 1, 2, "LA3114", "Path.symlink_to(target[, target_is_directory]) expects one or two arguments.", ParameterNames: ["target", "target_is_directory"]),
            new(AbstractValueKind.Path, "hardlink_to", 1, 1, "LA3114", "Path.hardlink_to(target) expects one argument.", ParameterNames: ["target"]),
            new(AbstractValueKind.Path, "chmod", 1, 1, "LA3114", "Path.chmod(mode) expects one argument.", ParameterNames: ["mode"]),
            new(AbstractValueKind.Path, "owner", 0, 0, "LA3114", "Path.owner() expects no arguments."),
            new(AbstractValueKind.Path, "group", 0, 0, "LA3114", "Path.group() expects no arguments."),
            new(AbstractValueKind.TextFileHandle, "close", 0, 0, "LA3108", "file.close() expects no arguments."),
            new(AbstractValueKind.TextFileHandle, "readable", 0, 0, "LA3108", "file.readable() expects no arguments."),
            new(AbstractValueKind.TextFileHandle, "writable", 0, 0, "LA3108", "file.writable() expects no arguments."),
            new(AbstractValueKind.TextFileHandle, "seekable", 0, 0, "LA3108", "file.seekable() expects no arguments."),
            new(AbstractValueKind.TextFileHandle, "tell", 0, 0, "LA3108", "file.tell() expects no arguments."),
            new(AbstractValueKind.TextFileHandle, "seek", 1, 2, "LA3108", "file.seek(offset[, whence]) expects one or two arguments.", ParameterNames: ["offset", "whence"]),
            new(AbstractValueKind.TextFileHandle, "read", 0, 1, "LA3108", "file.read([size]) expects zero or one argument.", ParameterNames: ["size"]),
            new(AbstractValueKind.TextFileHandle, "readline", 0, 1, "LA3108", "file.readline([size]) expects zero or one argument.", ParameterNames: ["size"]),
            new(AbstractValueKind.TextFileHandle, "readlines", 0, 1, "LA3108", "file.readlines([hint]) expects zero or one argument.", ParameterNames: ["hint"]),
            new(AbstractValueKind.TextFileHandle, "write", 1, 1, "LA3111", "file.write(text) expects one string argument.", ParameterNames: ["text"]),
            new(AbstractValueKind.TextFileHandle, "writelines", 1, 1, "LA3112", "file.writelines(lines) expects one iterable of strings argument.", ParameterNames: ["lines"]),
            new(AbstractValueKind.TextFileHandle, "flush", 0, 0, "LA3108", "file.flush() expects no arguments."),
            new(AbstractValueKind.CollectionsDefaultDict, "get", 1, 2, "LA3114", "defaultdict.get(key[, default]) expects one or two arguments.", ParameterNames: ["key", "default"]),
            new(AbstractValueKind.CollectionsDefaultDict, "keys", 0, 0, "LA3114", "defaultdict.keys() expects no arguments."),
            new(AbstractValueKind.CollectionsDefaultDict, "values", 0, 0, "LA3114", "defaultdict.values() expects no arguments."),
            new(AbstractValueKind.CollectionsDefaultDict, "items", 0, 0, "LA3114", "defaultdict.items() expects no arguments."),
            new(AbstractValueKind.CollectionsDefaultDict, "setdefault", 1, 2, "LA3114", "defaultdict.setdefault(key[, default]) expects one or two arguments.", ParameterNames: ["key", "default"]),
            new(AbstractValueKind.CollectionsDefaultDict, "copy", 0, 0, "LA3114", "defaultdict.copy() expects no arguments."),
            new(AbstractValueKind.CollectionsDefaultDict, "clear", 0, 0, "LA3114", "defaultdict.clear() expects no arguments.", StaticMutationKind.MutatesReceiver),
            new(AbstractValueKind.CollectionsCounter, "get", 1, 2, "LA3114", "Counter.get(key[, default]) expects one or two arguments.", ParameterNames: ["key", "default"]),
            new(AbstractValueKind.CollectionsCounter, "update", 0, null, "LA3114", "Counter.update([iterable], **kwargs) expects supported arguments.", StaticMutationKind.MutatesReceiver, ["iterable"], AllowsExtraKeywords: true),
            new(AbstractValueKind.CollectionsCounter, "subtract", 0, null, "LA3114", "Counter.subtract([iterable], **kwargs) expects supported arguments.", StaticMutationKind.MutatesReceiver, ["iterable"], AllowsExtraKeywords: true),
            new(AbstractValueKind.CollectionsCounter, "total", 0, 0, "LA3114", "Counter.total() expects no arguments."),
            new(AbstractValueKind.CollectionsCounter, "most_common", 0, 1, "LA3114", "Counter.most_common([n]) expects zero or one argument.", ParameterNames: ["n"]),
            new(AbstractValueKind.CollectionsCounter, "elements", 0, 0, "LA3114", "Counter.elements() expects no arguments."),
            new(AbstractValueKind.CollectionsCounter, "copy", 0, 0, "LA3114", "Counter.copy() expects no arguments."),
            new(AbstractValueKind.CollectionsCounter, "clear", 0, 0, "LA3114", "Counter.clear() expects no arguments.", StaticMutationKind.MutatesReceiver),
            new(AbstractValueKind.CollectionsCounter, "keys", 0, 0, "LA3114", "Counter.keys() expects no arguments."),
            new(AbstractValueKind.CollectionsCounter, "values", 0, 0, "LA3114", "Counter.values() expects no arguments."),
            new(AbstractValueKind.CollectionsCounter, "items", 0, 0, "LA3114", "Counter.items() expects no arguments."),
            new(AbstractValueKind.CollectionsDeque, "append", 1, 1, "LA3114", "deque.append(value) expects one argument.", StaticMutationKind.MutatesReceiver, ["value"]),
            new(AbstractValueKind.CollectionsDeque, "appendleft", 1, 1, "LA3114", "deque.appendleft(value) expects one argument.", StaticMutationKind.MutatesReceiver, ["value"]),
            new(AbstractValueKind.CollectionsDeque, "pop", 0, 0, "LA3114", "deque.pop() expects no arguments.", StaticMutationKind.MutatesReceiver),
            new(AbstractValueKind.CollectionsDeque, "popleft", 0, 0, "LA3114", "deque.popleft() expects no arguments.", StaticMutationKind.MutatesReceiver),
            new(AbstractValueKind.CollectionsDeque, "extend", 1, 1, "LA3114", "deque.extend(iterable) expects one argument.", StaticMutationKind.MutatesReceiver, ["iterable"]),
            new(AbstractValueKind.CollectionsDeque, "extendleft", 1, 1, "LA3114", "deque.extendleft(iterable) expects one argument.", StaticMutationKind.MutatesReceiver, ["iterable"]),
            new(AbstractValueKind.CollectionsDeque, "clear", 0, 0, "LA3114", "deque.clear() expects no arguments.", StaticMutationKind.MutatesReceiver),
            new(AbstractValueKind.CollectionsDeque, "copy", 0, 0, "LA3114", "deque.copy() expects no arguments."),
            new(AbstractValueKind.CollectionsDeque, "count", 1, 1, "LA3114", "deque.count(value) expects one argument.", ParameterNames: ["value"]),
            new(AbstractValueKind.CollectionsDeque, "index", 1, 3, "LA3114", "deque.index(value[, start[, stop]]) expects one to three arguments.", ParameterNames: ["value", "start", "stop"]),
            new(AbstractValueKind.CollectionsDeque, "insert", 2, 2, "LA3114", "deque.insert(index, value) expects two arguments.", StaticMutationKind.MutatesReceiver, ["index", "value"]),
            new(AbstractValueKind.CollectionsDeque, "remove", 1, 1, "LA3114", "deque.remove(value) expects one argument.", StaticMutationKind.MutatesReceiver, ["value"]),
            new(AbstractValueKind.CollectionsDeque, "reverse", 0, 0, "LA3114", "deque.reverse() expects no arguments.", StaticMutationKind.MutatesReceiver),
            new(AbstractValueKind.CollectionsDeque, "rotate", 0, 1, "LA3114", "deque.rotate([n]) expects zero or one argument.", StaticMutationKind.MutatesReceiver, ["n"]),
            new(AbstractValueKind.CollectionsChainMap, "get", 1, 2, "LA3114", "ChainMap.get(key[, default]) expects one or two arguments.", ParameterNames: ["key", "default"]),
            new(AbstractValueKind.CollectionsChainMap, "keys", 0, 0, "LA3114", "ChainMap.keys() expects no arguments."),
            new(AbstractValueKind.CollectionsChainMap, "values", 0, 0, "LA3114", "ChainMap.values() expects no arguments."),
            new(AbstractValueKind.CollectionsChainMap, "items", 0, 0, "LA3114", "ChainMap.items() expects no arguments."),
            new(AbstractValueKind.CollectionsChainMap, "new_child", 0, 1, "LA3114", "ChainMap.new_child([m]) expects zero or one argument.", ParameterNames: ["m"]),
            new(AbstractValueKind.CollectionsChainMap, "copy", 0, 0, "LA3114", "ChainMap.copy() expects no arguments."),
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
            new(AbstractValueKind.Set, "add", 1, 1, "LA3135", "set.add(value) expects one positional argument.", StaticMutationKind.MutatesReceiver),
            new(AbstractValueKind.Set, "discard", 1, 1, "LA3136", "set.discard(value) expects one positional argument.", StaticMutationKind.MutatesReceiver),
            new(AbstractValueKind.Set, "remove", 1, 1, "LA3137", "set.remove(value) expects one positional argument.", StaticMutationKind.MutatesReceiver),
            new(AbstractValueKind.Set, "copy", 0, 0, "LA3138", "set.copy() expects no arguments."),
            new(AbstractValueKind.Set, "clear", 0, 0, "LA3139", "set.clear() expects no arguments.", StaticMutationKind.MutatesReceiver),
            new(AbstractValueKind.Set, "pop", 0, 0, "LA3139", "set.pop() expects no arguments.", StaticMutationKind.MutatesReceiver),
            new(AbstractValueKind.Set, "union", 0, null, "LA3139", "set.union(*others) expects positional iterable operands."),
            new(AbstractValueKind.Set, "intersection", 0, null, "LA3139", "set.intersection(*others) expects positional iterable operands."),
            new(AbstractValueKind.Set, "difference", 0, null, "LA3139", "set.difference(*others) expects positional iterable operands."),
            new(AbstractValueKind.Set, "symmetric_difference", 1, 1, "LA3139", "set.symmetric_difference(other) expects one positional iterable operand."),
            new(AbstractValueKind.Set, "isdisjoint", 1, 1, "LA3139", "set.isdisjoint(other) expects one positional iterable operand."),
            new(AbstractValueKind.Set, "issubset", 1, 1, "LA3139", "set.issubset(other) expects one positional iterable operand."),
            new(AbstractValueKind.Set, "issuperset", 1, 1, "LA3139", "set.issuperset(other) expects one positional iterable operand."),
            new(AbstractValueKind.Set, "update", 0, null, "LA3139", "set.update(*others) expects positional iterable operands.", StaticMutationKind.MutatesReceiver),
            new(AbstractValueKind.Set, "intersection_update", 0, null, "LA3139", "set.intersection_update(*others) expects positional iterable operands.", StaticMutationKind.MutatesReceiver),
            new(AbstractValueKind.Set, "difference_update", 0, null, "LA3139", "set.difference_update(*others) expects positional iterable operands.", StaticMutationKind.MutatesReceiver),
            new(AbstractValueKind.Set, "symmetric_difference_update", 1, 1, "LA3139", "set.symmetric_difference_update(other) expects one positional iterable operand.", StaticMutationKind.MutatesReceiver),
            new(AbstractValueKind.SetType, "add", 1, 1, "LA3135", "set.add(value) expects one positional argument.", StaticMutationKind.MutatesReceiver),
            new(AbstractValueKind.SetType, "discard", 1, 1, "LA3136", "set.discard(value) expects one positional argument.", StaticMutationKind.MutatesReceiver),
            new(AbstractValueKind.SetType, "remove", 1, 1, "LA3137", "set.remove(value) expects one positional argument.", StaticMutationKind.MutatesReceiver),
            new(AbstractValueKind.SetType, "copy", 0, 0, "LA3138", "set.copy() expects no arguments."),
            new(AbstractValueKind.SetType, "clear", 0, 0, "LA3139", "set.clear() expects no arguments.", StaticMutationKind.MutatesReceiver),
            new(AbstractValueKind.SetType, "pop", 0, 0, "LA3139", "set.pop() expects no arguments.", StaticMutationKind.MutatesReceiver),
            new(AbstractValueKind.SetType, "union", 0, null, "LA3139", "set.union(*others) expects positional iterable operands."),
            new(AbstractValueKind.SetType, "intersection", 0, null, "LA3139", "set.intersection(*others) expects positional iterable operands."),
            new(AbstractValueKind.SetType, "difference", 0, null, "LA3139", "set.difference(*others) expects positional iterable operands."),
            new(AbstractValueKind.SetType, "symmetric_difference", 1, 1, "LA3139", "set.symmetric_difference(other) expects one positional iterable operand."),
            new(AbstractValueKind.SetType, "isdisjoint", 1, 1, "LA3139", "set.isdisjoint(other) expects one positional iterable operand."),
            new(AbstractValueKind.SetType, "issubset", 1, 1, "LA3139", "set.issubset(other) expects one positional iterable operand."),
            new(AbstractValueKind.SetType, "issuperset", 1, 1, "LA3139", "set.issuperset(other) expects one positional iterable operand."),
            new(AbstractValueKind.SetType, "update", 0, null, "LA3139", "set.update(*others) expects positional iterable operands.", StaticMutationKind.MutatesReceiver),
            new(AbstractValueKind.SetType, "intersection_update", 0, null, "LA3139", "set.intersection_update(*others) expects positional iterable operands.", StaticMutationKind.MutatesReceiver),
            new(AbstractValueKind.SetType, "difference_update", 0, null, "LA3139", "set.difference_update(*others) expects positional iterable operands.", StaticMutationKind.MutatesReceiver),
            new(AbstractValueKind.SetType, "symmetric_difference_update", 1, 1, "LA3139", "set.symmetric_difference_update(other) expects one positional iterable operand.", StaticMutationKind.MutatesReceiver),
            new(AbstractValueKind.RegexPattern, "search", 1, 3, "LA3152", "pattern.search(string[, pos[, endpos]]) expects one to three arguments.", ParameterNames: ["string", "pos", "endpos"]),
            new(AbstractValueKind.RegexPattern, "match", 1, 3, "LA3152", "pattern.match(string[, pos[, endpos]]) expects one to three arguments.", ParameterNames: ["string", "pos", "endpos"]),
            new(AbstractValueKind.RegexPattern, "fullmatch", 1, 3, "LA3152", "pattern.fullmatch(string[, pos[, endpos]]) expects one to three arguments.", ParameterNames: ["string", "pos", "endpos"]),
            new(AbstractValueKind.RegexPattern, "findall", 1, 3, "LA3152", "pattern.findall(string[, pos[, endpos]]) expects one to three arguments.", ParameterNames: ["string", "pos", "endpos"]),
            new(AbstractValueKind.RegexPattern, "finditer", 1, 3, "LA3152", "pattern.finditer(string[, pos[, endpos]]) expects one to three arguments.", ParameterNames: ["string", "pos", "endpos"]),
            new(AbstractValueKind.RegexPattern, "sub", 2, 5, "LA3152", "pattern.sub(repl, string[, count[, pos[, endpos]]]) expects two to five arguments.", ParameterNames: ["repl", "string", "count", "pos", "endpos"]),
            new(AbstractValueKind.RegexPattern, "subn", 2, 5, "LA3152", "pattern.subn(repl, string[, count[, pos[, endpos]]]) expects two to five arguments.", ParameterNames: ["repl", "string", "count", "pos", "endpos"]),
            new(AbstractValueKind.RegexPattern, "split", 1, 4, "LA3152", "pattern.split(string[, maxsplit[, pos[, endpos]]]) expects one to four arguments.", ParameterNames: ["string", "maxsplit", "pos", "endpos"]),
            new(AbstractValueKind.RegexMatch, "group", 0, null, "LA3152", "match.group([index...]) expects zero or more positional group identifiers."),
            new(AbstractValueKind.RegexMatch, "groups", 0, 1, "LA3152", "match.groups(default=None) expects zero or one argument.", ParameterNames: ["default"]),
            new(AbstractValueKind.RegexMatch, "groupdict", 0, 1, "LA3152", "match.groupdict(default=None) expects zero or one argument.", ParameterNames: ["default"]),
            new(AbstractValueKind.RegexMatch, "expand", 1, 1, "LA3152", "match.expand(template) expects one string argument.", ParameterNames: ["template"]),
            new(AbstractValueKind.RegexMatch, "start", 0, 1, "LA3152", "match.start(group=0) expects zero or one group identifier.", ParameterNames: ["group"]),
            new(AbstractValueKind.RegexMatch, "end", 0, 1, "LA3152", "match.end(group=0) expects zero or one group identifier.", ParameterNames: ["group"]),
            new(AbstractValueKind.RegexMatch, "span", 0, 1, "LA3152", "match.span(group=0) expects zero or one group identifier.", ParameterNames: ["group"]),
            new(AbstractValueKind.ArgparseParser, "add_mutually_exclusive_group", 0, 1, "LA3153", "argparse.ArgumentParser.add_mutually_exclusive_group([required]) expects zero or one argument.", ParameterNames: ["required"]),
            new(AbstractValueKind.ArgparseParser, "parse_args", 0, 2, "LA3154", "argparse.ArgumentParser.parse_args([args][, namespace]) expects zero to two arguments.", ParameterNames: ["args", "namespace"]),
            new(AbstractValueKind.ArgparseParser, "parse_known_args", 0, 2, "LA3154", "argparse.ArgumentParser.parse_known_args([args][, namespace]) expects zero to two arguments.", ParameterNames: ["args", "namespace"]),
            new(AbstractValueKind.ArgparseParser, "format_usage", 0, 0, "LA3154", "argparse.ArgumentParser.format_usage() expects no arguments."),
            new(AbstractValueKind.ArgparseParser, "format_help", 0, 0, "LA3154", "argparse.ArgumentParser.format_help() expects no arguments."),
            new(AbstractValueKind.ArgparseParser, "print_usage", 0, 1, "LA3154", "argparse.ArgumentParser.print_usage([file]) expects zero or one argument.", ParameterNames: ["file"]),
            new(AbstractValueKind.ArgparseParser, "print_help", 0, 1, "LA3154", "argparse.ArgumentParser.print_help([file]) expects zero or one argument.", ParameterNames: ["file"]),
            new(AbstractValueKind.ArgparseParser, "error", 1, 1, "LA3154", "argparse.ArgumentParser.error(message) expects one argument.", ParameterNames: ["message"]),
            new(AbstractValueKind.ArgparseParser, "exit", 0, 2, "LA3154", "argparse.ArgumentParser.exit([status][, message]) expects zero to two arguments.", ParameterNames: ["status", "message"]),
            new(AbstractValueKind.ArgparseParser, "get_default", 1, 1, "LA3154", "argparse.ArgumentParser.get_default(dest) expects one argument.", ParameterNames: ["dest"]),
            new(AbstractValueKind.ArgparseParser, "add_subparsers", 0, null, "LA3154", "argparse.ArgumentParser.add_subparsers(...) is not supported by Lython."),
            new(AbstractValueKind.CsvWriter, "writerow", 1, 1, "LA3155", "csv.writerow(row) expects one argument.", ParameterNames: ["row"]),
            new(AbstractValueKind.CsvWriter, "writerows", 1, 1, "LA3155", "csv.writerows(rows) expects one argument.", ParameterNames: ["rows"]),
            new(AbstractValueKind.CsvWriter, "getvalue", 0, 0, "LA3155", "csv.getvalue() expects no arguments."),
            new(AbstractValueKind.CsvDictWriter, "writeheader", 0, 0, "LA3155", "csv.DictWriter.writeheader() expects no arguments."),
            new(AbstractValueKind.CsvDictWriter, "writerow", 1, 1, "LA3155", "csv.DictWriter.writerow(rowdict) expects one argument.", ParameterNames: ["rowdict"]),
            new(AbstractValueKind.CsvDictWriter, "writerows", 1, 1, "LA3155", "csv.DictWriter.writerows(rowdicts) expects one argument.", ParameterNames: ["rowdicts"]),
            new(AbstractValueKind.Decimal, "quantize", 1, 3, "LA3156", "Decimal.quantize(exp[, rounding][, context]) expects one to three arguments.", ParameterNames: ["exp", "rounding", "context"]),
            new(AbstractValueKind.Decimal, "normalize", 0, 1, "LA3156", "Decimal.normalize([context]) expects zero or one argument.", ParameterNames: ["context"]),
            new(AbstractValueKind.Decimal, "sqrt", 0, 1, "LA3156", "Decimal.sqrt([context]) expects zero or one argument.", ParameterNames: ["context"]),
            new(AbstractValueKind.Decimal, "exp", 0, 1, "LA3156", "Decimal.exp([context]) expects zero or one argument.", ParameterNames: ["context"]),
            new(AbstractValueKind.Decimal, "ln", 0, 1, "LA3156", "Decimal.ln([context]) expects zero or one argument.", ParameterNames: ["context"]),
            new(AbstractValueKind.Decimal, "log10", 0, 1, "LA3156", "Decimal.log10([context]) expects zero or one argument.", ParameterNames: ["context"]),
            new(AbstractValueKind.Decimal, "copy_abs", 0, 1, "LA3156", "Decimal.copy_abs([context]) expects zero or one argument.", ParameterNames: ["context"]),
            new(AbstractValueKind.Decimal, "copy_negate", 0, 1, "LA3156", "Decimal.copy_negate([context]) expects zero or one argument.", ParameterNames: ["context"]),
            new(AbstractValueKind.Decimal, "copy_sign", 1, 1, "LA3156", "Decimal.copy_sign(other) expects one argument.", ParameterNames: ["other"]),
            new(AbstractValueKind.Decimal, "to_integral_value", 0, 2, "LA3156", "Decimal.to_integral_value([rounding][, context]) expects zero to two arguments.", ParameterNames: ["rounding", "context"]),
            new(AbstractValueKind.Decimal, "to_integral_exact", 0, 2, "LA3156", "Decimal.to_integral_exact([rounding][, context]) expects zero to two arguments.", ParameterNames: ["rounding", "context"]),
            new(AbstractValueKind.Decimal, "to_integral", 0, 2, "LA3156", "Decimal.to_integral([rounding][, context]) expects zero to two arguments.", ParameterNames: ["rounding", "context"]),
            new(AbstractValueKind.Decimal, "as_tuple", 0, 0, "LA3156", "Decimal.as_tuple() expects no arguments."),
            new(AbstractValueKind.Decimal, "adjusted", 0, 0, "LA3156", "Decimal.adjusted() expects no arguments."),
            new(AbstractValueKind.Decimal, "compare", 1, 2, "LA3156", "Decimal.compare(other[, context]) expects one or two arguments.", ParameterNames: ["other", "context"]),
            new(AbstractValueKind.Decimal, "compare_total", 1, 1, "LA3156", "Decimal.compare_total(other) expects one argument.", ParameterNames: ["other"]),
            new(AbstractValueKind.Decimal, "is_nan", 0, 0, "LA3156", "Decimal.is_nan() expects no arguments."),
            new(AbstractValueKind.Decimal, "is_infinite", 0, 0, "LA3156", "Decimal.is_infinite() expects no arguments."),
            new(AbstractValueKind.Decimal, "is_finite", 0, 0, "LA3156", "Decimal.is_finite() expects no arguments."),
            new(AbstractValueKind.Decimal, "is_zero", 0, 0, "LA3156", "Decimal.is_zero() expects no arguments."),
            new(AbstractValueKind.Decimal, "is_signed", 0, 0, "LA3156", "Decimal.is_signed() expects no arguments."),
            new(AbstractValueKind.Decimal, "to_eng_string", 0, 0, "LA3156", "Decimal.to_eng_string() expects no arguments."),
            new(AbstractValueKind.Decimal, "scaleb", 1, 2, "LA3156", "Decimal.scaleb(other[, context]) expects one or two arguments.", ParameterNames: ["other", "context"]),
            new(AbstractValueKind.Decimal, "shift", 1, 1, "LA3156", "Decimal.shift(other) expects one argument.", ParameterNames: ["other"]),
            new(AbstractValueKind.Decimal, "rotate", 1, 1, "LA3156", "Decimal.rotate(other) expects one argument.", ParameterNames: ["other"]),
            new(AbstractValueKind.Decimal, "same_quantum", 1, 1, "LA3156", "Decimal.same_quantum(other) expects one argument.", ParameterNames: ["other"]),
            new(AbstractValueKind.Decimal, "remainder_near", 1, 2, "LA3156", "Decimal.remainder_near(other[, context]) expects one or two arguments.", ParameterNames: ["other", "context"]),
            new(AbstractValueKind.Decimal, "min", 1, 2, "LA3156", "Decimal.min(other[, context]) expects one or two arguments.", ParameterNames: ["other", "context"]),
            new(AbstractValueKind.Decimal, "max", 1, 2, "LA3156", "Decimal.max(other[, context]) expects one or two arguments.", ParameterNames: ["other", "context"]),
            new(AbstractValueKind.Decimal, "min_mag", 1, 2, "LA3156", "Decimal.min_mag(other[, context]) expects one or two arguments.", ParameterNames: ["other", "context"]),
            new(AbstractValueKind.Decimal, "max_mag", 1, 2, "LA3156", "Decimal.max_mag(other[, context]) expects one or two arguments.", ParameterNames: ["other", "context"]),
            new(AbstractValueKind.DecimalContext, "copy", 0, 0, "LA3156", "Context.copy() expects no arguments."),
            new(AbstractValueKind.DecimalContext, "clear_flags", 0, 0, "LA3156", "Context.clear_flags() expects no arguments.", StaticMutationKind.MutatesReceiver),
            new(AbstractValueKind.DecimalContext, "create_decimal", 1, 1, "LA3156", "Context.create_decimal(value) expects one argument.", ParameterNames: ["value"]),
            new(AbstractValueKind.DecimalContext, "create_decimal_from_float", 1, 1, "LA3156", "Context.create_decimal_from_float(f) expects one argument.", ParameterNames: ["f"]),
            new(AbstractValueKind.DateTimeTimedelta, "total_seconds", 0, 0, "LA3162", "timedelta.total_seconds() expects no arguments."),
            new(AbstractValueKind.DateTimeDate, "weekday", 0, 0, "LA3162", "date.weekday() expects no arguments."),
            new(AbstractValueKind.DateTimeDate, "isoweekday", 0, 0, "LA3162", "date.isoweekday() expects no arguments."),
            new(AbstractValueKind.DateTimeDate, "isocalendar", 0, 0, "LA3162", "date.isocalendar() expects no arguments."),
            new(AbstractValueKind.DateTimeDate, "toordinal", 0, 0, "LA3162", "date.toordinal() expects no arguments."),
            new(AbstractValueKind.DateTimeDate, "timetuple", 0, 0, "LA3162", "date.timetuple() expects no arguments."),
            new(AbstractValueKind.DateTimeDate, "ctime", 0, 0, "LA3162", "date.ctime() expects no arguments."),
            new(AbstractValueKind.DateTimeDate, "isoformat", 0, 0, "LA3162", "date.isoformat() expects no arguments."),
            new(AbstractValueKind.DateTimeDate, "__format__", 1, 1, "LA3162", "date.__format__(format_spec) expects one argument.", ParameterNames: ["format_spec"]),
            new(AbstractValueKind.DateTimeDate, "strftime", 1, 1, "LA3162", "date.strftime(format) expects one argument.", ParameterNames: ["format"]),
            new(AbstractValueKind.DateTimeDate, "replace", 0, 3, "LA3162", "date.replace([year][, month][, day]) expects supported date fields.", ParameterNames: ["year", "month", "day"]),
            new(AbstractValueKind.DateTimeTime, "utcoffset", 0, 0, "LA3162", "time.utcoffset() expects no arguments."),
            new(AbstractValueKind.DateTimeTime, "tzname", 0, 0, "LA3162", "time.tzname() expects no arguments."),
            new(AbstractValueKind.DateTimeTime, "dst", 0, 0, "LA3162", "time.dst() expects no arguments."),
            new(AbstractValueKind.DateTimeTime, "isoformat", 0, 1, "LA3162", "time.isoformat([timespec]) expects zero or one argument.", ParameterNames: ["timespec"]),
            new(AbstractValueKind.DateTimeTime, "__format__", 1, 1, "LA3162", "time.__format__(format_spec) expects one argument.", ParameterNames: ["format_spec"]),
            new(AbstractValueKind.DateTimeTime, "strftime", 1, 1, "LA3162", "time.strftime(format) expects one argument.", ParameterNames: ["format"]),
            new(AbstractValueKind.DateTimeTime, "replace", 0, 6, "LA3162", "time.replace([hour][, minute][, second][, microsecond][, tzinfo][, fold]) expects supported fields.", ParameterNames: ["hour", "minute", "second", "microsecond", "tzinfo", "fold"]),
            new(AbstractValueKind.DateTimeDateTime, "date", 0, 0, "LA3162", "datetime.date() expects no arguments."),
            new(AbstractValueKind.DateTimeDateTime, "time", 0, 0, "LA3162", "datetime.time() expects no arguments."),
            new(AbstractValueKind.DateTimeDateTime, "timetz", 0, 0, "LA3162", "datetime.timetz() expects no arguments."),
            new(AbstractValueKind.DateTimeDateTime, "weekday", 0, 0, "LA3162", "datetime.weekday() expects no arguments."),
            new(AbstractValueKind.DateTimeDateTime, "isoweekday", 0, 0, "LA3162", "datetime.isoweekday() expects no arguments."),
            new(AbstractValueKind.DateTimeDateTime, "isocalendar", 0, 0, "LA3162", "datetime.isocalendar() expects no arguments."),
            new(AbstractValueKind.DateTimeDateTime, "toordinal", 0, 0, "LA3162", "datetime.toordinal() expects no arguments."),
            new(AbstractValueKind.DateTimeDateTime, "timetuple", 0, 0, "LA3162", "datetime.timetuple() expects no arguments."),
            new(AbstractValueKind.DateTimeDateTime, "utctimetuple", 0, 0, "LA3162", "datetime.utctimetuple() expects no arguments."),
            new(AbstractValueKind.DateTimeDateTime, "ctime", 0, 0, "LA3162", "datetime.ctime() expects no arguments."),
            new(AbstractValueKind.DateTimeDateTime, "timestamp", 0, 0, "LA3162", "datetime.timestamp() expects no arguments."),
            new(AbstractValueKind.DateTimeDateTime, "utcoffset", 0, 0, "LA3162", "datetime.utcoffset() expects no arguments."),
            new(AbstractValueKind.DateTimeDateTime, "tzname", 0, 0, "LA3162", "datetime.tzname() expects no arguments."),
            new(AbstractValueKind.DateTimeDateTime, "dst", 0, 0, "LA3162", "datetime.dst() expects no arguments."),
            new(AbstractValueKind.DateTimeDateTime, "astimezone", 0, 1, "LA3162", "datetime.astimezone([tz]) expects zero or one argument.", ParameterNames: ["tz"]),
            new(AbstractValueKind.DateTimeDateTime, "isoformat", 0, 2, "LA3162", "datetime.isoformat([sep][, timespec]) expects zero to two arguments.", ParameterNames: ["sep", "timespec"]),
            new(AbstractValueKind.DateTimeDateTime, "__format__", 1, 1, "LA3162", "datetime.__format__(format_spec) expects one argument.", ParameterNames: ["format_spec"]),
            new(AbstractValueKind.DateTimeDateTime, "strftime", 1, 1, "LA3162", "datetime.strftime(format) expects one argument.", ParameterNames: ["format"]),
            new(AbstractValueKind.DateTimeDateTime, "replace", 0, 9, "LA3162", "datetime.replace([year][, month][, day][, hour][, minute][, second][, microsecond][, tzinfo][, fold]) expects supported fields.", ParameterNames: ["year", "month", "day", "hour", "minute", "second", "microsecond", "tzinfo", "fold"]),
            new(AbstractValueKind.DateTimeTimezone, "utcoffset", 1, 1, "LA3162", "timezone.utcoffset(dt) expects one argument.", ParameterNames: ["dt"]),
            new(AbstractValueKind.DateTimeTimezone, "tzname", 1, 1, "LA3162", "timezone.tzname(dt) expects one argument.", ParameterNames: ["dt"]),
            new(AbstractValueKind.DateTimeTimezone, "dst", 1, 1, "LA3162", "timezone.dst(dt) expects one argument.", ParameterNames: ["dt"]),
            new(AbstractValueKind.StatisticsLinearRegression, "_asdict", 0, 0, "LA3163", "LinearRegression._asdict() expects no arguments."),
            new(AbstractValueKind.StatisticsLinearRegression, "_replace", 0, 2, "LA3163", "LinearRegression._replace([slope][, intercept]) expects zero to two field values.", ParameterNames: ["slope", "intercept"]),
            new(AbstractValueKind.StatisticsLinearRegression, "count", 1, 1, "LA3163", "LinearRegression.count(value) expects one argument.", ParameterNames: ["value"]),
            new(AbstractValueKind.StatisticsLinearRegression, "index", 1, 3, "LA3163", "LinearRegression.index(value[, start[, stop]]) expects one to three arguments.", ParameterNames: ["value", "start", "stop"]),
            new(AbstractValueKind.StatisticsNormalDist, "zscore", 1, 1, "LA3163", "NormalDist.zscore(x) expects one argument.", ParameterNames: ["x"]),
            new(AbstractValueKind.StatisticsNormalDist, "pdf", 1, 1, "LA3163", "NormalDist.pdf(x) expects one argument.", ParameterNames: ["x"]),
            new(AbstractValueKind.StatisticsNormalDist, "cdf", 1, 1, "LA3163", "NormalDist.cdf(x) expects one argument.", ParameterNames: ["x"]),
            new(AbstractValueKind.StatisticsNormalDist, "inv_cdf", 1, 1, "LA3163", "NormalDist.inv_cdf(p) expects one argument.", ParameterNames: ["p"]),
            new(AbstractValueKind.StatisticsNormalDist, "overlap", 1, 1, "LA3163", "NormalDist.overlap(other) expects one NormalDist argument.", ParameterNames: ["other"]),
            new(AbstractValueKind.StatisticsNormalDist, "quantiles", 0, 1, "LA3163", "NormalDist.quantiles([n]) expects zero or one argument.", ParameterNames: ["n"]),
            new(AbstractValueKind.StatisticsNormalDist, "samples", 1, 2, "LA3163", "NormalDist.samples(n[, seed]) expects one or two arguments.", ParameterNames: ["n", "seed"]),
            new(AbstractValueKind.Random, "seed", 0, 2, "LA3164", "Random.seed(a=None, version=2) expects zero to two arguments.", ParameterNames: ["a", "version"]),
            new(AbstractValueKind.Random, "random", 0, 0, "LA3164", "Random.random() expects no arguments."),
            new(AbstractValueKind.Random, "getstate", 0, 0, "LA3164", "Random.getstate() expects no arguments."),
            new(AbstractValueKind.Random, "setstate", 1, 1, "LA3164", "Random.setstate(state) expects one argument.", ParameterNames: ["state"]),
            new(AbstractValueKind.Random, "randrange", 1, 3, "LA3164", "Random.randrange(start, stop=None, step=1) expects one to three arguments.", ParameterNames: ["start", "stop", "step"]),
            new(AbstractValueKind.Random, "randint", 2, 2, "LA3164", "Random.randint(a, b) expects two arguments.", ParameterNames: ["a", "b"]),
            new(AbstractValueKind.Random, "choice", 1, 1, "LA3164", "Random.choice(seq) expects one argument.", ParameterNames: ["seq"]),
            new(AbstractValueKind.Random, "choices", 1, 4, "LA3164", "Random.choices(population, weights=None, cum_weights=None, k=1) expects supported arguments.", ParameterNames: ["population", "weights", "cum_weights", "k"]),
            new(AbstractValueKind.Random, "shuffle", 1, 1, "LA3164", "Random.shuffle(x) expects one argument.", StaticMutationKind.MutatesReceiver, ["x"]),
            new(AbstractValueKind.Random, "sample", 2, 3, "LA3164", "Random.sample(population, k, *, counts=None) expects two arguments plus optional counts.", ParameterNames: ["population", "k", "counts"]),
            new(AbstractValueKind.Random, "getrandbits", 1, 1, "LA3164", "Random.getrandbits(k) expects one argument.", ParameterNames: ["k"]),
            new(AbstractValueKind.Random, "randbytes", 1, 1, "LA3164", "Random.randbytes(n) expects one argument.", ParameterNames: ["n"]),
            new(AbstractValueKind.Random, "uniform", 2, 2, "LA3164", "Random.uniform(a, b) expects two arguments.", ParameterNames: ["a", "b"]),
            new(AbstractValueKind.Random, "triangular", 0, 3, "LA3164", "Random.triangular(low=0.0, high=1.0, mode=None) expects zero to three arguments.", ParameterNames: ["low", "high", "mode"]),
            new(AbstractValueKind.Random, "betavariate", 2, 2, "LA3164", "Random.betavariate(alpha, beta) expects two arguments.", ParameterNames: ["alpha", "beta"]),
            new(AbstractValueKind.Random, "expovariate", 0, 1, "LA3164", "Random.expovariate(lambd=1.0) expects zero or one argument.", ParameterNames: ["lambd"]),
            new(AbstractValueKind.Random, "gammavariate", 2, 2, "LA3164", "Random.gammavariate(alpha, beta) expects two arguments.", ParameterNames: ["alpha", "beta"]),
            new(AbstractValueKind.Random, "gauss", 0, 2, "LA3164", "Random.gauss(mu=0.0, sigma=1.0) expects zero to two arguments.", ParameterNames: ["mu", "sigma"]),
            new(AbstractValueKind.Random, "normalvariate", 0, 2, "LA3164", "Random.normalvariate(mu=0.0, sigma=1.0) expects zero to two arguments.", ParameterNames: ["mu", "sigma"]),
            new(AbstractValueKind.Random, "lognormvariate", 2, 2, "LA3164", "Random.lognormvariate(mu, sigma) expects two arguments.", ParameterNames: ["mu", "sigma"]),
            new(AbstractValueKind.Random, "paretovariate", 1, 1, "LA3164", "Random.paretovariate(alpha) expects one argument.", ParameterNames: ["alpha"]),
            new(AbstractValueKind.Random, "vonmisesvariate", 2, 2, "LA3164", "Random.vonmisesvariate(mu, kappa) expects two arguments.", ParameterNames: ["mu", "kappa"]),
            new(AbstractValueKind.Random, "weibullvariate", 2, 2, "LA3164", "Random.weibullvariate(alpha, beta) expects two arguments.", ParameterNames: ["alpha", "beta"]),
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
            new(AbstractValueKind.PkgutilModuleInfo, "_asdict", 0, 0, "LA3156", "ModuleInfo._asdict() expects no arguments."),
            new(AbstractValueKind.PkgutilModuleInfo, "_replace", 0, 3, "LA3156", "ModuleInfo._replace([module_finder][, name][, ispkg]) expects zero to three field values.", ParameterNames: ["module_finder", "name", "ispkg"]),
            new(AbstractValueKind.PkgutilModuleInfo, "count", 1, 1, "LA3156", "ModuleInfo.count(value) expects one argument.", ParameterNames: ["value"]),
            new(AbstractValueKind.PkgutilModuleInfo, "index", 1, 3, "LA3156", "ModuleInfo.index(value[, start[, stop]]) expects one to three arguments.", ParameterNames: ["value", "start", "stop"]),
            new(AbstractValueKind.PkgutilLoader, "is_package", 0, 1, "LA3156", "loader.is_package([fullname]) expects zero or one argument.", ParameterNames: ["fullname"]),
            new(AbstractValueKind.PkgutilLoader, "get_source", 0, 1, "LA3156", "loader.get_source([fullname]) expects zero or one argument.", ParameterNames: ["fullname"]),
            new(AbstractValueKind.SubprocessCompletedProcess, "check_returncode", 0, 0, "LA3160", "CompletedProcess.check_returncode() expects no arguments."),
            new(AbstractValueKind.SubprocessPopen, "communicate", 0, 2, "LA3160", "Popen.communicate([input][, timeout]) expects zero to two arguments.", ParameterNames: ["input", "timeout"]),
            new(AbstractValueKind.SubprocessPopen, "wait", 0, 1, "LA3160", "Popen.wait([timeout]) expects zero or one argument.", ParameterNames: ["timeout"]),
            new(AbstractValueKind.SubprocessPopen, "poll", 0, 0, "LA3160", "Popen.poll() expects no arguments."),
            new(AbstractValueKind.SubprocessPopen, "send_signal", 1, 1, "LA3160", "Popen.send_signal(signal) expects one argument.", ParameterNames: ["signal"]),
            new(AbstractValueKind.SubprocessPopen, "terminate", 0, 0, "LA3160", "Popen.terminate() expects no arguments."),
            new(AbstractValueKind.SubprocessPopen, "kill", 0, 0, "LA3160", "Popen.kill() expects no arguments."),
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
        AddStringCallableContract(contracts, "encode", 0, 2, "str.encode([encoding][, errors]) expects zero to two arguments.", "encoding", "errors");
        contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "decode", 0, 2, "LA3147", "bytes.decode([encoding][, errors]) expects zero to two arguments.", ParameterNames: ["encoding", "errors"]));
        contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "decode", 0, 2, "LA3147", "bytes.decode([encoding][, errors]) expects zero to two arguments.", ParameterNames: ["encoding", "errors"]));
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
