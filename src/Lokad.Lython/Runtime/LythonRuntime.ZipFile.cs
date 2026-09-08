using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Runtime.Zip;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    /// <summary>
    /// Read-only contained ZIP archive handle. The compressed payload is
    /// retained under a governor charge until <c>close()</c>; directory
    /// metadata stays available afterwards (CPython answers listings from its
    /// cache too), while data operations fail explicitly once closed.
    /// </summary>
    private sealed class PyZipFile : IPyDynamicAttributes, IPyMutableDynamicAttributes, IPyAsyncContextManager, IPyRenderableValue, IPyHashableValue
    {
        private const long StagedEntryBaseBytes = 128;

        private readonly PyString _fileName;
        private readonly string _mode;
        private readonly BigInteger _compression;
        private readonly ZipArchiveDirectory _directory;
        private readonly List<PyZipInfo> _infos;
        private readonly ExecutionContext _context;
        private readonly bool _isWriteMode;
        private readonly bool _allowZip64;
        private readonly int _defaultLevel;
        private readonly bool _strictTimestamps;
        private readonly List<ZipRecordWriter.StagedEntry> _staged = new();
        private readonly List<ZipRecordWriter.PreservedEntry>? _preserved;
        private long _stagedCharges;
        private bool _didModify;
        // R37: fresh appends (missing/empty target) publish an empty archive on
        // close even when unmodified; existing appends publish nothing until modified.
        private readonly bool _freshAppend;
        private PyZipMemberWriter? _activeWriter;
        private PyBytes _comment;
        private GovernedHostBytes? _payload;
        private Dictionary<string, int>? _nameIndex;

        public PyZipFile(
            PyString fileName,
            BigInteger compression,
            ZipArchiveDirectory directory,
            PyBytes comment,
            List<PyZipInfo> infos,
            GovernedHostBytes payload,
            ExecutionContext context)
        {
            _fileName = fileName;
            _mode = "r";
            _compression = compression;
            _directory = directory;
            _comment = comment;
            _infos = infos;
            _payload = payload;
            _context = context;
            _isWriteMode = false;
        }

        public PyZipFile(
            PyString fileName,
            BigInteger compression,
            bool allowZip64,
            int defaultLevel,
            bool strictTimestamps,
            PyBytes comment,
            ExecutionContext context)
        {
            _fileName = fileName;
            _mode = "w";
            _compression = compression;
            _directory = new ZipArchiveDirectory([], [], false, 0);
            _comment = comment;
            _infos = new List<PyZipInfo>();
            _payload = null;
            _context = context;
            _isWriteMode = true;
            _allowZip64 = allowZip64;
            _defaultLevel = defaultLevel;
            _strictTimestamps = strictTimestamps;
        }

        public PyZipFile(
            PyString fileName,
            BigInteger compression,
            bool allowZip64,
            int defaultLevel,
            bool strictTimestamps,
            PyBytes comment,
            ZipArchiveDirectory directory,
            List<PyZipInfo> infos,
            GovernedHostBytes? payload,
            List<ZipRecordWriter.PreservedEntry> preserved,
            ExecutionContext context,
            bool freshAppend = false)
        {
            _fileName = fileName;
            _mode = "a";
            _compression = compression;
            _directory = directory;
            _comment = comment;
            _infos = infos;
            _payload = payload;
            _context = context;
            _isWriteMode = true;
            _allowZip64 = allowZip64;
            _defaultLevel = defaultLevel;
            _strictTimestamps = strictTimestamps;
            _preserved = preserved;
            _freshAppend = freshAppend;
        }

        public bool IsClosed { get; private set; }

        public int GetPyHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "filename" => _fileName,
                "mode" => PyString.FromString(_mode),
                "comment" => _comment,
                "compression" => _compression,
                "namelist" => BoundCallable.CreateNoArguments(this, "zipfile.ZipFile.namelist", static (receiver, span, context) => receiver.NameList(span, context)),
                "infolist" => BoundCallable.CreateNoArguments(this, "zipfile.ZipFile.infolist", static (receiver, span, context) => receiver.InfoList(span, context)),
                "getinfo" => BoundCallable.Create((arguments, span, context) => GetInfo(arguments, span), "zipfile.ZipFile.getinfo", ["name"], 1),
                "read" => BoundCallable.Create((arguments, span, context) => Read(arguments, span, context), "zipfile.ZipFile.read", ["name", "pwd"], 1),
                "open" => BoundCallable.Create((arguments, span, context) => Open(arguments, span, context), "zipfile.ZipFile.open", ["name", "mode", "pwd", "force_zip64"], 1),
                "writestr" => BoundCallable.Create((arguments, span, context) => WriteString(arguments, span, context), "zipfile.ZipFile.writestr", ["zinfo_or_arcname", "data", "compress_type", "compresslevel"], 2),
                "write" => BoundCallable.Create((arguments, span, context) => WriteFile(arguments, span, context), async (arguments, span, context) => await WriteFileAsync(arguments, span, context).ConfigureAwait(false), "zipfile.ZipFile.write", ["filename", "arcname", "compress_type", "compresslevel"], 1),
                "mkdir" => BoundCallable.Create((arguments, span, context) => MakeDirectory(arguments, span, context), "zipfile.ZipFile.mkdir", ["zinfo_or_arcname", "mode"], 1),
                "printdir" => BoundCallable.CreateNoArguments(this, "zipfile.ZipFile.printdir", static (receiver, span, context) => receiver.PrintDirectory(span, context),
                static (receiver, span, context) => receiver.PrintDirectoryAsync(span, context)),
                "testzip" => BoundCallable.CreateNoArguments(this, "zipfile.ZipFile.testzip", static (receiver, span, context) => receiver.TestZip(span, context)),
                "extract" => BoundCallable.Create(
                    (arguments, span, context) => Extract(arguments, span, context),
                    async (arguments, span, context) => await ExtractAsync(arguments, span, context).ConfigureAwait(false),
                    "zipfile.ZipFile.extract", ["member", "path", "pwd"], 1),
                "extractall" => BoundCallable.Create(
                    (arguments, span, context) => ExtractAll(arguments, span, context),
                    async (arguments, span, context) => await ExtractAllAsync(arguments, span, context).ConfigureAwait(false),
                    "zipfile.ZipFile.extractall", ["path", "members", "pwd"], 0),
                "close" => BoundCallable.CreateNoArguments(this, "zipfile.ZipFile.close", static (receiver, span, _) =>
                {
                    receiver.Close(span);
                    return PyNone.Instance;
                },
                static async (receiver, span, _) =>
                {
                    await receiver.CloseAsync(span).ConfigureAwait(false);
                    return PyNone.Instance;
                }),
                "__enter__" => BoundCallable.CreateNoArguments(this, "zipfile.ZipFile.__enter__", static (receiver, _, _) => receiver),
                "__exit__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 3)
                    {
                        throw new LythonRuntimeException("TypeError", "zipfile.ZipFile.__exit__(exc_type, exc, tb) expects three arguments", span);
                    }

                    Close(span);
                    return false;
                },
                async (arguments, span, _) =>
                {
                    if (arguments.Length != 3)
                    {
                        throw new LythonRuntimeException("TypeError", "zipfile.ZipFile.__exit__(exc_type, exc, tb) expects three arguments", span);
                    }

                    await CloseAsync(span).ConfigureAwait(false);
                    return false;
                }, "zipfile.ZipFile.__exit__", ["exc_type", "exc_value", "traceback"], 3),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString($"<zipfile.ZipFile filename='{_fileName.AsString()}' mode='{_mode}'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        private object NameList(LythonSourceSpan? span, ExecutionContext context)
        {
            EnsureReadMode(span, "namelist");
            var names = new PyList([], context.MemoryGovernor, span);
            foreach (var entry in _directory.Entries)
            {
                names.Add(CreateString(entry.Name, context, span));
            }

            return names;
        }

        private object InfoList(LythonSourceSpan? span, ExecutionContext context)
        {
            EnsureReadMode(span, "infolist");
            var infos = new PyList([], context.MemoryGovernor, span);
            foreach (var info in _infos)
            {
                infos.Add(info);
            }

            return infos;
        }

        private object GetInfo(object[] arguments, LythonSourceSpan span)
        {
            EnsureReadMode(span, "getinfo");
            if (arguments[0] is PyString text && FindOrdinalByName(text.AsString(), span) is { } ordinal)
            {
                return _infos[ordinal];
            }

            throw new LythonRuntimeException("KeyError", $"There is no item named '{FormatKey(arguments[0])}' in the archive.", span);
        }

        private object Read(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            EnsureReadMode(span, "read");
            EnsureOpen(span);
            var payload = _payload;
            if (payload is null)
            {
                throw new LythonRuntimeException("ValueError", "Attempt to use ZIP archive that was already closed", span);
            }

            var target = ResolveReadTarget(arguments[0], span);
            object? password = arguments.Length < 2 ? null : arguments[1];
            if (password is null)
            {
                password = PyNone.Instance;
            }

            var bytes = ZipMemberReader.ReadMemberBytes(
                payload.Memory,
                target.Name,
                target.Method,
                target.Flags,
                target.Crc,
                target.CompressedSize,
                target.UncompressedSize,
                target.DataOffset,
                target.DataEndLimit,
                password,
                context,
                span);
            return CreateBytes(bytes, context, span);
        }

        private object Open(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var modeText = "r";
            // An explicit None mode is invalid (CPython parity); only an
            // omitted mode defaults to 'r', keeping omitted and None distinct.
            if (arguments.Length >= 2 && arguments[1] is not null)
            {
                if (arguments[1] is not PyString modeValue)
                {
                    throw new LythonRuntimeException("ValueError", "open() requires mode \"r\" or \"w\"", span);
                }

                modeText = modeValue.AsString();
            }

            if (modeText != "r" && modeText != "w")
            {
                throw new LythonRuntimeException("ValueError", "open() requires mode \"r\" or \"w\"", span);
            }

            object? password = arguments.Length >= 3 ? arguments[2] : null;
            if (password is not null and not PyNone && modeText == "w")
            {
                throw new LythonRuntimeException("ValueError", "pwd is only supported for reading files", span);
            }

            EnsureOpen(span);
            if (modeText == "w")
            {
                return OpenForWrite(arguments, span, context);
            }

            EnsureReadMode(span, "open");
            var payload = _payload;
            if (payload is null)
            {
                throw new LythonRuntimeException("ValueError", "Attempt to use ZIP archive that was already closed", span);
            }

            var target = ResolveReadTarget(arguments[0], span);
            var bytes = ZipMemberReader.ReadMemberBytes(
                payload.Memory,
                target.Name,
                target.Method,
                target.Flags,
                target.Crc,
                target.CompressedSize,
                target.UncompressedSize,
                target.DataOffset,
                target.DataEndLimit,
                password,
                context,
                span);
            return new PyZipMemberReader(
                CreateBytes(bytes, context, span),
                CreateString(target.Name, context, span),
                context);
        }

        private object OpenForWrite(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            EnsureWriteMode(span);
            RequireNoActiveWriter(span);
            var forceZip64 = arguments.Length >= 4 && arguments[3] is not null and not PyNone && IsTruthy(arguments[3]);
            if (forceZip64 && !_allowZip64)
            {
                throw new LythonRuntimeException("ValueError", "force_zip64 is True, but allowZip64 was False when opening the ZIP file.", span);
            }

            string memberName;
            ushort method;
            ushort dosTime;
            ushort dosDate;
            byte[] comment;
            byte[] extra;
            int createSystem;
            uint externalAttributes;
            PyZipInfo? info = null;
            if (arguments[0] is PyZipInfo zipInfo)
            {
                var fields = zipInfo.ReadStoredFields(span);
                memberName = SanitizeMemberName(fields.FileName);
                method = RequireWriteMethod(fields.CompressType, span);
                (dosTime, dosDate) = ZipRecordWriter.EncodeDosDateTime(
                    fields.Year, fields.Month, fields.Day, fields.Hour, fields.Minute, fields.Second, span);
                comment = fields.Comment.Memory.ToArray();
                extra = fields.Extra.Memory.ToArray();
                createSystem = ToInt32(fields.CreateSystem, "ZipInfo create_system", span);
                externalAttributes = ToUInt32(fields.ExternalAttributes, "ZipInfo external_attr", span);
                info = zipInfo;
            }
            else if (arguments[0] is PyString arcname)
            {
                memberName = SanitizeMemberName(arcname.AsString());
                method = RequireWriteMethod(_compression, span);
                context.RegisterHostCall(span);
                var now = context.Host.LocalNow;
                var local = now.ToOffset(now.Offset);
                (dosTime, dosDate) = ZipRecordWriter.EncodeDosDateTime(
                    local.Year, local.Month, local.Day, local.Hour, local.Minute, local.Second, span);
                comment = [];
                extra = [];
                createSystem = 0;
                externalAttributes = (uint)(memberName.EndsWith('/') ? (16877 << 16 | 0x10) : (384 << 16));
            }
            else
            {
                throw new LythonRuntimeException("TypeError", "open() filename must be str or ZipInfo.", span);
            }

            var writer = new PyZipMemberWriter(
                this,
                CreateString(memberName, context, span),
                dosTime, dosDate, method, _defaultLevel,
                comment, extra, createSystem, externalAttributes,
                forceZip64, info,
                new GovernedByteBuilder(context.MemoryGovernor, span),
                context);
            _activeWriter = writer;
            return writer;
        }

        private void RequireNoActiveWriter(LythonSourceSpan? span)
        {
            if (_activeWriter is not null)
            {
                throw new LythonRuntimeException("ValueError", "Can't write to ZIP archive while an open writing handle exists.", span);
            }
        }

        // Like writestr with an explicit ZipInfo, actual sizes and checksums flow back
        // through OnPublished at archive publication; file_size is known at commit time.
        internal void CommitWriterEntry(PyZipMemberWriter writer, byte[] data, LythonSourceSpan? span)
        {
            try
            {
                if (!ReferenceEquals(_activeWriter, writer))
                {
                    throw new LythonRuntimeException("RuntimeError", "ZIP member writer is not the active writing handle.", span);
                }

                Action<uint, ulong>? onPublished = null;
                if (writer.Info is { } info)
                {
                    onPublished = (crc, compressedSize) =>
                    {
                        _ = info.TrySetMember("CRC", new BigInteger(crc));
                        _ = info.TrySetMember("compress_size", new BigInteger(compressedSize));
                    };
                }

                StageEntry(
                    writer.EntryName,
                    writer.DosTime, writer.DosDate,
                    writer.Method, writer.Level,
                    writer.Comment, writer.Extra,
                    writer.CreateSystem, writer.ExternalAttributes,
                    data,
                    onPublished,
                    _context, span,
                    forceZip64: writer.ForceZip64);
                if (writer.Info is { } target)
                {
                    _ = target.TrySetMember("file_size", new BigInteger(data.Length));
                }
            }
            finally
            {
                ReleaseWriter(writer);
            }
        }

        internal void ReleaseWriter(PyZipMemberWriter writer)
        {
            if (ReferenceEquals(_activeWriter, writer))
            {
                _activeWriter = null;
            }
        }

        private readonly record struct PlannedExtraction(ResolvedMember Target, string TargetPath, bool IsDirectory);

        private object Extract(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            EnsureReadMode(span, "extract");
            EnsureOpen(span);
            var payload = _payload;
            if (payload is null)
            {
                throw new LythonRuntimeException("ValueError", "Attempt to use ZIP archive that was already closed", span);
            }

            var request = ParseExtractArguments(arguments, context, span, "zipfile.ZipFile.extract()");
            var plan = PlanExtraction(request.Target, request.Destination, span);
            ExtractPlanned(payload.Memory, plan, request.Password, context, span);
            return CreateString(plan.TargetPath, context, span);
        }

        private async ValueTask<object> ExtractAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            EnsureReadMode(span, "extract");
            EnsureOpen(span);
            var payload = _payload;
            if (payload is null)
            {
                throw new LythonRuntimeException("ValueError", "Attempt to use ZIP archive that was already closed", span);
            }

            var request = ParseExtractArguments(arguments, context, span, "zipfile.ZipFile.extract()");
            var plan = PlanExtraction(request.Target, request.Destination, span);
            await ExtractPlannedAsync(payload.Memory, plan, request.Password, context, span).ConfigureAwait(false);
            return CreateString(plan.TargetPath, context, span);
        }

        private readonly record struct ZipExtractRequest(ResolvedMember Target, object Password, string Destination);

        private readonly record struct ZipExtractAllRequest(object? Members, object Password, string Destination);

        private ZipExtractAllRequest ParseExtractAllArguments(object[] arguments, ExecutionContext context, LythonSourceSpan span, string owner)
        {
            object? members = arguments.Length >= 2 ? arguments[1] : null;
            object? password = arguments.Length >= 3 ? arguments[2] : null;
            var destination = ResolveExtractionRoot(
                arguments.Length >= 1 ? arguments[0] : null, context, span, owner);
            return new ZipExtractAllRequest(members, password ?? PyNone.Instance, destination);
        }

        private ZipExtractRequest ParseExtractArguments(object[] arguments, ExecutionContext context, LythonSourceSpan span, string owner)
        {
            var target = ResolveReadTarget(arguments[0], span);
            object? password = arguments.Length >= 3 ? arguments[2] : null;
            var destination = ResolveExtractionRoot(
                arguments.Length >= 2 ? arguments[1] : null, context, span, owner);
            return new ZipExtractRequest(target, password ?? PyNone.Instance, destination);
        }

        private object ExtractAll(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            EnsureReadMode(span, "extractall");
            EnsureOpen(span);
            var payload = _payload;
            if (payload is null)
            {
                throw new LythonRuntimeException("ValueError", "Attempt to use ZIP archive that was already closed", span);
            }

            var request = ParseExtractAllArguments(arguments, context, span, "zipfile.ZipFile.extractall()");
            EnsureDirectoryExists(request.Destination, context, span);
            var plans = PlanExtractions(request.Members, request.Destination, context, span);
            for (var i = 0; i < plans.Count; i++)
            {
                if ((i & 63) == 0)
                {
                    context.CheckExecutionBudget(span);
                }

                ExtractPlanned(payload.Memory, plans[i], request.Password, context, span);
            }

            return PyNone.Instance;
        }

        private async ValueTask<object> ExtractAllAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            EnsureReadMode(span, "extractall");
            EnsureOpen(span);
            var payload = _payload;
            if (payload is null)
            {
                throw new LythonRuntimeException("ValueError", "Attempt to use ZIP archive that was already closed", span);
            }

            var request = ParseExtractAllArguments(arguments, context, span, "zipfile.ZipFile.extractall()");
            await EnsureDirectoryExistsAsync(request.Destination, context, span).ConfigureAwait(false);
            var plans = PlanExtractions(request.Members, request.Destination, context, span);
            for (var i = 0; i < plans.Count; i++)
            {
                if ((i & 63) == 0)
                {
                    context.CheckExecutionBudget(span);
                }

                await ExtractPlannedAsync(payload.Memory, plans[i], request.Password, context, span).ConfigureAwait(false);
            }

            return PyNone.Instance;
        }

        private List<PlannedExtraction> PlanExtractions(
            object? members, string destination, ExecutionContext context, LythonSourceSpan span)
        {
            var plans = new List<PlannedExtraction>();
            if (members is not null && members is not PyNone)
            {
                var count = 0;
                foreach (var item in ToSequence(members, span, context))
                {
                    plans.Add(PlanExtraction(ResolveReadTarget(item, span), destination, span));
                    if ((++count & 63) == 0)
                    {
                        context.CheckExecutionBudget(span);
                    }

                    context.ObserveCollectionCount(count, span);
                }

                return plans;
            }

            for (var i = 0; i < _infos.Count; i++)
            {
                if ((i & 63) == 0)
                {
                    context.CheckExecutionBudget(span);
                }

                plans.Add(PlanExtraction(ResolveInfoTarget(_infos[i], span), destination, span));
                context.ObserveCollectionCount(plans.Count, span);
            }

            return plans;
        }

        private static PlannedExtraction PlanExtraction(ResolvedMember target, string destination, LythonSourceSpan? span)
        {
            RejectUnsupportedLink(target, span);
            var relative = SanitizeExtractionName(target.Name, span);
            var final = destination == "/" ? "/" + relative : destination + "/" + relative;
            // Component-aware root check: sanitizing already guarantees
            // containment, while the host boundary revalidates on every call.
            var prefix = destination == "/" ? "/" : destination + "/";
            if (!final.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw new LythonRuntimeException("ValueError", $"cannot extract member '{target.Name}' outside '{destination}'.", span);
            }

            return new PlannedExtraction(target, final, target.Name.EndsWith('/'));
        }

        private static void RejectUnsupportedLink(ResolvedMember target, LythonSourceSpan? span)
        {
            // Links are never created: listing-backed symlinks and special
            // files fail explicitly instead of materializing misleading
            // regular files. Foreign infos carry no trusted attributes, so
            // only their names decide, matching CPython selection behavior.
            if (target.IsDirectoryEntry && target.CreateSystem == 3)
            {
                var fileType = (target.ExternalAttributes >> 16) & 0xF000;
                if (fileType != 0 && fileType != 0x4000 && fileType != 0x8000)
                {
                    throw new LythonRuntimeException("NotImplementedError", "zipfile extraction of symlinks and special files is not supported.", span);
                }
            }
        }

        private static string SanitizeExtractionName(string name, LythonSourceSpan? span)
        {
            if (name.IndexOf('\0') >= 0)
            {
                throw new LythonRuntimeException("ValueError", "cannot extract member names with NUL characters.", span);
            }

            if (name.IndexOf('\\') >= 0)
            {
                throw new LythonRuntimeException("ValueError", "cannot extract member names with backslashes.", span);
            }

            if (name.Length >= 2 && char.IsAsciiLetter(name[0]) && name[1] == ':')
            {
                throw new LythonRuntimeException("ValueError", "cannot extract member names with Windows drive prefixes.", span);
            }

            if (name.StartsWith("//", StringComparison.Ordinal))
            {
                throw new LythonRuntimeException("ValueError", "cannot extract member names with UNC prefixes.", span);
            }

            // POSIX-like normalization matches CPython: absolute names turn
            // relative, while empty, ".", and ".." components are dropped.
            var kept = new List<string>();
            foreach (var part in name.TrimStart('/').Split('/'))
            {
                if (part.Length == 0 || part is "." or "..")
                {
                    continue;
                }

                kept.Add(part);
            }

            if (kept.Count == 0)
            {
                throw new LythonRuntimeException("ValueError", "cannot extract a member name with no path.", span);
            }

            return string.Join("/", kept);
        }

        private string ResolveExtractionRoot(object? value, ExecutionContext context, LythonSourceSpan span, string owner)
        {
            if (value is not null && value is not PyNone)
            {
                return ResolveZipPath(value, context, span, owner);
            }

            return context.Host.Cwd;
        }

        private static void EnsureDirectoryExists(string dir, ExecutionContext context, LythonSourceSpan? span)
        {
            var current = new StringBuilder(dir.Length);
            var depth = 0;
            foreach (var part in dir.Split('/'))
            {
                if (part.Length == 0)
                {
                    continue;
                }

                current.Append('/').Append(part);
                if ((++depth & 63) == 0)
                {
                    context.CheckExecutionBudget(span);
                }

                context.RegisterHostCall(span);
                var stat = context.HostStat(current.ToString(), span);
                if (stat.Exists)
                {
                    if (!stat.IsDir)
                    {
                        throw new LythonRuntimeException("ValueError", $"cannot create directory '{current}': a file exists at that path.", span);
                    }

                    continue;
                }

                context.RegisterHostCall(span);
                context.HostMkDir(current.ToString(), span);
            }
        }

        private static async ValueTask EnsureDirectoryExistsAsync(string dir, ExecutionContext context, LythonSourceSpan? span)
        {
            var current = new StringBuilder(dir.Length);
            var depth = 0;
            foreach (var part in dir.Split('/'))
            {
                if (part.Length == 0)
                {
                    continue;
                }

                current.Append('/').Append(part);
                if ((++depth & 63) == 0)
                {
                    context.CheckExecutionBudget(span);
                }

                context.RegisterHostCall(span);
                var stat = await context.HostStatAsync(current.ToString(), span).ConfigureAwait(false);
                if (stat.Exists)
                {
                    if (!stat.IsDir)
                    {
                        throw new LythonRuntimeException("ValueError", $"cannot create directory '{current}': a file exists at that path.", span);
                    }

                    continue;
                }

                context.RegisterHostCall(span);
                await context.HostMkDirAsync(current.ToString(), span).ConfigureAwait(false);
            }
        }

        private static void ExtractPlanned(
            ReadOnlyMemory<byte> payload,
            PlannedExtraction plan,
            object? password,
            ExecutionContext context,
            LythonSourceSpan? span)
        {
            if (plan.IsDirectory)
            {
                EnsureDirectoryExists(plan.TargetPath, context, span);
                return;
            }

            EnsureDirectoryExists(PathOps.Parent(plan.TargetPath), context, span);
            context.RegisterHostCall(span);
            if (context.HostStat(plan.TargetPath, span).IsDir)
            {
                throw new LythonRuntimeException("ValueError", $"cannot extract file '{plan.Target.Name}' onto directory '{plan.TargetPath}'.", span);
            }

            var target = plan.Target;
            var bytes = ZipMemberReader.ReadMemberBytes(
                payload,
                target.Name,
                target.Method,
                target.Flags,
                target.Crc,
                target.CompressedSize,
                target.UncompressedSize,
                target.DataOffset,
                target.DataEndLimit,
                password,
                context,
                span);
            context.RegisterHostCall(span);
            context.WriteHostBytes(plan.TargetPath, bytes, span);
        }

        private static async ValueTask ExtractPlannedAsync(
            ReadOnlyMemory<byte> payload,
            PlannedExtraction plan,
            object? password,
            ExecutionContext context,
            LythonSourceSpan? span)
        {
            if (plan.IsDirectory)
            {
                await EnsureDirectoryExistsAsync(plan.TargetPath, context, span).ConfigureAwait(false);
                return;
            }

            await EnsureDirectoryExistsAsync(PathOps.Parent(plan.TargetPath), context, span).ConfigureAwait(false);
            context.RegisterHostCall(span);
            if ((await context.HostStatAsync(plan.TargetPath, span).ConfigureAwait(false)).IsDir)
            {
                throw new LythonRuntimeException("ValueError", $"cannot extract file '{plan.Target.Name}' onto directory '{plan.TargetPath}'.", span);
            }

            var target = plan.Target;
            var bytes = ZipMemberReader.ReadMemberBytes(
                payload,
                target.Name,
                target.Method,
                target.Flags,
                target.Crc,
                target.CompressedSize,
                target.UncompressedSize,
                target.DataOffset,
                target.DataEndLimit,
                password,
                context,
                span);
            context.RegisterHostCall(span);
            await context.WriteHostBytesAsync(plan.TargetPath, bytes, span).ConfigureAwait(false);
        }

        private object TestZip(LythonSourceSpan? span, ExecutionContext context)
        {
            EnsureReadMode(span, "testzip");
            EnsureOpen(span);
            var payload = _payload;
            if (payload is null)
            {
                throw new LythonRuntimeException("ValueError", "Attempt to use ZIP archive that was already closed", span);
            }

            foreach (var info in _infos)
            {
                var target = ResolveInfoTarget(info, span);
                try
                {
                    ZipMemberReader.ReadMemberBytes(
                        payload.Memory,
                        target.Name,
                        target.Method,
                        target.Flags,
                        target.Crc,
                        target.CompressedSize,
                        target.UncompressedSize,
                        target.DataOffset,
                        target.DataEndLimit,
                        PyNone.Instance,
                        context,
                        span);
                }
                catch (LythonRuntimeException exception) when (exception.ExceptionType == "BadZipFile")
                {
                    return CreateString(target.Name, context, span);
                }
            }

            return PyNone.Instance;
        }

        public bool TrySetMember(string name, object value)
        {
            if (name != "comment")
            {
                return false;
            }

            if (value is not PyBytes comment)
            {
                throw new LythonRuntimeException(
                    "TypeError",
                    $"comment: expected bytes, got {ZipMemberReader.PythonTypeName(value)}.",
                    span: null);
            }

            _comment = comment;
            if (_isWriteMode)
            {
                _didModify = true;
            }

            return true;
        }

        public void Close(LythonSourceSpan? span)
        {
            if (IsClosed)
            {
                return;
            }

            if (_activeWriter is not null)
            {
                throw new LythonRuntimeException("ValueError", "Can't close the ZIP file while there is an open writing handle on it. Close the writing handle before closing the zip.", span);
            }

            if (_isWriteMode)
            {
                PublishStaged(span);
            }

            IsClosed = true;
            _payload?.Dispose();
            _payload = null;
        }

        object IPyContextManager.Enter() => this;

        ValueTask<object> IPyAsyncContextManager.EnterAsync() => ValueTask.FromResult<object>(this);

        bool IPyContextManager.Exit(object exceptionType, object exceptionValue, object traceback)
        {
            _ = exceptionValue;
            _ = traceback;
            Close(span: null);
            return false;
        }

        async ValueTask<bool> IPyAsyncContextManager.ExitAsync(object exceptionType, object exceptionValue, object traceback)
        {
            _ = exceptionValue;
            _ = traceback;
            await CloseAsync(span: null).ConfigureAwait(false);
            return false;
        }

        public async ValueTask CloseAsync(LythonSourceSpan? span)
        {
            if (IsClosed)
            {
                return;
            }

            if (_activeWriter is not null)
            {
                throw new LythonRuntimeException("ValueError", "Can't close the ZIP file while there is an open writing handle on it. Close the writing handle before closing the zip.", span);
            }

            if (_isWriteMode)
            {
                await PublishStagedAsync(span).ConfigureAwait(false);
            }

            IsClosed = true;
            _payload?.Dispose();
            _payload = null;
        }

        private void EnsureWriteMode(LythonSourceSpan? span)
        {
            if (!_isWriteMode)
            {
                throw new LythonRuntimeException("ValueError", "write() requires mode 'w', 'x', or 'a'", span);
            }
        }

        private void EnsureReadMode(LythonSourceSpan? span, string method)
        {
            if (_isWriteMode)
            {
                throw new LythonRuntimeException("ValueError", $"{method} requires mode 'r'", span);
            }
        }

        private void EnsureOpen(LythonSourceSpan? span)
        {
            if (IsClosed)
            {
                throw new LythonRuntimeException("ValueError", "Attempt to use ZIP archive that was already closed", span);
            }
        }

        private object WriteString(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            EnsureOpen(span);
            EnsureWriteMode(span);
            RequireNoActiveWriter(span);
            var level = _defaultLevel;
            if (arguments.Length >= 4 && arguments[3] is not null and not PyNone)
            {
                level = RequireCompressLevel(arguments[3], span);
            }

            ushort? methodOverride = null;
            if (arguments.Length >= 3 && arguments[2] is not null and not PyNone)
            {
                methodOverride = RequireWriteMethod(arguments[2], span);
            }

            byte[] data;
            if (arguments[1] is PyBytes bytes)
            {
                using (context.MemoryGovernor.ReserveTemporary(PyBytes.EstimateApproximateBytes(bytes.Memory.Length), span))
                {
                    data = bytes.Memory.ToArray();
                }
            }
            else if (arguments[1] is PyString text)
            {
                var plain = text.AsString();
                using (context.MemoryGovernor.ReserveTemporary(PyBytes.EstimateApproximateBytes(Encoding.UTF8.GetByteCount(plain)), span))
                {
                    data = Encoding.UTF8.GetBytes(plain);
                }
            }
            else
            {
                throw new LythonRuntimeException("TypeError", "writestr() data must be bytes or str.", span);
            }

            if (arguments[0] is PyZipInfo info)
            {
                var fields = info.ReadStoredFields(span);
                var method = methodOverride ?? RequireWriteMethod(fields.CompressType, span);
                // CPython trusts declared sizes for the ZIP64 decision even when
                // they disagree with the data; the serializer works from actual
                // lengths, so declared sizes only gate LargeZipFile here.
                if ((double)info.ReadFields(span).UncompressedSize * 1.05 > 2147483647.0 && !_allowZip64)
                {
                    throw new LythonRuntimeException(ModuleException("zipfile", "LargeZipFile"), "Filesize would require ZIP64 extensions", span);
                }
                StageEntry(
                    SanitizeMemberName(fields.FileName),
                    fields.Year, fields.Month, fields.Day, fields.Hour, fields.Minute, fields.Second,
                    method, level,
                    fields.Comment.Memory.ToArray(), fields.Extra.Memory.ToArray(),
                    ToInt32(fields.CreateSystem, "ZipInfo create_system", span),
                    ToUInt32(fields.ExternalAttributes, "ZipInfo external_attr", span),
                    data,
                    (crc, compressedSize) =>
                    {
                        _ = info.TrySetMember("CRC", new BigInteger(crc));
                        _ = info.TrySetMember("compress_size", new BigInteger(compressedSize));
                    },
                    context, span, false);
                _ = info.TrySetMember("file_size", new BigInteger(data.Length));
                return PyNone.Instance;
            }

            if (arguments[0] is not PyString arcname)
            {
                throw new LythonRuntimeException("TypeError", "writestr() filename must be str or ZipInfo.", span);
            }

            var memberName = SanitizeMemberName(arcname.AsString());
            context.RegisterHostCall(span);
            var now = context.Host.LocalNow;
            var local = now.ToOffset(now.Offset);
            StageEntry(
                memberName,
                local.Year, local.Month, local.Day, local.Hour, local.Minute, local.Second,
                methodOverride ?? RequireWriteMethod(_compression, span), level,
                [], [],
                0,
                (uint)(memberName.EndsWith('/') ? (16877 << 16 | 0x10) : (384 << 16)),
                data, null, context, span, false);
            return PyNone.Instance;
        }

        private (string Source, string MemberName) ParseWriteFileArgs(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            EnsureOpen(span);
            EnsureWriteMode(span);
            RequireNoActiveWriter(span);
            var source = PathOps.Normalize(
                CoercePathLike(arguments[0], context, span, "zipfile.ZipFile.write()").AsString(),
                context.Host.Cwd);
            string memberName;
            if (arguments.Length >= 2 && arguments[1] is not null and not PyNone)
            {
                memberName = SanitizeMemberName(CoercePathLike(arguments[1], context, span, "zipfile.ZipFile.write()").AsString().TrimStart('/'));
            }
            else
            {
                memberName = SanitizeMemberName(source.TrimStart('/'));
            }

            return (source, memberName);
        }

        private object WriteFile(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var (source, memberName) = ParseWriteFileArgs(arguments, span, context);
            context.RegisterHostCall(span);
            var stat = context.HostStat(source, span);
            var staged = StageWriteFileDirectoryTarget(source, memberName, stat, span, context);
            if (staged is not null)
            {
                return staged;
            }

            using var payload = ReadGovernedHostBytesAfterStat(source, stat, context, span);
            return FinishWriteFile(arguments, memberName, stat, payload, span, context);
        }

        private async ValueTask<object> WriteFileAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            // R34: same parsing and staging as WriteFile, awaiting host stat/read.
            var (source, memberName) = ParseWriteFileArgs(arguments, span, context);
            context.RegisterHostCall(span);
            var stat = await context.HostStatAsync(source, span).ConfigureAwait(false);
            var staged = StageWriteFileDirectoryTarget(source, memberName, stat, span, context);
            if (staged is not null)
            {
                return staged;
            }

            using var payload = await ReadGovernedHostBytesAfterStatAsync(source, stat, context, span).ConfigureAwait(false);
            return FinishWriteFile(arguments, memberName, stat, payload, span, context);
        }

        // Shared stat guard: missing sources fail before any payload read and
        // directories stage via mkdir, exactly as the synchronous path always did.
        private object? StageWriteFileDirectoryTarget(string source, string memberName, LythonPathStat stat, LythonSourceSpan span, ExecutionContext context)
        {
            if (!stat.Exists)
            {
                throw new LythonRuntimeException("FileNotFoundError", $"No such file or directory: '{source}'.", span);
            }

            if (!stat.IsFile)
            {
                return MakeDirectoryInternal(memberName, 511, context, span);
            }

            return null;
        }

        private object FinishWriteFile(object[] arguments, string memberName, LythonPathStat stat, GovernedHostBytes payload, LythonSourceSpan span, ExecutionContext context)
        {
            byte[] data;
            using (context.MemoryGovernor.ReserveTemporary(PyBytes.EstimateApproximateBytes(payload.Memory.Length), span))
            {
                data = payload.Memory.ToArray();
            }
            context.RegisterHostCall(span);
            var clock = context.Host.LocalNow;
            var stamp = stat.ModifiedAtTimestamp ?? clock;
            var local = stamp.ToOffset(clock.Offset);
            var (year, month, day, hour, minute, second) = ClampWriteDate(
                local.Year, local.Month, local.Day, local.Hour, local.Minute, local.Second, span);
            var method = RequireWriteMethod(_compression, span);
            var level = _defaultLevel;
            if (arguments.Length >= 4 && arguments[3] is not null and not PyNone)
            {
                level = RequireCompressLevel(arguments[3], span);
            }

            if (arguments.Length >= 3 && arguments[2] is not null and not PyNone)
            {
                method = RequireWriteMethod(arguments[2], span);
            }

            StageEntry(
                memberName, year, month, day, hour, minute, second,
                method, level, [], [],
                0, (uint)(384 << 16),
                data, null, context, span, false);
            return PyNone.Instance;
        }

        private object MakeDirectory(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            EnsureOpen(span);
            EnsureWriteMode(span);
            var mode = 511;
            if (arguments.Length >= 2 && arguments[1] is not null and not PyNone)
            {
                if (!Numbers.PyNumberOps.TryAsInteger(arguments[1], out var modeValue))
                {
                    throw new LythonRuntimeException("TypeError", "mkdir() mode must be an integer.", span);
                }

                mode = (int)modeValue;
            }

            if (arguments[0] is PyZipInfo info)
            {
                if (!info.IsDirectory())
                {
                    throw new LythonRuntimeException("ValueError", "The given ZipInfo does not describe a directory", span);
                }

                var fields = info.ReadStoredFields(span);
                var (dosTime, dosDate) = ZipRecordWriter.EncodeDosDateTime(
                    fields.Year, fields.Month, fields.Day, fields.Hour, fields.Minute, fields.Second, span);
                StageEntry(
                    SanitizeMemberName(fields.FileName),
                    dosTime, dosDate,
                    RequireWriteMethod(fields.CompressType, span), _defaultLevel,
                    fields.Comment.Memory.ToArray(), fields.Extra.Memory.ToArray(),
                    ToInt32(fields.CreateSystem, "ZipInfo create_system", span),
                    ToUInt32(fields.ExternalAttributes, "ZipInfo external_attr", span),
                    [], null, context, span, false);
                return PyNone.Instance;
            }

            if (arguments[0] is not PyString text)
            {
                throw new LythonRuntimeException("TypeError", "Expected type str or ZipInfo", span);
            }

            return MakeDirectoryInternal(SanitizeMemberName(text.AsString()), mode, context, span);
        }

        private object MakeDirectoryInternal(string name, int mode, ExecutionContext context, LythonSourceSpan span)
        {
            if (!name.EndsWith('/'))
            {
                name += "/";
            }

            var (dosTime, dosDate) = ZipRecordWriter.EncodeDosDateTime(1980, 1, 1, 0, 0, 0, span);
            StageEntry(
                name, dosTime, dosDate, 0, _defaultLevel, [], [],
                0, (uint)(((16384 | mode) & 0xFFFF) << 16 | 0x10),
                [], null, context, span, false);
            return PyNone.Instance;
        }

        private object PrintDirectory(LythonSourceSpan? span, ExecutionContext context)
        {
            var text = FormatDirectoryListing(span, context);
            _ = context.State.Stdout.Write(text, span);
            return PyNone.Instance;
        }

        private async ValueTask<object> PrintDirectoryAsync(LythonSourceSpan? span, ExecutionContext context)
        {
            var text = FormatDirectoryListing(span, context);
            _ = await context.State.Stdout.WriteAsync(text, span).ConfigureAwait(false);
            return PyNone.Instance;
        }

        private PyString FormatDirectoryListing(LythonSourceSpan? span, ExecutionContext context)
        {
            var builder = new StringBuilder();
            builder.Append("File Name".PadRight(46)).Append(' ').Append("Modified").Append(' ').Append("Size".PadLeft(12)).Append('\n');
            if (_isWriteMode && _preserved is null)
            {
                AppendStagedRows(builder, span, context);
            }
            else
            {
                foreach (var entry in _directory.Entries)
                {
                    ZipDirectoryReader.DecodeDosDateTime(entry.DosTime, entry.DosDate, out var year, out var month, out var day, out var hour, out var minute, out var second);
                    AppendDirectoryRow(builder, entry.Name, FormatPrintDate(year, month, day, hour, minute, second), (long)entry.UncompressedSize);
                    context.CheckExecutionBudget(span);
                }

                if (_isWriteMode)
                {
                    AppendStagedRows(builder, span, context);
                }
            }

            return CreateString(builder.ToString(), context, span);
        }

        private void AppendStagedRows(StringBuilder builder, LythonSourceSpan? span, ExecutionContext context)
        {
            foreach (var staged in _staged)
            {
                ZipDirectoryReader.DecodeDosDateTime(staged.DosTime, staged.DosDate, out var year, out var month, out var day, out var hour, out var minute, out var second);
                AppendDirectoryRow(builder, staged.Name, FormatPrintDate(year, month, day, hour, minute, second), staged.Data.Length);
                context.CheckExecutionBudget(span);
            }
        }

        private static void AppendDirectoryRow(StringBuilder builder, string name, string date, long size)
        {
            builder.Append(name.PadRight(46)).Append(' ').Append(date).Append(' ').Append(size.ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(12)).Append('\n');
        }

        private static string FormatPrintDate(int year, int month, int day, int hour, int minute, int second)
        {
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0:D4}-{1:D2}-{2:D2} {3:D2}:{4:D2}:{5:D2}",
                year, month, day, hour, minute, second);
        }

        private void StageEntry(
            string name,
            int year, int month, int day, int hour, int minute, int second,
            ushort method, int level,
            byte[] comment, byte[] extra,
            int createSystem, uint externalAttributes,
            byte[] data,
            Action<uint, ulong>? onPublished,
            ExecutionContext context,
            LythonSourceSpan? span,
            bool forceZip64)
        {
            var (dosTime, dosDate) = ZipRecordWriter.EncodeDosDateTime(year, month, day, hour, minute, second, span);
            StageEntry(name, dosTime, dosDate, method, level, comment, extra, createSystem, externalAttributes, data, onPublished, context, span, forceZip64);
        }

        private void StageEntry(
            string name,
            ushort dosTime, ushort dosDate,
            ushort method, int level,
            byte[] comment, byte[] extra,
            int createSystem, uint externalAttributes,
            byte[] data,
            Action<uint, ulong>? onPublished,
            ExecutionContext context,
            LythonSourceSpan? span,
            bool forceZip64)
        {
            EnsureOpen(span);
            var (nameBytes, flags) = EncodeMemberName(name);
            // R38: reject over-wide record fields before reserving or mutating,
            // so a failed staging leaves the archive contents untouched.
            ZipRecordWriter.RequireRecordFieldWidths(nameBytes, comment, extra, span);
            context.CheckExecutionBudget(span);
            context.ObserveCollectionCount(_staged.Count + 1, span);
            _didModify = true;
            var charge = checked(StagedEntryBaseBytes + data.Length + nameBytes.Length + comment.Length + extra.Length);
            context.MemoryGovernor.Reserve(charge, span);
            context.MemoryGovernor.Commit(charge);
            _stagedCharges = checked(_stagedCharges + charge);
            _staged.Add(new ZipRecordWriter.StagedEntry(
                name, nameBytes, flags, dosTime, dosDate, method, level,
                comment, extra, createSystem, externalAttributes, data, onPublished, forceZip64));
        }

        private static (byte[] NameBytes, ushort Flags) EncodeMemberName(string name)
        {
            var ascii = true;
            foreach (var character in name)
            {
                if (character >= 128)
                {
                    ascii = false;
                    break;
                }
            }

            return ascii
                ? (Encoding.ASCII.GetBytes(name), (ushort)0)
                : (Encoding.UTF8.GetBytes(name), (ushort)0x0800);
        }

        private static string SanitizeMemberName(string name) => name.Replace('\\', '/');

        private static ushort RequireWriteMethod(object value, LythonSourceSpan? span)
        {
            if (Numbers.PyNumberOps.TryAsInteger(value, out var method) && (method == 0 || method == 8))
            {
                return (ushort)method;
            }

            throw new LythonRuntimeException("NotImplementedError", "That compression method is not supported", span);
        }

        private static int RequireCompressLevel(object value, LythonSourceSpan? span)
        {
            if (Numbers.PyNumberOps.TryAsInteger(value, out var level) && level >= -1 && level <= 9)
            {
                return (int)level;
            }

            throw new LythonRuntimeException("ValueError", "Bad compression level", span);
        }

        private static int ToInt32(BigInteger value, string owner, LythonSourceSpan? span)
        {
            if (value >= int.MinValue && value <= int.MaxValue)
            {
                return (int)value;
            }

            throw new LythonRuntimeException("ValueError", $"{owner} is out of range.", span);
        }

        private static uint ToUInt32(BigInteger value, string owner, LythonSourceSpan? span)
        {
            if (value >= uint.MinValue && value <= uint.MaxValue)
            {
                return (uint)value;
            }

            throw new LythonRuntimeException("ValueError", $"{owner} is out of range.", span);
        }

        private (int Year, int Month, int Day, int Hour, int Minute, int Second) ClampWriteDate(
            int year, int month, int day, int hour, int minute, int second, LythonSourceSpan? span)
        {
            if (year < 1980)
            {
                if (_strictTimestamps)
                {
                    throw new LythonRuntimeException("ValueError", "ZIP does not support timestamps before 1980", span);
                }

                return (1980, 1, 1, 0, 0, 0);
            }

            if (year > 2107)
            {
                if (_strictTimestamps)
                {
                    throw new LythonRuntimeException("ValueError", "ZipInfo date_time is out of range for DOS timestamps.", span);
                }

                return (2107, 12, 31, 23, 59, 59);
            }

            return (year, month, day, hour, minute, second);
        }

        private void PublishStaged(LythonSourceSpan? span)
        {
            if (_mode == "a" && !_didModify && !_freshAppend)
            {
                // Appending nothing publishes nothing, matching CPython;
                // creating archives always serialize, even when empty.
                _preserved?.Clear();
                return;
            }

            var archive = _preserved is null
                ? ZipRecordWriter.SerializeArchive(_staged, _comment.Bytes.ToArray(), _allowZip64, _context, span)
                : ZipRecordWriter.SerializeMerge(_preserved, _staged, _comment.Bytes.ToArray(), _allowZip64, _context, span);
            PublishSerialized(archive, span);
        }

        private async ValueTask PublishStagedAsync(LythonSourceSpan? span)
        {
            if (_mode == "a" && !_didModify && !_freshAppend)
            {
                // Nothing publishes; drop the borrowed slices with the close.
                _preserved?.Clear();
                return;
            }

            var archive = _preserved is null
                ? ZipRecordWriter.SerializeArchive(_staged, _comment.Bytes.ToArray(), _allowZip64, _context, span)
                : ZipRecordWriter.SerializeMerge(_preserved, _staged, _comment.Bytes.ToArray(), _allowZip64, _context, span);
            await PublishSerializedAsync(archive, span).ConfigureAwait(false);
        }

        private void PublishSerialized(ZipRecordWriter.SerializedArchive archive, LythonSourceSpan? span)
        {
            var firstStaged = _preserved?.Count ?? 0;
            for (var i = 0; i < _staged.Count; i++)
            {
                _staged[i].OnPublished?.Invoke(archive.Entries[firstStaged + i].Crc, archive.Entries[firstStaged + i].CompressedSize);
            }

            var payloadCharge = PyBytes.EstimateApproximateBytes(archive.Bytes.Length);
            _context.MemoryGovernor.Reserve(payloadCharge, span);
            _context.MemoryGovernor.Commit(payloadCharge);
            try
            {
                _context.RegisterHostCall(span);
                _context.WriteHostBytes(_fileName.AsString(), archive.Bytes, span);
                // Published staged data is dead: release its charges and drop the
                // references together. A failed publish keeps both so a retry
                // re-serializes from still-charged state.
                _context.MemoryGovernor.Release(_stagedCharges);
                _stagedCharges = 0;
                _staged.Clear();
                _preserved?.Clear();
            }
            finally
            {
                _context.MemoryGovernor.Release(payloadCharge);
            }
        }

        private async ValueTask PublishSerializedAsync(ZipRecordWriter.SerializedArchive archive, LythonSourceSpan? span)
        {
            var firstStaged = _preserved?.Count ?? 0;
            for (var i = 0; i < _staged.Count; i++)
            {
                _staged[i].OnPublished?.Invoke(archive.Entries[firstStaged + i].Crc, archive.Entries[firstStaged + i].CompressedSize);
            }

            var payloadCharge = PyBytes.EstimateApproximateBytes(archive.Bytes.Length);
            _context.MemoryGovernor.Reserve(payloadCharge, span);
            _context.MemoryGovernor.Commit(payloadCharge);
            try
            {
                _context.RegisterHostCall(span);
                await _context.WriteHostBytesAsync(_fileName.AsString(), archive.Bytes, span).ConfigureAwait(false);
                // Published staged data is dead: release its charges and drop the
                // references together. A failed publish keeps both so a retry
                // re-serializes from still-charged state.
                _context.MemoryGovernor.Release(_stagedCharges);
                _stagedCharges = 0;
                _staged.Clear();
                _preserved?.Clear();
            }
            finally
            {
                _context.MemoryGovernor.Release(payloadCharge);
            }
        }

        private readonly record struct ResolvedMember(
            string Name,
            ushort Method,
            ushort Flags,
            uint Crc,
            ulong CompressedSize,
            ulong UncompressedSize,
            ulong DataOffset,
            ulong DataEndLimit,
            bool IsDirectoryEntry,
            uint ExternalAttributes,
            int CreateSystem);

        private ResolvedMember ResolveReadTarget(object name, LythonSourceSpan? span)
        {
            if (name is PyZipInfo info)
            {
                return ResolveInfoTarget(info, span);
            }

            if (name is PyString text && FindOrdinalByName(text.AsString(), span) is { } ordinal)
            {
                return ResolveInfoTarget(_infos[ordinal], span);
            }

            throw new LythonRuntimeException("KeyError", $"There is no item named '{FormatKey(name)}' in the archive.", span);
        }

        // R40: last-name-to-ordinal index over the frozen directory view. Names are
        // decoded once here instead of once per comparison, and later duplicates
        // overwrite earlier ones, so lookup matches the previous backwards scan
        // exactly (last wins). Mutable ZipInfo filenames do not affect it: keys
        // snapshot directory names at build, so a renamed info looks up nothing
        // new, matching CPython whose name map is likewise fixed at open.
        // Explicit ZipInfo access bypasses the index through ordinals. The charge
        // conservatively covers the decoded key strings plus table storage.
        private int? FindOrdinalByName(string name, LythonSourceSpan? span)
        {
            _nameIndex ??= BuildNameIndex(span);
            return _nameIndex.TryGetValue(name, out var ordinal) ? ordinal : null;
        }

        private Dictionary<string, int> BuildNameIndex(LythonSourceSpan? span)
        {
            var index = new Dictionary<string, int>(_infos.Count, StringComparer.Ordinal);
            var charge = 0L;
            for (var i = 0; i < _infos.Count; i++)
            {
                var key = _infos[i].FileName;
                index[key] = i;
                charge = checked(charge + 64 + 2L * key.Length);
            }

            _context.MemoryGovernor.Reserve(charge, span);
            _context.MemoryGovernor.Commit(charge);
            return index;
        }

        private ResolvedMember ResolveInfoTarget(PyZipInfo info, LythonSourceSpan? span)
        {
            if (info.DirectoryOrdinal >= 0 &&
                info.DirectoryOrdinal < _infos.Count &&
                ReferenceEquals(_infos[info.DirectoryOrdinal], info))
            {
                var entry = _directory.Entries[info.DirectoryOrdinal];
                return new ResolvedMember(
                    entry.Name,
                    entry.CompressionMethod,
                    entry.GeneralPurposeFlags,
                    entry.Crc32,
                    entry.CompressedSize,
                    entry.UncompressedSize,
                    entry.DataOffset,
                    entry.DataEndLimit,
                    true,
                    entry.ExternalAttributes,
                    entry.CreateSystem);
            }

            // Foreign or manually constructed info: trust the object (like
            // CPython) after validating the local header it points at.
            var payload = _payload;
            if (payload is null)
            {
                throw new LythonRuntimeException("ValueError", "Attempt to use ZIP archive that was already closed", span);
            }

            var fields = info.ReadFields(span);
            var dataOffset = ZipDirectoryReader.ReadLocalDataOffset(
                payload.Memory,
                fields.HeaderOffset,
                fields.Name,
                (fields.Flags & 0x0800) != 0,
                span);
            return new ResolvedMember(
                fields.Name,
                fields.Method,
                fields.Flags,
                fields.Crc,
                fields.CompressedSize,
                fields.UncompressedSize,
                dataOffset,
                (ulong)payload.Memory.Length,
                false,
                0,
                0);
        }

        private static string FormatKey(object name)
            => name is PyString text ? text.AsString() : name?.ToString() ?? "None";
    }
}
