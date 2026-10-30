using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Runtime.Zip;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    /// <summary>
    /// Contained <c>zipfile</c> surface (read/write stages): exception identities,
    /// compression constants, <c>is_zipfile</c>, <c>ZipInfo</c>, and <c>ZipFile</c>
    /// across read, staged-creation and append modes with sequential member handles.
    /// <c>BadZipfile</c> and <c>error</c> are the same object as <c>BadZipFile</c>,
    /// matching CPython's legacy aliases.
    /// Mode <c>'a'</c> preserves existing entries while staging additions;
    /// mode <c>'x'</c> stays explicitly rejected.
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
            => InvokeCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();

        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => InvokeCoreAsync(arguments, span, context, true);

        private static async ValueTask<object> InvokeCoreAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            var bound = CallBinder.BindNamedArgumentsWithPresence(arguments, span, LythonKnownCallableSignatures.ZipIsZipFile, PythonCallableKind.Builtin);
            using var scratch = context.MemoryGovernor.ReserveTemporary(256L, span);
            var path = asynchronous ? await GetPathAsync(bound.Values[0], "zipfile.is_zipfile()", span, context, scratch).ConfigureAwait(false)
                : GetPath(bound.Values[0], "zipfile.is_zipfile()", span, context, scratch);
            try
            {
                var kind = asynchronous ? await HostPathKindAsync(path, context, span, osPath: true).ConfigureAwait(false) : HostPathKind(path, context, span, osPath: true);
                if (kind != LythonPathKind.File) return false;
                using var payload = await ReadGovernedHostBytesCoreAsync(path, context, span, asynchronous).ConfigureAwait(false);
                var directory = ZipDirectoryReader.Read(payload.Memory, forceUtf8Names: false, context, span);
                context.MemoryGovernor.Release(directory.MetadataCharge);
                return true;
            }
            catch (InvalidDataException) { return false; }
            catch (HostOperationException error) when (IsFileSystemFailure(error)) { return false; }
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<built-in function zipfile.is_zipfile>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public int GetPyHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }

    private sealed class ZipInfoCallable : ICallable, INamedRuntimeCallable, IPyRenderableValue, IPyDynamicAttributes, IPyContextualDynamicAttributes, IPyHashableValue
    {
        public static readonly ZipInfoCallable Instance = new();

        private ZipInfoCallable()
        {
        }

        public string Name => "zipfile.ZipInfo";

        private readonly PyString _nameValue = PyString.FromString("ZipInfo");

        // Zip type constructors report shared names and per-read hierarchies
        // like the other singleton types (object bases cannot be shared).
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__" or "__qualname__")
            {
                value = _nameValue;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            if (name == "__module__")
            {
                value = ExceptionTypeValue.SharedModuleLabel("zipfile");
                return true;
            }

            if (name == "__bases__" || name == "__mro__")
            {
                return TryGetObjectBases(this, context, span, name, out value);
                }

            return TryGetMember(name, out value);
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            var bound = CallBinder.BindNamedArgumentsWithPresence(
                arguments, span, LythonKnownCallableSignatures.ZipInfo, PythonCallableKind.Builtin);
            var filename = bound.Assigned.Length > 0 && bound.Assigned[0]
                ? PyZipInfo.RequireZipString(bound.Values[0], "ZipInfo filename", span)
                : PyString.FromString("NoName", context.MemoryGovernor, span);
            var dateTime = bound.Assigned.Length > 1 && bound.Assigned[1]
                ? PyZipInfo.RequireZipDateTime(bound.Values[1], span, context.MemoryGovernor)
                : new PyTuple(
                    new object[]
                    {
                        new BigInteger(1980), BigInteger.One, BigInteger.One,
                        BigInteger.Zero, BigInteger.Zero, BigInteger.Zero,
                    },
                    context.MemoryGovernor,
                    span);
            context.MemoryGovernor.Reserve(PyZipInfo.ZipInfoValueBytes, span);
            context.MemoryGovernor.Commit(PyZipInfo.ZipInfoValueBytes);
            return new PyZipInfo(filename, dateTime, context.MemoryGovernor, span);
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

    private sealed class ZipFileCallable : ICallable, INamedRuntimeCallable, IPyRenderableValue, IPyDynamicAttributes, IPyContextualDynamicAttributes, IPyHashableValue
    {
        public static readonly ZipFileCallable Instance = new();

        private ZipFileCallable()
        {
        }

        public string Name => "zipfile.ZipFile";

        private readonly PyString _nameValue = PyString.FromString("ZipFile");

        // Zip type constructors report shared names and per-read hierarchies
        // like the other singleton types (object bases cannot be shared).
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__" or "__qualname__")
            {
                value = _nameValue;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            if (name == "__module__")
            {
                value = ExceptionTypeValue.SharedModuleLabel("zipfile");
                return true;
            }

            if (name == "__bases__" || name == "__mro__")
            {
                return TryGetObjectBases(this, context, span, name, out value);
                }

            return TryGetMember(name, out value);
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            var request = ParseZipFileArgumentsAsync(arguments, span, context, false).GetAwaiter().GetResult();
            return OpenZipFile(request, context, span);
        }

        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            var request = await ParseZipFileArgumentsAsync(arguments, span, context, true).ConfigureAwait(false);
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

    private sealed record ZipOpenRequest(PyString Path, string Mode, BigInteger Compression, bool AllowZip64, bool ForceUtf8Names, int DefaultLevel, bool StrictTimestamps);

    private static async ValueTask<ZipOpenRequest> ParseZipFileArgumentsAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        var bound = CallBinder.BindNamedArgumentsWithPresence(
            arguments, span, LythonKnownCallableSignatures.ZipFile, PythonCallableKind.Builtin);
        var values = bound.Values;
        bool IsAssigned(int index) => index < bound.Assigned.Length && bound.Assigned[index];

        var path = asynchronous ? await CoercePathLikeAsync(values[0], context, span, "zipfile.ZipFile()").ConfigureAwait(false) : CoercePathLike(values[0], context, span, "zipfile.ZipFile()");
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

    private static object OpenZipFile(ZipOpenRequest request, ExecutionContext context, LythonSourceSpan span)
        => OpenZipFileCoreAsync(request, context, span, false).GetAwaiter().GetResult();

    private static ValueTask<object> OpenZipFileAsync(ZipOpenRequest request, ExecutionContext context, LythonSourceSpan span)
        => OpenZipFileCoreAsync(request, context, span, true);

    private static async ValueTask<object> OpenZipFileCoreAsync(ZipOpenRequest request, ExecutionContext context,
        LythonSourceSpan span, bool asynchronous)
    {
        if (request.Mode == "x")
            throw new LythonRuntimeException("NotImplementedError", "ZipFile mode 'x' is not supported by Lython.", span);
        context.CheckExecution(span);
        using var pathStorage = PathOps.ReserveWorking(request.Path, context, span);
        using var shell = context.MemoryGovernor.ReserveTemporary(512, span);
        var path = request.Path.AsString();
        OpenedByteFile? target = null;
        GovernedHostBytes? payload = null;
        PyZipFile? archive = null;
        try
        {
            if (request.Mode != "r")
            {
                target = new OpenedByteFile(context.Services);
                await target.OpenAsync(path, new LythonFileOpenOptions(request.Mode == "a" ? LythonFileAccess.ReadWrite : LythonFileAccess.Write,
                    create: true, truncate: request.Mode == "w"), span, asynchronous).ConfigureAwait(false);
            }
            var directory = new ZipArchiveDirectory([], [], false, 0);
            List<ZipRecordWriter.PreservedEntry>? preserved = null;
            var freshAppend = false;
            if (request.Mode != "w")
            {
                payload = target is not null
                    ? await ReadGovernedOpenedBytesAsync(target, context, span, asynchronous).ConfigureAwait(false)
                    : await ReadGovernedHostBytesCoreAsync(path, context, span, asynchronous).ConfigureAwait(false);
                freshAppend = request.Mode == "a" && payload.Memory.IsEmpty;
                if (freshAppend) { payload.Dispose(); payload = null; }
                else
                {
                    try
                    {
                        directory = ZipDirectoryReader.Read(payload!.Memory, request.ForceUtf8Names, context, span);
                        if (request.Mode == "a") preserved = BuildPreservedEntries(payload.Memory, directory, context, span);
                    }
                    catch (InvalidDataException error)
                    { throw new LythonRuntimeException(ModuleException("zipfile", "BadZipFile"), error.Message, span); }
                }
                if (freshAppend) preserved = [];
            }
            var (comment, infos) = BuildDirectoryView(directory, context, span);
            archive = new PyZipFile(request.Path, request.Mode, request.Compression, request.AllowZip64,
                request.DefaultLevel, request.StrictTimestamps, comment, directory, infos, payload, preserved,
                target, context, freshAppend);
            context.CheckExecution(span);
            context.State.TrackOpenFile(archive);
            shell.Commit(512);
            return archive;
        }
        catch
        {
            if (archive is not null) await archive.RetireAfterFailureAsync(span, asynchronous).ConfigureAwait(false);
            else
            {
                if (target is not null) await target.CloseAfterFailureAsync(span, asynchronous).ConfigureAwait(false);
                payload?.Dispose();
            }
            throw;
        }
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
                context.CheckExecution(span);
            }

            infos.Add(PyZipInfo.FromEntry(directory.Entries[i], i, context, span));
        }

        return (comment, infos);
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
                context.CheckExecution(span);
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
