using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class GlobModule : PyModule
    {
        public static readonly GlobModule Instance = new();

        private GlobModule() : base("glob")
        {
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "glob" => BuiltinCallable.Create(LythonKnownCallableSignatures.Glob, Glob, GlobAsync),
                "iglob" => BuiltinCallable.Create(LythonKnownCallableSignatures.IGlob, IGlob, IGlobAsync),
                "escape" => BuiltinCallable.Create(LythonKnownCallableSignatures.GlobEscape, Escape, EscapeAsync),
                "has_magic" => BuiltinCallable.Create(LythonKnownCallableSignatures.GlobHasMagic, HasMagic, HasMagicAsync),
                "translate" => BuiltinCallable.Create(LythonKnownCallableSignatures.GlobTranslate, Translate, TranslateAsync),
                "glob0" => BuiltinCallable.Create(LythonKnownCallableSignatures.Glob0, UnsupportedGlobInternal),
                "glob1" => BuiltinCallable.Create(LythonKnownCallableSignatures.Glob1, UnsupportedGlobInternal),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    private sealed record GlobRequest(
        string Pattern,
        string RootDirectory,
        bool PatternIsAbsolute,
        bool Recursive,
        bool IncludeHidden);

    private static object Glob(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => GlobCore(arguments, span, context, false, false).GetAwaiter().GetResult();
    private static ValueTask<object> GlobAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => GlobCore(arguments, span, context, true, false);
    private static object IGlob(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => GlobCore(arguments, span, context, false, true).GetAwaiter().GetResult();
    private static ValueTask<object> IGlobAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => GlobCore(arguments, span, context, true, true);
    private static async ValueTask<object> GlobCore(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous, bool iterator)
    {
        var request = await ParseGlobArguments(arguments, context, span, asynchronous).ConfigureAwait(false);
        var cursor = new PyGlobIterator(request, context, span);
        if (iterator) return cursor;
        var result = new PyList([], context.MemoryGovernor, span);
        if (asynchronous)
        {
            while (await cursor.TryMoveNextAsync().ConfigureAwait(false) is { HasValue: true } item) result.Add(item.Value);
        }
        else while (cursor.TryMoveNext(out var item)) result.Add(item);
        return result;
    }

    private static object Escape(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => EscapeCore(arguments[0], span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> EscapeAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => EscapeCore(arguments[0], span, context, true);
    private static async ValueTask<object> EscapeCore(object argument, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        var path = asynchronous ? await CoercePathLikeAsync(argument, context, span, "glob.escape").ConfigureAwait(false) : CoercePathLike(argument, context, span, "glob.escape");
        var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
        try
        {
            for (var i = 0; i < path.Utf8Bytes.Length; i++)
            {
                if ((i & 4095) == 0) context.CheckExecution(span);
                var value = path.Utf8Bytes.Span[i];
                if (value is (byte)'*' or (byte)'?' or (byte)'[') { builder.Append((byte)'['); builder.Append(value); builder.Append((byte)']'); }
                else builder.Append(value);
            }
            return builder.ToPyStringAndRelease();
        }
        finally { builder.Release(); }
    }

    private static object HasMagic(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => HasMagicCore(arguments[0], span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> HasMagicAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => HasMagicCore(arguments[0], span, context, true);
    private static async ValueTask<object> HasMagicCore(object argument, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        var path = asynchronous ? await CoercePathLikeAsync(argument, context, span, "glob.has_magic").ConfigureAwait(false) : CoercePathLike(argument, context, span, "glob.has_magic");
        for (var i = 0; i < path.Utf8Bytes.Length; i++)
        {
            if ((i & 4095) == 0) context.CheckExecution(span);
            if (path.Utf8Bytes.Span[i] is (byte)'*' or (byte)'?' or (byte)'[') return true;
        }
        return false;
    }

    private static object Translate(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => TranslateCore(arguments, span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> TranslateAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => TranslateCore(arguments, span, context, true);
    private static async ValueTask<object> TranslateCore(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        var path = asynchronous ? await CoercePathLikeAsync(arguments[0], context, span, "glob.translate").ConfigureAwait(false) : CoercePathLike(arguments[0], context, span, "glob.translate");
        var recursive = arguments.Length >= 2 && (asynchronous ? await IsTruthyAsync(arguments[1], context, span).ConfigureAwait(false) : IsTruthy(arguments[1], context, span));
        var hidden = arguments.Length >= 3 && (asynchronous ? await IsTruthyAsync(arguments[2], context, span).ConfigureAwait(false) : IsTruthy(arguments[2], context, span));
        PyString? seps = null;
        if (arguments.Length >= 4 && arguments[3] is not PyNone && !PyStringOps.TryAsString(arguments[3], out seps))
            throw RuntimeErrors.Type("glob.translate seps expects a string or None", span);
        if (seps is not null && seps.Utf8Bytes.IsEmpty) throw RuntimeErrors.Value("glob.translate expects at least one separator character", span);
        using var scratch = PathOps.ReserveWorking(path, context, span, seps);
        // Admit decoding, segment copies and regex capacity/overlap before any
        // conversion, including separator storage when the pattern is empty.
        // UTF-8 lengths bound UTF-16 lengths without a preliminary scalar scan.
        using var output = context.MemoryGovernor.ReserveTemporary(checked(256L +
            64L * (path.Utf8Bytes.Length + 1L) * ((seps?.Utf8Bytes.Length ?? 1) + 1L)), span);
        var pattern = path.AsString(); var separators = seps?.AsString() ?? "/";
        if (separators.Length == 0) separators = "/";
        return PyString.FromString(TranslateGlobPattern(pattern, recursive, hidden, separators, context.State.CancellationToken), context.MemoryGovernor, span);
    }

    private static object UnsupportedGlobInternal(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = arguments;
        _ = context;
        throw new LythonRuntimeException("NotImplementedError", "glob.glob0() and glob.glob1() are CPython internal helpers and are not supported by Lython; use glob.glob() or glob.iglob().", span);
    }

    private static async ValueTask<GlobRequest> ParseGlobArguments(object[] arguments, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        var patternValue = asynchronous ? await CoercePathLikeAsync(arguments[0], context, span, "glob").ConfigureAwait(false) : CoercePathLike(arguments[0], context, span, "glob");
        var rootValue = arguments.Length >= 2 && arguments[1] is not PyNone
            ? (asynchronous ? await CoercePathLikeAsync(arguments[1], context, span, "glob root_dir").ConfigureAwait(false) : CoercePathLike(arguments[1], context, span, "glob root_dir"))
            : PyString.FromString(context.Host.Cwd, context.MemoryGovernor, span);
        if (arguments.Length >= 3 && arguments[2] is not PyNone)
            throw new LythonRuntimeException("NotImplementedError", "glob dir_fd is outside the host path model", span);
        var recursive = arguments.Length >= 4 && (asynchronous ? await IsTruthyAsync(arguments[3], context, span).ConfigureAwait(false) : IsTruthy(arguments[3], context, span));
        var hidden = arguments.Length >= 5 && (asynchronous ? await IsTruthyAsync(arguments[4], context, span).ConfigureAwait(false) : IsTruthy(arguments[4], context, span));
        var bytes = checked(PathOps.WorkingBytes(patternValue, context, span) + PathOps.WorkingBytes(rootValue, context, span) + 32L * context.Host.Cwd.Length);
        context.MemoryGovernor.Reserve(bytes, span); context.MemoryGovernor.Commit(bytes);
        var pattern = patternValue.AsString(); var root = rootValue.Utf8Bytes.IsEmpty ? "." : rootValue.AsString();
        return new GlobRequest(pattern, root, pattern.StartsWith("/", StringComparison.Ordinal), recursive, hidden);
    }

    private static string FormatGlobResult(string match, GlobRequest request)
    {
        if (request.PatternIsAbsolute) return match;
        var prefix = request.RootDirectory.EndsWith('/') ? request.RootDirectory : request.RootDirectory + "/";
        if (match.StartsWith(prefix, StringComparison.Ordinal)) return match[prefix.Length..];
        return match == request.RootDirectory ? "." : match;
    }

    private static IReadOnlyList<string> ListDirectoryNames(string path, ExecutionContext context, LythonSourceSpan span)
    {
        var kind = HostPathKind(path, context, span, osPath: true);
        if (kind != LythonPathKind.Directory)
        {
            return Array.Empty<string>();
        }

        context.CheckExecution(span);
        try { return context.HostListDir(path, span); }
        catch (HostOperationException error) when (IsFileSystemFailure(error)) { return []; }
    }

    private static async ValueTask<IReadOnlyList<string>> ListDirectoryNamesAsync(string path, ExecutionContext context, LythonSourceSpan span)
    {
        var kind = await HostPathKindAsync(path, context, span, osPath: true).ConfigureAwait(false);
        if (kind != LythonPathKind.Directory)
        {
            return Array.Empty<string>();
        }

        context.CheckExecution(span);
        try { return await context.HostListDirAsync(path, span).ConfigureAwait(false); }
        catch (HostOperationException error) when (IsFileSystemFailure(error)) { return []; }
    }

    private static bool HasGlobMeta(string text)
        => text.IndexOfAny(['*', '?', '[']) >= 0;

    private static bool ShouldConsiderGlobName(string name, string patternSegment, bool includeHidden)
    {
        return includeHidden ||
               !name.StartsWith(".", StringComparison.Ordinal) ||
               patternSegment.StartsWith(".", StringComparison.Ordinal);
    }

    private static string TranslateGlobPattern(string pattern, bool recursive, bool includeHidden, string separators, CancellationToken cancellationToken)
    {
        // CPython 3.13 glob.translate's segment cases, emitted directly without
        // materializing separate regex fragments for each recursive case.
        var segments = SplitGlobPattern();
        var builder = new StringBuilder();
        builder.Append("(?s:");
        for (var i = 0; i < segments.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var segment = segments[i];
            var last = i == segments.Count - 1;
            if (segment == "*") AppendOneSegment(trailingSeparator: !last);
            else if (recursive && segment == "**")
            {
                if (last)
                {
                    if (includeHidden) builder.Append(".*");
                    else
                    {
                        AppendAnySegments();
                        builder.Append("(?:");
                        AppendOneSegment(trailingSeparator: false);
                        builder.Append(")?");
                    }
                }
                else if (segments[i + 1] != "**") AppendAnySegments();
            }
            else
            {
                if (segment.Length != 0) AppendGlobSegmentRegex(builder, segment, includeHidden, separators, cancellationToken);
                if (!last) AppendSeparatorRegex(builder, separators, cancellationToken);
            }
        }
        builder.Append(@")\Z");
        return builder.ToString();

        void AppendOneSegment(bool trailingSeparator)
        {
            if (includeHidden)
            {
                AppendNonSeparatorClass(builder, separators, cancellationToken);
                builder.Append('+');
            }
            else
            {
                builder.Append("[^");
                AppendEscapedCharacterClassChars(builder, separators, cancellationToken);
                builder.Append(".]");
                AppendNonSeparatorClass(builder, separators, cancellationToken);
                builder.Append('*');
            }
            if (trailingSeparator) AppendSeparatorRegex(builder, separators, cancellationToken);
        }

        void AppendAnySegments()
        {
            builder.Append("(?:");
            if (includeHidden)
            {
                builder.Append(".+");
                AppendSeparatorRegex(builder, separators, cancellationToken);
                builder.Append(")?");
            }
            else
            {
                AppendOneSegment(trailingSeparator: true);
                builder.Append(")*");
            }
        }

        List<string> SplitGlobPattern()
        {
            var parts = new List<string>();
            var start = 0;
            for (var index = 0; index < pattern.Length;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var width = Rune.GetRuneAt(pattern, index).Utf16SequenceLength;
                var separator = width == 1 ? separators.IndexOf(pattern[index]) >= 0
                    : separators.AsSpan().IndexOf(pattern.AsSpan(index, width)) >= 0;
                if (separator) { parts.Add(pattern[start..index]); start = index + width; }
                index += width;
            }
            parts.Add(pattern[start..]);
            return parts;
        }
    }

    private static void AppendGlobSegmentRegex(StringBuilder builder, string segment, bool includeHidden, string separators, CancellationToken cancellationToken)
    {
        if (!includeHidden && segment.Length != 0 && segment[0] is '*' or '?')
        {
            builder.Append("(?!\\.)");
        }

        var noClosingBracketRemaining = false;
        for (var i = 0; i < segment.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ch = segment[i];
            switch (ch)
            {
                case '*':
                    AppendNonSeparatorClass(builder, separators, cancellationToken);
                    builder.Append('*');
                    break;
                case '?':
                    AppendNonSeparatorClass(builder, separators, cancellationToken);
                    break;
                case '[':
                    if (noClosingBracketRemaining)
                    {
                        AppendEscapedRegexLiteral(builder, ch);
                        break;
                    }

                    i = AppendWildcardCharacterClass(builder, segment, i, cancellationToken, out var foundClosingBracket);
                    noClosingBracketRemaining = !foundClosingBracket;
                    break;
                default:
                    AppendEscapedRegexLiteral(builder, ch);
                    break;
            }
        }
    }

    // Shared by fnmatch and glob: class syntax is the same, and UTF-16
    // positions must advance over whole scalars before looking for a range.
    private static int AppendWildcardCharacterClass(StringBuilder builder, string pattern, int openIndex,
        CancellationToken cancellationToken, out bool foundClosingBracket)
    {
        foundClosingBracket = false;
        var contentStart = openIndex + 1;
        if (contentStart >= pattern.Length) return Unclosed();
        var negated = pattern[contentStart] == '!';
        if (negated) contentStart++;
        if (contentStart >= pattern.Length) return Unclosed();
        var searchStart = contentStart;
        if (pattern[searchStart] == ']') searchStart++;
        var closeIndex = -1;
        for (var index = searchStart; index < pattern.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (pattern[index] == ']') { closeIndex = index; break; }
        }
        if (closeIndex < 0) return Unclosed();

        foundClosingBracket = true;
        var content = new StringBuilder(closeIndex - contentStart);
        for (var index = contentStart; index < closeIndex;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var first = Rune.GetRuneAt(pattern, index);
            index += first.Utf16SequenceLength;
            if (index + 1 < closeIndex && pattern[index] == '-')
            {
                var last = Rune.GetRuneAt(pattern, index + 1);
                index += 1 + last.Utf16SequenceLength;
                if (first.Value <= last.Value)
                {
                    AppendClassRune(content, first);
                    content.Append('-');
                    AppendClassRune(content, last);
                }
            }
            else AppendClassRune(content, first);
        }
        if (content.Length == 0) builder.Append(negated ? "." : "(?!)");
        else
        {
            builder.Append('[');
            if (negated) builder.Append('^');
            builder.Append(content);
            builder.Append(']');
        }
        return closeIndex;

        int Unclosed()
        {
            builder.Append(@"\[");
            return openIndex;
        }
    }

    private static void AppendClassRune(StringBuilder builder, Rune rune)
    {
        // Escape endpoints as literals as well as future character-set operators;
        // the caller alone emits a range separator.
        if (rune.Value is '\\' or '[' or ']' or '^' or '-' or '&' or '~' or '|') builder.Append('\\');
        Span<char> text = stackalloc char[2];
        var written = rune.EncodeToUtf16(text);
        builder.Append(text[..written]);
    }

    private static void AppendSeparatorRegex(StringBuilder builder, string separators, CancellationToken cancellationToken)
    {
        if (separators.Length == 1)
        {
            AppendEscapedRegexLiteral(builder, separators[0]);
            return;
        }

        builder.Append('[');
        AppendEscapedCharacterClassChars(builder, separators, cancellationToken);
        builder.Append(']');
    }

    private static void AppendNonSeparatorClass(StringBuilder builder, string separators, CancellationToken cancellationToken)
    {
        builder.Append("[^");
        AppendEscapedCharacterClassChars(builder, separators, cancellationToken);
        builder.Append(']');
    }

    private static void AppendEscapedCharacterClassChars(StringBuilder builder, string value, CancellationToken cancellationToken)
    {
        foreach (var rune in value.EnumerateRunes())
        {
            cancellationToken.ThrowIfCancellationRequested();
            AppendClassRune(builder, rune);
        }
    }

    private static void AppendEscapedRegexLiteral(StringBuilder builder, char ch)
    {
        if (".^$*+?{}[]\\|()".IndexOf(ch) >= 0)
        {
            builder.Append('\\');
        }

        builder.Append(ch);
    }
}
