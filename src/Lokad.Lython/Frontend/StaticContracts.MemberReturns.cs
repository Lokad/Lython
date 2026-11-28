namespace Lokad.Lython.Frontend;

internal static partial class StaticContracts
{
    private static class MemberReturnCatalog
    {
        internal static readonly Dictionary<StaticMemberContractKey, StaticReturnShape> ByMember = CreateMemberReturnShapes();
    }

    public static bool TryGetMemberReturn(AbstractValue receiver, string memberName, LythonSourceSpan span,
        StaticAnalysisContext? context, out AbstractValue value)
    {
        if (TryGetMemberReturnShape(receiver.Kind, memberName, out var shape))
        {
            value = CreateReturnValue(shape, receiver, span, context);
            return true;
        }

        value = default;
        return false;
    }

    internal static bool TryGetMemberReturnShape(AbstractValueKind receiverKind, string memberName, out StaticReturnShape shape)
    {
        if (receiverKind is AbstractValueKind.String or AbstractValueKind.StringType)
        {
            if (StringMemberCatalog.ByName.TryGetValue(memberName, out var member) && member.Return is { } returns)
            {
                shape = returns;
                return true;
            }

            shape = default;
            return false;
        }

        return MemberReturnCatalog.ByMember.TryGetValue(new(receiverKind, memberName), out shape);
    }

    private static Dictionary<StaticMemberContractKey, StaticReturnShape> CreateMemberReturnShapes()
    {
        var core = CreateCoreMemberReturnContracts();
        var extended = CreateExtendedMemberReturnContracts();
        var shapes = new Dictionary<StaticMemberContractKey, StaticReturnShape>(core.Length + extended.Length);

        foreach (var contract in core)
            shapes.Add(new(contract.ReceiverKind, contract.MemberName), contract.ReturnShape);
        foreach (var contract in extended)
            shapes.Add(new(contract.ReceiverKind, contract.MemberName), contract.ReturnShape);

        return shapes;
    }
}
