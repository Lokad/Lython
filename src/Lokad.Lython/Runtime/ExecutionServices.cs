using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class ExecutionServices
{
    public ExecutionServices(ExecutionState state)
    {
        State = state;
        BudgetGuards = state.BudgetGuards;
        ValueObservation = new ExecutionValueObservation(state);
    }

    public ExecutionState State { get; }

    public ExecutionBudgetGuards BudgetGuards { get; }

    public ExecutionValueObservation ValueObservation { get; }

    public PyException? CurrentException { get; private set; }

    public ILythonHost Host => State.Host;

    public LythonRuntime.ExecutionLimits Limits => State.Limits;

    public MemoryGovernor MemoryGovernor => State.MemoryGovernor;

    public void CheckExecutionBudget(LythonSourceSpan? span) => BudgetGuards.CheckExecutionBudget(span);

    public void RegisterHostCall(LythonSourceSpan? span) => BudgetGuards.RegisterHostCall(span);

    public void EnterFunctionCall(LythonSourceSpan? span) => BudgetGuards.EnterFunctionCall(span);

    public void LeaveFunctionCall() => BudgetGuards.LeaveFunctionCall();

    public void EnterInterpreterFrame(LythonSourceSpan? span) => BudgetGuards.EnterInterpreterFrame(span);

    public void LeaveInterpreterFrame() => BudgetGuards.LeaveInterpreterFrame();

    public PyException? SetCurrentException(PyException? exception)
    {
        var previous = CurrentException;
        CurrentException = exception;
        return previous;
    }

    public void ObserveString(PyString text, LythonSourceSpan? span)
    {
        BudgetGuards.CheckExecutionBudget(span);
        ValueObservation.ObserveString(text, span);
    }

    public void ObserveCollectionCount(int count, LythonSourceSpan? span)
    {
        BudgetGuards.CheckExecutionBudget(span);
        ValueObservation.ObserveCollectionCount(count, span);
    }

    public void ObserveValue(object value, LythonSourceSpan? span) => ValueObservation.ObserveValue(value, span);
}
