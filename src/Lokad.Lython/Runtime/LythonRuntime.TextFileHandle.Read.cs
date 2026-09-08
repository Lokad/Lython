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

                var end = TextLineScanning.FindLineEndByte(source, _cursorByte, newline);
                if (size >= 0)
                {
                    end = Math.Min(end, text.GetByteIndexAfterRunes(_cursorByte, size));
                }

                var line = text.SliceByByteRange(_cursorByte, end);
                _cursorByte = end;
                return line;
            }
        }
    }
}
