namespace Lokad.Lython.Runtime.Zip;

/// <summary>
/// Strict single-pass raw-DEFLATE (RFC 1951) inflater for ZIP member payloads.
/// Unlike a generic streaming decompressor, which reports a truncated stream as
/// clean end-of-data, this decoder tracks block-final (BFINAL) markers and rejects
/// truncated or trailing-garbage payloads even when the expanded bytes and CRC would
/// otherwise match. Output is bounded by the declared uncompressed size before any byte
/// is emitted, so a dishonest size cannot overproduce; match distances are validated
/// against the bytes already emitted. Work budgets are checked per block and per output
/// quantum so large members stay interruptible.
/// </summary>
internal static class StrictDeflateInflater
{
    private const int MaxHuffmanBits = 15;
    private const int EndOfBlock = 256;
    private const int BudgetQuantumBytes = 4096;

    private static readonly int[] LengthBases =
    {
        3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 17, 19, 23, 27, 31,
        35, 43, 51, 59, 67, 83, 99, 115, 131, 163, 195, 227, 258,
    };

    private static readonly int[] LengthExtras =
    {
        0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2,
        3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 0,
    };

    private static readonly int[] DistanceBases =
    {
        1, 2, 3, 4, 5, 7, 9, 13, 17, 25, 33, 49, 65, 97, 129, 193,
        257, 385, 513, 769, 1025, 1537, 2049, 3073, 4097, 6145, 8193, 12289, 16385, 24577,
    };

    private static readonly int[] DistanceExtras =
    {
        0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6,
        7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13,
    };

    private static readonly int[] CodeLengthOrder =
    {
        16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1,
    };

    private static readonly HuffmanTree FixedLiteralTree = BuildFixedLiteralTree();
    private static readonly HuffmanTree FixedDistanceTree = BuildFixedDistanceTree();

    internal static byte[] Inflate(
        ReadOnlySpan<byte> compressed,
        string name,
        ulong uncompressedSize,
        uint expectedCrc,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan? span)
    {
        // Array.MaxLength is the largest single-dimensional byte array the runtime
        // supports; anything larger cannot materialize and must fail as a memory error
        // rather than escaping as OutOfMemoryException.
        if (uncompressedSize > (ulong)Array.MaxLength)
        {
            throw RuntimeErrors.Memory($"zip member '{name}' is too large to materialize", span);
        }

        if (compressed.IsEmpty)
        {
            // CPython accepts a zero-length DEFLATED payload as empty output;
            // enforce only the declared size and checksum, mirroring that boundary.
            if (uncompressedSize != 0)
            {
                throw BadZipFile($"Corrupt member '{name}': length mismatch.", span);
            }

            if (expectedCrc != 0)
            {
                throw BadZipFile($"Bad CRC-32 for file '{name}'.", span);
            }

            return [];
        }

        var size = (int)uncompressedSize;
        var governor = context.MemoryGovernor;
        // Reserve the declared size before allocating: a dishonest size fails here,
        // and the decoder below can never emit more than was reserved.
        var reservation = governor.ReserveTemporary(size, span);
        var output = size == 0 ? [] : new byte[size];
        try
        {
            var decoder = new Decoder(compressed, output, size, name, context, span);
            decoder.Run();
            if (~decoder.Crc != expectedCrc)
            {
                throw new LythonRuntimeException(
                    LythonRuntime.ModuleException("zipfile", "BadZipFile"),
                    $"Bad CRC-32 for file '{name}'.",
                    span);
            }
        }
        catch (TruncatedStreamException)
        {
            reservation.Dispose();
            throw BadZipFile($"Corrupt member '{name}': incomplete or truncated deflated data.", span);
        }
        catch (InvalidStreamException)
        {
            reservation.Dispose();
            throw BadZipFile($"Corrupt member '{name}': invalid deflated data.", span);
        }
        catch
        {
            reservation.Dispose();
            throw;
        }

        reservation.Dispose();
        governor.EnsureCanReserve(size, span);
        return output;
    }

    private static HuffmanTree BuildFixedLiteralTree()
    {
        var lengths = new int[288];
        for (var i = 0; i < 144; i++) lengths[i] = 8;
        for (var i = 144; i < 256; i++) lengths[i] = 9;
        for (var i = 256; i < 280; i++) lengths[i] = 7;
        for (var i = 280; i < 288; i++) lengths[i] = 8;
        return HuffmanTree.Build(lengths, 288);
    }

    private static HuffmanTree BuildFixedDistanceTree()
    {
        var lengths = new int[32];
        Array.Fill(lengths, 5);
        return HuffmanTree.Build(lengths, 32);
    }

    private static LythonRuntimeException BadZipFile(string message, LythonSourceSpan? span)
        => new(LythonRuntime.ModuleException("zipfile", "BadZipFile"), message, span);

    private ref struct Decoder
    {
        private BitReader _bits;
        private readonly byte[] _output;
        private readonly int _size;
        private readonly string _name;
        private readonly LythonRuntime.ExecutionContext _context;
        private readonly LythonSourceSpan? _span;
        private int _total;
        private uint _crc;
        private int _crcAnchor;

        public Decoder(
            ReadOnlySpan<byte> compressed,
            byte[] output,
            int size,
            string name,
            LythonRuntime.ExecutionContext context,
            LythonSourceSpan? span)
        {
            _bits = new BitReader(compressed);
            _output = output;
            _size = size;
            _name = name;
            _context = context;
            _span = span;
            _total = 0;
            _crc = uint.MaxValue;
            _crcAnchor = 0;
        }

        public uint Crc => _crc;

        public void Run()
        {
            while (true)
            {
                _context.CheckExecutionBudget(_span);
                var final = _bits.ReadBit() == 1;
                var type = _bits.ReadBits(2);
                switch (type)
                {
                    case 0: DecodeStored(); break;
                    case 1: DecodeHuffmanBlock(FixedLiteralTree, FixedDistanceTree); break;
                    case 2: DecodeDynamic(); break;
                    default: throw new InvalidStreamException();
                }

                if (final) break;
            }

            // A complete stream consumes every input byte with fewer than 8 bits
            // of padding left in the final byte; anything more is trailing garbage.
            if (_bits.Position != _bits.Length || _bits.Buffered >= 8)
            {
                throw new InvalidStreamException();
            }

            if (_total != _size)
            {
                throw BadZipFile($"Corrupt member '{_name}': length mismatch.", _span);
            }

            FlushCrc();
        }

        private void DecodeStored()
        {
            _bits.AlignToByte();
            var length = _bits.ReadBits(16);
            var complement = _bits.ReadBits(16);
            if ((length ^ complement) != 0xFFFF)
            {
                throw new InvalidStreamException();
            }

            if (length > _bits.RemainingBytes)
            {
                throw new TruncatedStreamException();
            }

            if (length > _size - _total)
            {
                throw BadZipFile($"Corrupt member '{_name}': length mismatch.", _span);
            }

            _bits.ReadBytes(_output.AsSpan(_total, length));
            _total += length;
            _crc = Crc32.Update(_crc, _output.AsSpan(_crcAnchor, _total - _crcAnchor));
            _crcAnchor = _total;
            _context.CheckExecutionBudget(_span);
        }

        private void DecodeHuffmanBlock(HuffmanTree literals, HuffmanTree distances)
        {
            while (true)
            {
                var symbol = literals.Decode(ref _bits);
                if (symbol < 256)
                {
                    if (_total >= _size)
                    {
                        throw BadZipFile($"Corrupt member '{_name}': length mismatch.", _span);
                    }

                    _output[_total++] = (byte)symbol;
                }
                else if (symbol == EndOfBlock)
                {
                    FlushCrc();
                    return;
                }
                else if (symbol <= 285)
                {
                    var index = symbol - 257;
                    var length = LengthBases[index] + _bits.ReadBits(LengthExtras[index]);
                    var distanceSymbol = distances.Decode(ref _bits);
                    if (distanceSymbol > 29)
                    {
                        throw new InvalidStreamException();
                    }

                    var distance = DistanceBases[distanceSymbol] + _bits.ReadBits(DistanceExtras[distanceSymbol]);
                    if (distance > _total)
                    {
                        throw new InvalidStreamException();
                    }

                    if (length > _size - _total)
                    {
                        throw BadZipFile($"Corrupt member '{_name}': length mismatch.", _span);
                    }

                    CopyMatch(distance, length);
                }
                else
                {
                    throw new InvalidStreamException();
                }

                if (_total - _crcAnchor >= BudgetQuantumBytes)
                {
                    _crc = Crc32.Update(_crc, _output.AsSpan(_crcAnchor, _total - _crcAnchor));
                    _crcAnchor = _total;
                    _context.CheckExecutionBudget(_span);
                }
            }
        }

        private void CopyMatch(int distance, int length)
        {
            // Strides never exceed the distance, so each destination span starts at or
            // past the end of its source span and no unwritten source byte is clobbered.
            var remaining = length;
            while (remaining > 0)
            {
                var stride = Math.Min(distance, remaining);
                _output.AsSpan(_total - distance, stride).CopyTo(_output.AsSpan(_total, stride));
                _total += stride;
                remaining -= stride;
            }
        }

        private void FlushCrc()
        {
            if (_total > _crcAnchor)
            {
                _crc = Crc32.Update(_crc, _output.AsSpan(_crcAnchor, _total - _crcAnchor));
                _crcAnchor = _total;
            }
        }

        private void DecodeDynamic()
        {
            var literalCount = _bits.ReadBits(5) + 257;
            var distanceCount = _bits.ReadBits(5) + 1;
            var lengthCount = _bits.ReadBits(4) + 4;
            if (distanceCount > 30)
            {
                throw new InvalidStreamException();
            }

            var codeLengths = new int[19];
            for (var i = 0; i < lengthCount; i++)
            {
                codeLengths[CodeLengthOrder[i]] = _bits.ReadBits(3);
            }

            var codeLengthTree = HuffmanTree.Build(codeLengths, 19);
            var total = literalCount + distanceCount;
            var lengths = new int[total];
            var filled = 0;
            while (filled < total)
            {
                var symbol = codeLengthTree.Decode(ref _bits);
                if (symbol < 16)
                {
                    lengths[filled++] = symbol;
                }
                else if (symbol == 16)
                {
                    if (filled == 0)
                    {
                        throw new InvalidStreamException();
                    }

                    var repeat = _bits.ReadBits(2) + 3;
                    if (filled + repeat > total)
                    {
                        throw new InvalidStreamException();
                    }

                    var previous = lengths[filled - 1];
                    for (var i = 0; i < repeat; i++)
                    {
                        lengths[filled++] = previous;
                    }
                }
                else if (symbol == 17)
                {
                    var repeat = _bits.ReadBits(3) + 3;
                    if (filled + repeat > total)
                    {
                        throw new InvalidStreamException();
                    }

                    filled += repeat;
                }
                else
                {
                    var repeat = _bits.ReadBits(7) + 11;
                    if (filled + repeat > total)
                    {
                        throw new InvalidStreamException();
                    }

                    filled += repeat;
                }
            }

            var literalLengths = new int[literalCount];
            Array.Copy(lengths, 0, literalLengths, 0, literalCount);
            var distanceLengths = new int[distanceCount];
            Array.Copy(lengths, literalCount, distanceLengths, 0, distanceCount);

            // The end-of-block code must exist or the block could never terminate.
            if (literalLengths[EndOfBlock] == 0)
            {
                throw new InvalidStreamException();
            }

            // Length codes 286 and 287 are undefined and must not be used.
            if (literalCount > 286 && (literalLengths[286] != 0 || literalLengths[287] != 0))
            {
                throw new InvalidStreamException();
            }

            DecodeHuffmanBlock(
                HuffmanTree.Build(literalLengths, literalCount),
                HuffmanTree.Build(distanceLengths, distanceCount));
        }
    }

    private ref struct BitReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _position;
        private uint _buffer;
        private int _buffered;

        public BitReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _position = 0;
            _buffer = 0;
            _buffered = 0;
        }

        public int Position => _position;

        public int Length => _data.Length;

        public int Buffered => _buffered;

        public int RemainingBytes => (_data.Length - _position) + (_buffered / 8);

        public int ReadBit()
        {
            if (_buffered == 0)
            {
                if (_position >= _data.Length)
                {
                    throw new TruncatedStreamException();
                }

                _buffer = _data[_position];
                _position++;
                _buffered = 8;
            }

            var bit = (int)(_buffer & 1u);
            _buffer >>= 1;
            _buffered--;
            return bit;
        }

        public int ReadBits(int count)
        {
            var value = 0;
            for (var i = 0; i < count; i++)
            {
                value |= ReadBit() << i;
            }

            return value;
        }

        public void AlignToByte()
        {
            var skip = _buffered & 7;
            _buffer >>= skip;
            _buffered -= skip;
        }

        public void ReadBytes(Span<byte> destination)
        {
            var offset = 0;
            while (_buffered >= 8 && offset < destination.Length)
            {
                destination[offset] = (byte)(_buffer & 0xFFu);
                _buffer >>= 8;
                _buffered -= 8;
                offset++;
            }

            while (offset < destination.Length)
            {
                destination[offset] = _data[_position];
                _position++;
                offset++;
            }
        }
    }

    private sealed class HuffmanTree
    {
        private readonly List<int> _zero = new() { -1 };
        private readonly List<int> _one = new() { -1 };
        private readonly List<int> _symbol = new() { -1 };

        public int Decode(ref BitReader reader)
        {
            var node = 0;
            while (true)
            {
                var bit = reader.ReadBit();
                var next = bit == 0 ? _zero[node] : _one[node];
                if (next < 0)
                {
                    throw new InvalidStreamException();
                }

                node = next;
                var symbol = _symbol[node];
                if (symbol >= 0)
                {
                    return symbol;
                }
            }
        }

        public void Add(int reversedCode, int length, int symbol)
        {
            var node = 0;
            for (var i = 0; i < length; i++)
            {
                if (_symbol[node] >= 0)
                {
                    throw new InvalidStreamException();
                }

                var bit = (reversedCode >> i) & 1;
                var next = bit == 0 ? _zero[node] : _one[node];
                if (next < 0)
                {
                    next = _symbol.Count;
                    _zero.Add(-1);
                    _one.Add(-1);
                    _symbol.Add(-1);
                    if (bit == 0)
                    {
                        _zero[node] = next;
                    }
                    else
                    {
                        _one[node] = next;
                    }
                }

                node = next;
            }

            if (_symbol[node] >= 0 || _zero[node] >= 0 || _one[node] >= 0)
            {
                throw new InvalidStreamException();
            }

            _symbol[node] = symbol;
        }

        public static HuffmanTree Build(int[] lengths, int symbolCount)
        {
            var blCount = new int[MaxHuffmanBits + 1];
            for (var symbol = 0; symbol < symbolCount; symbol++)
            {
                var length = lengths[symbol];
                if (length < 0 || length > MaxHuffmanBits)
                {
                    throw new InvalidStreamException();
                }

                if (length != 0)
                {
                    blCount[length]++;
                }
            }

            var left = 1;
            for (var bits = 1; bits <= MaxHuffmanBits; bits++)
            {
                left <<= 1;
                left -= blCount[bits];
                if (left < 0)
                {
                    throw new InvalidStreamException();
                }
            }

            var tree = new HuffmanTree();
            var nextCode = new int[MaxHuffmanBits + 1];
            var code = 0;
            for (var bits = 1; bits <= MaxHuffmanBits; bits++)
            {
                code = (code + blCount[bits - 1]) << 1;
                nextCode[bits] = code;
            }

            for (var symbol = 0; symbol < symbolCount; symbol++)
            {
                var length = lengths[symbol];
                if (length != 0)
                {
                    tree.Add(ReverseBits(nextCode[length], length), length, symbol);
                    nextCode[length]++;
                }
            }

            return tree;
        }

        private static int ReverseBits(int code, int length)
        {
            var reversed = 0;
            for (var i = 0; i < length; i++)
            {
                reversed = (reversed << 1) | (code & 1);
                code >>= 1;
            }

            return reversed;
        }
    }

    private sealed class TruncatedStreamException : Exception
    {
    }

    private sealed class InvalidStreamException : Exception
    {
    }
}
