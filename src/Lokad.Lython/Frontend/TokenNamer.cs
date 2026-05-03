using Lokad.Parsing.Error;

namespace Lokad.Lython.Frontend;

internal sealed class TokenNamer : ITokenNamer<Token>
{
    public static readonly TokenNamer Instance = new();

    public bool IsFolded(Token t, ICollection<Token> others)
    {
        _ = t;
        _ = others;
        return false;
    }

    public string TokenName(Token t, ICollection<Token> others)
    {
        _ = others;

        return t switch
        {
            Token.Error => "error",
            Token.End => "end-of-script",
            Token.Eol => "end-of-line",
            Token.Indent => "indent",
            Token.Dedent => "dedent",
            Token.EqualEqual => "'=='",
            Token.BangEqual => "'!='",
            Token.StarStar => "'**'",
            Token.LessLess => "'<<'",
            Token.GreaterGreater => "'>>'",
            Token.ColonEqual => "':='",
            Token.StarStarEqual => "'**='",
            Token.AmpersandEqual => "'&='",
            Token.PipeEqual => "'|='",
            Token.CaretEqual => "'^='",
            Token.LessLessEqual => "'<<='",
            Token.GreaterGreaterEqual => "'>>='",
            Token.SlashSlash => "'//'",
            Token.LessEqual => "'<='",
            Token.GreaterEqual => "'>='",
            Token.Plus => "'+'",
            Token.Minus => "'-'",
            Token.Star => "'*'",
            Token.Slash => "'/'",
            Token.Percent => "'%'",
            Token.Ampersand => "'&'",
            Token.Pipe => "'|'",
            Token.Caret => "'^'",
            Token.Tilde => "'~'",
            Token.At => "'@'",
            Token.Less => "'<'",
            Token.Greater => "'>'",
            Token.Assign => "'='",
            Token.Dot => "'.'",
            Token.Comma => "','",
            Token.Colon => "':'",
            Token.OpenParen => "'('",
            Token.CloseParen => "')'",
            Token.OpenBracket => "'['",
            Token.CloseBracket => "']'",
            Token.OpenBrace => "'{'",
            Token.CloseBrace => "'}'",
            Token.Identifier => "identifier",
            Token.Import => "'import'",
            Token.Def => "'def'",
            Token.Return => "'return'",
            Token.Raise => "'raise'",
            Token.Try => "'try'",
            Token.Except => "'except'",
            Token.Finally => "'finally'",
            Token.As => "'as'",
            Token.Elif => "'elif'",
            Token.With => "'with'",
            Token.Yield => "'yield'",
            Token.Async => "'async'",
            Token.Await => "'await'",
            Token.Lambda => "'lambda'",
            Token.Global => "'global'",
            Token.Nonlocal => "'nonlocal'",
            Token.Assert => "'assert'",
            Token.Del => "'del'",
            Token.From => "'from'",
            Token.Match => "'match'",
            Token.Case => "'case'",
            Token.Class => "'class'",
            Token.And => "'and'",
            Token.Or => "'or'",
            Token.Not => "'not'",
            Token.Is => "'is'",
            Token.If => "'if'",
            Token.Else => "'else'",
            Token.For => "'for'",
            Token.In => "'in'",
            Token.While => "'while'",
            Token.Break => "'break'",
            Token.Continue => "'continue'",
            Token.Pass => "'pass'",
            Token.True => "'True'",
            Token.False => "'False'",
            Token.None => "'None'",
            Token.String => "string",
            Token.Float => "float",
            Token.Integer => "integer",
            _ => throw new ArgumentOutOfRangeException(nameof(t), t, null)
        };
    }
}
