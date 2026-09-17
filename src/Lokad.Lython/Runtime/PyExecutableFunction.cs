using Lokad.Lython.Frontend;
namespace Lokad.Lython.Runtime;

internal sealed class PyExecutableFunction : PyFunctionBase
{
    private readonly ExecutableCodeObject _codeObject;
    private readonly IReadOnlyList<LythonRuntime.ExecutableCell> _closureCells;
    // Layout-to-slot map shared across invocations: layout index to frame
    // slot, or -1 when the name is not a frame local (like a name-lookup
    // miss today). Built once per function value.
    private readonly int[] _argumentSlotMap;

    public PyExecutableFunction(
        string name,
        IReadOnlyList<LoweredFunctionParameter> parameters,
        ExecutableCodeObject codeObject,
        LythonRuntime.ExecutionContext closure,
        IReadOnlyList<LythonRuntime.ExecutableCell> closureCells,
        Dictionary<string, object> defaultValues,
        ScopeDirectiveFacts scopeFacts)
        : base(name, parameters, closure, defaultValues, scopeFacts)
    {
        _codeObject = codeObject;
        _closureCells = closureCells;
        var layout = BindingPlan.LayoutParameterNames;
        _argumentSlotMap = new int[layout.Count];
        for (var i = 0; i < layout.Count; i++)
        {
            _argumentSlotMap[i] = codeObject.LocalNameToSlot.TryGetValue(layout[i], out var slot) ? slot : -1;
        }
    }

    protected override bool RequiresArgumentMirroring => _codeObject.RequiresLocalVariableMirroring;

    protected override object ExecuteBody(
        LythonRuntime.ExecutionContext frame,
        BoundCallArguments boundArguments,
        LythonSourceSpan span)
    {
        _ = span;
        return LythonRuntime.ExecuteExecutableCodeObject(_codeObject, frame, boundArguments, _argumentSlotMap, _closureCells)
            ?? PyNone.Instance;
    }
}
