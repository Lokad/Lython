using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    /// <summary>
    /// Python-visible mutable ZIP member metadata. Directory-backed instances
    /// share nothing with the archive after construction; reads honor the
    /// current field values (header offset, sizes, flags), matching CPython,
    /// so cross-archive and manually constructed infos behave uniformly.
    /// </summary>
    private sealed class PyZipInfo : IPyDynamicAttributes, IPyMutableDynamicAttributes, IPyRenderableValue, IPyHashableValue
    {
        // Constructed infos retain the 12-field wrapper beside governed payloads;
        // charge the constructed-value unit at the guest factory. Directory-backed
        // infos ride the directory entry base charge instead (see ZipDirectoryReader).
        internal const long ZipInfoValueBytes = 128;

        private PyString _filename;
        private PyTuple _dateTime;
        private BigInteger _compressType;
        private PyBytes _comment;
        private PyBytes _extra;
        private BigInteger _createSystem;
        private BigInteger _externalAttributes;
        private BigInteger _fileSize;
        private BigInteger _compressSize;
        private BigInteger _crc;
        private BigInteger _headerOffset;
        private BigInteger _flags;

        public PyZipInfo(PyString filename, PyTuple dateTime)
        {
            _filename = filename;
            _dateTime = dateTime;
            _compressType = BigInteger.Zero;
            _comment = new PyBytes([]);
            _extra = new PyBytes([]);
            _createSystem = BigInteger.Zero;
            _externalAttributes = BigInteger.Zero;
            _fileSize = BigInteger.Zero;
            _compressSize = BigInteger.Zero;
            _crc = BigInteger.Zero;
            _headerOffset = BigInteger.Zero;
            _flags = BigInteger.Zero;
        }

        public string FileName => _filename.AsString();

        internal readonly record struct ZipStoredFields(
            string FileName,
            int Year,
            int Month,
            int Day,
            int Hour,
            int Minute,
            int Second,
            BigInteger CompressType,
            PyBytes Comment,
            PyBytes Extra,
            BigInteger CreateSystem,
            BigInteger ExternalAttributes);

        /// <summary>Reads the current fields for archive staging.</summary>
        internal ZipStoredFields ReadStoredFields(LythonSourceSpan? span)
        {
            var items = _dateTime.ToArray();
            var parts = new int[6];
            for (var i = 0; i < 6; i++)
            {
                if (!Numbers.PyNumberOps.TryAsInteger(items[i], out var integer) ||
                    integer < int.MinValue || integer > int.MaxValue)
                {
                    throw new LythonRuntimeException("TypeError", "ZipInfo date_time must be a 6-tuple of ints.", span);
                }

                parts[i] = (int)integer;
            }

            if (parts[0] < 1980)
            {
                throw new LythonRuntimeException("ValueError", "ZIP does not support timestamps before 1980", span);
            }

            return new ZipStoredFields(
                _filename.AsString(),
                parts[0],
                parts[1],
                parts[2],
                parts[3],
                parts[4],
                parts[5],
                _compressType,
                _comment,
                _extra,
                _createSystem,
                _externalAttributes);
        }

        internal static PyZipInfo FromEntry(
            Zip.ZipDirectoryEntry entry,
            int ordinal,
            ExecutionContext context,
            LythonSourceSpan? span)
        {
            var info = new PyZipInfo(
                CreateString(entry.Name, context, span),
                new PyTuple(
                    new object[]
                    {
                        new BigInteger(entry.DateYear),
                        new BigInteger(entry.DateMonth),
                        new BigInteger(entry.DateDay),
                        new BigInteger(entry.DateHour),
                        new BigInteger(entry.DateMinute),
                        new BigInteger(entry.DateSecond),
                    },
                    context.MemoryGovernor,
                    span));
            info._compressType = new BigInteger(entry.CompressionMethod);
            info._comment = CreateBytes(entry.Comment, context, span);
            info._extra = CreateBytes(entry.Extra, context, span);
            info._createSystem = new BigInteger(entry.CreateSystem);
            info._externalAttributes = new BigInteger(entry.ExternalAttributes);
            info._fileSize = new BigInteger(entry.UncompressedSize);
            info._compressSize = new BigInteger(entry.CompressedSize);
            info._crc = new BigInteger(entry.Crc32);
            info._headerOffset = new BigInteger(entry.HeaderOffset);
            info._flags = new BigInteger(entry.GeneralPurposeFlags);
            info.DirectoryOrdinal = ordinal;
            return info;
        }

        /// <summary>
        /// Gets the owning archive directory ordinal for infos handed out by
        /// listing calls, or -1 for foreign and manually constructed infos.
        /// </summary>
        internal int DirectoryOrdinal = -1;

        public int GetPyHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);

        internal readonly record struct ZipReadFields(
            string Name,
            ushort Method,
            ushort Flags,
            uint Crc,
            ulong CompressedSize,
            ulong UncompressedSize,
            ulong HeaderOffset);

        /// <summary>Reads the current field values with range validation for member access.</summary>
        internal ZipReadFields ReadFields(LythonSourceSpan? span)
        {
            return new ZipReadFields(
                FileName,
                RequireUShort(_compressType, "ZipInfo compress_type", span),
                RequireUShort(_flags, "ZipInfo flag_bits", span),
                RequireUInt(_crc, "ZipInfo CRC", span),
                RequireULong(_compressSize, "ZipInfo compress_size", span),
                RequireULong(_fileSize, "ZipInfo file_size", span),
                RequireULong(_headerOffset, "ZipInfo header_offset", span));
        }

        private static ushort RequireUShort(BigInteger value, string owner, LythonSourceSpan? span)
        {
            if (value < 0 || value > ushort.MaxValue)
            {
                throw new LythonRuntimeException("ValueError", $"{owner} is out of range.", span);
            }

            return (ushort)value;
        }

        private static uint RequireUInt(BigInteger value, string owner, LythonSourceSpan? span)
        {
            if (value < 0 || value > uint.MaxValue)
            {
                throw new LythonRuntimeException("ValueError", $"{owner} is out of range.", span);
            }

            return (uint)value;
        }

        private static ulong RequireULong(BigInteger value, string owner, LythonSourceSpan? span)
        {
            if (value < 0 || value > ulong.MaxValue)
            {
                throw new LythonRuntimeException("ValueError", $"{owner} is out of range.", span);
            }

            return (ulong)value;
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "filename" => _filename,
                "date_time" => _dateTime,
                "compress_type" => _compressType,
                "comment" => _comment,
                "extra" => _extra,
                "create_system" => _createSystem,
                "external_attr" => _externalAttributes,
                "file_size" => _fileSize,
                "compress_size" => _compressSize,
                "CRC" => _crc,
                "header_offset" => _headerOffset,
                "flag_bits" => _flags,
                "is_dir" => BoundCallable.CreateNoArguments(this, "zipfile.ZipInfo.is_dir", static (receiver, _, _) => receiver.IsDirectory()),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            switch (name)
            {
                case "filename":
                case "date_time":
                case "compress_type":
                case "comment":
                case "extra":
                case "create_system":
                case "external_attr":
                case "file_size":
                case "compress_size":
                case "CRC":
                case "header_offset":
                case "flag_bits":
                    break;
                default:
                    return false;
            }

            SetMember(name, value, span: null);
            return true;
        }

        public void SetMember(string name, object value, LythonSourceSpan? span)
        {
            switch (name)
            {
                case "filename":
                    _filename = RequireZipString(value, "ZipInfo filename", span);
                    break;
                case "date_time":
                    _dateTime = RequireZipDateTime(value, span);
                    break;
                case "compress_type":
                    _compressType = RequireZipInteger(value, "ZipInfo compress_type", span);
                    break;
                case "comment":
                    _comment = RequireZipBytes(value, "ZipInfo comment", span);
                    break;
                case "extra":
                    _extra = RequireZipBytes(value, "ZipInfo extra", span);
                    break;
                case "create_system":
                    _createSystem = RequireZipInteger(value, "ZipInfo create_system", span);
                    break;
                case "external_attr":
                    _externalAttributes = RequireZipInteger(value, "ZipInfo external_attr", span);
                    break;
                case "file_size":
                    _fileSize = RequireZipInteger(value, "ZipInfo file_size", span);
                    break;
                case "compress_size":
                    _compressSize = RequireZipInteger(value, "ZipInfo compress_size", span);
                    break;
                case "CRC":
                    _crc = RequireZipInteger(value, "ZipInfo CRC", span);
                    break;
                case "header_offset":
                    _headerOffset = RequireZipInteger(value, "ZipInfo header_offset", span);
                    break;
                case "flag_bits":
                    _flags = RequireZipInteger(value, "ZipInfo flag_bits", span);
                    break;
                default:
                    throw new LythonRuntimeException("AttributeError", $"ZipInfo has no attribute '{name}'.", span);
            }
        }

        public bool IsDirectory() => FileName.EndsWith('/');

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString($"<zipfile.ZipInfo filename='{FileName}' file_size={_fileSize}>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        internal static PyString RequireZipString(object value, string owner, LythonSourceSpan? span)
        {
            if (PyStringOps.TryAsString(value, out var text))
            {
                return text;
            }

            throw new LythonRuntimeException("TypeError", $"{owner} must be str.", span);
        }

        internal static PyBytes RequireZipBytes(object value, string owner, LythonSourceSpan? span)
        {
            if (value is PyBytes bytes)
            {
                return bytes;
            }

            throw new LythonRuntimeException("TypeError", $"{owner} must be bytes.", span);
        }

        internal static BigInteger RequireZipInteger(object value, string owner, LythonSourceSpan? span)
        {
            if (PyNumberOps.TryAsInteger(value, out var integer))
            {
                return integer;
            }

            throw new LythonRuntimeException("TypeError", $"{owner} must be an integer.", span);
        }

        internal static PyTuple RequireZipDateTime(object value, LythonSourceSpan? span, ExecutionContext? context = null)
        {
            var items = value switch
            {
                PyTuple tuple => tuple.ToArray(),
                PyList list => list.ToArray(),
                _ => throw new LythonRuntimeException("TypeError", "ZipInfo date_time must be a 6-tuple of ints.", span),
            };

            if (items.Length != 6)
            {
                throw new LythonRuntimeException("TypeError", "ZipInfo date_time must be a 6-tuple of ints.", span);
            }

            var parts = new BigInteger[6];
            var normalized = new object[6];
            for (var i = 0; i < 6; i++)
            {
                if (!PyNumberOps.TryAsInteger(items[i], out parts[i]))
                {
                    throw new LythonRuntimeException("TypeError", "ZipInfo date_time must be a 6-tuple of ints.", span);
                }

                // R18: preserve supplied booleans at the Python boundary instead
                // of rewriting them to integers; CPython exposes them as bools.
                normalized[i] = items[i] is bool ? items[i] : (object)parts[i];
            }

            if (parts[0] < 1980)
            {
                throw new LythonRuntimeException("ValueError", "ZIP does not support timestamps before 1980", span);
            }

            return context is null
                ? new PyTuple(normalized)
                : PyTuple.FromOwnedArray(normalized, context.MemoryGovernor, span);
        }
    }
}
