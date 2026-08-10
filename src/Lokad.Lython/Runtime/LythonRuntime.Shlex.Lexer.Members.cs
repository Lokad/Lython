using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed partial class ShlexModule
    {
        private sealed partial class ShlexLexer
        {
            public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            {
                value = name switch
                {
                    "get_token" => BoundCallable.CreateNoArguments(this, "shlex.get_token", static (receiver, span, _) => receiver.GetToken(span)),
                    "read_token" => BoundCallable.CreateNoArguments(this, "shlex.read_token", static (receiver, span, _) => receiver.ReadToken(span)),
                    "push_token" => BoundCallable.Create((arguments, span, _) =>
                    {
                        if (arguments.Length != 1)
                        {
                            throw new LythonRuntimeException("TypeError", "shlex.push_token(tok) expects one argument.", span);
                        }

                        _pushback.AddFirst(arguments[0]);
                        return PyNone.Instance;
                    }, "shlex.push_token", ["tok"]),
                    "sourcehook" => BoundCallable.Create((_, span, _) => throw SourceInclusionUnsupported(span), "shlex.sourcehook", ["newfile"]),
                    "push_source" => BoundCallable.Create(
                        (arguments, span, context) => PushSource(arguments, context, span),
                        async (arguments, span, context) => await PushSourceAsync(arguments, context, span).ConfigureAwait(false),
                        "shlex.push_source",
                        ["newstream", "newfile"],
                        1),
                    "pop_source" => BoundCallable.CreateNoArguments(this, "shlex.pop_source", static (receiver, span, _) =>
                    {
                        receiver.PopSource(span);
                        return PyNone.Instance;
                    }),
                    "__next__" => BoundCallable.CreateNoArguments(this, "shlex.__next__", static (receiver, span, _) =>
                    {
                        var token = receiver.GetToken(span);
                        if (AreEqual(token, receiver._eof))
                        {
                            throw new LythonRuntimeException("StopIteration", "iterator is exhausted", span);
                        }

                        return token;
                    }),
                    "lineno" => new BigInteger(LineNumber),
                    "infile" => _infile,
                    "instream" => _instream,
                    "eof" => _eof,
                    "posix" => Posix,
                    "commenters" => PyString.FromString(Commenters),
                    "whitespace" => PyString.FromString(Whitespace),
                    "whitespace_split" => WhitespaceSplit,
                    "quotes" => PyString.FromString(Quotes),
                    "escape" => PyString.FromString(Escape),
                    "escapedquotes" => PyString.FromString(EscapedQuotes),
                    "wordchars" => PyString.FromString(WordChars),
                    "punctuation_chars" => PyString.FromString(PunctuationChars),
                    "source" => _source,
                    _ => MissingMemberValue.Instance,
                };

                return !ReferenceEquals(value, MissingMemberValue.Instance);
            }

            public bool TrySetMember(string name, object value)
            {
                switch (name)
                {
                    case "lineno":
                        if (value is not BigInteger line || line < 0 || line > int.MaxValue)
                        {
                            throw new LythonRuntimeException("TypeError", "shlex.lineno must be a non-negative integer.", null);
                        }

                        LineNumber = (int)line;
                        return true;
                    case "infile":
                        _infile = value;
                        return true;
                    case "eof":
                        _eof = value;
                        return true;
                    case "posix":
                        Posix = IsTruthy(value);
                        return true;
                    case "commenters":
                        Commenters = RequireConfigurationString(name, value);
                        return true;
                    case "whitespace":
                        Whitespace = RequireConfigurationString(name, value);
                        return true;
                    case "whitespace_split":
                        WhitespaceSplit = IsTruthy(value);
                        return true;
                    case "quotes":
                        Quotes = RequireConfigurationString(name, value);
                        return true;
                    case "escape":
                        Escape = RequireConfigurationString(name, value);
                        return true;
                    case "escapedquotes":
                        EscapedQuotes = RequireConfigurationString(name, value);
                        return true;
                    case "wordchars":
                        WordChars = RequireConfigurationString(name, value);
                        return true;
                    case "source":
                        _source = value;
                        return true;
                    case "punctuation_chars":
                        throw new LythonRuntimeException(
                            "AttributeError",
                            "property 'punctuation_chars' of 'shlex' object has no setter",
                            null);
                    default:
                        return false;
                }
            }

            private static string RequireConfigurationString(string name, object value)
            {
                if (!PyStringOps.TryAsString(value, out var text))
                {
                    throw new LythonRuntimeException("TypeError", $"shlex.{name} must be a string.", null);
                }

                return text.AsString();
            }
        }
    }
}
