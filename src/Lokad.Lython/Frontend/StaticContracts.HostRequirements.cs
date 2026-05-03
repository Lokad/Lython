using Lokad.Lython.Runtime;

namespace Lokad.Lython.Frontend;

internal enum StaticHostCapability
{
    StandardInput,
    Subprocess,
}

internal readonly record struct StaticHostRequirement(
    StaticHostCapability Capability,
    string DiagnosticCode,
    string Message,
    LythonSourceSpan Span);

internal static partial class StaticContracts
{
    public static bool TryGetHostImportRequirement(ImportStatementSyntax statement, out StaticHostRequirement requirement)
    {
        if (string.Equals(statement.ModuleName, "subprocess", StringComparison.Ordinal))
        {
            requirement = new StaticHostRequirement(
                StaticHostCapability.Subprocess,
                "LA3041",
                "import subprocess requires subprocess support from the host.",
                statement.Span);
            return true;
        }

        requirement = default;
        return false;
    }

    public static bool TryGetHostCallRequirement(CallExpressionSyntax call, out StaticHostRequirement requirement)
    {
        if (call.Target is IdentifierExpressionSyntax { Name: "input" })
        {
            requirement = new StaticHostRequirement(
                StaticHostCapability.StandardInput,
                "LA3040",
                "input() requires standard input support from the host.",
                call.Span);
            return true;
        }

        if (call.Target is MemberExpressionSyntax
            {
                Target: MemberExpressionSyntax
                {
                    Target: IdentifierExpressionSyntax { Name: "sys" },
                    MemberName: "stdin"
                },
                MemberName: "read" or "readline"
            })
        {
            requirement = new StaticHostRequirement(
                StaticHostCapability.StandardInput,
                "LA3040",
                "sys.stdin requires standard input support from the host.",
                call.Span);
            return true;
        }

        if (call.Target is MemberExpressionSyntax
            {
                Target: IdentifierExpressionSyntax { Name: "subprocess" },
                MemberName: "run"
            })
        {
            requirement = new StaticHostRequirement(
                StaticHostCapability.Subprocess,
                "LA3041",
                "subprocess.run(...) requires subprocess support from the host.",
                call.Span);
            return true;
        }

        requirement = default;
        return false;
    }

    public static bool IsHostRequirementSatisfied(StaticHostRequirement requirement, ILythonHost host)
    {
        return requirement.Capability switch
        {
            StaticHostCapability.StandardInput => host.StandardInput is not null,
            StaticHostCapability.Subprocess => host.SubprocessRunner is not null,
            _ => true
        };
    }
}
