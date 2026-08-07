namespace Lokad.Lython.Runtime;

internal enum PythonCallableKind
{
    Builtin,
    Method,
    Function,
    Lambda,
}

internal static class CallErrors
{
    public static LythonRuntimeException NoKeywordArguments(PythonCallableKind callableKind, string callableName, LythonSourceSpan span)
        => new("TypeError", $"{DisplayName(callableKind)} '{callableName}' does not accept keyword arguments.", span);

    public static LythonRuntimeException TooManyPositional(PythonCallableKind callableKind, string callableName, LythonSourceSpan span)
        => new("TypeError", $"{DisplayName(callableKind)} '{callableName}' received too many positional arguments.", span);

    public static LythonRuntimeException UnexpectedKeyword(PythonCallableKind callableKind, string callableName, string argumentName, LythonSourceSpan span)
        => new("TypeError", $"{DisplayName(callableKind)} '{callableName}' got an unexpected keyword argument '{argumentName}'.", span);

    public static LythonRuntimeException MultipleValues(PythonCallableKind callableKind, string callableName, string argumentName, LythonSourceSpan span)
        => new("TypeError", $"{DisplayName(callableKind)} '{callableName}' got multiple values for argument '{argumentName}'.", span);

    public static LythonRuntimeException MissingArgument(PythonCallableKind callableKind, string callableName, string argumentName, LythonSourceSpan span)
        => new("TypeError", $"{DisplayName(callableKind)} '{callableName}' is missing argument '{argumentName}'.", span);

    private static string DisplayName(PythonCallableKind kind)
        => kind switch
        {
            PythonCallableKind.Builtin => "Builtin",
            PythonCallableKind.Method => "Method",
            PythonCallableKind.Function => "Function",
            PythonCallableKind.Lambda => "lambda",
            _ => throw new InvalidOperationException($"Unknown Python callable kind '{kind}'."),
        };
}
