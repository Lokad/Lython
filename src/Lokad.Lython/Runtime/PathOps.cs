using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static partial class PathOps
{
    internal static LythonPathRequest HostPathRequest(string path, string cwd, LythonSourceSpan? span)
    {
        if (path.Contains('\\') || path.Contains('\0'))
            throw RuntimeErrors.Value("host paths cannot contain backslashes or NUL characters", span);
        for (var index = 0; index + 1 < path.Length; index++)
            if ((index == 0 || path[index - 1] == '/') && char.IsAsciiLetter(path[index]) && path[index + 1] == ':')
                throw RuntimeErrors.Value("host paths cannot contain Windows drive prefixes", span);
        return new LythonPathRequest(path, RequireContainedPath(cwd, span));
    }

    public static string RequireContainedPath(string path, LythonSourceSpan? span)
    {
        if (path.Length == 0 || path[0] != '/')
        {
            throw RuntimeErrors.Value("host paths must be absolute within the contained namespace", span);
        }

        if (path.Contains('\\', StringComparison.Ordinal) || path.Contains('\0', StringComparison.Ordinal))
        {
            throw RuntimeErrors.Value("host paths cannot contain backslashes or NUL characters", span);
        }

        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment is "." or "..")
            {
                throw RuntimeErrors.Value("host paths must not contain unresolved dot segments", span);
            }

            if (segment.Length >= 2 && char.IsAsciiLetter(segment[0]) && segment[1] == ':')
            {
                throw RuntimeErrors.Value("host paths cannot contain Windows drive prefixes", span);
            }
        }

        return path;
    }

    public static string NormalizeLexical(string path)
    {
        if (path.Length == 0)
        {
            path = ".";
        }

        var isAbsolute = path.StartsWith("/", StringComparison.Ordinal);
        var parts = new List<string>();
        foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part != ".")
            {
                parts.Add(part);
            }
        }

        if (isAbsolute)
        {
            return "/" + string.Join("/", parts);
        }

        return parts.Count == 0 ? "." : string.Join("/", parts);
    }

    public static string Normalize(string path)
        => Normalize(path, null);

    public static string Normalize(string path, string? basePath)
    {
        if (path.Length == 0)
        {
            path = ".";
        }

        var absolute = path.StartsWith("/", StringComparison.Ordinal)
            ? path
            : basePath is null
                ? path
                : Combine(basePath, path);

        var isAbsolute = absolute.StartsWith("/", StringComparison.Ordinal);
        var parts = new List<string>();
        foreach (var rawPart in absolute.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (rawPart == ".")
            {
                continue;
            }

            if (rawPart == "..")
            {
                if (parts.Count > 0 && parts[^1] != "..")
                {
                    parts.RemoveAt(parts.Count - 1);
                }
                else if (!isAbsolute)
                {
                    parts.Add("..");
                }

                continue;
            }

            parts.Add(rawPart);
        }

        if (isAbsolute)
        {
            return "/" + string.Join("/", parts);
        }

        return parts.Count == 0 ? "." : string.Join("/", parts);
    }

    public static string Join(string left, string right)
    {
        if (right.StartsWith("/", StringComparison.Ordinal))
        {
            return NormalizeLexical(right);
        }

        return NormalizeLexical(Combine(left, right));
    }

    public static string Combine(string left, string right)
    {
        if (left.Length == 0)
        {
            return right;
        }

        if (right.Length == 0)
        {
            return left;
        }

        return left.EndsWith("/", StringComparison.Ordinal)
            ? left + right
            : left + "/" + right;
    }

    public static string Parent(string path)
    {
        var normalized = NormalizeLexical(path);
        if (normalized == "/" || normalized == ".")
        {
            return normalized;
        }

        var trimmed = normalized.TrimEnd('/');
        var slash = trimmed.LastIndexOf('/');
        if (slash < 0)
        {
            return ".";
        }

        if (slash == 0)
        {
            return "/";
        }

        return trimmed[..slash];
    }

    public static string BaseName(string path)
    {
        var normalized = NormalizeLexical(path);
        if (normalized == "/" || normalized == ".")
        {
            return string.Empty;
        }

        var trimmed = normalized.TrimEnd('/');
        var slash = trimmed.LastIndexOf('/');
        return slash < 0 ? trimmed : trimmed[(slash + 1)..];
    }

    public static string Suffix(string path)
    {
        var name = BaseName(path);
        var dot = name.LastIndexOf('.');
        return dot <= 0 || dot == name.Length - 1 ? string.Empty : name[dot..];
    }

    public static bool Match(string path, string pattern, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeLexical(path);
        var pathIsAbsolute = normalized.StartsWith("/", StringComparison.Ordinal);
        var patternIsAbsolute = pattern.StartsWith("/", StringComparison.Ordinal);
        if (patternIsAbsolute != pathIsAbsolute && patternIsAbsolute)
        {
            return false;
        }

        var pathParts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var patternParts = pattern.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (patternParts.Length == 0 || patternParts.Length > pathParts.Length ||
            (patternIsAbsolute && patternParts.Length != pathParts.Length))
        {
            return false;
        }

        var offset = pathParts.Length - patternParts.Length;
        for (var i = 0; i < patternParts.Length; i++)
        {
            if (!LythonRuntime.FnMatchModule.MatchSimple(
                    PyString.FromString(pathParts[offset + i]),
                    PyString.FromString(patternParts[i]), cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    public static string RelativeTo(string path, string parent)
    {
        var normalizedPath = NormalizeLexical(path);
        var normalizedParent = NormalizeLexical(parent);
        var prefix = normalizedParent == "/" ? "/" : normalizedParent + "/";

        if (normalizedPath == normalizedParent)
        {
            return ".";
        }

        if (!normalizedPath.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"'{normalizedPath}' is not under '{normalizedParent}'.");
        }

        return normalizedPath[prefix.Length..];
    }

}
