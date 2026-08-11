using System.Collections.Concurrent;

namespace Lokad.Lython.Runtime;

[Flags]
internal enum LythonVariadicParameters
{
    None = 0,
    Keywords = 1,
    Positional = 2,
}

internal readonly record struct ArgumentCountLimit
{
    private ArgumentCountLimit(int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximum);
        IsBounded = true;
        Maximum = maximum;
    }

    public static ArgumentCountLimit Unbounded => default;

    public bool IsBounded { get; }

    public int Maximum { get; }

    public static ArgumentCountLimit AtMost(int maximum) => new(maximum);

    public bool Accepts(int count) => !IsBounded || count <= Maximum;

    public static implicit operator ArgumentCountLimit(int maximum) => AtMost(maximum);
}

internal abstract class CallableParameterLayout
{
    private protected CallableParameterLayout(int minimumArgumentCount, ArgumentCountLimit maximumArgumentCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimumArgumentCount);
        if (maximumArgumentCount.IsBounded && minimumArgumentCount > maximumArgumentCount.Maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumArgumentCount), "A callable's minimum argument count cannot exceed its maximum.");
        }

        MinimumArgumentCount = minimumArgumentCount;
        MaximumArgumentCount = maximumArgumentCount;
    }

    public int MinimumArgumentCount { get; }

    public ArgumentCountLimit MaximumArgumentCount { get; }

    internal abstract bool HasSameShape(CallableParameterLayout other);
}

internal sealed class PositionalCallableParameterLayout : CallableParameterLayout
{
    public PositionalCallableParameterLayout(int minimumArgumentCount)
        : this(minimumArgumentCount, ArgumentCountLimit.Unbounded)
    {
    }

    public PositionalCallableParameterLayout(int minimumArgumentCount, ArgumentCountLimit maximumArgumentCount)
        : base(minimumArgumentCount, maximumArgumentCount)
    {
    }

    internal override bool HasSameShape(CallableParameterLayout other)
        => other is PositionalCallableParameterLayout positional &&
           MinimumArgumentCount == positional.MinimumArgumentCount &&
           MaximumArgumentCount == positional.MaximumArgumentCount;
}

internal sealed class NamedCallableParameterLayout : CallableParameterLayout
{
    public NamedCallableParameterLayout(
        string callableName,
        string[] parameterNames,
        int requiredCount,
        ArgumentCountLimit maximumArgumentCount,
        ArgumentCountLimit maximumPositionalArgumentCount,
        LythonVariadicParameters variadicParameters,
        int positionalOnlyCount)
        : base(requiredCount, maximumArgumentCount)
    {
        if (requiredCount > parameterNames.Length ||
            (maximumPositionalArgumentCount.IsBounded && maximumPositionalArgumentCount.Maximum > parameterNames.Length) ||
            positionalOnlyCount < 0 ||
            positionalOnlyCount > parameterNames.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(requiredCount), $"Callable signature '{callableName}' has inconsistent parameter bounds.");
        }

        ParameterNames = [.. parameterNames];
        RequiredCount = requiredCount;
        MaximumPositionalArgumentCount = maximumPositionalArgumentCount;
        AllowsExtraKeywords = (variadicParameters & LythonVariadicParameters.Keywords) != 0;
        AllowsExtraPositional = (variadicParameters & LythonVariadicParameters.Positional) != 0;
        PositionalOnlyCount = positionalOnlyCount;
        ParameterIndices = CreateParameterIndices(ParameterNames, callableName);

        static IReadOnlyDictionary<string, int> CreateParameterIndices(string[] names, string name)
        {
            var indices = new Dictionary<string, int>(names.Length, StringComparer.Ordinal);
            for (var index = 0; index < names.Length; index++)
            {
                if (!indices.TryAdd(names[index], index))
                {
                    throw new ArgumentException($"Callable signature '{name}' contains duplicate parameter '{names[index]}'.", nameof(parameterNames));
                }
            }

            return indices;
        }
    }

    public string[] ParameterNames { get; }

    public int RequiredCount { get; }

    public ArgumentCountLimit MaximumPositionalArgumentCount { get; }

    public bool AllowsExtraKeywords { get; }

    public bool AllowsExtraPositional { get; }

    public int PositionalOnlyCount { get; }

    public IReadOnlyDictionary<string, int> ParameterIndices { get; }

    internal override bool HasSameShape(CallableParameterLayout other)
        => other is NamedCallableParameterLayout named &&
           RequiredCount == named.RequiredCount &&
           MaximumPositionalArgumentCount == named.MaximumPositionalArgumentCount &&
           AllowsExtraKeywords == named.AllowsExtraKeywords &&
           AllowsExtraPositional == named.AllowsExtraPositional &&
           PositionalOnlyCount == named.PositionalOnlyCount &&
           ParameterNames.SequenceEqual(named.ParameterNames, StringComparer.Ordinal);
}

internal sealed class LythonCallableSignature
{
    private static readonly ConcurrentDictionary<string, SignatureBucket> SignaturesByName = new(StringComparer.Ordinal);

    private LythonCallableSignature(string name, CallableParameterLayout parameters)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("A callable signature requires a name.", nameof(name));
        }

        Name = name;
        Parameters = parameters;
    }

    public string Name { get; }

    public CallableParameterLayout Parameters { get; }

    public static LythonCallableSignature Create(string name)
        => CreatePositional(name, 0);

    public static LythonCallableSignature Create(string name, string[] parameterNames)
        => CreateNamed(name, parameterNames, parameterNames.Length, parameterNames.Length, LythonVariadicParameters.None, 0);

    public static LythonCallableSignature Create(string name, int requiredCount)
        => CreatePositional(name, requiredCount);

    public static LythonCallableSignature Create(string name, string[] parameterNames, int requiredCount)
        => CreateNamed(name, parameterNames, requiredCount, parameterNames.Length, LythonVariadicParameters.None, 0);

    public static LythonCallableSignature Create(string name, string[] parameterNames, int requiredCount, int maximumPositionalArgumentCount)
        => CreateNamed(name, parameterNames, requiredCount, maximumPositionalArgumentCount, LythonVariadicParameters.None, 0);

    public static LythonCallableSignature Create(string name, string[] parameterNames, int requiredCount, LythonVariadicParameters variadicParameters)
        => CreateNamed(
            name,
            parameterNames,
            requiredCount,
            (variadicParameters & LythonVariadicParameters.Positional) != 0
                ? ArgumentCountLimit.Unbounded
                : ArgumentCountLimit.AtMost(parameterNames.Length),
            variadicParameters,
            0);

    public static LythonCallableSignature Create(string name, string[] parameterNames, int requiredCount, int maximumPositionalArgumentCount, LythonVariadicParameters variadicParameters)
        => CreateNamed(name, parameterNames, requiredCount, maximumPositionalArgumentCount, variadicParameters, 0);

    public static LythonCallableSignature Create(
        string name,
        string[] parameterNames,
        int requiredCount,
        ArgumentCountLimit maximumPositionalArgumentCount,
        LythonVariadicParameters variadicParameters,
        int positionalOnlyCount)
        => CreateNamed(name, parameterNames, requiredCount, maximumPositionalArgumentCount, variadicParameters, positionalOnlyCount);

    private static LythonCallableSignature CreatePositional(string name, int requiredCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(requiredCount);
        var bucket = SignaturesByName.GetOrAdd(name, static _ => new SignatureBucket());
        lock (bucket.Signatures)
        {
            foreach (var signature in bucket.Signatures)
            {
                if (signature.Parameters is PositionalCallableParameterLayout positional &&
                    positional.MinimumArgumentCount == requiredCount)
                {
                    return signature;
                }
            }

            var created = new LythonCallableSignature(name, new PositionalCallableParameterLayout(requiredCount));
            bucket.Signatures.Add(created);
            return created;
        }
    }

    private static LythonCallableSignature CreateNamed(
        string name,
        string[] parameterNames,
        int requiredCount,
        ArgumentCountLimit maximumPositionalArgumentCount,
        LythonVariadicParameters variadicParameters,
        int positionalOnlyCount)
    {
        var bucket = SignaturesByName.GetOrAdd(name, static _ => new SignatureBucket());
        lock (bucket.Signatures)
        {
            foreach (var signature in bucket.Signatures)
            {
                if (signature.Parameters is NamedCallableParameterLayout named &&
                    named.RequiredCount == requiredCount &&
                    named.MaximumPositionalArgumentCount == maximumPositionalArgumentCount &&
                    named.AllowsExtraKeywords == ((variadicParameters & LythonVariadicParameters.Keywords) != 0) &&
                    named.AllowsExtraPositional == ((variadicParameters & LythonVariadicParameters.Positional) != 0) &&
                    named.PositionalOnlyCount == positionalOnlyCount &&
                    named.ParameterNames.SequenceEqual(parameterNames, StringComparer.Ordinal))
                {
                    return signature;
                }
            }

            var parameters = new NamedCallableParameterLayout(
                name,
                parameterNames,
                requiredCount,
                (variadicParameters & (LythonVariadicParameters.Keywords | LythonVariadicParameters.Positional)) != 0
                    ? ArgumentCountLimit.Unbounded
                    : ArgumentCountLimit.AtMost(parameterNames.Length),
                maximumPositionalArgumentCount,
                variadicParameters,
                positionalOnlyCount);
            var created = new LythonCallableSignature(name, parameters);
            bucket.Signatures.Add(created);
            return created;
        }
    }

    private sealed class SignatureBucket
    {
        public List<LythonCallableSignature> Signatures { get; } = [];
    }
}
