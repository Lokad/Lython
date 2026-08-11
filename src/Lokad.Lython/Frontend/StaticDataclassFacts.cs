namespace Lokad.Lython.Frontend;

internal static class StaticDataclassFacts
{
    public static bool IsFieldCall(ExpressionSyntax expression)
        => expression is CallExpressionSyntax
        {
            Target: IdentifierExpressionSyntax { Name: "field" } or
                MemberExpressionSyntax
                {
                    Target: IdentifierExpressionSyntax { Name: "dataclasses" },
                    MemberName: "field"
                }
        };
}
