using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Covers the function, its binding plan and inline metadata slots.
    // Default containers and metadata values fund their own storage.
    private const long FunctionValueBytes = 160;

    private static long FunctionAndAnnotationBytes(int capacity)
        => checked(FunctionValueBytes + PyDict.RecordKeys.RecordBytes(capacity));

    private static object?[] ReadEagerFunctionAnnotations(ExecutableInstruction[] reads, ExecutableCodeObject parent,
        ExecutableCodeObject code, ExecutionContext context, LythonSourceSpan span)
    {
        var capacity = code.DefinitionAnnotationKeys.RequireNotNull().PreparedValueCapacity;
        var bytes = FunctionAndAnnotationBytes(capacity);
        var governor = context.MemoryGovernor;
        governor.Reserve(bytes, span);
        try
        {
            context.CheckExecution(span);
            var values = new object?[capacity];
            for (var i = 0; i < reads.Length; i++)
            {
                ref readonly var read = ref reads[i];
                context.State.CheckExecution(read.Span);
                values[i] = read.OpCode == ExecutableOpCode.LoadGlobal
                    ? ResolveExecutableGlobal(parent, read.NameIndex, read.Span, context)
                    : RuntimeValue(parent.Constants[read.ConstantIndex]);
            }
            // Keep the reservation private until the callable takes the record.
            return values;
        }
        catch { governor.ReleaseReserved(bytes); throw; }
    }

    private static void RefundEagerFunctionAnnotations(object?[]? values, ExecutionContext context)
    {
        if (values is not null) context.MemoryGovernor.ReleaseReserved(FunctionAndAnnotationBytes(values.Length));
    }

    private static PyFunctionBase CreateFunctionValue(FunctionParameterLayout layout, ExecutableCodeObject code,
        ExecutionContext closure, IReadOnlyList<ExecutableCell> cells, FunctionDefaults defaults,
        bool generator, ExecutionContext context, LythonSourceSpan span, PythonCallableKind kind = PythonCallableKind.Function,
        ReadOnlySpan<ExecutableSlot> annotations = default, PyString[]? postponedAnnotations = null,
        object?[]? preparedAnnotations = null)
    {
        // The callable and its final annotation backing are still private.
        // Admit both before allocation and transfer the combined charge once.
        var keys = annotations.IsEmpty && postponedAnnotations is null && preparedAnnotations is null
            ? null : code.DefinitionAnnotationKeys.RequireNotNull();
        var capacity = keys?.PreparedValueCapacity ?? 0;
        var bytes = keys is null ? FunctionValueBytes : FunctionAndAnnotationBytes(capacity);
        var governor = context.MemoryGovernor;
        if (preparedAnnotations is null) governor.Reserve(bytes, span);
        try
        {
            context.CheckExecution(span);
            PyFunctionBase function = generator
                ? new PyGeneratorFunction(layout, code, closure, defaults, cells, kind)
                : new PyExecutableFunction(layout, code, closure, cells, defaults, kind);
            ChargeClosureRetention(closure, governor, span);
            if (keys is not null)
                function.AttachAdmittedAnnotations(preparedAnnotations ?? postponedAnnotations ??
                    keys.CopyAnnotationValues(annotations, capacity, context, span), context);
            governor.Commit(bytes);
            return function;
        }
        catch
        {
            if (preparedAnnotations is null) governor.ReleaseReserved(bytes);
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
        PythonCallableKind kind, Span<ExecutableSlot> operands, PyString[]? postponedAnnotations = null,
        object?[]? preparedAnnotations = null)
    {
        var firstAnnotation = layout.DefaultStorageCount;
        var defaults = operands[..firstAnnotation];
        var function = CreateFunctionValue(layout, code, GetGlobalContext(scope), cells, ReadExecutableDefaults(layout, defaults),
            generator, scope, span, kind, operands[firstAnnotation..], postponedAnnotations, preparedAnnotations);
        defaults.Clear();
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
            object?[]? preparedAnnotations = null;
            try
            {
                if (operation.EagerAnnotationReads is { } reads)
                    preparedAnnotations = ReadEagerFunctionAnnotations(reads, execution.Code, operation.CodeObject, scope, span);
                var cells = CaptureExecutableCells(operation.CodeObject, scope, span, ref execution, captures);
                function = CreateLexicalFunctionValue(operation.ParameterLayout, operation.CodeObject, cells, operation.IsGenerator,
                    scope, span, PythonCallableKind.Function, execution.Stack.Slice(first, operation.InputCount).Span,
                    operation.PostponedAnnotations, preparedAnnotations);
                preparedAnnotations = null;
            }
            finally
            {
                try { RefundEagerFunctionAnnotations(preparedAnnotations, scope); }
                finally { RetireExecutableDefinition(ref execution, scope, first); }
            }
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
            object?[]? preparedAnnotations = null;
            try
            {
                var layout = operation.ParameterLayout;
                var code = operation.CodeObject;
                var defaultStart = first + operation.DecoratorCount;
                var defaultCount = layout.DefaultStorageCount;
                var defaults = execution.Stack.Slice(defaultStart, defaultCount).Span;
                var next = defaultStart + defaultCount;

                if (operation.EagerAnnotationReads is { } reads)
                    preparedAnnotations = ReadEagerFunctionAnnotations(reads, execution.Code, code, context, span);
                var typeScope = context.IsClassBody ? CreateTypeScope(operation.TypeParameters, context, span) : default;
                var cells = CaptureExecutableClosures(code, context.IsClassBody ? typeScope.Closure.FunctionClosureContext : context, span);
                if (!context.IsClassBody) typeScope = CreateTypeScope(operation.TypeParameters, context, span);
                var closure = SelectDefinitionClosure(code, typeScope.Closure, cells);
                ReadOnlySpan<ExecutableSlot> annotations = operation.AnnotationOperandCount != 0
                    ? execution.Stack.Slice(next, operation.AnnotationOperandCount).Span : default;
                function = CreateFunctionValue(layout, code, closure, cells, ReadExecutableDefaults(layout, defaults),
                    operation.IsGenerator, context, span, annotations: annotations,
                    postponedAnnotations: operation.PostponedAnnotations, preparedAnnotations: preparedAnnotations);
                preparedAnnotations = null;
                var metadata = ValueTask.CompletedTask;
                if (operation.AnnotationOperandCount == 0 && operation.EagerAnnotationReads is null &&
                    (operation.DeferredAnnotations.Length != 0 || operation.TypeParameters is not null || HasTypeParameterScope(context)))
                {
                    metadata = AttachTypeMetadataAsync(function, operation.DeferredAnnotations, operation.TypeParameters is not null,
                        span, typeScope, context, asynchronous, code.DefinitionAnnotationKeys);
                }

                if (operation.DecoratorCount != 0 || !metadata.IsCompletedSuccessfully)
                {
                    var completion = context.FinishExecutableFunctionAsync(operation.DecoratorCount, defaultCount, function, metadata,
                        instruction, asynchronous);
                    transferred = true;
                    return completion;
                }

                metadata.GetAwaiter().GetResult();
                defaults.Clear();
            }
            finally
            {
                if (!transferred)
                {
                    try { RefundEagerFunctionAnnotations(preparedAnnotations, context); }
                    finally { RetireExecutableDefinition(ref execution, context, first); }
                }
            }
            PublishInstructionResult(ref execution, context, instruction, function);
            return ValueTask.CompletedTask;
        }

        private async ValueTask FinishExecutableFunctionAsync(int decoratorCount, int defaultCount, PyFunctionBase function,
            ValueTask metadata, ExecutableInstruction instruction, bool asynchronous)
        {
            var span = instruction.Span;
            var first = instruction.InputStart;
            object decorated = function;
            try
            {
                await metadata.ConfigureAwait(false);
                // Decorator callbacks can publish the original definition even
                // when they replace it or raise. Transfer the default owner first.
                _execution.Stack.Slice(first + decoratorCount, defaultCount).Span.Clear();
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
