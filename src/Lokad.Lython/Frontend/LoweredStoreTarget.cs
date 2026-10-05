namespace Lokad.Lython.Frontend;

// Header target expressions are lowered once, before execution, like body
// expressions. The runtime must not rebuild expression trees on every pull.
internal sealed class LoweredStoreTarget(AssignmentTargetSyntax syntax)
{
    public AssignmentTargetSyntax Syntax { get; } = syntax;
    public IReadOnlyDictionary<ExpressionSyntax, LoweredExpression> Reads { get; } =
        AssignmentTargetFacts.Reads(syntax).Distinct().ToDictionary(read => read, LoweredScript.LowerExpression);
}
