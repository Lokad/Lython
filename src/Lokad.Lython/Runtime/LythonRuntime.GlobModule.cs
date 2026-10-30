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
        var pattern = path.AsString(); var separators = seps?.AsString() ?? "/";
        using var output = context.MemoryGovernor.ReserveTemporary(checked(256L + 32L * pattern.Length * (separators.Length + 1L)), span);
        return PyString.FromString(TranslateGlobPattern(pattern, recursive, hidden, separators, context.Limits.CancellationToken), context.MemoryGovernor, span);
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
        var segments = SplitGlobPattern(pattern, separators, cancellationToken);
        var builder = new StringBuilder();
        builder.Append('^');
        for (var i = 0; i < segments.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (i > 0)
            {
                AppendSeparatorRegex(builder, separators, cancellationToken);
            }

            var segment = segments[i];
            if (recursive && segment == "**")
            {
                if (i == segments.Count - 1)
                {
                    builder.Append(".*");
                }
                else
                {
                    builder.Append("(?:.*");
                    AppendSeparatorRegex(builder, separators, cancellationToken);
                    builder.Append(")?");
                }

                continue;
            }

            AppendGlobSegmentRegex(builder, segment, includeHidden, separators, cancellationToken);
        }

        builder.Append('$');
        return builder.ToString();

        static List<string> SplitGlobPattern(string pattern, string separators, CancellationToken cancellationToken)
        {
            var segments = new List<string>();
            var start = 0;
            for (var i = 0; i < pattern.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (separators.IndexOf(pattern[i]) >= 0)
                {
                    segments.Add(pattern[start..i]);
                    start = i + 1;
                }
            }

            segments.Add(pattern[start..]);
            return segments;
        }
    }

    private static void AppendGlobSegmentRegex(StringBuilder builder, string segment, bool includeHidden, string separators, CancellationToken cancellationToken)
    {
        if (!includeHidden && segment.Length != 0 && !segment.StartsWith(".", StringComparison.Ordinal))
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

                    i = AppendTranslatedCharacterClass(builder, segment, i, cancellationToken, out var foundClosingBracket);
                    noClosingBracketRemaining = !foundClosingBracket;
                    break;
                default:
                    AppendEscapedRegexLiteral(builder, ch);
                    break;
            }
        }
    }

    private static int AppendTranslatedCharacterClass(
        StringBuilder builder,
        string segment,
        int openBracket,
        CancellationToken cancellationToken,
        out bool foundClosingBracket)
    {
        var closeBracket = segment.IndexOf(']', openBracket + 1);
        if (closeBracket < 0)
        {
            foundClosingBracket = false;
            builder.Append("\\[");
            return openBracket;
        }

        foundClosingBracket = true;
        builder.Append('[');
        var index = openBracket + 1;
        if (index < closeBracket && segment[index] is '!' or '^')
        {
            builder.Append('^');
            index++;
        }

        if (index < closeBracket && segment[index] == ']')
        {
            builder.Append("\\]");
            index++;
        }

        for (; index < closeBracket; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ch = segment[index];
            if (ch is '\\' or '[')
            {
                builder.Append('\\');
            }

            builder.Append(ch);
        }

        builder.Append(']');
        return closeBracket;
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
        foreach (var ch in value)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ch is '\\' or ']' or '-' or '^')
            {
                builder.Append('\\');
            }

            builder.Append(ch);
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
