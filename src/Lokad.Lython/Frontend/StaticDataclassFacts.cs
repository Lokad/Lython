using Lokad.Lython.Runtime;

namespace Lokad.Lython.Frontend;

internal static class StaticDataclassFacts
{
    public static bool IsFieldCall(ExpressionSyntax expression, AbstractState bindings)
        => expression is CallExpressionSyntax call &&
            StaticContractEngine.TryResolveKnownCallableTarget(call.Target, bindings, out var targetName) &&
            string.Equals(targetName, LythonKnownCallableSignatures.DataclassesField.Name, StringComparison.Ordinal);
}
