namespace Lokad.Lython.Runtime;

/// <summary>Identifies a Python exception class independently of its short display name.</summary>
internal readonly record struct PythonExceptionIdentity(string ModuleName, string TypeName)
{
    public static PythonExceptionIdentity Builtin(string typeName) => new("builtins", typeName);

    public static PythonExceptionIdentity Module(string moduleName, string typeName) => new(moduleName, typeName);

    /// <summary>
    /// Converts the legacy short-name boundary used by low-level runtime errors.
    /// Names that are unique to a standard-library exception retain their Python module identity.
    /// </summary>
    public static PythonExceptionIdentity FromRuntimeTypeName(string typeName)
        => typeName switch
        {
            "ArgumentError" or "ArgumentTypeError" => Module("argparse", typeName),
            "DivisionByZero" or "InvalidOperation" => Module("decimal", typeName),
            "FrozenInstanceError" => Module("dataclasses", typeName),
            "BadGzipFile" => Module("gzip", typeName),
            "JSONDecodeError" => Module("json", typeName),
            "StatisticsError" => Module("statistics", typeName),
            "CalledProcessError" or "SubprocessError" or "TimeoutExpired" => Module("subprocess", typeName),
            "BadZipFile" or "BadZipfile" => Module("zipfile", "BadZipFile"),
            "LargeZipFile" => Module("zipfile", typeName),
            "CellCoordinatesException" or
            "IllegalCharacterError" or
            "InvalidFileException" or
            "NamedRangeException" or
            "ReadOnlyWorkbookException" or
            "SheetTitleException" or
            "WorkbookAlreadySaved" => Module("openpyxl.utils.exceptions", typeName),
            _ => Builtin(typeName),
        };

    public bool IsBuiltin => string.Equals(ModuleName, "builtins", StringComparison.Ordinal);

    public string QualifiedName => IsBuiltin ? TypeName : $"{ModuleName}.{TypeName}";
}

internal interface IPythonExceptionType
{
    PythonExceptionIdentity ExceptionIdentity { get; }
}
