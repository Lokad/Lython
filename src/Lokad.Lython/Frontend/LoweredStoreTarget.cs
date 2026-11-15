using Lokad.Lython.Runtime;

namespace Lokad.Lython.Frontend;

// Stores carry their actual lowered reads and unpacking layout. Both executable
// construction and retained lowered execution consume this same preparation;
// neither hashes syntax nor reconstructs nested targets during an assignment.
internal abstract record LoweredStoreTarget(LythonSourceSpan Span)
{
    internal static LoweredStoreTarget FromSyntax(AssignmentTargetSyntax target) => target switch
    {
        NameAssignmentTargetSyntax name => new LoweredNameStoreTarget(name.Name, name.Span),
        MemberAssignmentTargetSyntax member => new LoweredMemberStoreTarget(
            LoweredScript.LowerExpression(member.Target), member.MemberName, member.Span),
        SubscriptAssignmentTargetSyntax item => new LoweredSubscriptStoreTarget(
            LoweredScript.LowerExpression(item.Target), LoweredScript.LowerExpression(item.Index), item.Span),
        SliceAssignmentTargetSyntax slice => new LoweredSliceStoreTarget(
            LoweredScript.LowerExpression(slice.Target), Lower(slice.Start), Lower(slice.End), Lower(slice.Step), slice.Span),
        UnpackingAssignmentTargetGroupSyntax group => FromUnpacking(group.Targets, group.Span),
        _ => throw new InvalidOperationException("Unknown assignment target."),
    };

    internal static LoweredStoreTarget FromUnpacking(IReadOnlyList<UnpackingTargetSyntax> targets, LythonSourceSpan span)
        => new LoweredUnpackingStoreTarget(targets.Select(LowerUnpacking).ToArray(), UnpackingLayout.FromTargets(targets), span);

    private static LoweredStoreTarget LowerUnpacking(UnpackingTargetSyntax target) => target switch
    {
        UnpackingNameTargetSyntax name => new LoweredNameStoreTarget(name.Name, name.Span),
        UnpackingMemberTargetSyntax member => new LoweredMemberStoreTarget(
            LoweredScript.LowerExpression(member.Target), member.MemberName, member.Span),
        UnpackingSubscriptTargetSyntax item => new LoweredSubscriptStoreTarget(
            LoweredScript.LowerExpression(item.Target), LoweredScript.LowerExpression(item.Index), item.Span),
        UnpackingSliceTargetSyntax slice => new LoweredSliceStoreTarget(
            LoweredScript.LowerExpression(slice.Target), Lower(slice.Start), Lower(slice.End), Lower(slice.Step), slice.Span),
        UnpackingNestedTargetSyntax nested => FromUnpacking(nested.Items, nested.Span),
        _ => throw new InvalidOperationException("Unknown unpacking target."),
    };

    internal static LoweredStoreTarget FromLoop(LoopTargetSyntax target, LythonSourceSpan span) => target switch
    {
        LoopNameTargetSyntax name => new LoweredNameStoreTarget(name.Name, span),
        LoopStarredTargetSyntax name => new LoweredNameStoreTarget(name.Name, span),
        LoopStoreTargetSyntax store => FromSyntax(store.Target),
        LoopTupleTargetSyntax tuple => new LoweredUnpackingStoreTarget(
            tuple.Items.Select(item => FromLoop(item, span)).ToArray(), LoopLayout(tuple.Items), span),
        _ => throw new InvalidOperationException("Unknown loop target."),
    };

    private static UnpackingLayout LoopLayout(IReadOnlyList<LoopTargetSyntax> targets)
    {
        var starred = -1;
        for (var i = 0; i < targets.Count; i++)
            if (AssignmentTargetFacts.IsStarred(targets[i]))
            {
                if (starred >= 0) throw new InvalidOperationException("Multiple starred loop targets.");
                starred = i;
            }
        return new(targets.Count, starred);
    }

    private static LoweredExpression? Lower(ExpressionSyntax? value)
        => value is null ? null : LoweredScript.LowerExpression(value);

    internal IEnumerable<LoweredExpression> EnumerateReads()
    {
        switch (this)
        {
            case LoweredMemberStoreTarget member: yield return member.Receiver; break;
            case LoweredSubscriptStoreTarget item:
                yield return item.Receiver; yield return item.Index; break;
            case LoweredSliceStoreTarget slice:
                yield return slice.Receiver;
                if (slice.Start is not null) yield return slice.Start;
                if (slice.End is not null) yield return slice.End;
                if (slice.Step is not null) yield return slice.Step;
                break;
            case LoweredUnpackingStoreTarget group:
                foreach (var child in group.Items)
                    foreach (var read in child.EnumerateReads()) yield return read;
                break;
        }
    }
}

internal sealed record LoweredNameStoreTarget(string Name, LythonSourceSpan Span) : LoweredStoreTarget(Span);
internal sealed record LoweredMemberStoreTarget(LoweredExpression Receiver, string MemberName, LythonSourceSpan Span) : LoweredStoreTarget(Span);
internal sealed record LoweredSubscriptStoreTarget(LoweredExpression Receiver, LoweredExpression Index, LythonSourceSpan Span) : LoweredStoreTarget(Span);
internal sealed record LoweredSliceStoreTarget(LoweredExpression Receiver, LoweredExpression? Start,
    LoweredExpression? End, LoweredExpression? Step, LythonSourceSpan Span) : LoweredStoreTarget(Span);
internal sealed record LoweredUnpackingStoreTarget(IReadOnlyList<LoweredStoreTarget> Items,
    UnpackingLayout Layout, LythonSourceSpan Span) : LoweredStoreTarget(Span);
