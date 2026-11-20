using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Frontend;

internal sealed record LoweredTypeParameter(TypeParameterSyntax Syntax, LoweredExpression? Bound, LoweredExpression? Default);

internal readonly record struct LoweredFunctionAnnotation(PyString Key, ExpressionSyntax Syntax, LoweredExpression? Expression);

internal sealed record LoweredTypeAlias(TypeAliasStatementSyntax Syntax, LoweredExpression Value)
{
    internal LythonSourceSpan Span => Syntax.Span;
    internal IReadOnlyList<LoweredTypeParameter>? TypeParameters { get; } = LoweredScript.LowerTypeParameters(Syntax.TypeParameters);
}
