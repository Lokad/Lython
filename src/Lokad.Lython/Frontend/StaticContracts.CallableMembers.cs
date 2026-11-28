using Lokad.Lython.Runtime;

namespace Lokad.Lython.Frontend;

internal static partial class StaticContracts
{
    private static class CallableMemberCatalog
    {
        internal static readonly Dictionary<StaticMemberContractKey, StaticCallableContract> ByMember = CreateCallableMemberIndex();
    }

    private static class WorkbookCallableMemberCatalog
    {
        internal static readonly Dictionary<StaticMemberContractKey, StaticCallableContract> ByMember =
            CreateOpenPyxlCallableContracts().ToDictionary(static contract => new StaticMemberContractKey(contract.ReceiverKind, contract.MemberName));
    }

    private static class NumericCallableMemberCatalog
    {
        internal static readonly Dictionary<StaticMemberContractKey, StaticCallableContract> ByMember =
            CreateNumericCallableContracts().ToDictionary(static contract => new StaticMemberContractKey(contract.ReceiverKind, contract.MemberName));
    }

    public static bool TryGetCallableContract(AbstractValue receiver, string memberName, out StaticCallableContract contract)
    {
        var kind = receiver.Kind switch
        {
            AbstractValueKind.IntegerType or AbstractValueKind.Boolean or AbstractValueKind.BooleanType => AbstractValueKind.Integer,
            AbstractValueKind.FloatType => AbstractValueKind.Float,
            AbstractValueKind.DecimalTuple => AbstractValueKind.Tuple,
            _ => receiver.Kind
        };
        if (kind is AbstractValueKind.String or AbstractValueKind.StringType)
            return TryGetStringCallableContract(kind, memberName, out contract);

        var catalog = kind switch
        {
            AbstractValueKind.Integer or AbstractValueKind.Float => NumericCallableMemberCatalog.ByMember,
            >= AbstractValueKind.OpenPyxlWorkbook and <= AbstractValueKind.OpenPyxlMergedCellSet => WorkbookCallableMemberCatalog.ByMember,
            _ => CallableMemberCatalog.ByMember
        };
        return catalog.TryGetValue(new(kind, memberName), out contract);
    }

    public static bool IsMutatingMember(AbstractValue receiver, string memberName)
        => TryGetCallableContract(receiver, memberName, out var contract) &&
           contract.Mutation == StaticMutationKind.MutatesReceiver;

    private static Dictionary<StaticMemberContractKey, StaticCallableContract> CreateCallableMemberIndex()
    {
        var core = CreateCoreCallableContracts();
        var data = CreateDataCallableContracts();
        var library = CreateLibraryCallableContracts();
        var bytesAndLists = new List<StaticCallableContract>();
        AddBytesCallableContracts(bytesAndLists);
        AddListCallableContracts(bytesAndLists);

        var contracts = new Dictionary<StaticMemberContractKey, StaticCallableContract>(
            core.Length + data.Length + library.Length + bytesAndLists.Count);
        AddContracts(core);
        AddContracts(data);
        AddContracts(library);
        foreach (var member in bytesAndLists)
            contracts.Add(new(member.ReceiverKind, member.MemberName), member);
        return contracts;

        void AddContracts(ReadOnlySpan<StaticCallableContract> members)
        {
            foreach (var member in members)
                contracts.Add(new(member.ReceiverKind, member.MemberName), member);
        }

        static void AddBytesCallableContracts(List<StaticCallableContract> contracts)
        {
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
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "__iter__", 0, 0, "LA3179", "bytes.__iter__() expects no arguments."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "__iter__", 0, 0, "LA3179", "bytes.__iter__() expects no arguments."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "__len__", 0, 0, "LA3179", "bytes.__len__() expects no arguments."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "__len__", 0, 0, "LA3179", "bytes.__len__() expects no arguments."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "__contains__", 1, 1, "LA3179", "bytes.__contains__(item) expects one argument.", parameterNames: ["item"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "__contains__", 1, 1, "LA3179", "bytes.__contains__(item) expects one argument.", parameterNames: ["item"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "__getitem__", 1, 1, "LA3179", "bytes.__getitem__(index) expects one argument.", parameterNames: ["index"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "__getitem__", 1, 1, "LA3179", "bytes.__getitem__(index) expects one argument.", parameterNames: ["index"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "__add__", 1, 1, "LA3179", "bytes.__add__(value) expects one argument.", parameterNames: ["value"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "__add__", 1, 1, "LA3179", "bytes.__add__(value) expects one argument.", parameterNames: ["value"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "__mul__", 1, 1, "LA3179", "bytes.__mul__(value) expects one argument.", parameterNames: ["value"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "__mul__", 1, 1, "LA3179", "bytes.__mul__(value) expects one argument.", parameterNames: ["value"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "__rmul__", 1, 1, "LA3179", "bytes.__rmul__(value) expects one argument.", parameterNames: ["value"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "__rmul__", 1, 1, "LA3179", "bytes.__rmul__(value) expects one argument.", parameterNames: ["value"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "__eq__", 1, 1, "LA3179", "bytes.__eq__(value) expects one argument.", parameterNames: ["value"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "__eq__", 1, 1, "LA3179", "bytes.__eq__(value) expects one argument.", parameterNames: ["value"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "__ne__", 1, 1, "LA3179", "bytes.__ne__(value) expects one argument.", parameterNames: ["value"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "__ne__", 1, 1, "LA3179", "bytes.__ne__(value) expects one argument.", parameterNames: ["value"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "__lt__", 1, 1, "LA3179", "bytes.__lt__(value) expects one argument.", parameterNames: ["value"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "__lt__", 1, 1, "LA3179", "bytes.__lt__(value) expects one argument.", parameterNames: ["value"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "__le__", 1, 1, "LA3179", "bytes.__le__(value) expects one argument.", parameterNames: ["value"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "__le__", 1, 1, "LA3179", "bytes.__le__(value) expects one argument.", parameterNames: ["value"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "__gt__", 1, 1, "LA3179", "bytes.__gt__(value) expects one argument.", parameterNames: ["value"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "__gt__", 1, 1, "LA3179", "bytes.__gt__(value) expects one argument.", parameterNames: ["value"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "__ge__", 1, 1, "LA3179", "bytes.__ge__(value) expects one argument.", parameterNames: ["value"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "__ge__", 1, 1, "LA3179", "bytes.__ge__(value) expects one argument.", parameterNames: ["value"]));
            contracts.Add(new StaticCallableContract(AbstractValueKind.Bytes, "__hash__", 0, 0, "LA3179", "bytes.__hash__() expects no arguments."));
            contracts.Add(new StaticCallableContract(AbstractValueKind.BytesType, "__hash__", 0, 0, "LA3179", "bytes.__hash__() expects no arguments."));
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
            AddNonMutatingListCallableContract(contracts, "__iter__", 0, 0, "LA3124", "list.__iter__() expects no arguments.");
            AddNonMutatingListCallableContract(contracts, "__len__", 0, 0, "LA3124", "list.__len__() expects no arguments.");
            AddNonMutatingListCallableContract(contracts, "__contains__", 1, 1, "LA3124", "list.__contains__(item) expects one argument.", "item");
            AddNonMutatingListCallableContract(contracts, "__getitem__", 1, 1, "LA3124", "list.__getitem__(index) expects one argument.", "index");
            AddNonMutatingListCallableContract(contracts, "__add__", 1, 1, "LA3124", "list.__add__(value) expects one argument.", "value");
            AddNonMutatingListCallableContract(contracts, "__mul__", 1, 1, "LA3124", "list.__mul__(value) expects one argument.", "value");
            AddNonMutatingListCallableContract(contracts, "__rmul__", 1, 1, "LA3124", "list.__rmul__(value) expects one argument.", "value");
            AddNonMutatingListCallableContract(contracts, "__reversed__", 0, 0, "LA3124", "list.__reversed__() expects no arguments.");
            AddNonMutatingListCallableContract(contracts, "__eq__", 1, 1, "LA3124", "list.__eq__(value) expects one argument.", "value");
            AddNonMutatingListCallableContract(contracts, "__ne__", 1, 1, "LA3124", "list.__ne__(value) expects one argument.", "value");
            AddNonMutatingListCallableContract(contracts, "__lt__", 1, 1, "LA3124", "list.__lt__(value) expects one argument.", "value");
            AddNonMutatingListCallableContract(contracts, "__le__", 1, 1, "LA3124", "list.__le__(value) expects one argument.", "value");
            AddNonMutatingListCallableContract(contracts, "__gt__", 1, 1, "LA3124", "list.__gt__(value) expects one argument.", "value");
            AddNonMutatingListCallableContract(contracts, "__ge__", 1, 1, "LA3124", "list.__ge__(value) expects one argument.", "value");
            AddListCallableContract(contracts, "__setitem__", 2, 2, "LA3124", "list.__setitem__(index, value) expects two arguments.", StaticMutationKind.MutatesReceiver, "index", "value");
            AddListCallableContract(contracts, "__delitem__", 1, 1, "LA3124", "list.__delitem__(index) expects one argument.", StaticMutationKind.MutatesReceiver, "index");
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
