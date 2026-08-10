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
                    "__fspath__" => BoundCallable.Create((arguments, span, _) =>
                    {
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.__fspath__() expects no arguments.", span);
                        }

                        return path.Value;
                    }, "Path.__fspath__", []),
                    "is_absolute" => BoundCallable.Create((arguments, span, _) =>
                    {
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.is_absolute() expects no arguments.", span);
                        }

                        return PathOps.IsAbsolute(path.Value.AsString());
                    }),
                    "is_mount" => BoundCallable.Create((arguments, span, _) =>
                    {
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.is_mount() expects no arguments.", span);
                        }

                        return string.Equals(PathOps.Normalize(path.Value.AsString()), "/", StringComparison.Ordinal);
                    }, "Path.is_mount", []),
                    "is_reserved" => BoundCallable.Create((arguments, span, _) =>
                    {
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.is_reserved() expects no arguments.", span);
                        }

                        return false;
                    }, "Path.is_reserved", []),
                    "joinpath" => BoundCallable.Create((arguments, span, _) =>
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

                        return new PyPath(current);
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
                    "as_posix" => BoundCallable.Create((arguments, span, _) =>
                    {
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.as_posix() expects no arguments.", span);
                        }

                        return path.Value;
                    }),
                    "resolve" => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.resolve() expects no arguments.", span);
                        }

                        return new PyPath(PathOps.Normalize(path.Value, PyString.FromString(context.Host.Cwd)));
                    }),
                    "absolute" => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.absolute() expects no arguments.", span);
                        }

                        return new PyPath(PathOps.MakeAbsoluteLexical(path.Value, PyString.FromString(context.Host.Cwd)));
                    }),
                    "relative_to" => BoundCallable.Create((arguments, span, _) =>
                    {
                        if (arguments.Length != 1)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.relative_to(other) expects one argument.", span);
                        }

                        var other = RequirePath(arguments[0], "Path.relative_to(other)", span);
                        try
                        {
                            return new PyPath(PyString.FromString(PathOps.RelativeTo(path.Value.AsString(), other.Value.AsString())));
                        }
                        catch (InvalidOperationException ex)
                        {
                            throw new LythonRuntimeException("ValueError", ex.Message, span);
                        }
                    }, "Path.relative_to", ["other"]),
                    "with_suffix" => BoundCallable.Create((arguments, span, _) =>
                    {
                        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var suffix))
                        {
                            throw new LythonRuntimeException("TypeError", "Path.with_suffix(suffix) expects one string argument.", span);
                        }

                        try
                        {
                            return new PyPath(PyString.FromString(PathOps.WithSuffix(path.Value.AsString(), suffix.AsString())));
                        }
                        catch (InvalidOperationException ex)
                        {
                            throw new LythonRuntimeException("ValueError", ex.Message, span);
                        }
                    }, "Path.with_suffix", ["suffix"]),
                    "with_name" => BoundCallable.Create((arguments, span, _) =>
                    {
                        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var name))
                        {
                            throw new LythonRuntimeException("TypeError", "Path.with_name(name) expects one string argument.", span);
                        }

                        try
                        {
                            return new PyPath(PyString.FromString(PathOps.WithName(path.Value.AsString(), name.AsString())));
                        }
                        catch (InvalidOperationException ex)
                        {
                            throw new LythonRuntimeException("ValueError", ex.Message, span);
                        }
                    }, "Path.with_name", ["name"]),
                    "with_stem" => BoundCallable.Create((arguments, span, _) =>
                    {
                        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var stem))
                        {
                            throw new LythonRuntimeException("TypeError", "Path.with_stem(stem) expects one string argument.", span);
                        }

                        try
                        {
                            return new PyPath(PyString.FromString(PathOps.WithName(path.Value.AsString(), stem.AsString() + PathOps.Suffix(path.Value.AsString()))));
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