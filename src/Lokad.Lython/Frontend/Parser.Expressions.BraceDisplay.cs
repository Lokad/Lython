using Lokad.Parsing.Lexer;

namespace Lokad.Lython.Frontend;

internal sealed partial class Parser
{
    /// <summary>
    /// Parses the grammar shared by dictionary displays, set displays, and
    /// their comprehensions while keeping the display-kind state explicit.
    /// </summary>
    private sealed class BraceDisplayParser
    {
        private readonly Parser _parser;
        private readonly int _openBrace;
        private readonly List<DictionaryDisplayItemSyntax> _dictionaryItems = [];
        private readonly List<CollectionDisplayItemSyntax> _setItems = [];
        private BraceDisplayKind? _displayKind;

        public BraceDisplayParser(Parser parser)
        {
            _parser = parser;
            _openBrace = parser.ReadToken();
        }

        public ExpressionSyntax? Parse()
        {
            _parser.SkipGroupedExpressionTrivia();
            if (_parser.CurrentToken == Token.End)
            {
                AddUnexpectedEndDiagnostic();
                return null;
            }

            while (_parser.CurrentToken != Token.CloseBrace)
            {
                if (_parser.CurrentToken == Token.End)
                {
                    AddUnexpectedEndDiagnostic();
                    return null;
                }

                if (!TryParseDisplayItem(out var completedComprehension))
                {
                    return null;
                }

                if (completedComprehension is not null)
                {
                    return completedComprehension;
                }

                if (_parser.CurrentToken != Token.Comma)
                {
                    break;
                }

                _parser.ReadToken();
                _parser.SkipGroupedExpressionTrivia();
            }

            _parser.SkipGroupedExpressionTrivia();
            if (!_parser.TryRead(Token.CloseBrace, out var closeBrace))
            {
                _parser.AddDiagnostic("LA1026", "Expected '}' after dictionary literal.", _openBrace);
                return null;
            }

            // Python reserves the empty brace display for a dictionary; a set
            // must establish its kind with at least one value or starred value.
            return _displayKind == BraceDisplayKind.Set
                ? new SetLiteralExpressionSyntax(
                    _setItems,
                    Merge(_parser.SpanOf(_openBrace), _parser.SpanOf(closeBrace)))
                : new DictLiteralExpressionSyntax(
                    _dictionaryItems,
                    Merge(_parser.SpanOf(_openBrace), _parser.SpanOf(closeBrace)));
        }

        private bool TryParseDisplayItem(out ExpressionSyntax? completedComprehension)
        {
            completedComprehension = null;
            if (_parser.CurrentToken == Token.StarStar)
            {
                return TryParseDictionaryUnpacking();
            }

            var isSetUnpacking = _parser.CurrentToken == Token.Star;
            var setUnpackingSpan = default(LythonSourceSpan?);
            if (isSetUnpacking)
            {
                if (_displayKind == BraceDisplayKind.Dictionary)
                {
                    _parser.AddDiagnostic(
                        "LA2000",
                        "Unsupported Python construct 'set unpacking in dictionary display'.",
                        _parser._position);
                    return false;
                }

                setUnpackingSpan = _parser.SpanOf(_parser.ReadToken());
            }

            var key = _parser.ParseExpression();
            if (key is null)
            {
                return false;
            }
            _parser.SkipGroupedExpressionTrivia();

            if (_parser.TryRead(Token.Colon, out var colonToken))
            {
                return TryParseDictionaryItem(
                    key,
                    colonToken,
                    isSetUnpacking,
                    out completedComprehension);
            }

            return TryParseSetItem(key, isSetUnpacking, setUnpackingSpan, out completedComprehension);
        }

        private bool TryParseDictionaryItem(
            ExpressionSyntax key,
            int colonToken,
            bool isSetUnpacking,
            out ExpressionSyntax? completedComprehension)
        {
            completedComprehension = null;
            if (isSetUnpacking || _displayKind == BraceDisplayKind.Set)
            {
                _parser.AddDiagnostic("LA1024", "Cannot mix set items with dictionary entries.", key.Span);
                return false;
            }

            _parser.SkipGroupedExpressionTrivia();
            var value = _parser.ParseExpression();
            if (value is null)
            {
                _parser.AddDiagnostic("LA1025", "Expected value in dictionary literal.", colonToken);
                return false;
            }
            _parser.SkipGroupedExpressionTrivia();

            _displayKind = BraceDisplayKind.Dictionary;
            _dictionaryItems.Add(new DictionaryKeyValueItemSyntax(
                key,
                value,
                Merge(key.Span, value.Span)));
            if (_parser.CurrentToken == Token.For)
            {
                completedComprehension = ParseDictionaryComprehension(key, value);
                return completedComprehension is not null;
            }

            return true;
        }

        private bool TryParseSetItem(
            ExpressionSyntax item,
            bool isUnpacking,
            LythonSourceSpan? unpackingSpan,
            out ExpressionSyntax? completedComprehension)
        {
            completedComprehension = null;
            if (_displayKind == BraceDisplayKind.Dictionary)
            {
                _parser.AddDiagnostic("LA1024", "Expected ':' in dictionary literal.", item.Span);
                return false;
            }

            if (_parser.CurrentToken == Token.For)
            {
                if (isUnpacking || _setItems.Count != 0)
                {
                    _parser.AddDiagnostic(
                        "LA2000",
                        "Unsupported Python construct 'iterable unpacking in comprehension'.",
                        _parser._position);
                    return false;
                }

                completedComprehension = ParseSetComprehension(item);
                return completedComprehension is not null;
            }

            _displayKind = BraceDisplayKind.Set;
            _setItems.Add(isUnpacking
                ? new CollectionUnpackingItemSyntax(
                    item,
                    Merge(unpackingSpan ?? item.Span, item.Span))
                : new CollectionValueItemSyntax(item));
            return true;
        }

        private bool TryParseDictionaryUnpacking()
        {
            if (_displayKind == BraceDisplayKind.Set)
            {
                _parser.AddDiagnostic(
                    "LA2000",
                    "Unsupported Python construct 'dictionary unpacking in set display'.",
                    _parser._position);
                return false;
            }

            var unpackToken = _parser.ReadToken();
            _parser.SkipGroupedExpressionTrivia();
            var mapping = _parser.ParseExpression();
            if (mapping is null)
            {
                _parser.AddDiagnostic(
                    "LA1025",
                    "Expected mapping after '**' in dictionary literal.",
                    unpackToken);
                return false;
            }

            _displayKind = BraceDisplayKind.Dictionary;
            _dictionaryItems.Add(new DictionaryUnpackingItemSyntax(
                mapping,
                Merge(_parser.SpanOf(unpackToken), mapping.Span)));
            _parser.SkipGroupedExpressionTrivia();
            return true;
        }

        private ExpressionSyntax? ParseDictionaryComprehension(ExpressionSyntax key, ExpressionSyntax value)
        {
            // A comprehension consumes the whole display and is legal only as
            // its first entry; earlier unpacking or entries cannot be combined.
            if (_dictionaryItems.Count != 1 || _dictionaryItems[0] is not DictionaryKeyValueItemSyntax)
            {
                _parser.AddDiagnostic("LA2000", "Unsupported Python construct 'comprehension'.", _parser._position);
                return null;
            }

            if (!_parser.TryParseComprehensionClauses(out var clauses, out _))
            {
                return null;
            }

            _parser.SkipGroupedExpressionTrivia();
            if (!_parser.TryRead(Token.CloseBrace, out var closeComprehension))
            {
                _parser.AddDiagnostic("LA1026", "Expected '}' after dictionary literal.", _openBrace);
                return null;
            }

            return new DictComprehensionExpressionSyntax(
                key,
                value,
                clauses,
                Merge(_parser.SpanOf(_openBrace), _parser.SpanOf(closeComprehension)));
        }

        private ExpressionSyntax? ParseSetComprehension(ExpressionSyntax item)
        {
            if (!_parser.TryParseComprehensionClauses(out var clauses, out _))
            {
                return null;
            }

            _parser.SkipGroupedExpressionTrivia();
            if (!_parser.TryRead(Token.CloseBrace, out var closeComprehension))
            {
                _parser.AddDiagnostic("LA1026", "Expected '}' after set comprehension.", _openBrace);
                return null;
            }

            return new SetComprehensionExpressionSyntax(
                item,
                clauses,
                Merge(_parser.SpanOf(_openBrace), _parser.SpanOf(closeComprehension)));
        }

        private void AddUnexpectedEndDiagnostic()
        {
            _parser.AddDiagnostic(
                "LA1026",
                "Unexpected end of file while parsing dictionary or set literal; expected '}'.",
                _openBrace);
        }

        private enum BraceDisplayKind
        {
            Dictionary,
            Set
        }
    }
}
