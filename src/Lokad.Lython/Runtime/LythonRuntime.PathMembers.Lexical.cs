using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static partial class PathMembers
    {
        private sealed class LexicalPathMemberProvider : IPathMemberProvider
        {
            public static readonly LexicalPathMemberProvider Instance = new();

            public bool TryGetMember(PyPath path, string name, [MaybeNullWhen(false)] out object value)
            {
                value = name switch
                {
                    "name" => PyString.FromString(PathOps.BaseName(path.Value.AsString())),
                    "suffix" => PyString.FromString(PathOps.Suffix(path.Value.AsString())),
                    "suffixes" => PathSuffixes(path.Value),
                    "stem" => PyString.FromString(PathOps.Stem(path.Value.AsString())),
                    "parent" => new PyPath(PathOps.Parent(path.Value)),
                    "parents" => PathOps.Parents(path.Value),
                    "parts" => PathOps.Parts(path.Value),
                    "drive" => PyString.Empty,
                    "root" => PathOps.IsAbsolute(path.Value.AsString()) ? PyStringOps.SlashLiteral : PyString.Empty,
                    "anchor" => PathOps.IsAbsolute(path.Value.AsString()) ? PyStringOps.SlashLiteral : PyString.Empty,
                    "__fspath__" => BoundCallable.CreateNoArguments(path, "Path.__fspath__", static (receiver, _, _) => receiver.Value),
                    "is_absolute" => BoundCallable.CreateNoArguments(
                        path,
                        "Path.is_absolute",
                        static (receiver, _, _) => PathOps.IsAbsolute(receiver.Value.AsString())),
                    "is_mount" => BoundCallable.CreateNoArguments(
                        path,
                        "Path.is_mount",
                        static (receiver, _, _) => string.Equals(PathOps.Normalize(receiver.Value.AsString()), "/", StringComparison.Ordinal)),
                    "is_reserved" => BoundCallable.CreateNoArguments(path, "Path.is_reserved", static (_, _, _) => false),
                    "joinpath" => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length == 0)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.joinpath(*other) expects at least one string argument.", span);
                        }

                        var current = path.Value;
                        foreach (var argument in arguments)
                        {
                            var part = argument switch
                            {
                                PyPath pathArgument => pathArgument.Value,
                                _ when PyStringOps.TryAsString(argument, out var text) => text,
                                _ => throw new LythonRuntimeException("TypeError", "Path.joinpath(*other) expects Path or string arguments.", span)
                            };

                            if (part.Length == 0)
                            {
                                continue;
                            }

                            current = PathOps.Join(current, part);
                        }

                        return OwnPathResult(current, path.Value, context.MemoryGovernor, span);
                    }),
                    "expanduser" => UnsupportedPathMember("Path.expanduser", "Path.expanduser() is not supported by Lython; the host does not expose an ambient user home directory."),
                    "match" => BoundCallable.Create((arguments, span, _) =>
                    {
                        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var pattern))
                        {
                            throw new LythonRuntimeException("TypeError", "Path.match(pattern) expects one string argument.", span);
                        }

                        return PathOps.Match(path.Value.AsString(), pattern.AsString());
                    }, "Path.match", ["pattern"]),
                    "is_relative_to" => BoundCallable.Create((arguments, span, _) =>
                    {
                        if (arguments.Length != 1)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.is_relative_to(other) expects one argument.", span);
                        }

                        var other = RequirePath(arguments[0], "Path.is_relative_to(other)", span);
                        try
                        {
                            PathOps.RelativeTo(path.Value.AsString(), other.Value.AsString());
                            return true;
                        }
                        catch (InvalidOperationException)
                        {
                            return false;
                        }
                    }, "Path.is_relative_to", ["other"]),
                    "as_posix" => BoundCallable.CreateNoArguments(path, "Path.as_posix", static (receiver, _, _) => receiver.Value),
                    "resolve" => BoundCallable.CreateNoArguments(
                        path,
                        "Path.resolve",
                        static (receiver, span, context) => OwnPathResult(PathOps.Normalize(receiver.Value, PyString.FromString(context.Host.Cwd)), receiver.Value, context.MemoryGovernor, span)),
                    "absolute" => BoundCallable.CreateNoArguments(
                        path,
                        "Path.absolute",
                        static (receiver, span, context) => OwnPathResult(PathOps.MakeAbsoluteLexical(receiver.Value, PyString.FromString(context.Host.Cwd)), receiver.Value, context.MemoryGovernor, span)),
                    "relative_to" => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length != 1)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.relative_to(other) expects one argument.", span);
                        }

                        var other = RequirePath(arguments[0], "Path.relative_to(other)", span);
                        try
                        {
                            return OwnPathResult(PyString.FromString(PathOps.RelativeTo(path.Value.AsString(), other.Value.AsString())), path.Value, context.MemoryGovernor, span);
                        }
                        catch (InvalidOperationException ex)
                        {
                            throw new LythonRuntimeException("ValueError", ex.Message, span);
                        }
                    }, "Path.relative_to", ["other"]),
                    "with_suffix" => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var suffix))
                        {
                            throw new LythonRuntimeException("TypeError", "Path.with_suffix(suffix) expects one string argument.", span);
                        }

                        try
                        {
                            return OwnPathResult(PyString.FromString(PathOps.WithSuffix(path.Value.AsString(), suffix.AsString())), path.Value, context.MemoryGovernor, span);
                        }
                        catch (InvalidOperationException ex)
                        {
                            throw new LythonRuntimeException("ValueError", ex.Message, span);
                        }
                    }, "Path.with_suffix", ["suffix"]),
                    "with_name" => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var name))
                        {
                            throw new LythonRuntimeException("TypeError", "Path.with_name(name) expects one string argument.", span);
                        }

                        try
                        {
                            return OwnPathResult(PyString.FromString(PathOps.WithName(path.Value.AsString(), name.AsString())), path.Value, context.MemoryGovernor, span);
                        }
                        catch (InvalidOperationException ex)
                        {
                            throw new LythonRuntimeException("ValueError", ex.Message, span);
                        }
                    }, "Path.with_name", ["name"]),
                    "with_stem" => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var stem))
                        {
                            throw new LythonRuntimeException("TypeError", "Path.with_stem(stem) expects one string argument.", span);
                        }

                        try
                        {
                            return OwnPathResult(PyString.FromString(PathOps.WithName(path.Value.AsString(), stem.AsString() + PathOps.Suffix(path.Value.AsString()))), path.Value, context.MemoryGovernor, span);
                        }
                        catch (InvalidOperationException ex)
                        {
                            throw new LythonRuntimeException("ValueError", ex.Message, span);
                        }
                    }, "Path.with_stem", ["stem"]),
                    _ => MissingMemberValue.Instance,
                };

                return !ReferenceEquals(value, MissingMemberValue.Instance);
            }
        }
    }
}
