using System.Buffers;
using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // The parser borrows an immutable, funded guest string. Decode only the
    // requested window; no whole-input UTF16 string or private buffer survives.
    // The enclosing parser reservation covers this small adapter's metadata.
    internal sealed class Utf8XmlTextReader(PyString source, ExecutionState services, LythonSourceSpan span) : TextReader
    {
        private PyString? _source = source;
        private readonly ExecutionState _services = services;
        private readonly LythonSourceSpan _span = span;
        private int _position;
        private char? _pending;

        public override int Read(char[] buffer, int index, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            return Read(buffer.AsSpan(index, count));
        }

        public override int Read(Span<char> buffer)
        {
            var bytes = Source().Utf8Span[_position..];
            _services.CheckExecution(_span);
            if (buffer.IsEmpty) return 0;
            if (_pending is { } pending)
            {
                buffer[0] = pending;
                _pending = null;
                return 1;
            }
            if (bytes.IsEmpty) return 0;

            // A UTF8 byte contributes at most one UTF16 unit. Restrict bytes
            // by output capacity, then stop before an incomplete scalar.
            var count = Math.Min(1024, Math.Min(buffer.Length, bytes.Length));
            while (count > 0 && count < bytes.Length && (bytes[count] & 0xc0) == 0x80) count--;
            if (count != 0)
            {
                var written = Encoding.UTF8.GetChars(bytes[..count], buffer);
                _position += count;
                return written;
            }

            // A small destination can still consume a supplementary scalar.
            // Return its high surrogate and retain only the low code unit.
            var rune = FirstRune(bytes, out var consumed);
            Span<char> scalar = stackalloc char[2];
            var length = rune.EncodeToUtf16(scalar);
            var copied = Math.Min(length, buffer.Length);
            scalar[..copied].CopyTo(buffer);
            if (copied < length) _pending = scalar[copied];
            _position += consumed;
            return copied;
        }

        public override int Read()
        {
            Span<char> value = stackalloc char[1];
            return Read(value) == 0 ? -1 : value[0];
        }

        public override int Peek()
        {
            var bytes = Source().Utf8Span[_position..];
            _services.CheckExecution(_span);
            if (_pending is { } pending) return pending;
            if (bytes.IsEmpty) return -1;
            Span<char> value = stackalloc char[2];
            FirstRune(bytes, out _).EncodeToUtf16(value);
            return value[0];
        }

        private PyString Source() => _source ?? throw new ObjectDisposedException(nameof(Utf8XmlTextReader));

        private static Rune FirstRune(ReadOnlySpan<byte> bytes, out int consumed)
        {
            if (Rune.DecodeFromUtf8(bytes, out var rune, out consumed) != OperationStatus.Done)
                throw new InvalidOperationException("XML text input must contain complete UTF8 scalars.");
            return rune;
        }

        protected override void Dispose(bool disposing)
        {
            _source = null;
            _pending = null;
            base.Dispose(disposing);
        }
    }
}
