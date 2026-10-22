using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class ShutilModule : PyModule
    {
        public static readonly ShutilModule Instance = new();
        private static readonly ExceptionTypeValue ShutilError = new(ModuleException("shutil", "Error"));
        private static readonly ExceptionTypeValue ShutilSameFileError = new(ModuleException("shutil", "SameFileError"));

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

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "Error" => ShutilError,
                "SameFileError" => ShutilSameFileError,
                "copyfile" => BuiltinCallable.Create(LythonKnownCallableSignatures.ShutilCopyFile, ShutilCopyFile, ShutilCopyFileAsync),
                "copy" => BuiltinCallable.Create(LythonKnownCallableSignatures.ShutilCopy, ShutilCopy, ShutilCopyAsync),
                "copy2" => BuiltinCallable.Create(LythonKnownCallableSignatures.ShutilCopy2, ShutilCopy2),
                "move" => BuiltinCallable.Create(LythonKnownCallableSignatures.ShutilMove, ShutilMove, ShutilMoveAsync),
                "copyfileobj" => BuiltinCallable.Create(LythonKnownCallableSignatures.ShutilCopyFileObj, ShutilCopyFileObj, ShutilCopyFileObjAsync),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
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

        context.CheckExecution(span);
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

        var destinationStat = await HostStatAsync(resolved, context, span).ConfigureAwait(false);
        if (destinationStat.Exists)
        {
            throw new LythonRuntimeException("RuntimeError", $"shutil.move() destination already exists: {resolved}", span);
        }

        context.CheckExecution(span);
        await context.HostMoveAsync(source, resolved, span).ConfigureAwait(false);
        return PyString.FromString(resolved, context.MemoryGovernor, span);
    }

    private static object ShutilCopyFileObj(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => ShutilCopyFileObjCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();

    private static ValueTask<object> ShutilCopyFileObjAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => ShutilCopyFileObjCoreAsync(arguments, span, context, true);

    private static async ValueTask<object> ShutilCopyFileObjCoreAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        object length = arguments.Length > 2 ? arguments[2] : BigInteger.Zero;
        var hasLength = asynchronous ? await IsTruthyAsync(length, context, span).ConfigureAwait(false) : IsTruthy(length, context, span);
        if (!hasLength) length = new BigInteger(64 * 1024);
        var read = await GetCopyStreamCallableAsync(arguments[0], "read", context, span, asynchronous).ConfigureAwait(false);
        var write = await GetCopyStreamCallableAsync(arguments[1], "write", context, span, asynchronous).ConfigureAwait(false);
        while (true)
        {
            context.CheckExecution(span);
            var chunk = asynchronous ? await CallableInvocation.InvokeUnaryAsync(read, length, span, context).ConfigureAwait(false)
                : CallableInvocation.InvokeUnary(read, length, span, context);
            var nonempty = asynchronous ? await IsTruthyAsync(chunk, context, span).ConfigureAwait(false) : IsTruthy(chunk, context, span);
            context.CheckExecution(span);
            if (!nonempty) return PyNone.Instance;
            if (asynchronous) await CallableInvocation.InvokeUnaryAsync(write, chunk, span, context).ConfigureAwait(false);
            else CallableInvocation.InvokeUnary(write, chunk, span, context);
        }
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

        context.CheckExecution(span);
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
        var sourceStat = await HostStatAsync(source, context, span).ConfigureAwait(false);
        if (!sourceStat.Exists || !sourceStat.IsFile)
        {
            throw new LythonRuntimeException("RuntimeError", $"{owner}() source is not a file: {source}", span);
        }

        var resolved = await ResolveCopyDestinationAsync(source, destination, destinationMayBeDirectory, owner, context, span).ConfigureAwait(false);
        EnsureDistinctFiles(source, resolved, owner, span);
        await RemoveExistingFileDestinationAsync(resolved, owner, context, span).ConfigureAwait(false);

        context.CheckExecution(span);
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
        var destinationStat = await HostStatAsync(destination, context, span).ConfigureAwait(false);
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
        var destinationStat = await HostStatAsync(destination, context, span).ConfigureAwait(false);
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

        context.CheckExecution(span);
        context.HostRemove(destination, span);
    }

    private static async ValueTask RemoveExistingFileDestinationAsync(string destination, string owner, ExecutionContext context, LythonSourceSpan span)
    {
        var destinationStat = await HostStatAsync(destination, context, span).ConfigureAwait(false);
        if (!destinationStat.Exists)
        {
            return;
        }

        if (destinationStat.IsDir)
        {
            throw new LythonRuntimeException("RuntimeError", $"{owner}() destination is a directory: {destination}", span);
        }

        context.CheckExecution(span);
        await context.HostRemoveAsync(destination, span).ConfigureAwait(false);
    }

    private static string NormalizeShutilPath(object value, string owner, LythonSourceSpan span, ExecutionContext context)
        => PathOps.Normalize(GetPath(value, owner, span), context.Host.Cwd);

    private static void EnsureDistinctFiles(string source, string destination, string owner, LythonSourceSpan span)
    {
        if (string.Equals(source, destination, StringComparison.Ordinal))
        {
            throw new LythonRuntimeException(ModuleException("shutil", "SameFileError"), $"{owner}() source and destination are the same file.", span);
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

    private static async ValueTask<ICallable> GetCopyStreamCallableAsync(object target, string memberName, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        var member = asynchronous ? await TryResolveRuntimeMemberAsync(target, memberName, context, span).ConfigureAwait(false)
            : TryResolveRuntimeMember(target, memberName, context, span, out var value) ? (true, value) : (false, (object)PyNone.Instance);
        return member.Item1 && member.Item2 is ICallable callable ? callable
            : throw new LythonRuntimeException("TypeError", $"shutil.copyfileobj() expects file-like objects with callable {memberName}().", span);
    }
}
