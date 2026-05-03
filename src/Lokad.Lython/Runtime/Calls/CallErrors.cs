namespace Lokad.Lython.Runtime;

internal static class CallErrors
{
    public static LythonRuntimeException NoKeywordArguments(string callableKind, string callableName, LythonSourceSpan span)
        => new("TypeError", $"{callableKind} '{callableName}' does not accept keyword arguments.", span);

    public static LythonRuntimeException TooManyPositional(string callableKind, string callableName, LythonSourceSpan span)
        => new("TypeError", $"{callableKind} '{callableName}' received too many positional arguments.", span);

    public static LythonRuntimeException UnexpectedKeyword(string callableKind, string callableName, string argumentName, LythonSourceSpan span)
        => new("TypeError", $"{callableKind} '{callableName}' got an unexpected keyword argument '{argumentName}'.", span);

    public static LythonRuntimeException MultipleValues(string callableKind, string callableName, string argumentName, LythonSourceSpan span)
        => new("TypeError", $"{callableKind} '{callableName}' got multiple values for argument '{argumentName}'.", span);

    public static LythonRuntimeException MissingArgument(string callableKind, string callableName, string argumentName, LythonSourceSpan span)
        => new("TypeError", $"{callableKind} '{callableName}' is missing argument '{argumentName}'.", span);
}
