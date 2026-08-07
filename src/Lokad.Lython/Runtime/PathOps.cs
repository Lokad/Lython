using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static class PathOps
{
    public static PyString Normalize(PyString path)
        => Normalize(path, null);

    public static PyString Normalize(PyString path, PyString? basePath)
        => PyString.FromString(Normalize(path.AsString(), basePath?.AsString()));

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

    public static PyString Join(PyString left, PyString right)
        => PyString.FromString(Join(left.AsString(), right.AsString()));

    public static string Join(string left, string right)
    {
        if (right.StartsWith("/", StringComparison.Ordinal))
        {
            return Normalize(right);
        }

        return Normalize(Combine(left, right));
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

    public static PyString Parent(PyString path) => PyString.FromString(Parent(path.AsString()));

    public static string Parent(string path)
    {
        var normalized = Normalize(path);
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
        var normalized = Normalize(path);
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

    public static string Stem(string path)
    {
        var name = BaseName(path);
        var dot = name.LastIndexOf('.');
        return dot <= 0 || dot == name.Length - 1 ? name : name[..dot];
    }

    public static string WithSuffix(string path, string suffix)
    {
        if (suffix.Length != 0 && !suffix.StartsWith(".", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid suffix.");
        }

        var normalized = Normalize(path);
        var parent = Parent(normalized);
        var stem = Stem(normalized);
        var nextName = stem + suffix;
        return parent switch
        {
            "/" => "/" + nextName,
            "." => nextName,
            _ => parent + "/" + nextName
        };
    }

    public static string WithName(string path, string name)
    {
        if (name.Length == 0 || name.Contains('/'))
        {
            throw new InvalidOperationException("Invalid name.");
        }

        var normalized = Normalize(path);
        var parent = Parent(normalized);
        return parent switch
        {
            "/" => "/" + name,
            "." => name,
            _ => parent + "/" + name
        };
    }

    public static bool IsAbsolute(string path) => Normalize(path).StartsWith("/", StringComparison.Ordinal);

    public static bool Match(string path, string pattern)
    {
        var normalized = Normalize(path);
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
                    PyString.FromString(patternParts[i])))
            {
                return false;
            }
        }

        return true;
    }

    public static string RelativeTo(string path, string parent)
    {
        var normalizedPath = Normalize(path);
        var normalizedParent = Normalize(parent);
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

    public static PyList Parents(PyString path)
        => Parents(path, null, null);

    public static PyList Parents(PyString path, MemoryGovernor? governor)
        => Parents(path, governor, null);

    public static PyList Parents(PyString path, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        var values = governor is null ? new PyList() : new PyList([], governor, span);
        var current = Parent(path);
        if (current.Equals(path))
        {
            return values;
        }

        while (true)
        {
            values.Add(new PyPath(current));
            if (current.Equals(PyStringOps.SlashLiteral) || current.Equals(PyStringOps.DotLiteral))
            {
                break;
            }

            current = Parent(current);
        }

        return values;
    }

    public static PyTuple Parts(PyString path)
        => Parts(path, null, null);

    public static PyTuple Parts(PyString path, MemoryGovernor? governor)
        => Parts(path, governor, null);

    public static PyTuple Parts(PyString path, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        var normalized = Normalize(path.AsString());
        if (normalized == ".")
        {
            return governor is null ? new PyTuple([]) : new PyTuple([], governor, span);
        }

        var values = new List<object>();
        if (normalized.StartsWith("/", StringComparison.Ordinal))
        {
            values.Add(PyStringOps.SlashLiteral);
            normalized = normalized[1..];
        }

        if (normalized.Length != 0)
        {
            foreach (var part in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                values.Add(PyString.FromString(part));
            }
        }

        return governor is null ? new PyTuple(values) : new PyTuple(values, governor, span);
    }
}
