using Lokad.Lython.Runtime;

namespace Lokad.Lython.Frontend;

internal static class StaticTypingContractFamily
{
    public static bool TryResolveKnownCallReturn(
        string targetName,
        ConcreteCallArguments arguments,
        AbstractState bindings,
        LythonSourceSpan span,
        out AbstractValue value)
    {
        if (string.Equals(targetName, LythonKnownCallableSignatures.TypingCast.Name, StringComparison.Ordinal) &&
            arguments.TryGetValue(1, "val", out var valueExpression))
        {
            value = StaticAbstractValueResolver.TryResolve(valueExpression, bindings, out var resolved)
                ? resolved.WithSpan(span)
                : AbstractValue.Unknown(span);
            return true;
        }

        value = default;
        return false;
    }
}
