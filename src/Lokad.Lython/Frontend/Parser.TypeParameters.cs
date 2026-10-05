namespace Lokad.Lython.Frontend;

internal sealed partial class Parser
{
    private bool TryParseTypeParameters(out IReadOnlyList<TypeParameterSyntax>? parameters)
    {
        parameters = null;
        if (!TryRead(Token.OpenBracket, out var opening)) return true;
        SkipGroupedExpressionTrivia();
        var result = new List<TypeParameterSyntax>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var hasDefault = false;
        while (CurrentToken != Token.CloseBracket)
        {
            var kind = TryRead(Token.StarStar, out _) ? TypeParameterKind.ParamSpec
                : TryRead(Token.Star, out _) ? TypeParameterKind.TypeVarTuple : TypeParameterKind.TypeVar;
            if (!TryReadNameToken(out var nameToken)) return Fail("Expected type parameter name.", opening);
            var name = IdentifierText(nameToken);
            if (!names.Add(name)) return Fail("Duplicate type parameter name.", nameToken);
            ExpressionSyntax? bound = null, defaultValue = null;
            if (TryRead(Token.Colon, out _))
            {
                if (kind != TypeParameterKind.TypeVar) return Fail("Only TypeVar parameters accept bounds or constraints.", nameToken);
                bound = ParseExpression();
                if (bound is null) return false;
            }
            var unpackDefault = false;
            if (TryRead(Token.Assign, out _))
            {
                unpackDefault = kind == TypeParameterKind.TypeVarTuple && TryRead(Token.Star, out _);
                defaultValue = ParseExpression();
                if (defaultValue is null) return false;
                hasDefault = true;
            }
            else if (hasDefault) return Fail("Non-default type parameter follows default type parameter.", nameToken);
            result.Add(new TypeParameterSyntax(name, kind, bound, defaultValue, unpackDefault, SpanOf(nameToken)));
            SkipGroupedExpressionTrivia();
            if (!TryRead(Token.Comma, out _)) break;
            SkipGroupedExpressionTrivia();
        }
        if (result.Count == 0) return Fail("Type parameter list cannot be empty.", opening);
        if (!TryRead(Token.CloseBracket, out _)) return Fail("Expected ']' after type parameters.", opening);
        parameters = result;
        return true;

        bool Fail(string message, int token)
        {
            AddDiagnostic("LA1100", message, token);
            return false;
        }
    }

    private StatementSyntax? ParseTypeAlias()
    {
        var typeToken = ReadToken();
        if (!TryReadNameToken(out var nameToken) || !TryParseTypeParameters(out var parameters)) return null;
        if (!TryRead(Token.Assign, out _))
        {
            AddDiagnostic("LA1100", "Expected '=' after type alias name.", nameToken);
            return null;
        }
        var value = ParseExpression();
        return value is null ? null : new TypeAliasStatementSyntax(IdentifierText(nameToken), parameters, value, Merge(SpanOf(typeToken), value.Span));
    }
}
