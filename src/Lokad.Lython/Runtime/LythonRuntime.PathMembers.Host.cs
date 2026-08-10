using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static partial class PathMembers
    {
        private sealed class HostPathMemberProvider : IPathMemberProvider
        {
            public static readonly HostPathMemberProvider Instance = new();

            public bool TryGetMember(PyPath path, string name, [MaybeNullWhen(false)] out object value)
            {
                value = name switch
                {
                    "stat" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.stat() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    return context.HostStat(path.Value.AsString(), span);
                },
                async (arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.stat() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    return await context.HostStatAsync(path.Value.AsString(), span).ConfigureAwait(false);
                }),
                    "lstat" => new BoundCallable((arguments, span, context) =>
                    {
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.lstat() expects no arguments.", span);
                        }

                        context.RegisterHostCall(span);
                        return context.HostStat(path.Value.AsString(), span);
                    },
                    async (arguments, span, context) =>
                    {
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.lstat() expects no arguments.", span);
                        }

                        context.RegisterHostCall(span);
                        return await context.HostStatAsync(path.Value.AsString(), span).ConfigureAwait(false);
                    }, "Path.lstat", []),
                    "exists" => new BoundCallable((arguments, span, context) =>
                    {
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.exists() expects no arguments.", span);
                        }

                        context.RegisterHostCall(span);
                        return context.HostExists(path.Value.AsString(), span);
                    },
                    async (arguments, span, context) =>
                    {
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.exists() expects no arguments.", span);
                        }

                        context.RegisterHostCall(span);
                        return await context.HostExistsAsync(path.Value.AsString(), span).ConfigureAwait(false);
                    }),
                    "is_file" => new BoundCallable((arguments, span, context) =>
                    {
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.is_file() expects no arguments.", span);
                        }

                        context.RegisterHostCall(span);
                        return context.HostStat(path.Value.AsString(), span).IsFile;
                    },
                    async (arguments, span, context) =>
                    {
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.is_file() expects no arguments.", span);
                        }

                        context.RegisterHostCall(span);
                        return (await context.HostStatAsync(path.Value.AsString(), span).ConfigureAwait(false)).IsFile;
                    }),
                    "is_dir" => new BoundCallable((arguments, span, context) =>
                    {
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.is_dir() expects no arguments.", span);
                        }

                        context.RegisterHostCall(span);
                        return context.HostStat(path.Value.AsString(), span).IsDir;
                    },
                    async (arguments, span, context) =>
                    {
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.is_dir() expects no arguments.", span);
                        }

                        context.RegisterHostCall(span);
                        return (await context.HostStatAsync(path.Value.AsString(), span).ConfigureAwait(false)).IsDir;
                    }),
                    "is_symlink" => new BoundCallable((arguments, span, _) =>
                    {
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.is_symlink() expects no arguments.", span);
                        }

                        return false;
                    }),
                    "unlink" => new BoundCallable((arguments, span, context) =>
                    {
                        if (arguments.Length > 1)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.unlink([missing_ok]) expects zero or one argument.", span);
                        }

                        var missingOk = ParseOptionalBool(arguments, 0, false, "Path.unlink([missing_ok])", "missing_ok", span);
                        if (missingOk)
                        {
                            context.RegisterHostCall(span);
                            if (!context.HostStat(path.Value.AsString(), span).Exists)
                            {
                                return PyNone.Instance;
                            }
                        }

                        context.RegisterHostCall(span);
                        context.HostRemove(path.Value.AsString(), span);
                        return PyNone.Instance;
                    },
                    async (arguments, span, context) =>
                    {
                        if (arguments.Length > 1)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.unlink([missing_ok]) expects zero or one argument.", span);
                        }

                        var missingOk = ParseOptionalBool(arguments, 0, false, "Path.unlink([missing_ok])", "missing_ok", span);
                        if (missingOk)
                        {
                            context.RegisterHostCall(span);
                            if (!(await context.HostStatAsync(path.Value.AsString(), span).ConfigureAwait(false)).Exists)
                            {
                                return PyNone.Instance;
                            }
                        }

                        context.RegisterHostCall(span);
                        await context.HostRemoveAsync(path.Value.AsString(), span).ConfigureAwait(false);
                        return PyNone.Instance;
                    }, "Path.unlink", ["missing_ok"], 0),
                    "rmdir" => new BoundCallable((arguments, span, context) =>
                    {
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.rmdir() expects no arguments.", span);
                        }

                        context.RegisterHostCall(span);
                        context.HostRemove(path.Value.AsString(), span);
                        return PyNone.Instance;
                    },
                    async (arguments, span, context) =>
                    {
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.rmdir() expects no arguments.", span);
                        }

                        context.RegisterHostCall(span);
                        await context.HostRemoveAsync(path.Value.AsString(), span).ConfigureAwait(false);
                        return PyNone.Instance;
                    }),
                    "rename" => new BoundCallable((arguments, span, context) =>
                    {
                        if (arguments.Length != 1)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.rename(target) expects one argument.", span);
                        }

                        var target = RequirePath(arguments[0], "Path.rename(target)", span);
                        context.RegisterHostCall(span);
                        context.HostMove(path.Value.AsString(), target.Value.AsString(), span);
                        return target;
                    },
                    async (arguments, span, context) =>
                    {
                        if (arguments.Length != 1)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.rename(target) expects one argument.", span);
                        }

                        var target = RequirePath(arguments[0], "Path.rename(target)", span);
                        context.RegisterHostCall(span);
                        await context.HostMoveAsync(path.Value.AsString(), target.Value.AsString(), span).ConfigureAwait(false);
                        return target;
                    }, "Path.rename", ["target"]),
                    "replace" => new BoundCallable((arguments, span, context) =>
                    {
                        if (arguments.Length != 1)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.replace(target) expects one argument.", span);
                        }

                        var target = RequirePath(arguments[0], "Path.replace(target)", span);
                        context.RegisterHostCall(span);
                        if (context.HostStat(target.Value.AsString(), span).Exists)
                        {
                            context.RegisterHostCall(span);
                            context.HostRemove(target.Value.AsString(), span);
                        }

                        context.RegisterHostCall(span);
                        context.HostMove(path.Value.AsString(), target.Value.AsString(), span);
                        return target;
                    },
                    async (arguments, span, context) =>
                    {
                        if (arguments.Length != 1)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.replace(target) expects one argument.", span);
                        }

                        var target = RequirePath(arguments[0], "Path.replace(target)", span);
                        context.RegisterHostCall(span);
                        if ((await context.HostStatAsync(target.Value.AsString(), span).ConfigureAwait(false)).Exists)
                        {
                            context.RegisterHostCall(span);
                            await context.HostRemoveAsync(target.Value.AsString(), span).ConfigureAwait(false);
                        }

                        context.RegisterHostCall(span);
                        await context.HostMoveAsync(path.Value.AsString(), target.Value.AsString(), span).ConfigureAwait(false);
                        return target;
                    }, "Path.replace", ["target"]),
                    "mkdir" => new BoundCallable((arguments, span, context) =>
                    {
                        PathMkDir(path.Value.AsString(), arguments, span, context);
                        return PyNone.Instance;
                    },
                    async (arguments, span, context) =>
                    {
                        await PathMkDirAsync(path.Value.AsString(), arguments, span, context).ConfigureAwait(false);
                        return PyNone.Instance;
                    }, "Path.mkdir", ["mode", "parents", "exist_ok"], 0),
                    "touch" => new BoundCallable((arguments, span, context) =>
                    {
                        PathTouch(path.Value.AsString(), arguments, span, context);
                        return PyNone.Instance;
                    },
                    async (arguments, span, context) =>
                    {
                        await PathTouchAsync(path.Value.AsString(), arguments, span, context).ConfigureAwait(false);
                        return PyNone.Instance;
                    }, "Path.touch", ["mode", "exist_ok"], 0),
                    "read_bytes" => UnsupportedPathMember("Path.read_bytes", "Path.read_bytes() is not supported by Lython under the text-only host boundary."),
                    "write_bytes" => UnsupportedPathMember("Path.write_bytes", "Path.write_bytes(data) is not supported by Lython under the text-only host boundary."),
                    "readlink" => UnsupportedPathMember("Path.readlink", "Path.readlink() is not supported by Lython because symlink targets are not exposed by the host path model."),
                    "symlink_to" => UnsupportedPathMember("Path.symlink_to", "Path.symlink_to(target, target_is_directory=False) is not supported by Lython because symlink mutation is outside the host path model."),
                    "hardlink_to" => UnsupportedPathMember("Path.hardlink_to", "Path.hardlink_to(target) is not supported by Lython because hardlink mutation is outside the host path model."),
                    "chmod" => UnsupportedPathMember("Path.chmod", "Path.chmod(mode) is not supported by Lython because permissions are not exposed by the host path model."),
                    "owner" => UnsupportedPathMember("Path.owner", "Path.owner() is not supported by Lython because user ownership is not exposed by the host path model."),
                    "group" => UnsupportedPathMember("Path.group", "Path.group() is not supported by Lython because group ownership is not exposed by the host path model."),
                    "open" => new PathOpenCallable(path.Value.AsString()),
                    "glob" => new BoundCallable((arguments, span, context) =>
                    {
                        var pattern = ParsePathGlobArguments(arguments, "Path.glob", span);

                        var results = new PyList([], context.MemoryGovernor, span);
                        context.RegisterHostCall(span);
                        foreach (var name in context.HostListDir(path.Value.AsString(), span))
                        {
                            context.CheckExecutionBudget(span);
                            if (LythonRuntime.FnMatchModule.MatchSimple(PyString.FromString(name), pattern))
                            {
                                results.Add(new PyPath(PyString.FromString(
                                    PathOps.Join(path.Value.AsString(), name),
                                    context.MemoryGovernor,
                                    span)));
                                context.ObserveCollectionCount(results.Count, span);
                            }
                        }

                        return results;
                    },
                    async (arguments, span, context) =>
                    {
                        var pattern = ParsePathGlobArguments(arguments, "Path.glob", span);

                        var results = new PyList([], context.MemoryGovernor, span);
                        context.RegisterHostCall(span);
                        var names = await context.HostListDirAsync(path.Value.AsString(), span).ConfigureAwait(false);
                        foreach (var name in names)
                        {
                            context.CheckExecutionBudget(span);
                            if (LythonRuntime.FnMatchModule.MatchSimple(PyString.FromString(name), pattern))
                            {
                                results.Add(new PyPath(PyString.FromString(
                                    PathOps.Join(path.Value.AsString(), name),
                                    context.MemoryGovernor,
                                    span)));
                                context.ObserveCollectionCount(results.Count, span);
                            }
                        }

                        return results;
                    }, "Path.glob", ["pattern", "case_sensitive", "recurse_symlinks"], 1),
                    "iterdir" => new BoundCallable((arguments, span, context) =>
                    {
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.iterdir() expects no arguments.", span);
                        }

                        context.RegisterHostCall(span);
                        var entries = context.HostListDir(path.Value.AsString(), span)
                            .Select<string, object>(name => new PyPath(PathOps.Join(path.Value, PyString.FromString(name))));
                        return new PyList(entries, context.MemoryGovernor, span);
                    },
                    async (arguments, span, context) =>
                    {
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.iterdir() expects no arguments.", span);
                        }

                        context.RegisterHostCall(span);
                        var names = await context.HostListDirAsync(path.Value.AsString(), span).ConfigureAwait(false);
                        var entries = names.Select<string, object>(name => new PyPath(PathOps.Join(path.Value, PyString.FromString(name))));
                        return new PyList(entries, context.MemoryGovernor, span);
                    }),
                    "read_text" => new BoundCallable((arguments, span, context) =>
                    {
                        var (encodingMode, errors, newline) = ParsePathReadTextArguments(arguments, span);
                        return ReadPathText(path.Value.AsString(), encodingMode, errors, newline, context, span);
                    },
                    async (arguments, span, context) =>
                    {
                        var (encodingMode, errors, newline) = ParsePathReadTextArguments(arguments, span);
                        return await ReadPathTextAsync(path.Value.AsString(), encodingMode, errors, newline, context, span).ConfigureAwait(false);
                    }, "Path.read_text", ["encoding", "errors", "newline"], 0),
                    "write_text" => new BoundCallable((arguments, span, context) =>
                    {
                        var (text, encodingMode, errors, newline) = ParsePathWriteTextArguments(arguments, span);
                        context.ObserveString(text, span);
                        var payload = EncodePathText(text, encodingMode, errors, newline, context, span);
                        context.RegisterHostCall(span);
                        WriteEncodedHostText(path.Value.AsString(), payload, encodingMode, context, span);
                        return new BigInteger(text.Length);
                    },
                    async (arguments, span, context) =>
                    {
                        var (text, encodingMode, errors, newline) = ParsePathWriteTextArguments(arguments, span);
                        context.ObserveString(text, span);
                        var payload = EncodePathText(text, encodingMode, errors, newline, context, span);
                        context.RegisterHostCall(span);
                        await WriteEncodedHostTextAsync(path.Value.AsString(), payload, encodingMode, context, span).ConfigureAwait(false);
                        return new BigInteger(text.Length);
                    }, "Path.write_text", ["text", "encoding", "errors", "newline"], 1),
                    "rglob" => new BoundCallable((arguments, span, context) =>
                    {
                        var pattern = ParsePathGlobArguments(arguments, "Path.rglob", span);

                        var results = new PyList([], context.MemoryGovernor, span);
                        foreach (var item in EnumerateRecursive(path.Value, pattern, context, span))
                        {
                            results.Add(item);
                            context.ObserveCollectionCount(results.Count, span);
                        }

                        return results;
                    },
                    async (arguments, span, context) =>
                    {
                        var pattern = ParsePathGlobArguments(arguments, "Path.rglob", span);

                        var results = new PyList([], context.MemoryGovernor, span);
                        await EnumerateRecursiveAsync(path.Value, pattern, context, span, results).ConfigureAwait(false);
                        return results;
                    }, "Path.rglob", ["pattern", "case_sensitive", "recurse_symlinks"], 1),
                    "samefile" => new BoundCallable((arguments, span, context) =>
                    {
                        if (arguments.Length != 1)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.samefile(other_path) expects one argument.", span);
                        }

                        var other = RequirePath(arguments[0], "Path.samefile(other_path)", span);
                        var left = PathOps.Normalize(path.Value.AsString(), context.Host.Cwd);
                        var right = PathOps.Normalize(other.Value.AsString(), context.Host.Cwd);
                        context.RegisterHostCall(span);
                        var leftStat = context.HostStat(left, span);
                        context.RegisterHostCall(span);
                        var rightStat = context.HostStat(right, span);
                        if (!leftStat.Exists || !rightStat.Exists)
                        {
                            throw new LythonRuntimeException("RuntimeError", "Path.samefile() expects both paths to exist.", span);
                        }

                        return string.Equals(left, right, StringComparison.Ordinal);
                    }, "Path.samefile", ["other_path"]),
                    _ => MissingMemberValue.Instance,
                };

                return !ReferenceEquals(value, MissingMemberValue.Instance);
            }
        }
    }
}