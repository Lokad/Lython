namespace Lokad.Lython.Frontend;

/// <summary>Validated internal form of a stable Lython diagnostic identifier.</summary>
internal readonly record struct LythonDiagnosticCode
{
    private LythonDiagnosticCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static LythonDiagnosticCode Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length != 6 ||
            value[0] != 'L' ||
            value[1] != 'A' ||
            !char.IsAsciiDigit(value[2]) ||
            !char.IsAsciiDigit(value[3]) ||
            !char.IsAsciiDigit(value[4]) ||
            !char.IsAsciiDigit(value[5]))
        {
            throw new ArgumentException("A Lython diagnostic code must have the form LA0000.", nameof(value));
        }

        return new LythonDiagnosticCode(value);
    }

    public static implicit operator LythonDiagnosticCode(string value) => Parse(value);
}
