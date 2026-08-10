using System.Text;
using Lokad.Parsing.Lexer;

namespace Lokad.Lython.Frontend;

internal sealed partial class Parser
{
    private StatementSyntax? ParseImportStatement()
    {
        if (CurrentToken == Token.From)
        {
            return ParseFromImportStatement();
        }

        var importToken = ReadToken();
        var statements = new List<StatementSyntax>();
        while (true)
        {
            if (!TryReadDottedModuleName(
                    "LA1001",
                    statements.Count == 0 ? "Expected module name after 'import'." : "Expected module name after ','.",
                    importToken,
                    out var moduleName,
                    out var moduleStartToken,
                    out var moduleEndToken))
            {
                return null;
            }

            var boundModuleName = moduleName.Split('.')[0];
            var bindingName = boundModuleName;
            var endToken = moduleEndToken;

            if (CurrentToken == Token.As)
            {
                ReadToken();
                if (!TryRead(Token.Identifier, out var aliasToken))
                {
                    AddDiagnostic("LA1051", "Expected alias name after 'as'.", _position);
                    return null;
                }

                bindingName = IdentifierText(aliasToken);
                boundModuleName = moduleName;
                endToken = aliasToken;
            }

            if (!IsSupportedImport(moduleName))
            {
                AddDiagnostic("LA1002", $"Unsupported module '{moduleName}'.", moduleStartToken);
                return null;
            }

            statements.Add(new ImportStatementSyntax(
                moduleName,
                bindingName,
                boundModuleName,
                null,
                Merge(importToken, endToken)));

            if (CurrentToken != Token.Comma)
            {
                break;
            }

            ReadToken();
        }

        for (var i = 1; i < statements.Count; i++)
        {
            _pendingStatements.Enqueue(statements[i]);
        }

        return statements[0];
    }

    private StatementSyntax? ParseFromImportStatement()
    {
        var fromToken = ReadToken();
        if (!TryReadDottedModuleName(
                "LA1052",
                "Expected module name after 'from'.",
                fromToken,
                out var moduleName,
                out var moduleStartToken,
                out var moduleEndToken))
        {
            return null;
        }

        if (!IsSupportedImport(moduleName))
        {
            AddDiagnostic("LA1002", $"Unsupported module '{moduleName}'.", moduleStartToken);
            return null;
        }

        if (!TryRead(Token.Import, out _))
        {
            AddDiagnostic("LA1053", "Expected 'import' after module name.", moduleEndToken);
            return null;
        }

        var importedMembers = new List<ImportedMemberSyntax>();
        var grouped = false;
        if (CurrentToken == Token.OpenParen)
        {
            ReadToken();
            grouped = true;
            SkipGroupedImportTrivia();
        }

        if (CurrentToken == Token.Star)
        {
            var starToken = ReadToken();
            importedMembers.Add(new ImportedMemberSyntax("*", "*"));
            if (grouped)
            {
                SkipGroupedImportTrivia();
                if (!TryRead(Token.CloseParen, out _))
                {
                    AddDiagnostic("LA1067", "Expected ')' after grouped import list.", _position);
                    return null;
                }
            }

            return new ImportStatementSyntax(
                moduleName,
                moduleName,
                moduleName,
                importedMembers,
                Merge(fromToken, starToken));
        }

        while (true)
        {
            if (!TryRead(Token.Identifier, out var memberToken))
            {
                AddDiagnostic("LA1054", "Expected imported member name.", _position);
                return null;
            }

            var memberName = IdentifierText(memberToken);
            var bindingName = memberName;
            if (CurrentToken == Token.As)
            {
                ReadToken();
                if (!TryRead(Token.Identifier, out var aliasToken))
                {
                    AddDiagnostic("LA1055", "Expected alias name after 'as'.", _position);
                    return null;
                }

                bindingName = IdentifierText(aliasToken);
            }

            importedMembers.Add(new ImportedMemberSyntax(memberName, bindingName));

            if (CurrentToken != Token.Comma)
            {
                break;
            }

            ReadToken();
            if (grouped)
            {
                SkipGroupedImportTrivia();
                if (CurrentToken == Token.CloseParen)
                {
                    break;
                }
            }
        }

        if (grouped)
        {
            SkipGroupedImportTrivia();
        }

        if (grouped && !TryRead(Token.CloseParen, out _))
        {
            AddDiagnostic("LA1067", "Expected ')' after grouped import list.", _position);
            return null;
        }

        return new ImportStatementSyntax(
            moduleName,
            moduleName,
            moduleName,
            importedMembers,
            Merge(fromToken, moduleEndToken));
    }

    private StatementSyntax? ParseScopeDirectiveStatement()
    {
        var directiveToken = ReadToken();
        var kind = _tokens.Tokens[directiveToken].Token == Token.Global
            ? ScopeDirectiveKind.Global
            : ScopeDirectiveKind.Nonlocal;
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var endToken = directiveToken;

        while (true)
        {
            if (!TryReadNameToken(out var nameToken))
            {
                AddDiagnostic(
                    "LA1070",
                    $"Expected identifier after '{(kind == ScopeDirectiveKind.Global ? "global" : "nonlocal")}'.",
                    _position);
                return null;
            }

            var name = IdentifierText(nameToken);
            if (!seen.Add(name))
            {
                AddDiagnostic("LA1071", $"Duplicate scope directive name '{name}'.", nameToken);
                return null;
            }

            names.Add(name);
            endToken = nameToken;

            if (CurrentToken != Token.Comma)
            {
                break;
            }

            ReadToken();
        }

        return new ScopeDirectiveStatementSyntax(kind, names, Merge(directiveToken, endToken));
    }

    private bool TryReadDottedModuleName(
        string diagnosticCode,
        string diagnosticMessage,
        int anchorToken,
        out string moduleName,
        out int startToken,
        out int endToken)
    {
        moduleName = string.Empty;
        startToken = -1;
        endToken = -1;

        if (!TryRead(Token.Identifier, out startToken))
        {
            AddDiagnostic(diagnosticCode, diagnosticMessage, anchorToken);
            return false;
        }

        endToken = startToken;
        var parts = new List<string> { IdentifierText(startToken) };
        while (CurrentToken == Token.Dot)
        {
            ReadToken();
            if (!TryRead(Token.Identifier, out var partToken))
            {
                AddDiagnostic(diagnosticCode, "Expected module name after '.'.", _position);
                return false;
            }

            parts.Add(IdentifierText(partToken));
            endToken = partToken;
        }

        moduleName = string.Join(".", parts);
        return true;
    }
    private static bool IsSupportedImport(string moduleName)
    {
        return !string.IsNullOrWhiteSpace(moduleName);
    }

}
