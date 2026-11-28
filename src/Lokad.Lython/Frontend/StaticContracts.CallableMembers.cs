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
        if (GetBuiltinMemberCatalog(kind) is { } members)
        {
            if (members.TryGetValue(memberName, out var member) && member.Call is { } call)
            {
                contract = call with { ReceiverKind = kind };
                return true;
            }

            contract = default;
            return false;
        }

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
        var contracts = new Dictionary<StaticMemberContractKey, StaticCallableContract>(core.Length + data.Length + library.Length);
        AddContracts(core);
        AddContracts(data);
        AddContracts(library);
        return contracts;

        void AddContracts(ReadOnlySpan<StaticCallableContract> members)
        {
            foreach (var member in members)
                contracts.Add(new(member.ReceiverKind, member.MemberName), member);
        }
    }
}
