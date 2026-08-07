using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed class FunctionBindingPlan
{
    public FunctionBindingPlan(
        string callableName,
        PythonCallableKind callableKind,
        IReadOnlyList<LoweredFunctionParameter> parameters,
        IReadOnlyDictionary<string, object> defaultValues)
    {
        CallableName = callableName;
        CallableKind = callableKind;
        Parameters = parameters;
        DefaultValues = defaultValues;

        var positional = new List<LoweredFunctionParameter>(parameters.Count);
        var keywordOnly = new List<LoweredFunctionParameter>(parameters.Count);
        var named = new Dictionary<string, LoweredFunctionParameter>(parameters.Count, StringComparer.Ordinal);
        foreach (var parameter in parameters)
        {
            switch (parameter.Kind)
            {
                case FunctionParameterKind.Positional:
                    positional.Add(parameter);
                    named[parameter.Name] = parameter;
                    break;
                case FunctionParameterKind.KeywordOnly:
                    keywordOnly.Add(parameter);
                    named[parameter.Name] = parameter;
                    break;
                case FunctionParameterKind.VariadicList:
                    VariadicList = parameter;
                    break;
                case FunctionParameterKind.VariadicDictionary:
                    VariadicDictionary = parameter;
                    break;
            }
        }

        PositionalParameters = positional.ToArray();
        KeywordOnlyParameters = keywordOnly.ToArray();
        NamedParameters = named;
    }

    public string CallableName { get; }

    public PythonCallableKind CallableKind { get; }

    public IReadOnlyList<LoweredFunctionParameter> Parameters { get; }

    public IReadOnlyList<LoweredFunctionParameter> PositionalParameters { get; }

    public IReadOnlyList<LoweredFunctionParameter> KeywordOnlyParameters { get; }

    public IReadOnlyDictionary<string, LoweredFunctionParameter> NamedParameters { get; }

    public LoweredFunctionParameter? VariadicList { get; }

    public LoweredFunctionParameter? VariadicDictionary { get; }

    public IReadOnlyDictionary<string, object> DefaultValues { get; }
}
