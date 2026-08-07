namespace Lokad.Lython;

/// <summary>Classifies the impact of a compile-time diagnostic.</summary>
public enum LythonDiagnosticSeverity
{
    /// <summary>Provides information without rejecting the script.</summary>
    Info,
    /// <summary>Reports a suspicious construct without rejecting the script.</summary>
    Warning,
    /// <summary>Reports a condition that prevents execution.</summary>
    Error
}
