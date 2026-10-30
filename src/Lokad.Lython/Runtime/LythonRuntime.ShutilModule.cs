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
        => ShutilCopyCoreAsync(arguments, false, span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> ShutilCopyFileAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => ShutilCopyCoreAsync(arguments, false, span, context, true);
    private static object ShutilCopy(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => ShutilCopyCoreAsync(arguments, true, span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> ShutilCopyAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => ShutilCopyCoreAsync(arguments, true, span, context, true);

    private static async ValueTask<object> ShutilCopyCoreAsync(object[] arguments, bool destinationMayBeDirectory,
        LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        var owner = destinationMayBeDirectory ? "shutil.copy" : "shutil.copyfile";
        if (!ParseFollowSymlinks(arguments, 2, owner, span))
            throw new LythonRuntimeException("NotImplementedError", $"{owner}(..., follow_symlinks=False) is not supported because Lython's host path model does not expose symlinks.", span);
        using var scratch = context.MemoryGovernor.ReserveTemporary(256L, span);
        var source = asynchronous ? await GetPathAsync(arguments[0], owner, span, context, scratch).ConfigureAwait(false)
            : GetPath(arguments[0], owner, span, context, scratch);
        var destination = asynchronous ? await GetPathAsync(arguments[1], owner, span, context, scratch).ConfigureAwait(false)
            : GetPath(arguments[1], owner, span, context, scratch);
        var sourceStat = RequireExistingPath(asynchronous ? await HostStatAsync(source, context, span).ConfigureAwait(false)
            : HostStat(source, context, span), span);
        if (!sourceStat.IsFile) throw new LythonRuntimeException("IsADirectoryError", $"{owner}() source is not a file: {source}", span);
        var destinationStat = asynchronous ? await HostStatAsync(destination, context, span).ConfigureAwait(false) : HostStat(destination, context, span);
        if (!destinationMayBeDirectory && destinationStat.IsDir)
            throw new LythonRuntimeException("IsADirectoryError", $"{owner}() destination is a directory: {destination}", span);
        var resolved = destinationStat.IsDir ? PathOps.Combine(destination, PathOps.BaseName(source)) : destination;
        EnsureDistinctFiles(source, resolved, owner, context, span);
        // Prepare the mandatory result before the host can publish its copy.
        var result = PyString.FromString(resolved, context.MemoryGovernor, span);
        context.CheckExecution(span);
        if (asynchronous) await context.HostCopyAsync(source, resolved, span).ConfigureAwait(false);
        else context.HostCopy(source, resolved, span);
        context.CheckExecution(span);
        return result;
    }

    private static object ShutilCopy2(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => throw new LythonRuntimeException("NotImplementedError", "shutil.copy2() is not supported because Lython's host contract cannot preserve file metadata.", span);

    private static object ShutilMove(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => ShutilMoveCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> ShutilMoveAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => ShutilMoveCoreAsync(arguments, span, context, true);

    private static async ValueTask<object> ShutilMoveCoreAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        RejectCustomCopyFunction(arguments, "shutil.move", span);
        using var scratch = context.MemoryGovernor.ReserveTemporary(256L, span);
        var source = asynchronous ? await GetPathAsync(arguments[0], "shutil.move", span, context, scratch).ConfigureAwait(false)
            : GetPath(arguments[0], "shutil.move", span, context, scratch);
        var destination = asynchronous ? await GetPathAsync(arguments[1], "shutil.move", span, context, scratch).ConfigureAwait(false)
            : GetPath(arguments[1], "shutil.move", span, context, scratch);
        RequireExistingPath(asynchronous ? await HostStatAsync(source, context, span).ConfigureAwait(false) : HostStat(source, context, span), span);
        var destinationStat = asynchronous ? await HostStatAsync(destination, context, span).ConfigureAwait(false) : HostStat(destination, context, span);
        var resolved = destinationStat.IsDir ? PathOps.Combine(destination, PathOps.BaseName(source)) : destination;
        EnsureDistinctFiles(source, resolved, "shutil.move", context, span);
        if (destinationStat.IsDir) destinationStat = asynchronous ? await HostStatAsync(resolved, context, span).ConfigureAwait(false) : HostStat(resolved, context, span);
        if (destinationStat.Exists) throw new LythonRuntimeException("RuntimeError", $"shutil.move() destination already exists: {resolved}", span);
        var result = PyString.FromString(resolved, context.MemoryGovernor, span);
        context.CheckExecution(span);
        if (asynchronous) await context.HostMoveAsync(source, resolved, span).ConfigureAwait(false);
        else context.HostMove(source, resolved, span);
        context.CheckExecution(span);
        return result;
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

    private static void EnsureDistinctFiles(string source, string destination, string owner, ExecutionContext context, LythonSourceSpan span)
    {
        if (PathOps.Normalize(source, context.Host.Cwd) == PathOps.Normalize(destination, context.Host.Cwd))
            throw new LythonRuntimeException(ModuleException("shutil", "SameFileError"), $"{owner}() source and destination are the same file.", span);
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
