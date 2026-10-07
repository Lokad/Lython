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

    private static async ValueTask ValidateBinaryOpenOptionsAsync(BoundOpenArguments arguments, int bufferingIndex,
        string owner, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        var values = arguments.Values;
        if (arguments.Assigned[bufferingIndex])
        {
            var buffering = await CoerceIoIntegerAsync(values[bufferingIndex], span, context, asynchronous).ConfigureAwait(false);
            if (buffering is < int.MinValue or > int.MaxValue)
                throw new LythonRuntimeException("OverflowError", "Python int too large to convert to C int", span);
            if (buffering == 0)
                throw new LythonRuntimeException("NotImplementedError", owner + " does not support unbuffered binary handles", span);
        }
        string[] options = ["encoding", "errors", "newline"];
        // Argument types are checked before binary-mode applicability, including
        // later invalid options. None in an omitted slot remains the default.
        for (var index = 0; index < options.Length; index++)
            if (values[bufferingIndex + 1 + index] is not PyNone and not PyString)
                throw new LythonRuntimeException("TypeError", owner + " argument '" + options[index] + "' must be str or None", span);
        for (var index = 0; index < options.Length; index++)
            if (values[bufferingIndex + 1 + index] is not PyNone)
                throw new LythonRuntimeException("ValueError", "binary mode doesn't take a " + options[index] + " argument", span);
    }
}
