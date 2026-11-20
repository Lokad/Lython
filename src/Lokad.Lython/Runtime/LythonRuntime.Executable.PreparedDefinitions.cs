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

    private static bool CanDefineLexically(ExecutableCodeObject code)
        => code.UsesOnlyLexicalSlots && !code.ScopeFacts.RequiresClassCell;

    private static bool NeedsDefinitionContext(ExecutableCreateFunction operation, ExecutionContext scope)
        => operation.DecoratorCount != 0 || operation.DeferredAnnotations.Length != 0 ||
            operation.TypeParameters is not null || HasTypeParameterScope(scope);

    // Inline and array-backed lexical calls have the same owner and operand
    // order. Only the caller retires the storage the operands came from.
    private static PyFunctionBase CreateLexicalFunctionValue(FunctionParameterLayout layout, ExecutableCodeObject code,
        IReadOnlyList<ExecutableCell> cells, bool generator, ExecutionContext scope, LythonSourceSpan span,
        PythonCallableKind kind, ReadOnlySpan<ExecutableSlot> operands)
    {
        var firstAnnotation = layout.Defaults.Length == 0 ? 0 : 1;
        var defaults = firstAnnotation == 0 ? null : (PendingFunctionDefaults)operands[0].Reference!;
        var function = CreateFunctionValue(layout, code, GetGlobalContext(scope), cells, defaults?.Values ?? default,
            generator, scope, span, kind, operands[firstAnnotation..]);
        defaults?.Publish();
        return function;
    }

    private static bool CanDefineWithoutInvocation(ExecutableCodeObject code, ExecutableCodeObject parentCode,
        IReadOnlyList<ExecutableCell>? parentCells, ExecutionContext scope, LythonSourceSpan span, out int[]? slots)
    {
        slots = null;
        return CanDefineLexically(code) && (code.ClosureNames.Count == 0 ||
            TryGetStandaloneCaptures(code, parentCode, parentCells, scope, span, out slots));
    }

    private static bool TryGetStandaloneCaptures(ExecutableCodeObject code, ExecutableCodeObject parentCode,
        IReadOnlyList<ExecutableCell>? parentCells, ExecutionContext scope, LythonSourceSpan span, out int[]? slots)
    {
        slots = null;
        if (code.CaptureSources is not { } sources ||
            !ReferenceEquals(sources.ParentLocalSlots, parentCode.LocalNameToSlot)) return false;

        // Locally captured cells can stand alone in lexical work. Forwarded
        // cells can still belong to a live namespace outside this work.
        for (var i = 0; i < sources.Slots.Length; i++)
        {
            if ((i & 255) == 0) scope.CheckExecution(span);
            var slot = sources.Slots[i];
            if (slot < 0 && ReadCapturedCell(parentCells, ~slot) is not { HasNamespaceOwner: false })
                return false;
        }
        slots = sources.Slots;
        return true;
    }

    internal sealed partial class ExecutionContext
    {
        private async ValueTask ExecutePreparedClassDefinitionAsync(ExecutableCreateClass create, int first, bool asynchronous)
        {
            var decorators = _execution.Stack.Slice(first, create.Syntax.Decorators.Count);
            var headerStart = first + decorators.Length;
            var header = create.ExpandedHeader
                ? (CallExpansion.CallArgumentAccumulator)_execution.Stack[headerStart] : null;
            var directHeader = _execution.Stack.Slice(headerStart, create.DirectHeaderCount);
            try
            {
                await ExecuteClassDefinitionAsync(create, this, asynchronous, decorators, header, directHeader).ConfigureAwait(false);
            }
            finally
            {
                // Keep the operand owner and its exact expanded arguments live
                // through all class and decorator callbacks, then retire once.
                _execution.Stack.RetireTo(first);
            }
            SyncExecutableLocalsFromContext(_execution.Code, _execution.Locals, this);
        }

        private static void CreateBorrowedExecutableFunction(ref ExecutableWorkState execution,
            ExecutionContext scope, ExecutableCreateFunction operation, in ExecutableInstruction instruction, int[]? captures)
        {
            var span = instruction.Span;
            var first = instruction.InputStart;
            scope.EnterInterpreterFrame(span);
            PyFunctionBase function;
            try
            {
                var cells = CaptureExecutableCells(operation.CodeObject, scope, span, ref execution, captures);
                function = CreateLexicalFunctionValue(operation.ParameterLayout, operation.CodeObject, cells, operation.IsGenerator,
                    scope, span, PythonCallableKind.Function, execution.Stack.Slice(first, operation.InputCount).Span);
            }
            finally { RetireExecutableDefinition(ref execution, scope, first); }
            PublishInstructionResult(ref execution, null, instruction, function);
        }

        private static ValueTask CreateExecutableFunction(ref ExecutableWorkState execution,
            ExecutionContext context, ExecutableCreateFunction operation, in ExecutableInstruction instruction, bool asynchronous)
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

                var typeScope = context.IsClassBody ? CreateTypeScope(operation.TypeParameters, context, span) : default;
                var cells = CaptureExecutableClosures(code, context.IsClassBody ? typeScope.Closure.FunctionClosureContext : context, span);
                if (!context.IsClassBody) typeScope = CreateTypeScope(operation.TypeParameters, context, span);
                var closure = SelectDefinitionClosure(code, typeScope.Closure, cells);
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
                    var completion = context.FinishExecutableFunctionAsync(operation.DecoratorCount, function, defaults, metadata,
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
            PublishInstructionResult(ref execution, context, instruction, function);
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
