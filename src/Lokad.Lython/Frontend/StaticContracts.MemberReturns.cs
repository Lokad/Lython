namespace Lokad.Lython.Frontend;

internal static partial class StaticContracts
{
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
        if (GetBuiltinMemberCatalog(receiverKind) is { } members)
        {
            if (members.TryGetValue(memberName, out var member) && member.Return is { } returns)
            {
                shape = returns;
                return true;
            }

            shape = default;
            return false;
        }

        var fallback = LookupFallbackMemberReturn(receiverKind, memberName);
        shape = fallback.GetValueOrDefault();
        return fallback.HasValue;
    }
}
