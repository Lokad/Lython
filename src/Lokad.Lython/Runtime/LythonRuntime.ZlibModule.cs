using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed class ZlibModule : PyModule
    {
        internal static readonly ZlibModule Instance = new();
        internal static readonly ExceptionTypeValue ErrorType = new(ModuleException("zlib", "error"));
        private static readonly string[] Members =
            ["compress", "decompress", "error", "DEFLATED", "MAX_WBITS", "DEF_BUF_SIZE",
             "Z_NO_COMPRESSION", "Z_BEST_SPEED", "Z_BEST_COMPRESSION", "Z_DEFAULT_COMPRESSION",
             "compressobj", "decompressobj", "adler32", "crc32"];

        private ZlibModule() : base("zlib") { }
        public override IReadOnlyList<string> ExportedNames => Members;
        public override IReadOnlyList<string> MemberNames => Members;
        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "compress" => ZlibCallable.Compress,
                "decompress" => ZlibCallable.Decompress,
                "error" => ErrorType,
                "DEFLATED" => new BigInteger(8),
                "MAX_WBITS" => new BigInteger(15),
                "DEF_BUF_SIZE" => new BigInteger(16384),
                "Z_NO_COMPRESSION" => BigInteger.Zero,
                "Z_BEST_SPEED" => BigInteger.One,
                "Z_BEST_COMPRESSION" => new BigInteger(9),
                "Z_DEFAULT_COMPRESSION" => BigInteger.MinusOne,
                "compressobj" or "decompressobj" or "adler32" or "crc32" =>
                    BuiltinCallable.CreateUnsupported("zlib." + name),
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static LythonRuntimeException Error(string message, LythonSourceSpan span, Exception? inner = null)
            => new(ModuleException("zlib", "error"), message, span, inner, null);

        internal static bool IsCallable(object value) => value is ZlibCallable;

        private sealed class ZlibCallable : BoundArgumentsCallable, INamedRuntimeCallable,
            IPyDynamicAttributes, IPyRenderableValue, IPyHashableValue
        {
            internal static readonly ZlibCallable Compress = new(true);
            internal static readonly ZlibCallable Decompress = new(false);
            private readonly bool _compress;
            private ZlibCallable(bool compress)
                : base(compress ? LythonKnownCallableSignatures.ZlibCompress : LythonKnownCallableSignatures.ZlibDecompress,
                       PythonCallableKind.Builtin) => _compress = compress;
            public string Name => Signature.Name;
            public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            {
                value = name switch
                {
                    "__name__" or "__qualname__" => PyString.FromString(_compress ? "compress" : "decompress"),
                    "__module__" => ExceptionTypeValue.SharedModuleLabel("zlib"),
                    "__self__" => Instance,
                    _ => MissingMemberValue.Instance,
                };
                return !ReferenceEquals(value, MissingMemberValue.Instance);
            }
            public PyString RenderPython(PyRenderingContext context)
                => PyString.FromString("<built-in function " + (_compress ? "compress" : "decompress") + ">");
            public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
            public int GetPyHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
            protected override bool PreservePresence => true;
            protected override object InvokeBound(object[] arguments, LythonSourceSpan span, ExecutionContext context)
                => throw new InvalidOperationException("zlib calls require argument presence.");
            protected override object InvokeBoundWithPresence(BoundCallArguments bound, LythonSourceSpan span, ExecutionContext context)
                => RunAsync(bound, span, context, false).GetAwaiter().GetResult();
            protected override ValueTask<object> InvokeBoundWithPresenceAsync(BoundCallArguments bound,
                LythonSourceSpan span, ExecutionContext context) => RunAsync(bound, span, context, true);

            private async ValueTask<object> RunAsync(BoundCallArguments bound, LythonSourceSpan span,
                ExecutionContext context, bool asynchronous)
            {
                if (bound.Values[0] is not PyBytes bytes)
                    throw RuntimeErrors.Type(Name + " requires a bytes object", span);
                // Convert all supplied parameters in Python's argument order
                // before validating their supported ranges. Presence preserves
                // the difference between an omitted value and explicit None.
                var first = bound.Assigned[1]
                    ? await IndexValue(bound.Values[1], int.MinValue, int.MaxValue, context, span, asynchronous).ConfigureAwait(false)
                    : new BigInteger(_compress ? -1 : 15);
                var second = bound.Assigned[2]
                    ? await IndexValue(bound.Values[2], _compress ? int.MinValue : long.MinValue,
                        _compress ? int.MaxValue : long.MaxValue, context, span, asynchronous).ConfigureAwait(false)
                    : new BigInteger(_compress ? 15 : 16384);
                context.CheckExecution(span);
                if (_compress)
                {
                    if (first < -1 || first > 9) throw Error("Bad compression level", span);
                    if (second != 15) throw UnsupportedWindow(span);
                    return CompressBytes(bytes.Memory, (int)first, context, span);
                }
                if (second < 0) throw RuntimeErrors.Value("bufsize must be non-negative", span);
                if (first != 0 && first != 15) throw UnsupportedWindow(span);
                // bufsize is an allocation hint, not an output limit. A fixed,
                // governed read buffer avoids allocating the guest's full hint.
                return DecompressBytes(bytes.Memory, context, span);
            }
        }

        private static LythonRuntimeException UnsupportedWindow(LythonSourceSpan span)
            => new("NotImplementedError", "zlib supports wbits=15 for compression and wbits=0 or 15 for decompression.", span);

        private static async ValueTask<BigInteger> IndexValue(object value, long minimum, long maximum,
            ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            if (!PyNumberOps.TryAsInteger(value, out var integer))
            {
                if (value is not PyInstance instance || !instance.Type.TryLookupInMro("__index__", 0, out var raw, out _))
                    throw RuntimeErrors.Type("an integer is required", span);
                var method = asynchronous
                    ? await PyAttributeLookup.BindForInstanceAsync(instance, raw, context, span).ConfigureAwait(false)
                    : PyAttributeLookup.BindForInstance(instance, raw, context, span);
                value = asynchronous
                    ? await InvokeCallableTargetAsync(method, span, span, context,
                        () => ValueTask.FromResult(Array.Empty<CallArgumentValue>())).ConfigureAwait(false)
                    : InvokeCallableTarget(method, span, span, context, Array.Empty<CallArgumentValue>());
                if (!PyNumberOps.TryAsInteger(value, out integer))
                    throw RuntimeErrors.Type("__index__ returned non-int", span);
            }
            if (integer < minimum || integer > maximum)
                throw new LythonRuntimeException("OverflowError", "Python int too large to convert to the requested integer width", span);
            return integer;
        }

        // zlib documents about 268 KiB for default deflation and 40 KiB for
        // inflation. These conservative coupons also cover CLR stream wrappers,
        // managed native-I/O buffers and the bounded explicit buffer below.
        internal const long CompressionScratchBytes = 512 * 1024;
        internal const long InflationScratchBytes = 128 * 1024;
        private const int ChunkBytes = 8192;

        internal static PyBytes CompressBytes(ReadOnlyMemory<byte> input, int level,
            ExecutionContext context, LythonSourceSpan span)
        {
            context.CheckExecution(span);
            if (input.IsEmpty) return CompressEmpty(level, context, span);
            using var scratch = context.MemoryGovernor.ReserveTemporary(CompressionScratchBytes, span);
            var maximumLength = (long)input.Length + (input.Length >> 12) + (input.Length >> 14) + (input.Length >> 25) + 13;
            if (maximumLength > int.MaxValue) throw RuntimeErrors.Memory("zlib compressed output is too large", span);
            var output = new GovernedByteBuilder(context.MemoryGovernor, span, 0, (int)maximumLength, "zlib compressed output");
            try
            {
                using var target = new GzipBufferWriteStream(output);
                using (var compressor = new ZLibStream(target, new ZLibCompressionOptions { CompressionLevel = level }, true))
                {
                    for (var offset = 0; offset < input.Length; offset += Math.Min(ChunkBytes, input.Length - offset))
                    {
                        context.CheckExecution(span);
                        compressor.Write(input.Span.Slice(offset, Math.Min(ChunkBytes, input.Length - offset)));
                    }
                }
                context.CheckExecution(span);
                return Publish(output, context, span);
            }
            catch (InvalidDataException ex) { throw Error(ex.Message, span, ex); }
            catch (IOException ex) { throw Error(ex.Message, span, ex); }
            finally { output.Release(); }
        }

        private static PyBytes CompressEmpty(int level, ExecutionContext context, LythonSourceSpan span)
        {
            using var scratch = context.MemoryGovernor.ReserveTemporary(128, span);
            var output = new GovernedByteBuilder(context.MemoryGovernor, span);
            try
            {
                var flags = (level == -1 ? 2 : level <= 1 ? 0 : level <= 5 ? 1 : level == 6 ? 2 : 3) << 6;
                flags += (31 - (((0x78 << 8) | flags) % 31)) % 31;
                output.Append((byte)0x78);
                output.Append((byte)flags);
                using var target = new GzipBufferWriteStream(output);
                WriteEmptyDeflate(target, level);
                ReadOnlySpan<byte> trailer = [0, 0, 0, 1];
                output.Append(trailer);
                return Publish(output, context, span);
            }
            finally { output.Release(); }
        }

        internal static PyBytes DecompressBytes(ReadOnlyMemory<byte> input, ExecutionContext context, LythonSourceSpan span)
        {
            context.CheckExecution(span);
            if (input.Length < 2) throw Error("incomplete or truncated stream", span);
            var cmf = input.Span[0];
            var flg = input.Span[1];
            if ((cmf & 15) != 8 || cmf >> 4 > 7 || ((cmf << 8) | flg) % 31 != 0)
                throw Error("incorrect zlib header", span);
            if ((flg & 32) != 0)
                throw new LythonRuntimeException("NotImplementedError", "zlib preset dictionaries are unsupported.", span);
            using var scratch = context.MemoryGovernor.ReserveTemporary(InflationScratchBytes, span);
            var output = new GovernedByteBuilder(context.MemoryGovernor, span);
            try
            {
                var buffer = new byte[ChunkBytes];
                uint a = 1, b = 0;
                using var cursor = new GzipByteCursorStream(input, 2, context, span);
                using (var inflater = new DeflateStream(cursor, CompressionMode.Decompress, true))
                {
                    while (true)
                    {
                        context.CheckExecution(span);
                        var count = inflater.Read(buffer);
                        if (count == 0) break;
                        if (count > int.MaxValue - output.Length) throw RuntimeErrors.Memory("zlib decompressed output is too large", span);
                        output.Append(buffer.AsSpan(0, count));
                        for (var i = 0; i < count; i++)
                        {
                            a = (a + buffer[i]) % 65521;
                            b = (b + a) % 65521;
                        }
                    }
                }
                if (input.Length - cursor.BytePosition < 4) throw Error("incomplete or truncated stream", span);
                var checksum = BinaryPrimitives.ReadUInt32BigEndian(input.Span.Slice(cursor.BytePosition, 4));
                if (((b << 16) | a) != checksum) throw Error("incorrect data check", span);
                context.CheckExecution(span);
                // Python's convenience function returns the first stream and
                // ignores unused trailing input, including a second zlib stream.
                return Publish(output, context, span);
            }
            catch (InvalidDataException ex) { throw Error(ex.Message, span, ex); }
            catch (IOException ex) { throw Error(ex.Message, span, ex); }
            finally { output.Release(); }
        }

        private static PyBytes Publish(GovernedByteBuilder output, ExecutionContext context, LythonSourceSpan span)
        {
            // Fund the exact copy alongside builder capacity, then transfer its
            // coupon immediately without guest callbacks between the steps.
            using var copy = context.MemoryGovernor.ReserveTemporary(PyBytes.EstimateApproximateBytes(output.Length), span);
            var bytes = output.WrittenSpan.ToArray();
            output.Release();
            copy.Dispose();
            return CreateBytes(bytes, context, span);
        }
    }
}
