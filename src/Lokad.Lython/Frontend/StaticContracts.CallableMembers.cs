using Lokad.Lython.Runtime;

namespace Lokad.Lython.Frontend;

internal static partial class StaticContracts
{
    private static readonly StaticCallableContract[] CallableContracts = CreateCallableContracts();
    private static readonly IReadOnlyDictionary<StaticMemberContractKey, StaticCallableContract> CallableContractsByMember =
        CallableContracts.ToDictionary(static contract => new StaticMemberContractKey(contract.ReceiverKind, contract.MemberName));

    public static bool TryGetCallableContract(AbstractValue receiver, string memberName, out StaticCallableContract contract)
        => CallableContractsByMember.TryGetValue(new StaticMemberContractKey(receiver.Kind, memberName), out contract);

    public static bool IsMutatingMember(AbstractValue receiver, string memberName)
        => TryGetCallableContract(receiver, memberName, out var contract) &&
           contract.Mutation == StaticMutationKind.MutatesReceiver;

    private static StaticCallableContract[] CreateCallableContracts()
    {
        var contracts = new List<StaticCallableContract>(
            CoreCallableContractCatalog.Contracts.Length +
            DataCallableContractCatalog.Contracts.Length +
            LibraryCallableContractCatalog.Contracts.Length +
            OpenPyxlCallableContractCatalog.Contracts.Length);
        contracts.AddRange(CoreCallableContractCatalog.Contracts);
        contracts.AddRange(DataCallableContractCatalog.Contracts);
        contracts.AddRange(LibraryCallableContractCatalog.Contracts);
        contracts.AddRange(OpenPyxlCallableContractCatalog.Contracts);
        AddStringCallableContracts(contracts);
        AddListCallableContracts(contracts);


        return contracts.ToArray();

        static void AddStringCallableContracts(List<StaticCallableContract> contracts)
        {
            AddStringCallableContract(contracts, "lower", 0, 0, "str.lower() expects no arguments.");
            AddStringCallableContract(contracts, "capitalize", 0, 0, "str.capitalize() expects no arguments.");
            AddStringCallableContract(contracts, "islower", 0, 0, "str.islower() expects no arguments.");
            AddStringCallableContract(contracts, "upper", 0, 0, "str.upper() expects no arguments.");
            AddStringCallableContract(contracts, "swapcase", 0, 0, "str.swapcase() expects no arguments.");
            AddStringCallableContract(contracts, "title", 0, 0, "str.title() expects no arguments.");
            AddStringCallableContract(contracts, "casefold", 0, 0, "str.casefold() expects no arguments.");
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
            AddStringCallableContract(contracts, "isascii", 0, 0, "str.isascii() expects no arguments.");
            AddStringCallableContract(contracts, "isdecimal", 0, 0, "str.isdecimal() expects no arguments.");
            AddStringCallableContract(contracts, "isidentifier", 0, 0, "str.isidentifier() expects no arguments.");
            AddStringCallableContract(contracts, "isnumeric", 0, 0, "str.isnumeric() expects no arguments.");
            AddStringCallableContract(contracts, "isprintable", 0, 0, "str.isprintable() expects no arguments.");
            AddStringCallableContract(contracts, "istitle", 0, 0, "str.istitle() expects no arguments.");
            AddStringCallableContract(contracts, "translate", 1, 1, "str.translate(table) expects one argument.", "table");
            AddStringCallableContract(contracts, "maketrans", 1, 3, "str.maketrans(x[, y[, z]]) expects one to three arguments.");
            AddStringCallableContract(contracts, "encode", 0, 2, "str.encode([encoding][, errors]) expects zero to two arguments.", "encoding", "errors");
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "decode", 0, 2, "LA3147", "bytes.decode([encoding][, errors]) expects zero to two arguments.", parameterNames: ["encoding", "errors"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "decode", 0, 2, "LA3147", "bytes.decode([encoding][, errors]) expects zero to two arguments.", parameterNames: ["encoding", "errors"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "fromhex", 1, 1, "LA3166", "bytes.fromhex(string) expects one argument."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "fromhex", 1, 1, "LA3166", "bytes.fromhex(string) expects one argument."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "maketrans", 2, 2, "LA3172", "bytes.maketrans(from, to) expects two arguments.", parameterNames: ["from", "to"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "maketrans", 2, 2, "LA3172", "bytes.maketrans(from, to) expects two arguments.", parameterNames: ["from", "to"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "translate", 1, 2, "LA3172", "bytes.translate(table[, delete]) expects one or two arguments.", parameterNames: ["table", "delete"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "translate", 1, 2, "LA3172", "bytes.translate(table[, delete]) expects one or two arguments.", parameterNames: ["table", "delete"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "count", 1, 3, "LA3174", "bytes.count(sub[, start[, end]]) expects one to three arguments.", parameterNames: ["sub", "start", "end"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "count", 1, 3, "LA3174", "bytes.count(sub[, start[, end]]) expects one to three arguments.", parameterNames: ["sub", "start", "end"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "find", 1, 3, "LA3174", "bytes.find(sub[, start[, end]]) expects one to three arguments.", parameterNames: ["sub", "start", "end"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "find", 1, 3, "LA3174", "bytes.find(sub[, start[, end]]) expects one to three arguments.", parameterNames: ["sub", "start", "end"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "index", 1, 3, "LA3174", "bytes.index(sub[, start[, end]]) expects one to three arguments.", parameterNames: ["sub", "start", "end"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "index", 1, 3, "LA3174", "bytes.index(sub[, start[, end]]) expects one to three arguments.", parameterNames: ["sub", "start", "end"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "rfind", 1, 3, "LA3174", "bytes.rfind(sub[, start[, end]]) expects one to three arguments.", parameterNames: ["sub", "start", "end"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "rfind", 1, 3, "LA3174", "bytes.rfind(sub[, start[, end]]) expects one to three arguments.", parameterNames: ["sub", "start", "end"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "rindex", 1, 3, "LA3174", "bytes.rindex(sub[, start[, end]]) expects one to three arguments.", parameterNames: ["sub", "start", "end"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "rindex", 1, 3, "LA3174", "bytes.rindex(sub[, start[, end]]) expects one to three arguments.", parameterNames: ["sub", "start", "end"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "hex", 0, 2, "LA3173", "bytes.hex([sep[, bytes_per_sep]]) expects zero to two arguments.", parameterNames: ["sep", "bytes_per_sep"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "hex", 0, 2, "LA3173", "bytes.hex([sep[, bytes_per_sep]]) expects zero to two arguments.", parameterNames: ["sep", "bytes_per_sep"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "startswith", 1, 3, "LA3175", "bytes.startswith(prefix[, start[, end]]) expects one to three arguments.", parameterNames: ["prefix", "start", "end"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "startswith", 1, 3, "LA3175", "bytes.startswith(prefix[, start[, end]]) expects one to three arguments.", parameterNames: ["prefix", "start", "end"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "endswith", 1, 3, "LA3175", "bytes.endswith(suffix[, start[, end]]) expects one to three arguments.", parameterNames: ["suffix", "start", "end"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "endswith", 1, 3, "LA3175", "bytes.endswith(suffix[, start[, end]]) expects one to three arguments.", parameterNames: ["suffix", "start", "end"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "replace", 2, 3, "LA3176", "bytes.replace(old, new[, count]) expects two or three arguments.", parameterNames: ["old", "new", "count"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "replace", 2, 3, "LA3176", "bytes.replace(old, new[, count]) expects two or three arguments.", parameterNames: ["old", "new", "count"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "removeprefix", 1, 1, "LA3178", "bytes.removeprefix(prefix) expects one argument.", parameterNames: ["prefix"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "removeprefix", 1, 1, "LA3178", "bytes.removeprefix(prefix) expects one argument.", parameterNames: ["prefix"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "removesuffix", 1, 1, "LA3178", "bytes.removesuffix(suffix) expects one argument.", parameterNames: ["suffix"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "removesuffix", 1, 1, "LA3178", "bytes.removesuffix(suffix) expects one argument.", parameterNames: ["suffix"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "isalnum", 0, 0, "LA3177", "bytes.isalnum() expects no arguments."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "isalnum", 0, 0, "LA3177", "bytes.isalnum() expects no arguments."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "isalpha", 0, 0, "LA3177", "bytes.isalpha() expects no arguments."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "isalpha", 0, 0, "LA3177", "bytes.isalpha() expects no arguments."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "isascii", 0, 0, "LA3177", "bytes.isascii() expects no arguments."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "isascii", 0, 0, "LA3177", "bytes.isascii() expects no arguments."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "isdigit", 0, 0, "LA3177", "bytes.isdigit() expects no arguments."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "isdigit", 0, 0, "LA3177", "bytes.isdigit() expects no arguments."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "islower", 0, 0, "LA3177", "bytes.islower() expects no arguments."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "islower", 0, 0, "LA3177", "bytes.islower() expects no arguments."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "isspace", 0, 0, "LA3177", "bytes.isspace() expects no arguments."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "isspace", 0, 0, "LA3177", "bytes.isspace() expects no arguments."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "istitle", 0, 0, "LA3177", "bytes.istitle() expects no arguments."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "istitle", 0, 0, "LA3177", "bytes.istitle() expects no arguments."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "isupper", 0, 0, "LA3177", "bytes.isupper() expects no arguments."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "isupper", 0, 0, "LA3177", "bytes.isupper() expects no arguments."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "expandtabs", 0, 1, "LA3185", "bytes.expandtabs([tabsize]) expects zero or one argument.", parameterNames: ["tabsize"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "expandtabs", 0, 1, "LA3185", "bytes.expandtabs([tabsize]) expects zero or one argument.", parameterNames: ["tabsize"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "center", 1, 2, "LA3184", "bytes.center(width[, fillchar]) expects one or two arguments.", parameterNames: ["width", "fillchar"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "center", 1, 2, "LA3184", "bytes.center(width[, fillchar]) expects one or two arguments.", parameterNames: ["width", "fillchar"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "ljust", 1, 2, "LA3184", "bytes.ljust(width[, fillchar]) expects one or two arguments.", parameterNames: ["width", "fillchar"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "ljust", 1, 2, "LA3184", "bytes.ljust(width[, fillchar]) expects one or two arguments.", parameterNames: ["width", "fillchar"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "rjust", 1, 2, "LA3184", "bytes.rjust(width[, fillchar]) expects one or two arguments.", parameterNames: ["width", "fillchar"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "rjust", 1, 2, "LA3184", "bytes.rjust(width[, fillchar]) expects one or two arguments.", parameterNames: ["width", "fillchar"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "zfill", 1, 1, "LA3184", "bytes.zfill(width) expects one argument.", parameterNames: ["width"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "zfill", 1, 1, "LA3184", "bytes.zfill(width) expects one argument.", parameterNames: ["width"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "join", 1, 1, "LA3183", "bytes.join(iterable) expects one argument.", parameterNames: ["iterable"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "join", 1, 1, "LA3183", "bytes.join(iterable) expects one argument.", parameterNames: ["iterable"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "partition", 1, 1, "LA3182", "bytes.partition(sep) expects one argument.", parameterNames: ["sep"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "partition", 1, 1, "LA3182", "bytes.partition(sep) expects one argument.", parameterNames: ["sep"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "rpartition", 1, 1, "LA3182", "bytes.rpartition(sep) expects one argument.", parameterNames: ["sep"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "rpartition", 1, 1, "LA3182", "bytes.rpartition(sep) expects one argument.", parameterNames: ["sep"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "split", 0, 2, "LA3181", "bytes.split([sep[, maxsplit]]) expects zero to two arguments.", parameterNames: ["sep", "maxsplit"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "split", 0, 2, "LA3181", "bytes.split([sep[, maxsplit]]) expects zero to two arguments.", parameterNames: ["sep", "maxsplit"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "rsplit", 0, 2, "LA3181", "bytes.rsplit([sep[, maxsplit]]) expects zero to two arguments.", parameterNames: ["sep", "maxsplit"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "rsplit", 0, 2, "LA3181", "bytes.rsplit([sep[, maxsplit]]) expects zero to two arguments.", parameterNames: ["sep", "maxsplit"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "splitlines", 0, 1, "LA3181", "bytes.splitlines([keepends]) expects zero or one argument.", parameterNames: ["keepends"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "splitlines", 0, 1, "LA3181", "bytes.splitlines([keepends]) expects zero or one argument.", parameterNames: ["keepends"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "strip", 0, 1, "LA3180", "bytes.strip([chars]) expects zero or one argument.", parameterNames: ["chars"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "strip", 0, 1, "LA3180", "bytes.strip([chars]) expects zero or one argument.", parameterNames: ["chars"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "lstrip", 0, 1, "LA3180", "bytes.lstrip([chars]) expects zero or one argument.", parameterNames: ["chars"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "lstrip", 0, 1, "LA3180", "bytes.lstrip([chars]) expects zero or one argument.", parameterNames: ["chars"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "rstrip", 0, 1, "LA3180", "bytes.rstrip([chars]) expects zero or one argument.", parameterNames: ["chars"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "rstrip", 0, 1, "LA3180", "bytes.rstrip([chars]) expects zero or one argument.", parameterNames: ["chars"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "capitalize", 0, 0, "LA3179", "bytes.capitalize() expects no arguments."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "capitalize", 0, 0, "LA3179", "bytes.capitalize() expects no arguments."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "lower", 0, 0, "LA3179", "bytes.lower() expects no arguments."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "lower", 0, 0, "LA3179", "bytes.lower() expects no arguments."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "swapcase", 0, 0, "LA3179", "bytes.swapcase() expects no arguments."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "swapcase", 0, 0, "LA3179", "bytes.swapcase() expects no arguments."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "title", 0, 0, "LA3179", "bytes.title() expects no arguments."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "title", 0, 0, "LA3179", "bytes.title() expects no arguments."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "upper", 0, 0, "LA3179", "bytes.upper() expects no arguments."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "upper", 0, 0, "LA3179", "bytes.upper() expects no arguments."));
        }

        static void AddStringCallableContract(
        List<StaticCallableContract> contracts,
        string memberName,
        int minimumArgumentCount,
        ArgumentCountLimit maximumArgumentCount,
        string message,
        params string[] parameterNames)
        {
            if (parameterNames.Length == 0)
            {
                contracts.Add(new StaticCallableContract(AbstractValueKind.String, memberName, minimumArgumentCount, maximumArgumentCount, "LA3147", message));
                contracts.Add(new StaticCallableContract(AbstractValueKind.StringType, memberName, minimumArgumentCount, maximumArgumentCount, "LA3147", message));
                return;
            }

            contracts.Add(new StaticCallableContract(AbstractValueKind.String, memberName, minimumArgumentCount, maximumArgumentCount, "LA3147", message, parameterNames: parameterNames));
            contracts.Add(new StaticCallableContract(AbstractValueKind.StringType, memberName, minimumArgumentCount, maximumArgumentCount, "LA3147", message, parameterNames: parameterNames));
        }

        static void AddListCallableContracts(List<StaticCallableContract> contracts)
        {
            AddListCallableContract(contracts, "append", 1, 1, "LA3121", "list.append(value) expects one argument.", StaticMutationKind.MutatesReceiver, "value");
            AddListCallableContract(contracts, "extend", 1, 1, "LA3122", "list.extend(iterable) expects one argument.", StaticMutationKind.MutatesReceiver, "iterable");
            AddNonMutatingListCallableContract(contracts, "index", 1, 3, "LA3123", "list.index(value[, start[, stop]]) expects one to three arguments.", "value", "start", "stop");
            AddNonMutatingListCallableContract(contracts, "count", 1, 1, "LA3123", "list.count(value) expects one argument.", "value");
            AddListCallableContract(contracts, "insert", 2, 2, "LA3123", "list.insert(index, value) expects two arguments.", StaticMutationKind.MutatesReceiver, "index", "value");
            AddListCallableContract(contracts, "remove", 1, 1, "LA3123", "list.remove(value) expects one argument.", StaticMutationKind.MutatesReceiver, "value");
            AddListCallableContract(contracts, "pop", 0, 1, "LA3123", "list.pop([index]) expects zero or one argument.", StaticMutationKind.MutatesReceiver, "index");
            AddListCallableContract(contracts, "reverse", 0, 0, "LA3123", "list.reverse() expects no arguments.", StaticMutationKind.MutatesReceiver);
            AddListCallableContract(contracts, "sort", 0, 2, "LA3123", "list.sort(*, key=None, reverse=False) expects optional key/reverse keyword arguments.", StaticMutationKind.MutatesReceiver, "key", "reverse");
            AddNonMutatingListCallableContract(contracts, "copy", 0, 0, "LA3124", "list.copy() expects no arguments.");
            AddListCallableContract(contracts, "clear", 0, 0, "LA3125", "list.clear() expects no arguments.", StaticMutationKind.MutatesReceiver);
        }

        static void AddNonMutatingListCallableContract(List<StaticCallableContract> contracts, string memberName, int minimumArgumentCount, ArgumentCountLimit maximumArgumentCount, string diagnosticCode, string message, params string[] parameterNames)
            => AddListCallableContract(contracts, memberName, minimumArgumentCount, maximumArgumentCount, diagnosticCode, message, StaticMutationKind.None, parameterNames);

        static void AddListCallableContract(
        List<StaticCallableContract> contracts,
        string memberName,
        int minimumArgumentCount,
        ArgumentCountLimit maximumArgumentCount,
        string diagnosticCode,
        string message,
        StaticMutationKind mutation,
        params string[] parameterNames)
        {
            if (parameterNames.Length == 0)
            {
                contracts.Add(new StaticCallableContract(AbstractValueKind.List, memberName, minimumArgumentCount, maximumArgumentCount, diagnosticCode, message, mutation));
                contracts.Add(new StaticCallableContract(AbstractValueKind.ListType, memberName, minimumArgumentCount, maximumArgumentCount, diagnosticCode, message, mutation));
                return;
            }

            contracts.Add(new StaticCallableContract(AbstractValueKind.List, memberName, minimumArgumentCount, maximumArgumentCount, diagnosticCode, message, mutation, parameterNames));
            contracts.Add(new StaticCallableContract(AbstractValueKind.ListType, memberName, minimumArgumentCount, maximumArgumentCount, diagnosticCode, message, mutation, parameterNames));
        }

    }
}
