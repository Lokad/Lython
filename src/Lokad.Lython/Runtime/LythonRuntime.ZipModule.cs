using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Runtime.Zip;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    /// <summary>
    /// Contained <c>zipfile</c> surface (read/write stages): exception identities,
    /// compression constants, <c>is_zipfile</c>, <c>ZipInfo</c>, read-only and
    /// staged-creation <c>ZipFile</c> with sequential member handles.
    /// <c>BadZipfile</c> and <c>error</c> are the same object as <c>BadZipFile</c>,
    /// matching CPython's legacy aliases.
    /// Modes <c>'a'</c> and <c>'x'</c> stay explicitly rejected.
    /// </summary>
    private sealed class ZipModule : PyModule
    {
        public static readonly ZipModule Instance = new();

        private static readonly ExceptionTypeValue BadZipFileValue =
            new(ModuleException("zipfile", "BadZipFile"));

        private static readonly ExceptionTypeValue LargeZipFileValue =
            new(ModuleException("zipfile", "LargeZipFile"));

        private ZipModule() : base("zipfile")
        {
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "BadZipFile" or "BadZipfile" or "error" => BadZipFileValue,
                "LargeZipFile" => LargeZipFileValue,
                "ZIP_STORED" => new BigInteger(0),
                "ZIP_DEFLATED" => new BigInteger(8),
                "ZIP_BZIP2" => new BigInteger(12),
                "ZIP_LZMA" => new BigInteger(14),
                "is_zipfile" => ZipIsZipFileCallable.Instance,
                "ZipInfo" => ZipInfoCallable.Instance,
                "ZipFile" => ZipFileCallable.Instance,
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    private sealed class ZipIsZipFileCallable : ICallable, INamedRuntimeCallable, IPyRenderableValue, IPyHashableValue
    {
        public static readonly ZipIsZipFileCallable Instance = new();

        private ZipIsZipFileCallable()
        {
        }

        public string Name => "zipfile.is_zipfile";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var bound = CallBinder.BindNamedArgumentsWithPresence(
                arguments, span, LythonKnownCallableSignatures.ZipIsZipFile, PythonCallableKind.Builtin);
            var path = ResolveZipPath(bound.Values[0], context, span, "zipfile.is_zipfile()");
            var stat = context.HostStat(path, span);
            if (!stat.Exists || !stat.IsFile)
            {
                return false;
            }

            using var payload = ReadGovernedHostBytes(path, context, span);
            try
            {
                ZipDirectoryReader.Read(payload.Memory, forceUtf8Names: false, context, span);
                return true;
            }
            catch (InvalidDataException)
            {
                return false;
            }
        }

        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var bound = CallBinder.BindNamedArgumentsWithPresence(
                arguments, span, LythonKnownCallableSignatures.ZipIsZipFile, PythonCallableKind.Builtin);
            var path = ResolveZipPath(bound.Values[0], context, span, "zipfile.is_zipfile()");
            var stat = await context.HostStatAsync(path, span).ConfigureAwait(false);
            if (!stat.Exists || !stat.IsFile)
            {
                return false;
            }

            using var payload = await ReadGovernedHostBytesAsync(path, context, span).ConfigureAwait(false);
            try
            {
                ZipDirectoryReader.Read(payload.Memory, forceUtf8Names: false, context, span);
                return true;
            }
            catch (InvalidDataException)
            {
                return false;
            }
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<built-in function zipfile.is_zipfile>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public int GetPyHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }

    private sealed class ZipInfoCallable : ICallable, INamedRuntimeCallable, IPyRenderableValue, IPyHashableValue
    {
        public static readonly ZipInfoCallable Instance = new();

        private ZipInfoCallable()
        {
        }

        public string Name => "zipfile.ZipInfo";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var bound = CallBinder.BindNamedArgumentsWithPresence(
                arguments, span, LythonKnownCallableSignatures.ZipInfo, PythonCallableKind.Builtin);
            var filename = bound.Assigned.Length > 0 && bound.Assigned[0]
                ? PyZipInfo.RequireZipString(bound.Values[0], "ZipInfo filename", span)
                : PyString.FromString("NoName");
            var dateTime = bound.Assigned.Length > 1 && bound.Assigned[1]
                ? PyZipInfo.RequireZipDateTime(bound.Values[1], span)
                : new PyTuple(
                    new object[]
                    {
                        new BigInteger(1980), BigInteger.One, BigInteger.One,
                        BigInteger.Zero, BigInteger.Zero, BigInteger.Zero,
                    },
                    context.MemoryGovernor,
                    span);
            return new PyZipInfo(filename, dateTime);
        }

        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => ValueTask.FromResult(Invoke(arguments, span, context));

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<class 'zipfile.ZipInfo'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public int GetPyHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }

    private sealed class ZipFileCallable : ICallable, INamedRuntimeCallable, IPyRenderableValue, IPyHashableValue
    {
        public static readonly ZipFileCallable Instance = new();

        private ZipFileCallable()
        {
        }

        public string Name => "zipfile.ZipFile";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var request = ParseZipFileArguments(arguments, span, context);
            return OpenZipFile(request, context, span);
        }

        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var request = ParseZipFileArguments(arguments, span, context);
            return await OpenZipFileAsync(request, context, span).ConfigureAwait(false);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<class 'zipfile.ZipFile'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public int GetPyHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }

    private sealed record ZipOpenRequest(string Path, string Mode, BigInteger Compression, bool AllowZip64, bool ForceUtf8Names, int DefaultLevel, bool StrictTimestamps);

    private static ZipOpenRequest ParseZipFileArguments(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var bound = CallBinder.BindNamedArgumentsWithPresence(
            arguments, span, LythonKnownCallableSignatures.ZipFile, PythonCallableKind.Builtin);
        var values = bound.Values;
        bool IsAssigned(int index) => index < bound.Assigned.Length && bound.Assigned[index];

        var path = PathOps.Normalize(
            CoercePathLike(values[0], context, span, "zipfile.ZipFile()").AsString(),
            context.Host.Cwd);
        var mode = "r";
        if (IsAssigned(1))
        {
            if (values[1] is not PyString modeValue)
            {
                throw new LythonRuntimeException("ValueError", "ZipFile requires mode 'r', 'w', 'x', or 'a'", span);
            }

            mode = modeValue.AsString();
        }

        if (mode is not ("r" or "w" or "x" or "a"))
        {
            throw new LythonRuntimeException("ValueError", "ZipFile requires mode 'r', 'w', 'x', or 'a'", span);
        }

        var compression = BigInteger.Zero;
        if (IsAssigned(2))
        {
            if (!PyNumberOps.TryAsInteger(values[2], out var method) ||
                (method != 0 && method != 8 && method != 12 && method != 14))
            {
                throw new LythonRuntimeException("NotImplementedError", "That compression method is not supported", span);
            }

            compression = method;
        }

        var forceUtf8 = false;
        string? metadataName = null;
        if (IsAssigned(6))
        {
            if (values[6] is null or PyNone)
            {
            }
            else if (PyStringOps.TryAsString(values[6], out var encoding))
            {
                metadataName = encoding.AsString();
                var lowered = metadataName.ToLowerInvariant();
                if (lowered is "utf-8" or "utf8")
                {
                    forceUtf8 = true;
                }
                else if (lowered is not "cp437" && lowered.Length != 0)
                {
                    throw new LythonRuntimeException(
                        "LookupError",
                        $"metadata_encoding '{metadataName}' is not supported (use None, 'utf-8', or 'cp437').",
                        span);
                }
            }
            else
            {
                throw new LythonRuntimeException("TypeError", "metadata_encoding must be str or None.", span);
            }
        }

        if (metadataName is not null && metadataName.Length != 0 && mode != "r")
        {
            throw new LythonRuntimeException("ValueError", "metadata_encoding is only supported for reading files", span);
        }

        var allowZip64 = !IsAssigned(3) || IsTruthy(values[3]);
        var defaultLevel = -1;
        if (IsAssigned(4) && values[4] is not null and not PyNone)
        {
            if (!PyNumberOps.TryAsInteger(values[4], out var level))
            {
                throw new LythonRuntimeException("TypeError", "compresslevel must be an integer or None.", span);
            }

            if (level < -1 || level > 9)
            {
                throw new LythonRuntimeException("ValueError", "Bad compression level", span);
            }

            defaultLevel = (int)level;
        }

        var strictTimestamps = !IsAssigned(5) || IsTruthy(values[5]);
        return new ZipOpenRequest(path, mode, compression, allowZip64, forceUtf8, defaultLevel, strictTimestamps);
    }

    private static string ResolveZipPath(object value, ExecutionContext context, LythonSourceSpan span, string owner)
        => PathOps.Normalize(CoercePathLike(value, context, span, owner).AsString(), context.Host.Cwd);

    private static object OpenZipFile(ZipOpenRequest request, ExecutionContext context, LythonSourceSpan span)
    {
        if (request.Mode == "a")
        {
            return OpenZipFileAppend(request, context, span);
        }

        if (request.Mode == "x")
        {
            throw new LythonRuntimeException("NotImplementedError", "ZipFile mode 'x' is not supported without a host atomic create-if-absent operation.", span);
        }

        if (request.Mode != "r")
        {
            // Staged creation: nothing is published (or validated against the
            // host) until close(), so a missing parent surfaces there.
            return new PyZipFile(
                CreateString(request.Path, context, span),
                request.Compression,
                request.AllowZip64,
                request.DefaultLevel,
                request.StrictTimestamps,
                CreateBytes([], context, span),
                context);
        }

        var stat = context.HostStat(request.Path, span);
        if (!stat.Exists)
        {
            throw new LythonRuntimeException("FileNotFoundError", $"No such file or directory: '{request.Path}'.", span);
        }

        if (!stat.IsFile)
        {
            throw new LythonRuntimeException("IsADirectoryError", $"Is a directory: '{request.Path}'.", span);
        }

        var payload = ReadGovernedHostBytes(request.Path, context, span);
        ZipArchiveDirectory directory;
        try
        {
            directory = ZipDirectoryReader.Read(payload.Memory, request.ForceUtf8Names, context, span);
        }
        catch (InvalidDataException exception)
        {
            payload.Dispose();
            throw new LythonRuntimeException(ModuleException("zipfile", "BadZipFile"), exception.Message, span);
        }

        return FinishZipFileOpen(request, payload, directory, context, span);
    }

    private static async ValueTask<object> OpenZipFileAsync(ZipOpenRequest request, ExecutionContext context, LythonSourceSpan span)
    {
        if (request.Mode == "a")
        {
            return await OpenZipFileAppendAsync(request, context, span).ConfigureAwait(false);
        }

        if (request.Mode == "x")
        {
            throw new LythonRuntimeException("NotImplementedError", "ZipFile mode 'x' is not supported without a host atomic create-if-absent operation.", span);
        }

        if (request.Mode != "r")
        {
            return new PyZipFile(
                CreateString(request.Path, context, span),
                request.Compression,
                request.AllowZip64,
                request.DefaultLevel,
                request.StrictTimestamps,
                CreateBytes([], context, span),
                context);
        }

        var stat = await context.HostStatAsync(request.Path, span).ConfigureAwait(false);
        if (!stat.Exists)
        {
            throw new LythonRuntimeException("FileNotFoundError", $"No such file or directory: '{request.Path}'.", span);
        }

        if (!stat.IsFile)
        {
            throw new LythonRuntimeException("IsADirectoryError", $"Is a directory: '{request.Path}'.", span);
        }

        var payload = await ReadGovernedHostBytesAsync(request.Path, context, span).ConfigureAwait(false);
        ZipArchiveDirectory directory;
        try
        {
            directory = ZipDirectoryReader.Read(payload.Memory, request.ForceUtf8Names, context, span);
        }
        catch (InvalidDataException exception)
        {
            payload.Dispose();
            throw new LythonRuntimeException(ModuleException("zipfile", "BadZipFile"), exception.Message, span);
        }

        return FinishZipFileOpen(request, payload, directory, context, span);
    }

    private static PyZipFile FinishZipFileOpen(
        ZipOpenRequest request,
        GovernedHostBytes payload,
        ZipArchiveDirectory directory,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        var (comment, infos) = BuildDirectoryView(directory, context, span);
        return new PyZipFile(
            CreateString(request.Path, context, span),
            request.Compression,
            directory,
            comment,
            infos,
            payload,
            context);
    }

    private static (PyBytes Comment, List<PyZipInfo> Infos) BuildDirectoryView(
        ZipArchiveDirectory directory,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        var comment = CreateBytes(directory.Comment, context, span);
        var infos = new List<PyZipInfo>(directory.Entries.Count);
        for (var i = 0; i < directory.Entries.Count; i++)
        {
            if ((i & 63) == 0)
            {
                context.CheckExecutionBudget(span);
            }

            infos.Add(PyZipInfo.FromEntry(directory.Entries[i], i, context, span));
        }

        return (comment, infos);
    }

    private static object OpenZipFileAppend(ZipOpenRequest request, ExecutionContext context, LythonSourceSpan span)
    {
        var stat = context.HostStat(request.Path, span);
        if (!stat.Exists)
        {
            return FreshAppendArchive(request, context, span, null);
        }

        if (!stat.IsFile)
        {
            throw new LythonRuntimeException("IsADirectoryError", $"Is a directory: '{request.Path}'.", span);
        }

        var payload = ReadGovernedHostBytes(request.Path, context, span);
        if (payload.Memory.Length == 0)
        {
            // CPython treats an empty file as a new archive; anything else
            // must parse or fail explicitly instead of silently resetting.
            payload.Dispose();
            return FreshAppendArchive(request, context, span, null);
        }

        ZipArchiveDirectory directory;
        List<ZipRecordWriter.PreservedEntry> preserved;
        try
        {
            directory = ZipDirectoryReader.Read(payload.Memory, request.ForceUtf8Names, context, span);
            preserved = BuildPreservedEntries(payload.Memory, directory, context, span);
        }
        catch (InvalidDataException exception)
        {
            payload.Dispose();
            throw new LythonRuntimeException(ModuleException("zipfile", "BadZipFile"), exception.Message, span);
        }

        var (comment, infos) = BuildDirectoryView(directory, context, span);
        return new PyZipFile(
            CreateString(request.Path, context, span),
            request.Compression,
            request.AllowZip64,
            request.DefaultLevel,
            request.StrictTimestamps,
            comment,
            directory,
            infos,
            payload,
            preserved,
            context);
    }

    private static async ValueTask<object> OpenZipFileAppendAsync(ZipOpenRequest request, ExecutionContext context, LythonSourceSpan span)
    {
        var stat = await context.HostStatAsync(request.Path, span).ConfigureAwait(false);
        if (!stat.Exists)
        {
            return FreshAppendArchive(request, context, span, null);
        }

        if (!stat.IsFile)
        {
            throw new LythonRuntimeException("IsADirectoryError", $"Is a directory: '{request.Path}'.", span);
        }

        var payload = await ReadGovernedHostBytesAsync(request.Path, context, span).ConfigureAwait(false);
        if (payload.Memory.Length == 0)
        {
            payload.Dispose();
            return FreshAppendArchive(request, context, span, null);
        }

        ZipArchiveDirectory directory;
        List<ZipRecordWriter.PreservedEntry> preserved;
        try
        {
            directory = ZipDirectoryReader.Read(payload.Memory, request.ForceUtf8Names, context, span);
            preserved = BuildPreservedEntries(payload.Memory, directory, context, span);
        }
        catch (InvalidDataException exception)
        {
            payload.Dispose();
            throw new LythonRuntimeException(ModuleException("zipfile", "BadZipFile"), exception.Message, span);
        }

        var (comment, infos) = BuildDirectoryView(directory, context, span);
        return new PyZipFile(
            CreateString(request.Path, context, span),
            request.Compression,
            request.AllowZip64,
            request.DefaultLevel,
            request.StrictTimestamps,
            comment,
            directory,
            infos,
            payload,
            preserved,
            context);
    }

    private static PyZipFile FreshAppendArchive(ZipOpenRequest request, ExecutionContext context, LythonSourceSpan span, GovernedHostBytes? payload)
    {
        var directory = new ZipArchiveDirectory([], [], false);
        return new PyZipFile(
            CreateString(request.Path, context, span),
            request.Compression,
            request.AllowZip64,
            request.DefaultLevel,
            request.StrictTimestamps,
            CreateBytes([], context, span),
            directory,
            new List<PyZipInfo>(),
            payload,
            new List<ZipRecordWriter.PreservedEntry>(),
            context);
    }

    private static List<ZipRecordWriter.PreservedEntry> BuildPreservedEntries(
        ReadOnlyMemory<byte> payload,
        ZipArchiveDirectory directory,
        ExecutionContext context,
        LythonSourceSpan? span)
    {
        var preserved = new List<ZipRecordWriter.PreservedEntry>(directory.Entries.Count);
        for (var i = 0; i < directory.Entries.Count; i++)
        {
            if ((i & 63) == 0)
            {
                context.CheckExecutionBudget(span);
            }

            var entry = directory.Entries[i];
            // Directory parsing already validated the local header, method,
            // names, and sizes; the overlap chain below keeps every slice
            // disjoint and inside the archive records.
            var dataEnd = checked(entry.DataOffset + entry.CompressedSize);
            if (dataEnd > entry.DataEndLimit || dataEnd > (ulong)payload.Length)
            {
                throw new InvalidDataException($"Invalid ZIP archive: append slice for '{entry.Name}' is out of bounds.");
            }

            preserved.Add(new ZipRecordWriter.PreservedEntry(
                entry.Name, entry.NameBytes, entry.GeneralPurposeFlags,
                entry.DosTime, entry.DosDate, entry.CompressionMethod,
                entry.Comment, entry.Extra, entry.CreateSystem, entry.ExternalAttributes,
                entry.Crc32, entry.UncompressedSize,
                payload.Slice(checked((int)entry.DataOffset), checked((int)entry.CompressedSize))));
        }

        return preserved;
    }
}
