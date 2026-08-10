namespace Lokad.Lython.Frontend;

/// <summary>Maps subprocess run-family parameters to their bound-call positions.</summary>
internal readonly record struct SubprocessRunArgumentLayout(
    int Args,
    int Input,
    int CurrentDirectory,
    int Timeout,
    int Check,
    int CaptureOutput,
    int StandardInput,
    int StandardOutput,
    int StandardError,
    int Shell,
    int Text,
    int Encoding,
    int Errors,
    int Environment,
    int UniversalNewlines)
{
    public static readonly SubprocessRunArgumentLayout Standard = new(
        Args: 0,
        Input: 1,
        CurrentDirectory: 2,
        Timeout: 3,
        Check: 4,
        CaptureOutput: 5,
        StandardInput: 6,
        StandardOutput: 7,
        StandardError: 8,
        Shell: 9,
        Text: 10,
        Encoding: 11,
        Errors: 12,
        Environment: 13,
        UniversalNewlines: 14);
}
