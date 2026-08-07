using Lokad.Parsing.Lexer;

namespace Lokad.Lython.Frontend;

internal sealed class FAttribute : FromAttribute
{
    public FAttribute(Token parent) : this(parent, false) { }

    public FAttribute(Token parent, bool isPrivate) : base((int)parent, isPrivate)
    {
    }
}

[Tokens(EscapeNewlines = true, Comments = "#[^\\n]*")]
internal enum Token
{
    [Error] Error,
    [End] End,
    [EndOfLine] Eol,
    [Indent] Indent,
    [Dedent] Dedent,

    [Any("==")] EqualEqual,
    [Any("!=")] BangEqual,
    [Any("**")] StarStar,
    [Any("<<")] LessLess,
    [Any(">>")] GreaterGreater,
    [Any("->")] Arrow,
    [Any(":=")] ColonEqual,
    [Any("//=")] SlashSlashEqual,
    [Any("**=")] StarStarEqual,
    [Any("&=")] AmpersandEqual,
    [Any("|=")] PipeEqual,
    [Any("^=")] CaretEqual,
    [Any("<<=")] LessLessEqual,
    [Any(">>=")] GreaterGreaterEqual,
    [Any("+=")] PlusEqual,
    [Any("-=")] MinusEqual,
    [Any("*=")] StarEqual,
    [Any("/=")] SlashEqual,
    [Any("%=")] PercentEqual,
    [Any("//")] SlashSlash,
    [Any("<=")] LessEqual,
    [Any(">=")] GreaterEqual,
    [Any("+")] Plus,
    [Any("-")] Minus,
    [Any("*")] Star,
    [Any("/")] Slash,
    [Any("%")] Percent,
    [Any("&")] Ampersand,
    [Any("|")] Pipe,
    [Any("^")] Caret,
    [Any("~")] Tilde,
    [Any("@")] At,
    [Any("<")] Less,
    [Any(">")] Greater,
    [Any("=")] Assign,
    [Any(".")] Dot,
    [Any(",")] Comma,
    [Any(";")] Semicolon,
    [Any(":")] Colon,
    [Any("(")] OpenParen,
    [Any(")")] CloseParen,
    [Any("[")] OpenBracket,
    [Any("]")] CloseBracket,
    [Any("{")] OpenBrace,
    [Any("}")] CloseBrace,

    [Pattern("[_\\p{L}\\p{Nl}][\\p{L}\\p{Nl}\\p{Mn}\\p{Mc}\\p{Nd}\\p{Pc}]*")] Identifier,
    [Any("import"), F(Identifier, true)] Import,
    [Any("def"), F(Identifier, true)] Def,
    [Any("return"), F(Identifier, true)] Return,
    [Any("raise"), F(Identifier, true)] Raise,
    [Any("try"), F(Identifier, true)] Try,
    [Any("except"), F(Identifier, true)] Except,
    [Any("finally"), F(Identifier, true)] Finally,
    [Any("as"), F(Identifier, true)] As,
    [Any("elif"), F(Identifier, true)] Elif,
    [Any("with"), F(Identifier, true)] With,
    [Any("yield"), F(Identifier, true)] Yield,
    [Any("async"), F(Identifier, true)] Async,
    [Any("await"), F(Identifier, true)] Await,
    [Any("lambda"), F(Identifier, true)] Lambda,
    [Any("global"), F(Identifier, true)] Global,
    [Any("nonlocal"), F(Identifier, true)] Nonlocal,
    [Any("assert"), F(Identifier, true)] Assert,
    [Any("del"), F(Identifier, true)] Del,
    [Any("from"), F(Identifier, true)] From,
    [Any("match"), F(Identifier, true)] Match,
    [Any("case"), F(Identifier, true)] Case,
    [Any("class"), F(Identifier, true)] Class,
    [Any("and"), F(Identifier, true)] And,
    [Any("or"), F(Identifier, true)] Or,
    [Any("not"), F(Identifier, true)] Not,
    [Any("is"), F(Identifier, true)] Is,
    [Any("if"), F(Identifier, true)] If,
    [Any("else"), F(Identifier, true)] Else,
    [Any("for"), F(Identifier, true)] For,
    [Any("in"), F(Identifier, true)] In,
    [Any("while"), F(Identifier, true)] While,
    [Any("break"), F(Identifier, true)] Break,
    [Any("continue"), F(Identifier, true)] Continue,
    [Any("pass"), F(Identifier, true)] Pass,
    [Any("True"), F(Identifier, true)] True,
    [Any("False"), F(Identifier, true)] False,
    [Any("None"), F(Identifier, true)] None,

    [Pattern("(?:[rR]\"\"\"[\\s\\S]*?\"\"\")|(?:[rR]'''[\\s\\S]*?''')|(?:\"\"\"[\\s\\S]*?\"\"\")|(?:'''[\\s\\S]*?''')|(?:[rR]\"([^\"\\\\\\n]|\\\\[^\\n])*\")|(?:[rR]'([^'\\\\\\n]|\\\\[^\\n])*')|(?:\"([^\"\\\\\\n]|\\\\[^\\n])*\")|(?:'([^'\\\\\\n]|\\\\[^\\n])*')", Start = "\"'rR")]
    String,
    [Pattern("(?:0(?:_?0)*_?[1-9][0-9_]*|[0-9][0-9_]*__[0-9_]*|[0-9][0-9_]*_)", Start = "0123456789")]
    MalformedNumber,
    [Pattern("(?:(?:[0-9](?:_?[0-9])*)?\\.[0-9](?:_?[0-9])*|[0-9](?:_?[0-9])*\\.)(?:[eE][+-]?[0-9](?:_?[0-9])*)?|[0-9](?:_?[0-9])*[eE][+-]?[0-9](?:_?[0-9])*", Start = "0123456789.")]
    Float,
    [Pattern("(?:[1-9](?:_?[0-9])*|0(?:_?0)*)", Start = "0123456789")]
    Integer,
}
