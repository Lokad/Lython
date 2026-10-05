namespace Lokad.Lython.Frontend;

internal static class AssignmentTargetFacts
{
    public static AssignmentTargetSyntax FromUnpacking(UnpackingTargetSyntax target) => target switch
    {
        UnpackingNameTargetSyntax name => new NameAssignmentTargetSyntax(name.Name, name.Span),
        UnpackingSubscriptTargetSyntax item => new SubscriptAssignmentTargetSyntax(item.Target, item.Index, item.Span),
        UnpackingSliceTargetSyntax slice => new SliceAssignmentTargetSyntax(slice.Target, slice.Start, slice.End, slice.Step, slice.Span),
        UnpackingMemberTargetSyntax member => new MemberAssignmentTargetSyntax(member.Target, member.MemberName, member.Span),
        UnpackingNestedTargetSyntax nested => new UnpackingAssignmentTargetGroupSyntax(nested.Items, nested.Span),
        _ => throw new InvalidOperationException("Unknown unpacking target."),
    };

    public static LoopTargetSyntax ToLoop(AssignmentTargetSyntax target) => target switch
    {
        NameAssignmentTargetSyntax name => new LoopNameTargetSyntax(name.Name),
        UnpackingAssignmentTargetGroupSyntax group => new LoopTupleTargetSyntax(group.Targets.Select(item =>
            item.IsStarred ? Starred(FromUnpacking(item)) : ToLoop(FromUnpacking(item))).ToArray()),
        _ => new LoopStoreTargetSyntax(target),
    };

    public static LoopTargetSyntax Starred(AssignmentTargetSyntax target) => target is NameAssignmentTargetSyntax name
        ? new LoopStarredTargetSyntax(name.Name) : new LoopStoreTargetSyntax(target, true);

    public static bool IsStarred(LoopTargetSyntax target)
        => target is LoopStarredTargetSyntax or LoopStoreTargetSyntax { IsStarred: true };

    public static IEnumerable<string> Names(AssignmentTargetSyntax target)
    {
        if (target is NameAssignmentTargetSyntax name) yield return name.Name;
        if (target is UnpackingAssignmentTargetGroupSyntax group)
            foreach (var item in group.Targets)
                foreach (var child in Names(FromUnpacking(item))) yield return child;
    }

    public static IEnumerable<ExpressionSyntax> Reads(LoopTargetSyntax target)
    {
        if (target is LoopStoreTargetSyntax store)
            foreach (var read in Reads(store.Target)) yield return read;
        if (target is LoopTupleTargetSyntax tuple)
            foreach (var child in tuple.Items)
                foreach (var read in Reads(child)) yield return read;
    }

    public static IEnumerable<ExpressionSyntax> Reads(AssignmentTargetSyntax target)
    {
        switch (target)
        {
            case MemberAssignmentTargetSyntax member: yield return member.Target; break;
            case SubscriptAssignmentTargetSyntax item: yield return item.Target; yield return item.Index; break;
            case SliceAssignmentTargetSyntax slice:
                yield return slice.Target;
                if (slice.Start is not null) yield return slice.Start;
                if (slice.End is not null) yield return slice.End;
                if (slice.Step is not null) yield return slice.Step;
                break;
            case UnpackingAssignmentTargetGroupSyntax group:
                foreach (var item in group.Targets)
                    foreach (var read in Reads(FromUnpacking(item))) yield return read;
                break;
        }
    }
}
