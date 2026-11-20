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
        bool generator, ExecutionContext context, LythonSourceSpan span, PythonCallableKind kind = PythonCallableKind.Function,
        ReadOnlySpan<ExecutableSlot> annotations = default)
    {
        // The callable and its final annotation backing are still private.
        // Admit both before allocation and transfer the combined charge once.
        var keys = annotations.IsEmpty ? null : code.DefinitionAnnotationKeys.RequireNotNull();
        var capacity = keys?.PreparedValueCapacity ?? 0;
        var bytes = keys is null ? FunctionValueBytes : checked(FunctionValueBytes + PyDict.RecordKeys.RecordBytes(capacity));
        var governor = context.MemoryGovernor;
        governor.Reserve(bytes, span);
        try
        {
            context.CheckExecution(span);
            PyFunctionBase function = generator
                ? new PyGeneratorFunction(layout, code, closure, defaults, cells, kind)
                : new PyExecutableFunction(layout, code, closure, cells, defaults, kind);
            ChargeClosureRetention(closure, governor, span);
            if (keys is not null)
                function.AttachAdmittedAnnotations(keys.CopyAnnotationValues(annotations, capacity, context, span), context);
            governor.Commit(bytes);
            return function;
        }
        catch
        {
            governor.ReleaseReserved(bytes);
            throw;
        }
    }

    internal sealed partial class ExecutionContext
    {
        private async ValueTask ExecutePreparedClassDefinitionAsync(ExecutableCreateClass create, int first, bool asynchronous)
        {
            var definition = create.Definition;
            var decorators = _execution.Stack.Slice(first, definition.Decorators.Count);
            var headerStart = first + decorators.Length;
            var header = create.ExpandedHeader
                ? (CallExpansion.CallArgumentAccumulator)_execution.Stack[headerStart] : null;
            var directHeader = _execution.Stack.Slice(headerStart, create.DirectHeaderCount);
            try
            {
                await ExecuteClassDefinitionAsync(definition, this, asynchronous, decorators, header,
                    directHeader, create.PreparedBaseCount).ConfigureAwait(false);
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
            var span = instruction.Span;
            var first = instruction.InputStart;
            context.EnterInterpreterFrame(span);
            PyFunctionBase function;
            var transferred = false;
            try
            {
                var layout = operation.ParameterLayout;
                var code = operation.CodeObject;
                var next = first + operation.DecoratorCount;
                var defaults = layout.Defaults.Length == 0 ? null : (PendingFunctionDefaults)execution.Stack[next++];

                var cells = invocation is null
                    ? CaptureExecutableCells(code, context, span, ref execution, borrowedCaptures)
                    : CaptureExecutableClosures(code, context, span);
                var typeScope = CreateTypeScope(operation.TypeParameters, context, span);
                var closure = invocation is null ? GetGlobalContext(context)
                    : SelectDefinitionClosure(code, typeScope.Closure, cells);
                ReadOnlySpan<ExecutableSlot> annotations = operation.AnnotationOperandCount != 0
                    ? execution.Stack.Slice(next, operation.AnnotationOperandCount).Span : default;
                function = CreateFunctionValue(layout, code, closure, cells, defaults?.Values ?? default,
                    operation.IsGenerator, context, span, annotations: annotations);
                var metadata = ValueTask.CompletedTask;
                if (operation.AnnotationOperandCount == 0 &&
                    (operation.DeferredAnnotations.Length != 0 || operation.TypeParameters is not null || HasTypeParameterScope(context)))
                {
                    metadata = AttachTypeMetadataAsync(function, operation.DeferredAnnotations, operation.TypeParameters is not null,
                        span, typeScope, context, asynchronous, code.DefinitionAnnotationKeys);
                }

                if (operation.DecoratorCount != 0 || !metadata.IsCompletedSuccessfully)
                {
                    var completion = invocation!.FinishExecutableFunctionAsync(operation.DecoratorCount, function, defaults, metadata,
                        instruction, asynchronous);
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

        private async ValueTask FinishExecutableFunctionAsync(int decoratorCount, PyFunctionBase function,
            PendingFunctionDefaults? defaults, ValueTask metadata, ExecutableInstruction instruction, bool asynchronous)
        {
            var span = instruction.Span;
            var first = instruction.InputStart;
            object decorated = function;
            try
            {
                await metadata.ConfigureAwait(false);
                // Decorator callbacks can publish the original definition even
                // when they replace it or raise. Transfer the default owner first.
                defaults?.Publish();
                for (var i = first + decoratorCount - 1; i >= first; i--)
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
