using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Covers the function, its binding plan and inline metadata slots.
    // Default containers and metadata values fund their own storage.
    private const long FunctionValueBytes = 160;

    private static PyFunctionBase CreateFunctionValue(FunctionParameterLayout layout, ExecutableCodeObject code,
        ExecutionContext closure, IReadOnlyList<ExecutableCell> cells, FunctionDefaults defaults,
        bool generator, ExecutionContext context, LythonSourceSpan span, PythonCallableKind kind = PythonCallableKind.Function)
    {
        // Constructor storage has a fixed estimate and one owner. No separate
        // reservation object needs to survive the synchronous construction.
        var governor = context.MemoryGovernor;
        governor.Reserve(FunctionValueBytes, span);
        PyFunctionBase function;
        try
        {
            context.CheckExecution(span);
            function = generator
                ? new PyGeneratorFunction(layout, code, closure, defaults, cells, kind)
                : new PyExecutableFunction(layout, code, closure, cells, defaults, kind);
            governor.Commit(FunctionValueBytes);
        }
        catch
        {
            governor.ReleaseReserved(FunctionValueBytes);
            throw;
        }
        ChargeClosureRetention(closure, governor, span);
        return function;
    }

    internal sealed partial class ExecutionContext
    {
        private async ValueTask ExecutePreparedClassDefinitionAsync(ExecutableCreateClass create, int first, bool asynchronous)
        {
            var definition = create.Definition;
            var decorators = _execution.Stack.Slice(first, definition.Decorators.Count);
            var header = create.ExpandedHeader
                ? (CallExpansion.CallArgumentAccumulator)_execution.Stack[first + decorators.Length] : null;
            try
            {
                await ExecuteClassDefinitionAsync(definition, this, asynchronous, decorators, header).ConfigureAwait(false);
            }
            finally
            {
                // Keep the operand owner and its exact expanded arguments live
                // through all class and decorator callbacks, then retire once.
                _execution.Stack.RetireTo(first);
            }
            SyncExecutableLocalsFromContext(_execution.Code, _execution.Locals, this);
        }

        private static bool CanDefineWithoutInvocation(ExecutableCodeObject code, in ExecutableWorkState parent,
            ExecutionContext scope, LythonSourceSpan span, out int[]? slots)
        {
            slots = null;
            if (!code.UsesOnlyLexicalSlots || code.ScopeFacts.RequiresClassCell) return false;
            if (code.ClosureNames.Count == 0) return true;
            if (code.CaptureSources is not { } sources ||
                !ReferenceEquals(sources.ParentLocalSlots, parent.Code.LocalNameToSlot)) return false;

            // Prepared local slots already hold standalone cells. A forwarded
            // cell can still belong to a live namespace outside this work.
            for (var i = 0; i < sources.Slots.Length; i++)
            {
                if ((i & 255) == 0) scope.CheckExecution(span);
                var slot = sources.Slots[i];
                if (slot < 0 && ReadCapturedCell(parent.ClosureCells, ~slot) is not { HasNamespaceOwner: false })
                    return false;
            }
            slots = sources.Slots;
            return true;
        }

        private static ValueTask CreateExecutableFunction(ref ExecutableWorkState execution,
            ExecutionContext context, ExecutionContext? invocation, ExecutableCreateFunction operation,
            in ExecutableInstruction instruction, bool asynchronous, int[]? borrowedCaptures)
        {
            var binding = operation.Binding;
            var span = binding.Span;
            var first = instruction.InputStart;
            context.EnterInterpreterFrame(span);
            PyFunctionBase function;
            var transferred = false;
            try
            {
                var layout = binding.ParameterLayout;
                var next = first + binding.DecoratorCount;
                var defaults = layout.Defaults.Length == 0 ? null : (PendingFunctionDefaults)execution.Stack[next++];

                var cells = invocation is null
                    ? CaptureExecutableCells(binding.CodeObject, context, span, ref execution, borrowedCaptures)
                    : CaptureExecutableClosures(binding.CodeObject, context, span);
                var typeScope = CreateTypeScope(binding.TypeParameters, context, span);
                var closure = invocation is null ? GetGlobalContext(context)
                    : SelectDefinitionClosure(binding.CodeObject, typeScope.Closure, cells);
                function = CreateFunctionValue(layout, binding.CodeObject, closure, cells, defaults?.Values ?? default,
                    binding.IsGenerator, context, span);
                var metadata = ValueTask.CompletedTask;
                if (binding.Annotations.Length != 0 || binding.TypeParameters is not null || HasTypeParameterScope(context))
                {
                    var evaluated = operation.AnnotationsOnStack ? execution.Stack.Slice(next, binding.Annotations.Length) : default;
                    metadata = AttachTypeMetadataAsync(function, binding.Annotations, binding.TypeParameters is not null,
                        span, typeScope, context, asynchronous, evaluated, binding.AnnotationKeys);
                }

                if (binding.DecoratorCount != 0 || !metadata.IsCompletedSuccessfully)
                {
                    var completion = invocation!.FinishExecutableFunctionAsync(binding, function, defaults, metadata, instruction, asynchronous);
                    transferred = true;
                    return completion;
                }

                metadata.GetAwaiter().GetResult();
                defaults?.Publish();
            }
            finally
            {
                if (!transferred) RetireExecutableDefinition(ref execution, context, first);
            }
            PublishInstructionResult(ref execution, invocation, instruction, function);
            return ValueTask.CompletedTask;
        }

        private async ValueTask FinishExecutableFunctionAsync(ExecutableFunctionBinding binding, PyFunctionBase function,
            PendingFunctionDefaults? defaults, ValueTask metadata, ExecutableInstruction instruction, bool asynchronous)
        {
            var span = binding.Span;
            var first = instruction.InputStart;
            object decorated = function;
            try
            {
                await metadata.ConfigureAwait(false);
                // Decorator callbacks can publish the original definition even
                // when they replace it or raise. Transfer the default owner first.
                defaults?.Publish();
                for (var i = first + binding.DecoratorCount - 1; i >= first; i--)
                {
                    if (_execution.Stack[i] is not ICallable decorator)
                        throw new LythonRuntimeException("TypeError", "Decorator expression must evaluate to a callable.", span);
                    decorated = asynchronous ? await CallableInvocation.InvokeUnaryAsync(decorator, decorated, span, this).ConfigureAwait(false)
                        : CallableInvocation.InvokeUnary(decorator, decorated, span, this);
                }
            }
            finally
            {
                RetireExecutableDefinition(ref _execution, this, first);
            }
            PublishInstructionResult(instruction, decorated);
        }

        private static void RetireExecutableDefinition(ref ExecutableWorkState execution, ExecutionContext context, int first)
        {
            try { execution.Stack.RetireTo(first); }
            finally { context.LeaveInterpreterFrame(); }
        }
    }
}
