using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        private sealed class TextFileReadState(PyString text, TextNewlineMode newline) : TextFileState(TextFileMode.Read)
        {
            private int _cursorByte;

            public BigInteger Position => new(_cursorByte);

            public PyString Read(int size)
            {
                if (_cursorByte >= text.Utf8Bytes.Length)
                {
                    return PyString.Empty;
                }

                var end = size < 0
                    ? text.Utf8Bytes.Length
                    : text.GetByteIndexAfterRunes(_cursorByte, size);
                var result = text.SliceByByteRange(_cursorByte, end);
                _cursorByte = end;
                return result;
            }

            public PyString ReadLine(int size)
            {
                var source = text.Utf8Bytes.Span;
                if (_cursorByte >= source.Length)
                {
                    return PyString.Empty;
                }

                var end = FindLineEndByte(source, _cursorByte);
                if (size >= 0)
                {
                    end = Math.Min(end, text.GetByteIndexAfterRunes(_cursorByte, size));
                }

                var line = text.SliceByByteRange(_cursorByte, end);
                _cursorByte = end;
                return line;

                int FindLineEndByte(ReadOnlySpan<byte> bytes, int startByte)
                {
                    for (var i = startByte; i < bytes.Length; i++)
                    {
                        if (bytes[i] == (byte)'\n' &&
                            newline is TextNewlineMode.TranslateUniversal or TextNewlineMode.PreserveUniversal or TextNewlineMode.PreserveLineFeed)
                        {
                            return i + 1;
                        }

                        if (bytes[i] != (byte)'\r')
                        {
                            continue;
                        }

                        if (newline is TextNewlineMode.PreserveCarriageReturn)
                        {
                            return i + 1;
                        }

                        if (newline is TextNewlineMode.PreserveCarriageReturnLineFeed)
                        {
                            if (i + 1 < bytes.Length && bytes[i + 1] == (byte)'\n')
                            {
                                return i + 2;
                            }

                            continue;
                        }

                        if (newline == TextNewlineMode.PreserveUniversal)
                        {
                            return i + 1 < bytes.Length && bytes[i + 1] == (byte)'\n'
                                ? i + 2
                                : i + 1;
                        }
                    }

                    return bytes.Length;
                }
            }
        }
    }
}
