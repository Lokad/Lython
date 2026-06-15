using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class ShutilModule : PyModule
    {
        public static readonly ShutilModule Instance = new();
        private static readonly ExceptionTypeValue ShutilError = new("Error");
        private static readonly ExceptionTypeValue ShutilSameFileError = new("SameFileError");

        private ShutilModule() : base("shutil")
        {
        }

        public override IReadOnlyList<string> ExportedNames { get; } =
        [
            "Error",
            "SameFileError",
            "copyfile",
            "copy",
            "copy2",
            "move",
            "copyfileobj",
        ];

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "Error" => ShutilError,
                "SameFileError" => ShutilSameFileError,
                "copyfile" => new BuiltinCallable(LythonKnownCallableSignatures.ShutilCopyFile, ShutilCopyFile, ShutilCopyFileAsync),
                "copy" => new BuiltinCallable(LythonKnownCallableSignatures.ShutilCopy, ShutilCopy, ShutilCopyAsync),
                "copy2" => new BuiltinCallable(LythonKnownCallableSignatures.ShutilCopy2, ShutilCopy2),
                "move" => new BuiltinCallable(LythonKnownCallableSignatures.ShutilMove, ShutilMove, ShutilMoveAsync),
                "copyfileobj" => new BuiltinCallable(LythonKnownCallableSignatures.ShutilCopyFileObj, ShutilCopyFileObj, ShutilCopyFileObjAsync),
                _ => null!,
            };

            return value is not null;
        }
    }

    private static object ShutilCopyFile(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (!ParseFollowSymlinks(arguments, 2, "shutil.copyfile", span))
        {
            throw new LythonRuntimeException("NotImplementedError", "shutil.copyfile(..., follow_symlinks=False) is not supported because Lython's host path model does not expose symlinks.", span);
        }

        var source = NormalizeShutilPath(arguments[0], "shutil.copyfile", span, context);
        var destination = NormalizeShutilPath(arguments[1], "shutil.copyfile", span, context);
        CopyHostFile(source, destination, destinationMayBeDirectory: false, "shutil.copyfile", context, span);
        return PyString.FromString(destination, context.MemoryGovernor, span);
    }

    private static async ValueTask<object> ShutilCopyFileAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (!ParseFollowSymlinks(arguments, 2, "shutil.copyfile", span))
        {
            throw new LythonRuntimeException("NotImplementedError", "shutil.copyfile(..., follow_symlinks=False) is not supported because Lython's host path model does not expose symlinks.", span);
        }

        var source = NormalizeShutilPath(arguments[0], "shutil.copyfile", span, context);
        var destination = NormalizeShutilPath(arguments[1], "shutil.copyfile", span, context);
        await CopyHostFileAsync(source, destination, destinationMayBeDirectory: false, "shutil.copyfile", context, span).ConfigureAwait(false);
        return PyString.FromString(destination, context.MemoryGovernor, span);
    }

    private static object ShutilCopy(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (!ParseFollowSymlinks(arguments, 2, "shutil.copy", span))
        {
            throw new LythonRuntimeException("NotImplementedError", "shutil.copy(..., follow_symlinks=False) is not supported because Lython's host path model does not expose symlinks.", span);
        }

        var source = NormalizeShutilPath(arguments[0], "shutil.copy", span, context);
        var destination = NormalizeShutilPath(arguments[1], "shutil.copy", span, context);
        var resolved = CopyHostFile(source, destination, destinationMayBeDirectory: true, "shutil.copy", context, span);
        return PyString.FromString(resolved, context.MemoryGovernor, span);
    }

    private static async ValueTask<object> ShutilCopyAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (!ParseFollowSymlinks(arguments, 2, "shutil.copy", span))
        {
            throw new LythonRuntimeException("NotImplementedError", "shutil.copy(..., follow_symlinks=False) is not supported because Lython's host path model does not expose symlinks.", span);
        }

        var source = NormalizeShutilPath(arguments[0], "shutil.copy", span, context);
        var destination = NormalizeShutilPath(arguments[1], "shutil.copy", span, context);
        var resolved = await CopyHostFileAsync(source, destination, destinationMayBeDirectory: true, "shutil.copy", context, span).ConfigureAwait(false);
        return PyString.FromString(resolved, context.MemoryGovernor, span);
    }

    private static object ShutilCopy2(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = arguments;
        _ = context;
        throw new LythonRuntimeException("NotImplementedError", "shutil.copy2() is not supported because Lython's host contract cannot preserve file metadata.", span);
    }

    private static object ShutilMove(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        RejectCustomCopyFunction(arguments, "shutil.move", span);
        var source = NormalizeShutilPath(arguments[0], "shutil.move", span, context);
        var destination = NormalizeShutilPath(arguments[1], "shutil.move", span, context);
        var resolved = ResolveMoveDestination(source, destination, context, span);
        EnsureDistinctFiles(source, resolved, "shutil.move", span);

        var destinationStat = HostStat(resolved, context, span);
        if (destinationStat.Exists)
        {
            throw new LythonRuntimeException("RuntimeError", $"shutil.move() destination already exists: {resolved}", span);
        }

        context.RegisterHostCall(span);
        context.HostMove(source, resolved, span);
        return PyString.FromString(resolved, context.MemoryGovernor, span);
    }

    private static async ValueTask<object> ShutilMoveAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        RejectCustomCopyFunction(arguments, "shutil.move", span);
        var source = NormalizeShutilPath(arguments[0], "shutil.move", span, context);
        var destination = NormalizeShutilPath(arguments[1], "shutil.move", span, context);
        var resolved = await ResolveMoveDestinationAsync(source, destination, context, span).ConfigureAwait(false);
        EnsureDistinctFiles(source, resolved, "shutil.move", span);

        var destinationStat = await OsHostStatAsync(resolved, context, span).ConfigureAwait(false);
        if (destinationStat.Exists)
        {
            throw new LythonRuntimeException("RuntimeError", $"shutil.move() destination already exists: {resolved}", span);
        }

        context.RegisterHostCall(span);
        await context.HostMoveAsync(source, resolved, span).ConfigureAwait(false);
        return PyString.FromString(resolved, context.MemoryGovernor, span);
    }

    private static object ShutilCopyFileObj(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        EnsureSupportedCopyFileObjLength(arguments, span);
        var read = GetCallableMember(arguments[0], "read", "shutil.copyfileobj", span, context);
        var write = GetCallableMember(arguments[1], "write", "shutil.copyfileobj", span, context);
        var text = read.Invoke([], span, context);
        if (!PyStringOps.TryAsString(text, out var pyText))
        {
            throw new LythonRuntimeException("TypeError", "shutil.copyfileobj() expects fsrc.read() to return text.", span);
        }

        _ = write.Invoke([new CallArgumentValue(null, pyText)], span, context);
        return PyNone.Instance;
    }

    private static async ValueTask<object> ShutilCopyFileObjAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        EnsureSupportedCopyFileObjLength(arguments, span);
        var read = GetCallableMember(arguments[0], "read", "shutil.copyfileobj", span, context);
        var write = GetCallableMember(arguments[1], "write", "shutil.copyfileobj", span, context);
        var text = await read.InvokeAsync([], span, context).ConfigureAwait(false);
        if (!PyStringOps.TryAsString(text, out var pyText))
        {
            throw new LythonRuntimeException("TypeError", "shutil.copyfileobj() expects fsrc.read() to return text.", span);
        }

        _ = await write.InvokeAsync([new CallArgumentValue(null, pyText)], span, context).ConfigureAwait(false);
        return PyNone.Instance;
    }

    private static string CopyHostFile(
        string source,
        string destination,
        bool destinationMayBeDirectory,
        string owner,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        var sourceStat = HostStat(source, context, span);
        if (!sourceStat.Exists || !sourceStat.IsFile)
        {
            throw new LythonRuntimeException("RuntimeError", $"{owner}() source is not a file: {source}", span);
        }

        var resolved = ResolveCopyDestination(source, destination, destinationMayBeDirectory, owner, context, span);
        EnsureDistinctFiles(source, resolved, owner, span);
        RemoveExistingFileDestination(resolved, owner, context, span);

        context.RegisterHostCall(span);
        context.HostCopy(source, resolved, span);
        return resolved;
    }

    private static async ValueTask<string> CopyHostFileAsync(
        string source,
        string destination,
        bool destinationMayBeDirectory,
        string owner,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        var sourceStat = await OsHostStatAsync(source, context, span).ConfigureAwait(false);
        if (!sourceStat.Exists || !sourceStat.IsFile)
        {
            throw new LythonRuntimeException("RuntimeError", $"{owner}() source is not a file: {source}", span);
        }

        var resolved = await ResolveCopyDestinationAsync(source, destination, destinationMayBeDirectory, owner, context, span).ConfigureAwait(false);
        EnsureDistinctFiles(source, resolved, owner, span);
        await RemoveExistingFileDestinationAsync(resolved, owner, context, span).ConfigureAwait(false);

        context.RegisterHostCall(span);
        await context.HostCopyAsync(source, resolved, span).ConfigureAwait(false);
        return resolved;
    }

    private static string ResolveCopyDestination(
        string source,
        string destination,
        bool destinationMayBeDirectory,
        string owner,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        var destinationStat = HostStat(destination, context, span);
        if (!destinationStat.IsDir)
        {
            return destination;
        }

        if (!destinationMayBeDirectory)
        {
            throw new LythonRuntimeException("RuntimeError", $"{owner}() destination is a directory: {destination}", span);
        }

        return PathOps.Normalize(PathOps.Join(destination, PathOps.BaseName(source)), context.Host.Cwd);
    }

    private static async ValueTask<string> ResolveCopyDestinationAsync(
        string source,
        string destination,
        bool destinationMayBeDirectory,
        string owner,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        var destinationStat = await OsHostStatAsync(destination, context, span).ConfigureAwait(false);
        if (!destinationStat.IsDir)
        {
            return destination;
        }

        if (!destinationMayBeDirectory)
        {
            throw new LythonRuntimeException("RuntimeError", $"{owner}() destination is a directory: {destination}", span);
        }

        return PathOps.Normalize(PathOps.Join(destination, PathOps.BaseName(source)), context.Host.Cwd);
    }

    private static string ResolveMoveDestination(string source, string destination, ExecutionContext context, LythonSourceSpan span)
    {
        var destinationStat = HostStat(destination, context, span);
        return destinationStat.IsDir
            ? PathOps.Normalize(PathOps.Join(destination, PathOps.BaseName(source)), context.Host.Cwd)
            : destination;
    }

    private static async ValueTask<string> ResolveMoveDestinationAsync(string source, string destination, ExecutionContext context, LythonSourceSpan span)
    {
        var destinationStat = await OsHostStatAsync(destination, context, span).ConfigureAwait(false);
        return destinationStat.IsDir
            ? PathOps.Normalize(PathOps.Join(destination, PathOps.BaseName(source)), context.Host.Cwd)
            : destination;
    }

    private static void RemoveExistingFileDestination(string destination, string owner, ExecutionContext context, LythonSourceSpan span)
    {
        var destinationStat = HostStat(destination, context, span);
        if (!destinationStat.Exists)
        {
            return;
        }

        if (destinationStat.IsDir)
        {
            throw new LythonRuntimeException("RuntimeError", $"{owner}() destination is a directory: {destination}", span);
        }

        context.RegisterHostCall(span);
        context.HostRemove(destination, span);
    }

    private static async ValueTask RemoveExistingFileDestinationAsync(string destination, string owner, ExecutionContext context, LythonSourceSpan span)
    {
        var destinationStat = await OsHostStatAsync(destination, context, span).ConfigureAwait(false);
        if (!destinationStat.Exists)
        {
            return;
        }

        if (destinationStat.IsDir)
        {
            throw new LythonRuntimeException("RuntimeError", $"{owner}() destination is a directory: {destination}", span);
        }

        context.RegisterHostCall(span);
        await context.HostRemoveAsync(destination, span).ConfigureAwait(false);
    }

    private static string NormalizeShutilPath(object value, string owner, LythonSourceSpan span, ExecutionContext context)
        => PathOps.Normalize(GetPath(value, owner, span), context.Host.Cwd);

    private static void EnsureDistinctFiles(string source, string destination, string owner, LythonSourceSpan span)
    {
        if (string.Equals(source, destination, StringComparison.Ordinal))
        {
            throw new LythonRuntimeException("SameFileError", $"{owner}() source and destination are the same file.", span);
        }
    }

    private static bool ParseFollowSymlinks(object[] arguments, int index, string owner, LythonSourceSpan span)
    {
        if (arguments.Length <= index)
        {
            return true;
        }

        return arguments[index] switch
        {
            bool value => value,
            _ => throw new LythonRuntimeException("TypeError", $"{owner}(..., follow_symlinks=...) expects a bool.", span),
        };
    }

    private static void RejectCustomCopyFunction(object[] arguments, string owner, LythonSourceSpan span)
    {
        if (arguments.Length > 2)
        {
            throw new LythonRuntimeException("NotImplementedError", $"{owner}(..., copy_function=...) is not supported by Lython.", span);
        }
    }

    private static void EnsureSupportedCopyFileObjLength(object[] arguments, LythonSourceSpan span)
    {
        if (arguments.Length <= 2)
        {
            return;
        }

        if (arguments[2] is not BigInteger length)
        {
            throw new LythonRuntimeException("TypeError", "shutil.copyfileobj(..., length=...) expects an integer.", span);
        }

        if (length != BigInteger.Zero)
        {
            throw new LythonRuntimeException("NotImplementedError", "shutil.copyfileobj(..., length=...) only supports length=0 in Lython.", span);
        }
    }

    private static ICallable GetCallableMember(object target, string memberName, string owner, LythonSourceSpan span, ExecutionContext context)
    {
        if (!PyMemberAccess.TryResolve(target, memberName, context, span, out var member) ||
            ReferenceEquals(member, PyNone.Instance) ||
            member is not ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", $"{owner}() expects file-like objects with callable {memberName}().", span);
        }

        return callable;
    }
}
