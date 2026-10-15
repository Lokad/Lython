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
        SupportsDirectPositional = !_codeObject.RequiresLocalVariableMirroring &&
            BindingPlan.KeywordOnlyParameters.Count == 0 && BindingPlan.VariadicList is null && BindingPlan.VariadicDictionary is null;
        var layout = BindingPlan.LayoutParameterNames;
        _argumentSlotMap = new int[layout.Count];
        for (var i = 0; i < layout.Count; i++)
        {
            _argumentSlotMap[i] = codeObject.LocalNameToSlot.TryGetValue(layout[i], out var slot) ? slot : -1;
        }
    }

    protected override bool RequiresArgumentMirroring => _codeObject.RequiresLocalVariableMirroring;

    internal bool SupportsDirectPositional { get; }

    internal object InvokePositional(ReadOnlySpan<object?> arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecution(span);
        return InvokeBound(LythonRuntime.BindPositionalFunctionArguments(arguments, span, BindingPlan), span);
    }

    internal ValueTask<object> InvokePositionalAsync(ReadOnlySpan<object?> arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecution(span);
        var bound = LythonRuntime.BindPositionalFunctionArguments(arguments, span, BindingPlan);
        return InvokeBoundAsync(bound, span);
    }

    protected override object ExecuteBody(
        LythonRuntime.ExecutionContext frame,
        BoundCallArguments boundArguments,
        LythonSourceSpan span)
    {
        _ = span;
        if (LythonRuntime.TryExecuteSimpleReturn(_codeObject, frame, boundArguments, _argumentSlotMap, out var result))
            return result;
        return LythonRuntime.ExecuteExecutableCodeObject(_codeObject, frame, boundArguments, _argumentSlotMap, _closureCells)
            ?? PyNone.Instance;
    }

    protected override async ValueTask<object> ExecuteBodyAsync(
        LythonRuntime.ExecutionContext frame,
        BoundCallArguments boundArguments,
        LythonSourceSpan span)
    {
        _ = span;
        // Only statically proven bodies without callbacks use this shared shortcut.
        if (LythonRuntime.TryExecuteSimpleReturn(_codeObject, frame, boundArguments, _argumentSlotMap, out var result))
            return result;
        return await LythonRuntime.ExecuteExecutableCodeObjectAsync(
            _codeObject, frame, boundArguments, _argumentSlotMap, _closureCells).ConfigureAwait(false)
            ?? PyNone.Instance;
    }
}
