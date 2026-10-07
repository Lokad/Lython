using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static (TextFileOperation Operation, bool Binary) ParseFileOpenMode(
        PyString mode, string owner, LythonSourceSpan span)
    {
        var text = mode.AsString();
        if (!text.Contains('b', StringComparison.Ordinal))
            return (ParseTextOpenMode(mode, owner, span), false);
        if (text.Contains('+', StringComparison.Ordinal))
            throw new LythonRuntimeException("NotImplementedError", owner + " does not support updating file modes", span);
        return text switch
        {
            "rb" or "br" => (TextFileOperation.Read, true),
            "wb" or "bw" => (TextFileOperation.Write, true),
            "ab" or "ba" => (TextFileOperation.Append, true),
            _ => throw new LythonRuntimeException("ValueError", "invalid binary mode: '" + text + "'", span),
        };
    }

    private static void ValidateBinaryOpenOptions(object[] values, int bufferingIndex,
        string owner, LythonSourceSpan span)
    {
        if (values[bufferingIndex] is not PyNone &&
            PyNumberOps.TryAsInteger(values[bufferingIndex], out var buffering) && buffering == 0)
            throw new LythonRuntimeException("NotImplementedError", owner + " does not support unbuffered binary handles", span);
        var options = new[] { "encoding", "errors", "newline" };
        for (var index = 0; index < options.Length; index++)
            if (values[bufferingIndex + 1 + index] is not PyNone)
                throw new LythonRuntimeException("ValueError", "binary mode doesn't take a " + options[index] + " argument", span);
    }
}
