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
            AddStringCallableContract(contracts, "maketrans", 1, 3, "str.maketrans(x[, y[, z]]) expects one to three arguments.");
            AddStringCallableContract(contracts, "encode", 0, 2, "str.encode([encoding][, errors]) expects zero to two arguments.", "encoding", "errors");
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "decode", 0, 2, "LA3147", "bytes.decode([encoding][, errors]) expects zero to two arguments.", parameterNames: ["encoding", "errors"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "decode", 0, 2, "LA3147", "bytes.decode([encoding][, errors]) expects zero to two arguments.", parameterNames: ["encoding", "errors"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "fromhex", 1, 1, "LA3166", "bytes.fromhex(string) expects one argument."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "fromhex", 1, 1, "LA3166", "bytes.fromhex(string) expects one argument."));
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
