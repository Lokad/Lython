using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed class StructModule : PyModule
    {
        internal static readonly StructModule Instance = new();
        private static readonly string[] Members =
            ["pack", "unpack", "calcsize", "iter_unpack", "error", "Struct", "pack_into", "unpack_from"];
        private static readonly ExceptionTypeValue ErrorType = new(ModuleException("struct", "error"));

        private StructModule() : base("struct") { }
        public override IReadOnlyList<string> ExportedNames => Members;
        public override IReadOnlyList<string> MemberNames => Members;
        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "pack" => BuiltinCallable.Create(LythonKnownCallableSignatures.StructPack,
                    (args, span, context) => PackAsync(args, span, context, false).GetAwaiter().GetResult(),
                    (args, span, context) => PackAsync(args, span, context, true)),
                "unpack" => BuiltinCallable.Create(LythonKnownCallableSignatures.StructUnpack, Unpack),
                "calcsize" => BuiltinCallable.Create(LythonKnownCallableSignatures.StructCalcsize,
                    (args, span, context) => new BigInteger(ParseLayout(args[0], context, span).Size)),
                "iter_unpack" => BuiltinCallable.Create(LythonKnownCallableSignatures.StructIterUnpack, IterUnpack),
                "error" => ErrorType,
                "Struct" or "pack_into" or "unpack_from" => BuiltinCallable.CreateUnsupported("struct." + name),
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static LythonRuntimeException Error(string message, LythonSourceSpan span)
            => new(ModuleException("struct", "error"), message, span);

        // Retain the immutable format owner when an iterator outlives its call.
        // Repeats stay counters: no metadata or arrays scale with a repeat count.
        internal readonly record struct Layout(object Owner, ReadOnlyMemory<byte> Format,
            int Start, bool LittleEndian, long Size, long Items);

        private static Layout ParseLayout(object format, ExecutionContext context, LythonSourceSpan span)
        {
            var bytes = format switch
            {
                PyString text => text.Utf8Bytes,
                PyBytes data => data.Memory,
                _ => throw RuntimeErrors.Type("Struct() argument 1 must be a str or bytes object", span),
            };
            for (var i = 0; i < bytes.Length; i++)
            {
                if ((i & 255) == 0) context.CheckExecutionBudget(span);
                if (bytes.Span[i] >= 128)
                    throw new LythonRuntimeException("UnicodeEncodeError", "struct format must be ASCII", span);
                if (bytes.Span[i] == 0) throw Error("embedded null character", span);
            }
            var prefix = bytes.Length == 0 ? (byte)'<' : bytes.Span[0];
            var start = bytes.Length != 0 && prefix is (byte)'<' or (byte)'>' or (byte)'=' or (byte)'!' or (byte)'@' ? 1 : 0;
            if (prefix == (byte)'@') throw NativeLayout(span);
            long size = 0, items = 0;
            var fields = new FieldCursor(bytes, start);
            while (fields.Next(out var code, out var count, span))
            {
                context.CheckExecutionBudget(span);
                if (start == 0 && code is (byte)'n' or (byte)'N' or (byte)'P') throw NativeLayout(span);
                var width = Width(code, span);
                try
                {
                    size = checked(size + checked(count * width));
                    items = checked(items + (code is (byte)'s' or (byte)'p' ? 1 : code == (byte)'x' ? 0 : count));
                }
                catch (OverflowException) { throw Error("total struct size too long", span); }
            }
            if (bytes.Length != 0 && (start == 0 || prefix == (byte)'@'))
                throw NativeLayout(span);
            return new(format, bytes, start, prefix is (byte)'<' or (byte)'=', size, items);
        }

        private static LythonRuntimeException NativeLayout(LythonSourceSpan span)
            => new("NotImplementedError",
                "struct native ABI layouts require an explicit supported standard prefix (<, >, = or !)", span);

        private static int Width(byte code, LythonSourceSpan span) => code switch
        {
            (byte)'x' or (byte)'c' or (byte)'s' or (byte)'p' or (byte)'?' or (byte)'b' or (byte)'B' => 1,
            (byte)'h' or (byte)'H' or (byte)'e' => 2,
            (byte)'i' or (byte)'I' or (byte)'l' or (byte)'L' or (byte)'f' => 4,
            (byte)'q' or (byte)'Q' or (byte)'d' => 8,
            (byte)'n' or (byte)'N' or (byte)'P' => throw Error("bad char in struct format", span),
            _ => throw Error("bad char in struct format", span),
        };

        private struct FieldCursor(ReadOnlyMemory<byte> format, int position)
        {
            private int _position = position;
            public bool Next(out byte code, out long count, LythonSourceSpan span)
            {
                var bytes = format.Span;
                while (_position < bytes.Length && bytes[_position] is 9 or 10 or 11 or 12 or 13 or 32) _position++;
                code = 0; count = 1;
                if (_position == bytes.Length) return false;
                if (bytes[_position] is >= (byte)'0' and <= (byte)'9')
                {
                    count = 0;
                    while (_position < bytes.Length && bytes[_position] is >= (byte)'0' and <= (byte)'9')
                    {
                        var digit = bytes[_position++] - '0';
                        if (count > (long.MaxValue - digit) / 10) throw Error("total struct size too long", span);
                        count = count * 10 + digit;
                    }
                    if (_position == bytes.Length) throw Error("repeat count given without format specifier", span);
                }
                code = bytes[_position++];
                return true;
            }
        }

        private static async ValueTask<object> PackAsync(object[] args, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous)
        {
            var layout = ParseLayout(args[0], context, span);
            if (layout.Items != args.Length - 1)
                throw Error($"pack expected {layout.Items} items for packing (got {args.Length - 1})", span);
            if (layout.Size > int.MaxValue) throw RuntimeErrors.Memory("struct packed output is too large", span);
            using var funding = context.MemoryGovernor.ReserveTemporary(32L + layout.Size, span);
            var output = new byte[(int)layout.Size];
            var fields = new FieldCursor(layout.Format, layout.Start);
            var offset = 0; var argument = 1;
            while (fields.Next(out var code, out var count, span))
            {
                context.CheckExecutionBudget(span);
                if (code == (byte)'x') { offset += (int)count; continue; }
                if (code is (byte)'s' or (byte)'p')
                {
                    if (args[argument++] is not PyBytes data)
                        throw Error(code == (byte)'s' ? "argument for 's' must be a bytes object" : "argument for 'p' must be a bytes object", span);
                    var length = (int)count;
                    if (code == (byte)'s')
                        data.Bytes[..Math.Min(data.Length, length)].CopyTo(output.AsSpan(offset));
                    else if (length != 0)
                    {
                        var copied = Math.Min(data.Length, length - 1);
                        output[offset] = (byte)Math.Min(copied, 255);
                        data.Bytes[..copied].CopyTo(output.AsSpan(offset + 1));
                    }
                    offset += length;
                    continue;
                }
                for (long i = 0; i < count; i++)
                {
                    context.CheckExecutionBudget(span);
                    var value = args[argument++];
                    var width = Width(code, span);
                    ulong bits;
                    if (code == (byte)'c')
                    {
                        if (value is not PyBytes { Length: 1 } character)
                            throw Error("char format requires a bytes object of length 1", span);
                        bits = character.Bytes[0];
                    }
                    else if (code == (byte)'?')
                        bits = (asynchronous ? await IsTruthyAsync(value, context, span).ConfigureAwait(false) : IsTruthy(value, context, span)) ? 1UL : 0;
                    else if (code is (byte)'e' or (byte)'f' or (byte)'d')
                    {
                        var number = await FloatValue(value, context, span, asynchronous).ConfigureAwait(false);
                        bits = FloatBits(number, code, span);
                    }
                    else
                    {
                        var integer = await IntegerValue(value, context, span, asynchronous).ConfigureAwait(false);
                        var signed = code is (byte)'b' or (byte)'h' or (byte)'i' or (byte)'l' or (byte)'q';
                        var bitCount = width * 8;
                        var minimum = signed ? -(BigInteger.One << (bitCount - 1)) : BigInteger.Zero;
                        var maximum = (BigInteger.One << (signed ? bitCount - 1 : bitCount)) - 1;
                        if (integer < minimum || integer > maximum) throw Error("argument out of range", span);
                        bits = integer.Sign < 0 ? (ulong)(integer + (BigInteger.One << bitCount)) : (ulong)integer;
                    }
                    WriteBits(output, offset, width, bits, layout.LittleEndian);
                    offset += width;
                }
            }
            // No callbacks occur between refund and immediate ownership transfer.
            funding.Dispose();
            return CreateBytes(output, context, span);
        }

        private static async ValueTask<SpecialMethodInvocation> Hook(object value, string name,
            ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            if (value is not PyInstance instance ||
                !instance.Type.TryLookupInMro(name, 0, out var raw, out _))
                return SpecialMethodInvocation.Missing;
            var member = asynchronous
                ? await PyAttributeLookup.BindForInstanceAsync(instance, raw, context, span).ConfigureAwait(false)
                : PyAttributeLookup.BindForInstance(instance, raw, context, span);
            var result = asynchronous
                ? await InvokeCallableTargetAsync(member, span, span, context,
                    () => ValueTask.FromResult(Array.Empty<CallArgumentValue>())).ConfigureAwait(false)
                : InvokeCallableTarget(member, span, span, context, Array.Empty<CallArgumentValue>());
            return SpecialMethodInvocation.Invoked(result);
        }

        private static async ValueTask<BigInteger> IntegerValue(object value, ExecutionContext context,
            LythonSourceSpan span, bool asynchronous)
        {
            if (PyNumberOps.TryAsInteger(value, out var integer)) return integer;
            var indexed = await Hook(value, "__index__", context, span, asynchronous).ConfigureAwait(false);
            if (indexed.Kind != SpecialMethodInvocationKind.Invoked) throw Error("required argument is not an integer", span);
            if (!PyNumberOps.TryAsInteger(indexed.Value, out integer))
                throw RuntimeErrors.Type("__index__ returned non-int", span);
            return integer;
        }

        private static async ValueTask<double> FloatValue(object value, ExecutionContext context,
            LythonSourceSpan span, bool asynchronous)
        {
            // CPython replaces numeric-conversion failures with struct.error;
            // integer and truth callbacks retain their original exceptions.
            try
            {
                if (PyNumberOps.TryAsNumber(value, out var number)) return PyNumberOps.ToDoubleChecked(number);
                if (value is PyDecimal dec) return (double)dec.Value;
                var floating = await Hook(value, "__float__", context, span, asynchronous).ConfigureAwait(false);
                if (floating.Kind == SpecialMethodInvocationKind.Invoked)
                    return floating.Value is double converted ? converted : throw Error("required argument is not a float", span);
                var indexed = await IntegerValue(value, context, span, asynchronous).ConfigureAwait(false);
                return PyNumberOps.BigIntegerToDouble(indexed);
            }
            catch (OverflowException) { throw Error("required argument is not a float", span); }
            catch (LythonRuntimeException ex) when (ex.ExceptionType != "MemoryError")
            {
                context.CheckExecutionBudget(span);
                throw Error("required argument is not a float", span);
            }
        }

        private static ulong FloatBits(double value, byte code, LythonSourceSpan span)
        {
            if (code == (byte)'d') return unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
            if (code == (byte)'f')
            {
                var single = (float)value;
                if (double.IsFinite(value) && float.IsInfinity(single))
                    throw new LythonRuntimeException("OverflowError", "float too large to pack with f format", span);
                return unchecked((uint)BitConverter.SingleToInt32Bits(single));
            }
            var sign = BitConverter.DoubleToInt64Bits(value) < 0 ? 0x8000UL : 0;
            var magnitude = Math.Abs(value);
            if (double.IsNaN(value)) return sign | 0x7e00;
            if (double.IsInfinity(value)) return sign | 0x7c00;
            if (magnitude < Math.ScaleB(1.0, -14))
                return sign | (ulong)Math.Round(Math.ScaleB(magnitude, 24), MidpointRounding.ToEven);
            var exponent = Math.ILogB(magnitude);
            var significand = (ulong)Math.Round(Math.ScaleB(magnitude, 10 - exponent), MidpointRounding.ToEven);
            if (significand == 2048) { significand = 1024; exponent++; }
            if (exponent > 15) throw new LythonRuntimeException("OverflowError", "float too large to pack with e format", span);
            return sign | (uint)((exponent + 15) << 10) | (significand - 1024);
        }

        private static void WriteBits(byte[] output, int offset, int width, ulong bits, bool little)
        {
            for (var i = 0; i < width; i++)
                output[offset + (little ? i : width - 1 - i)] = (byte)(bits >> (i * 8));
        }

        private static PyBytes Buffer(object value, LythonSourceSpan span)
            => value is PyBytes bytes ? bytes : throw RuntimeErrors.Type("a bytes-like object is required", span);

        private static object Unpack(object[] args, LythonSourceSpan span, ExecutionContext context)
        {
            var layout = ParseLayout(args[0], context, span);
            var bytes = Buffer(args[1], span);
            if (bytes.Length != layout.Size) throw Error($"unpack requires a buffer of {layout.Size} bytes", span);
            return UnpackRecord(layout, bytes.Memory, context, span);
        }

        private static PyTuple UnpackRecord(Layout layout, ReadOnlyMemory<byte> data,
            ExecutionContext context, LythonSourceSpan span)
        {
            if (layout.Items == 0) return PyTuple.Empty;
            if (layout.Items > int.MaxValue) throw RuntimeErrors.Memory("struct unpacked tuple is too large", span);
            context.ObserveCollectionCount((int)layout.Items, span);
            using var funding = context.MemoryGovernor.ReserveTemporary(PyTuple.EstimateApproximateBytes((int)layout.Items), span);
            var items = new object[(int)layout.Items];
            var fields = new FieldCursor(layout.Format, layout.Start);
            var offset = 0; var index = 0;
            while (fields.Next(out var code, out var count, span))
            {
                context.CheckExecutionBudget(span);
                if (code == (byte)'x') { offset += (int)count; continue; }
                if (code is (byte)'s' or (byte)'p')
                {
                    if (code == (byte)'p' && count == 0)
                        throw new LythonRuntimeException("NotImplementedError", "zero-width Pascal unpacking is unsupported", span);
                    var length = code == (byte)'s' ? (int)count : Math.Min(data.Span[offset], (int)count - 1);
                    items[index++] = CopyField(data.Slice(offset + (code == (byte)'p' ? 1 : 0), length), context, span);
                    offset += (int)count;
                    continue;
                }
                for (long i = 0; i < count; i++)
                {
                    context.CheckExecutionBudget(span);
                    var width = Width(code, span);
                    ulong bits = 0;
                    for (var b = 0; b < width; b++)
                        bits |= (ulong)data.Span[offset + (layout.LittleEndian ? b : width - 1 - b)] << (8 * b);
                    object item;
                    if (code == (byte)'c') item = CopyField(data.Slice(offset, 1), context, span);
                    else if (code == (byte)'?') item = bits != 0;
                    else if (code == (byte)'d') item = BitConverter.Int64BitsToDouble(unchecked((long)bits));
                    else if (code == (byte)'f') item = (double)BitConverter.Int32BitsToSingle(unchecked((int)bits));
                    else if (code == (byte)'e') item = HalfValue((ushort)bits);
                    else
                    {
                        var integer = new BigInteger(bits);
                        if (code is (byte)'b' or (byte)'h' or (byte)'i' or (byte)'l' or (byte)'q' &&
                            (bits & (1UL << (width * 8 - 1))) != 0)
                            integer -= BigInteger.One << (width * 8);
                        item = integer;
                    }
                    items[index++] = item;
                    offset += width;
                }
            }
            funding.Dispose();
            var result = PyTuple.FromOwnedArray(items, context.MemoryGovernor, span);
            context.State.CallTemporaries.TrackFreshMutable(result, result.CommittedStorageBytes, span);
            return result;
        }

        private static PyBytes CopyField(ReadOnlyMemory<byte> source, ExecutionContext context, LythonSourceSpan span)
        {
            context.MemoryGovernor.EnsureCanReserve(PyBytes.EstimateApproximateBytes(source.Length), span);
            return CreateBytes(source.ToArray(), context, span);
        }

        private static double HalfValue(ushort bits)
        {
            var negative = (bits & 0x8000) != 0;
            var exponent = (bits >> 10) & 31;
            var fraction = bits & 1023;
            var value = exponent == 31 ? fraction == 0 ? double.PositiveInfinity : PythonNaN
                : exponent == 0 ? Math.ScaleB(fraction, -24)
                : Math.ScaleB(1024 + fraction, exponent - 25);
            return negative ? -value : value;
        }

        private static object IterUnpack(object[] args, LythonSourceSpan span, ExecutionContext context)
        {
            var layout = ParseLayout(args[0], context, span);
            var bytes = Buffer(args[1], span);
            if (layout.Size == 0) throw Error("cannot iteratively unpack with a struct of length 0", span);
            if (bytes.Length % layout.Size != 0)
                throw Error($"iterative unpacking requires a buffer of a multiple of {layout.Size} bytes", span);
            var iterator = new RecordIterator(layout, bytes, context, span);
            context.State.CallTemporaries.TrackFreshMutable(iterator, PyIteratorBase.IteratorValueBytes, span);
            return iterator;
        }

        internal sealed class RecordIterator : PyIteratorBase
        {
            private readonly Layout _layout;
            private readonly PyBytes _bytes;
            private readonly ExecutionContext _context;
            private readonly LythonSourceSpan _span;
            private int _offset;
            internal RecordIterator(Layout layout, PyBytes bytes, ExecutionContext context, LythonSourceSpan span)
            {
                _layout = layout; _bytes = bytes; _context = context; _span = span;
                ChargeIteratorValue(context.MemoryGovernor, span);
            }
            public int Remaining => (int)((_bytes.Length - _offset) / _layout.Size);
            private static readonly LythonCallableSignature LengthHintSignature = LythonCallableSignature.Create("unpack_iterator.__length_hint__");
            public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            {
                if (name == "__length_hint__")
                {
                    value = BoundCallable.Create((_, _, _) => new BigInteger(Remaining), LengthHintSignature);
                    return true;
                }
                return base.TryGetMember(name, out value);
            }
            public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
            {
                if (_offset == _bytes.Length) { value = null; return false; }
                _context.CheckExecutionBudget(_span);
                value = UnpackRecord(_layout, _bytes.Memory.Slice(_offset, (int)_layout.Size), _context, _span);
                _offset += (int)_layout.Size;
                return true;
            }
            public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<_struct.unpack_iterator object>");
        }
    }
}
