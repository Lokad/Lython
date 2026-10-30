using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static partial class PathMembers
    {
        private sealed class HostStatusPathMemberProvider : IPathMemberProvider
        {
            // N17: hot fixed signatures hoisted per family.
            private static readonly LythonCallableSignature PathLstatSignature = LythonCallableSignature.Create("Path.lstat", []);
            public static readonly HostStatusPathMemberProvider Instance = new();

            public bool TryGetMember(PyPath path, string name, [MaybeNullWhen(false)] out object value)
            {
                value = name switch
                {
                    "stat" => BoundCallable.Create((arguments, span, context) =>
                {
                    using var pathScratch = PathOps.ReserveWorking(path.Value, context, span);
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.stat() expects no arguments.", span);
                    }

                    context.CheckExecution(span);
                    return context.HostStat(path.Value.AsString(), span);
                },
                async (arguments, span, context) =>
                {
                    using var pathScratch = PathOps.ReserveWorking(path.Value, context, span);
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.stat() expects no arguments.", span);
                    }

                    context.CheckExecution(span);
                    return await context.HostStatAsync(path.Value.AsString(), span).ConfigureAwait(false);
                }),
                    "lstat" => BoundCallable.Create((arguments, span, context) =>
                    {
                        using var pathScratch = PathOps.ReserveWorking(path.Value, context, span);
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.lstat() expects no arguments.", span);
                        }

                        context.CheckExecution(span);
                        return context.HostStat(path.Value.AsString(), span);
                    },
                    PathLstatSignature, async (arguments, span, context) =>
                    {
                        using var pathScratch = PathOps.ReserveWorking(path.Value, context, span);
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.lstat() expects no arguments.", span);
                        }

                        context.CheckExecution(span);
                        return await context.HostStatAsync(path.Value.AsString(), span).ConfigureAwait(false);
                    }),
                    "exists" => BoundCallable.Create((arguments, span, context) =>
                    {
                        using var pathScratch = PathOps.ReserveWorking(path.Value, context, span);
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.exists() expects no arguments.", span);
                        }

                        context.CheckExecution(span);
                        return context.HostExists(path.Value.AsString(), span);
                    },
                    async (arguments, span, context) =>
                    {
                        using var pathScratch = PathOps.ReserveWorking(path.Value, context, span);
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.exists() expects no arguments.", span);
                        }

                        context.CheckExecution(span);
                        return await context.HostExistsAsync(path.Value.AsString(), span).ConfigureAwait(false);
                    }),
                    "is_file" => BoundCallable.Create((arguments, span, context) =>
                    {
                        using var pathScratch = PathOps.ReserveWorking(path.Value, context, span);
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.is_file() expects no arguments.", span);
                        }

                        context.CheckExecution(span);
                        return context.HostStat(path.Value.AsString(), span).IsFile;
                    },
                    async (arguments, span, context) =>
                    {
                        using var pathScratch = PathOps.ReserveWorking(path.Value, context, span);
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.is_file() expects no arguments.", span);
                        }

                        context.CheckExecution(span);
                        return (await context.HostStatAsync(path.Value.AsString(), span).ConfigureAwait(false)).IsFile;
                    }),
                    "is_dir" => BoundCallable.Create((arguments, span, context) =>
                    {
                        using var pathScratch = PathOps.ReserveWorking(path.Value, context, span);
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.is_dir() expects no arguments.", span);
                        }

                        context.CheckExecution(span);
                        return context.HostStat(path.Value.AsString(), span).IsDir;
                    },
                    async (arguments, span, context) =>
                    {
                        using var pathScratch = PathOps.ReserveWorking(path.Value, context, span);
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.is_dir() expects no arguments.", span);
                        }

                        context.CheckExecution(span);
                        return (await context.HostStatAsync(path.Value.AsString(), span).ConfigureAwait(false)).IsDir;
                    }),
                    "is_symlink" => BoundCallable.Create((arguments, span, _) =>
                    {
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.is_symlink() expects no arguments.", span);
                        }

                        return false;
                    }),
                    _ => MissingMemberValue.Instance,
                };

                return !ReferenceEquals(value, MissingMemberValue.Instance);
            }
        }

        private sealed class HostMutationPathMemberProvider : IPathMemberProvider
        {
            // N17: hot fixed signatures hoisted per family.
            private static readonly LythonCallableSignature PathUnlinkSignature = LythonCallableSignature.Create("Path.unlink", ["missing_ok"], 0);
            private static readonly LythonCallableSignature PathRenameSignature = LythonCallableSignature.Create("Path.rename", ["target"]);
            private static readonly LythonCallableSignature PathReplaceSignature = LythonCallableSignature.Create("Path.replace", ["target"]);
            private static readonly LythonCallableSignature PathMkdirSignature = LythonCallableSignature.Create("Path.mkdir", ["mode", "parents", "exist_ok"], 0);
            private static readonly LythonCallableSignature PathTouchSignature = LythonCallableSignature.Create("Path.touch", ["mode", "exist_ok"], 0);
            public static readonly HostMutationPathMemberProvider Instance = new();

            public bool TryGetMember(PyPath path, string name, [MaybeNullWhen(false)] out object value)
            {
                value = name switch
                {
                    "unlink" => BoundCallable.Create((arguments, span, context) =>
                    {
                        using var pathScratch = PathOps.ReserveWorking(path.Value, context, span);
                        if (arguments.Length > 1)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.unlink([missing_ok]) expects zero or one argument.", span);
                        }

                        var missingOk = arguments.Length > 0 && IsTruthy(arguments[0], context, span);
                        context.CheckExecution(span);
                        context.HostRemoveFile(path.Value.AsString(), missingOk, span);
                        return PyNone.Instance;
                    },
                    PathUnlinkSignature, async (arguments, span, context) =>
                    {
                        using var pathScratch = PathOps.ReserveWorking(path.Value, context, span);
                        if (arguments.Length > 1)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.unlink([missing_ok]) expects zero or one argument.", span);
                        }

                        var missingOk = arguments.Length > 0 && await IsTruthyAsync(arguments[0], context, span).ConfigureAwait(false);
                        context.CheckExecution(span);
                        await context.HostRemoveFileAsync(path.Value.AsString(), missingOk, span).ConfigureAwait(false);
                        return PyNone.Instance;
                    }),
                    "rmdir" => BoundCallable.Create((arguments, span, context) =>
                    {
                        using var pathScratch = PathOps.ReserveWorking(path.Value, context, span);
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.rmdir() expects no arguments.", span);
                        }

                        context.CheckExecution(span);
                        context.HostRemoveDirectory(path.Value.AsString(), span);
                        return PyNone.Instance;
                    },
                    async (arguments, span, context) =>
                    {
                        using var pathScratch = PathOps.ReserveWorking(path.Value, context, span);
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.rmdir() expects no arguments.", span);
                        }

                        context.CheckExecution(span);
                        await context.HostRemoveDirectoryAsync(path.Value.AsString(), span).ConfigureAwait(false);
                        return PyNone.Instance;
                    }),
                    "rename" => BoundCallable.Create((arguments, span, context) =>
                    {
                        using var pathScratch = PathOps.ReserveWorking(path.Value, context, span);
                        if (arguments.Length != 1)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.rename(target) expects one argument.", span);
                        }

                        var target = RequirePath(arguments[0], "Path.rename(target)", span, context, path.IsPure);
                        using var targetScratch = PathOps.ReserveWorking(target.Value, context, span);
                        context.CheckExecution(span);
                        context.HostMove(path.Value.AsString(), target.Value.AsString(), span);
                        return target;
                    },
                    PathRenameSignature, async (arguments, span, context) =>
                    {
                        using var pathScratch = PathOps.ReserveWorking(path.Value, context, span);
                        if (arguments.Length != 1)
                        {
                            throw new LythonRuntimeException("TypeError", "Path.rename(target) expects one argument.", span);
                        }

                        var target = await RequirePathAsync(arguments[0], "Path.rename(target)", span, context, path.IsPure).ConfigureAwait(false);
                        using var targetScratch = PathOps.ReserveWorking(target.Value, context, span);
                        context.CheckExecution(span);
                        await context.HostMoveAsync(path.Value.AsString(), target.Value.AsString(), span).ConfigureAwait(false);
                        return target;
                    }),
                    "replace" => BoundCallable.Create((arguments, span, context) =>
                    {
                        using var pathScratch = PathOps.ReserveWorking(path.Value, context, span);
                        var target = RequirePath(arguments[0], "Path.replace(target)", span, context, path.IsPure);
                        using var targetScratch = PathOps.ReserveWorking(target.Value, context, span);
                        context.CheckExecution(span);
                        context.HostReplace(path.Value.AsString(), target.Value.AsString(), span);
                        return target;
                    },
                    PathReplaceSignature, async (arguments, span, context) =>
                    {
                        using var pathScratch = PathOps.ReserveWorking(path.Value, context, span);
                        var target = await RequirePathAsync(arguments[0], "Path.replace(target)", span, context, path.IsPure).ConfigureAwait(false);
                        using var targetScratch = PathOps.ReserveWorking(target.Value, context, span);
                        context.CheckExecution(span);
                        await context.HostReplaceAsync(path.Value.AsString(), target.Value.AsString(), span).ConfigureAwait(false);
                        return target;
                    }),
                    "mkdir" => BoundCallable.CreateWithPresence(
                        (arguments, span, context) => PathMkDirCoreAsync(path.Value, arguments, span, context, false).GetAwaiter().GetResult(),
                        PathMkdirSignature, (arguments, span, context) => PathMkDirCoreAsync(path.Value, arguments, span, context, true)),
                    "touch" => BoundCallable.CreateWithPresence(
                        (arguments, span, context) => PathTouchCoreAsync(path.Value, arguments, span, context, false).GetAwaiter().GetResult(),
                        PathTouchSignature, (arguments, span, context) => PathTouchCoreAsync(path.Value, arguments, span, context, true)),
                    _ => MissingMemberValue.Instance,
                };

                return !ReferenceEquals(value, MissingMemberValue.Instance);
            }
        }

        private sealed class UnsupportedHostPathMemberProvider : IPathMemberProvider
        {
            // N17: hot fixed signatures hoisted per family.
            private static readonly LythonCallableSignature PathReadlinkSignature = LythonCallableSignature.Create("Path.readlink");
            private static readonly LythonCallableSignature PathSymlinkToSignature = LythonCallableSignature.Create("Path.symlink_to");
            private static readonly LythonCallableSignature PathHardlinkToSignature = LythonCallableSignature.Create("Path.hardlink_to");
            private static readonly LythonCallableSignature PathChmodSignature = LythonCallableSignature.Create("Path.chmod");
            private static readonly LythonCallableSignature PathOwnerSignature = LythonCallableSignature.Create("Path.owner");
            private static readonly LythonCallableSignature PathGroupSignature = LythonCallableSignature.Create("Path.group");
            public static readonly UnsupportedHostPathMemberProvider Instance = new();

            public bool TryGetMember(PyPath path, string name, [MaybeNullWhen(false)] out object value)
            {
                _ = path;
                value = name switch
                {
                    "readlink" => UnsupportedPathMember(PathReadlinkSignature, "Path.readlink() is not supported by Lython because symlink targets are not exposed by the host path model."),
                    "symlink_to" => UnsupportedPathMember(PathSymlinkToSignature, "Path.symlink_to(target, target_is_directory=False) is not supported by Lython because symlink mutation is outside the host path model."),
                    "hardlink_to" => UnsupportedPathMember(PathHardlinkToSignature, "Path.hardlink_to(target) is not supported by Lython because hardlink mutation is outside the host path model."),
                    "chmod" => UnsupportedPathMember(PathChmodSignature, "Path.chmod(mode) is not supported by Lython because permissions are not exposed by the host path model."),
                    "owner" => UnsupportedPathMember(PathOwnerSignature, "Path.owner() is not supported by Lython because user ownership is not exposed by the host path model."),
                    "group" => UnsupportedPathMember(PathGroupSignature, "Path.group() is not supported by Lython because group ownership is not exposed by the host path model."),
                    _ => MissingMemberValue.Instance,
                };

                return !ReferenceEquals(value, MissingMemberValue.Instance);
            }
        }

        private sealed class HostContentPathMemberProvider : IPathMemberProvider
        {
            // N17: hot fixed signatures hoisted per family.
            private static readonly LythonCallableSignature PathGlobSignature = LythonCallableSignature.Create("Path.glob", ["pattern", "case_sensitive", "recurse_symlinks"], 1);
            private static readonly LythonCallableSignature PathReadTextSignature = LythonCallableSignature.Create("Path.read_text", ["encoding", "errors", "newline"], 0);
            private static readonly LythonCallableSignature PathWriteTextSignature = LythonCallableSignature.Create("Path.write_text", ["text", "encoding", "errors", "newline"], 1);
            private static readonly LythonCallableSignature PathReadBytesSignature = LythonCallableSignature.Create("Path.read_bytes", []);
            private static readonly LythonCallableSignature PathWriteBytesSignature = LythonCallableSignature.Create("Path.write_bytes", ["data"], 1);
            private static readonly LythonCallableSignature PathRglobSignature = LythonCallableSignature.Create("Path.rglob", ["pattern", "case_sensitive", "recurse_symlinks"], 1);
            private static readonly LythonCallableSignature PathSamefileSignature = LythonCallableSignature.Create("Path.samefile", ["other_path"]);
            public static readonly HostContentPathMemberProvider Instance = new();

            public bool TryGetMember(PyPath path, string name, [MaybeNullWhen(false)] out object value)
            {
                value = name switch
                {
                    "read_bytes" => BoundCallable.Create(
                        (arguments, span, context) => ReadPathBytesAsync(path.Value, span, context, false).GetAwaiter().GetResult(),
                        PathReadBytesSignature,
                        (arguments, span, context) => ReadPathBytesAsync(path.Value, span, context, true)),
                    "write_bytes" => BoundCallable.Create(
                        (arguments, span, context) => WritePathBytesAsync(path.Value, arguments[0], span, context, false).GetAwaiter().GetResult(),
                        PathWriteBytesSignature,
                        (arguments, span, context) => WritePathBytesAsync(path.Value, arguments[0], span, context, true)),
                    "open" => new PathOpenCallable(path.Value),
                    "glob" => BoundCallable.Create(
                        (arguments, span, context) => PathGlob(path, arguments, span, context, false, false).GetAwaiter().GetResult(),
                        PathGlobSignature, (arguments, span, context) => PathGlob(path, arguments, span, context, true, false)),
                    "iterdir" => BoundCallable.CreateNoArguments(path, "Path.iterdir",
                        (receiver, span, context) => PathGlob(receiver, [], span, context, false, false, true).GetAwaiter().GetResult(),
                        (receiver, span, context) => PathGlob(receiver, [], span, context, true, false, true)),
                    "read_text" => BoundCallable.Create((arguments, span, context) =>
                    {
                        using var pathScratch = PathOps.ReserveWorking(path.Value, context, span);
                        var (encodingMode, errors, newline) = ParsePathReadTextArguments(arguments, span);
                        return ReadPathText(path.Value.AsString(), encodingMode, errors, newline, context, span);
                    },
                    PathReadTextSignature, async (arguments, span, context) =>
                    {
                        using var pathScratch = PathOps.ReserveWorking(path.Value, context, span);
                        var (encodingMode, errors, newline) = ParsePathReadTextArguments(arguments, span);
                        return await ReadPathTextAsync(path.Value.AsString(), encodingMode, errors, newline, context, span).ConfigureAwait(false);
                    }),
                    "write_text" => BoundCallable.Create(
                        (arguments,span,context)=>WritePathTextAsync(path.Value,arguments,span,context,false).GetAwaiter().GetResult(),
                        PathWriteTextSignature,
                        (arguments,span,context)=>WritePathTextAsync(path.Value,arguments,span,context,true)),
                    "rglob" => BoundCallable.Create(
                        (arguments, span, context) => PathGlob(path, arguments, span, context, false, true).GetAwaiter().GetResult(),
                        PathRglobSignature, (arguments, span, context) => PathGlob(path, arguments, span, context, true, true)),
                    "samefile" => BoundCallable.Create(
                        (arguments, span, context) => PathSameFile(path, arguments[0], span, context, false).GetAwaiter().GetResult(),
                        PathSamefileSignature, (arguments, span, context) => PathSameFile(path, arguments[0], span, context, true)),
                    _ => MissingMemberValue.Instance,
                };

                return !ReferenceEquals(value, MissingMemberValue.Instance);
            }
        }
    }
}
