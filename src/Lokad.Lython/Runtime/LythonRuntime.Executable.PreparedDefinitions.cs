using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;
using PreparedFunctionAnnotations = (object? Values, int Capacity, bool Repeated);

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Covers the function, its binding plan and inline metadata slots.
    // Default containers and metadata values fund their own storage.
    private const long FunctionValueBytes = 160;

    private static long FunctionAndAnnotationBytes(int capacity)
        => checked(FunctionValueBytes + PyDict.RecordKeys.RecordBytes(capacity));

    private static PreparedFunctionAnnotations ReadEagerFunctionAnnotations(ExecutableInstruction[] reads, ExecutableCodeObject parent,
        ExecutableCodeObject code, ExecutionContext context, LythonSourceSpan span)
    {
        var capacity = code.DefinitionAnnotationKeys.RequireNotNull().PreparedValueCapacity;
        var bytes = FunctionAndAnnotationBytes(capacity);
        var governor = context.MemoryGovernor;
        governor.Reserve(bytes, span);
        try
        {
            context.CheckExecution(span);
            if (reads.Length == 1)
                return (ReadAnnotation(reads[0]), capacity, true);
            var values = new object?[capacity];
            for (var i = 0; i < reads.Length; i++)
                values[i] = ReadAnnotation(reads[i]);
            // Keep the reservation private until the callable takes the record.
            return (values, capacity, false);
        }
        catch { governor.ReleaseReserved(bytes); throw; }

        object ReadAnnotation(in ExecutableInstruction read)
        {
            context.State.CheckExecution(read.Span);
            return read.OpCode == ExecutableOpCode.LoadGlobal
                ? ResolveExecutableGlobal(parent, read.NameIndex, read.Span, context)
                : RuntimeValue(parent.Constants[read.ConstantIndex]);
        }
    }

    private static void RefundEagerFunctionAnnotations(PreparedFunctionAnnotations prepared, ExecutionContext context)
    {
        if (prepared.Values is not null) context.MemoryGovernor.ReleaseReserved(FunctionAndAnnotationBytes(prepared.Capacity));
    }

    private static PyFunctionBase CreateFunctionValue(FunctionParameterLayout layout, ExecutableCodeObject code,
        ExecutionContext closure, ExecutableCaptures cells, FunctionDefaults defaults,
        bool generator, ExecutionContext context, LythonSourceSpan span,
        ReadOnlySpan<ExecutableSlot> annotations = default, PyString[]? postponedAnnotations = null,
        PreparedFunctionAnnotations preparedAnnotations = default, bool directKeywordDefault = false,
        Span<ExecutableSlot> uncapturedLocal = default)
    {
        // The callable and its final annotation backing are still private.
        // Admit both before allocation and transfer the combined charge once.
        var keys = annotations.IsEmpty && postponedAnnotations is null && preparedAnnotations.Values is null
            ? null : code.DefinitionAnnotationKeys.RequireNotNull();
        var capacity = keys?.PreparedValueCapacity ?? 0;
        var bytes = keys is null ? FunctionValueBytes : FunctionAndAnnotationBytes(capacity);
        if (directKeywordDefault)
        {
            System.Diagnostics.Debug.Assert(defaults.InlineKeyword && preparedAnnotations.Values is null);
            bytes = checked(bytes + PyDict.RecordKeys.SinglePreparedValueBytes);
        }
        var captureByValue = !uncapturedLocal.IsEmpty && code.CaptureSources is { CanCaptureByValue: true };
        if (!uncapturedLocal.IsEmpty)
        {
            System.Diagnostics.Debug.Assert(uncapturedLocal.Length == 1 && cells.IsEmpty && preparedAnnotations.Values is null);
            bytes = checked(bytes + (captureByValue ? RuntimeMemoryEstimates.ScalarStorageBytes : ExecutableCell.StorageBytes));
        }
        var governor = context.MemoryGovernor;
        if (preparedAnnotations.Values is null) governor.Reserve(bytes, span);
        try
        {
            context.CheckExecution(span);
            // Inline execution owns this unexposed slot. Both objects stay
            // private until their combined reservation is committed.
            ExecutableCell? cell = null;
            if (captureByValue)
                cells = ExecutableCaptures.FromValue(uncapturedLocal[0].Object.RequireNotNull());
            else if (!uncapturedLocal.IsEmpty)
            {
                cell = new ExecutableCell(uncapturedLocal[0]);
                cells = ExecutableCaptures.FromCell(cell);
            }
            PyFunctionBase function = generator
                ? new PyGeneratorFunction(layout, code, closure, defaults, cells)
                : new PyExecutableFunction(layout, code, closure, cells, defaults);
            ChargeClosureRetention(closure, governor, span);
            if (keys is not null)
                function.AttachAdmittedAnnotations(preparedAnnotations.Values ?? postponedAnnotations ??
                    keys.CopyAnnotationValues(annotations, capacity, context, span), context, preparedAnnotations.Repeated);
            governor.Commit(bytes);
            if (cell is not null)
            {
                cell.RetentionCharged = true;
                uncapturedLocal[0].Object = cell;
            }
            return function;
        }
        catch
        {
            if (preparedAnnotations.Values is null) governor.ReleaseReserved(bytes);
            throw;
        }
    }

    internal static PyFunctionBase CreateClosedFunctionDefinition(ExecutableCreateFunction definition,
        ExecutionContext scope, ExecutableSlot argument, LythonSourceSpan span)
    {
        scope.CheckExecution(span);
        return CreateFunctionValue(definition.ParameterLayout, definition.CodeObject, GetGlobalContext(scope),
            default, default, false, scope, span, uncapturedLocal: new Span<ExecutableSlot>(ref argument));
    }

    internal static PyFunctionBase CreateClosedComputedDefinition(ExecutableCreateFunction definition,
        ExecutableCodeObject factory, ExecutionContext scope, long argument, object? keywordCapture)
    {
        var instructions = factory.Instructions;
        var defaults = AllocateExecutableDefaults(definition.ParameterLayout, false, scope, instructions[0].Span);
        try
        {
            var index = 1;
            var positional = EvaluateClosedDefault(factory, argument, scope, ref index);
            defaults.Positional!.SetPrivateDefault(0, positional, scope.State, instructions[index++].Span);
            ExecutableSlot keyword = default;
            if (keywordCapture is null) keyword = EvaluateClosedDefault(factory, argument, scope, ref index);
            else scope.CheckExecution(instructions[index++].Span);
            scope.CheckExecution(instructions[index++].Span);
            defaults = defaults with { Keywords = RuntimeValue(keywordCapture ?? keyword.Object) };
            var span = instructions[index].Span;
            scope.CheckExecution(span);
            var result = definition.EagerAnnotationReads is { } reads
                ? CreateClosedEagerDefinition(definition, factory, reads, scope, span, defaults)
                : CreateFunctionValue(definition.ParameterLayout, definition.CodeObject, GetGlobalContext(scope),
                    default, defaults, false, scope, span, postponedAnnotations: definition.PostponedAnnotations);
            defaults = default;
            return result;
        }
        finally { defaults.RefundUnpublished(scope.MemoryGovernor); }
    }

    private static ExecutableSlot EvaluateClosedDefault(ExecutableCodeObject factory, long argument,
        ExecutionContext scope, ref int index)
    {
        var result = default(ExecutableSlot);
        while (factory.Instructions[index].OpCode == ExecutableOpCode.Binary)
        {
            ref readonly var instruction = ref factory.Instructions[index++];
            scope.CheckExecution(instruction.Span);
            if (TryReadInteger(instruction.InputStart, in result, out var a) &&
                TryReadInteger(instruction.SecondInput, in result, out var b) &&
                TryMachineBinary(instruction.BinaryOperator, a, b, out var integer))
                result.SetInteger(integer);
            else result.Object = EvaluateBinaryOperator(instruction.BinaryOperator,
                ReadObject(instruction.InputStart, result), ReadObject(instruction.SecondInput, result), scope, instruction.Span);
        }
        return result;

        bool TryReadInteger(int source, in ExecutableSlot prior, out long value)
        {
            if (source == 0)
            {
                value = argument;
                return true;
            }
            return source < 0 ? TryMachineInteger(factory.Constants[~source], out value) : prior.TryGetInteger(out value);
        }

        object ReadObject(int source, ExecutableSlot prior) => source == 0 ? (BigInteger)argument : source < 0
            ? factory.Constants[~source]! : prior.Object!;
    }

    // Keep the eager-only annotation owner out of the default producer's frame.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static PyFunctionBase CreateClosedEagerDefinition(ExecutableCreateFunction definition,
        ExecutableCodeObject factory, ExecutableInstruction[] reads, ExecutionContext scope, LythonSourceSpan span,
        FunctionDefaults defaults)
    {
        var prepared = ReadEagerFunctionAnnotations(reads, factory, definition.CodeObject, scope, span);
        try
        {
            var function = CreateFunctionValue(definition.ParameterLayout, definition.CodeObject, GetGlobalContext(scope),
                default, defaults, false, scope, span, postponedAnnotations: definition.PostponedAnnotations, preparedAnnotations: prepared);
            prepared = default;
            return function;
        }
        finally { RefundEagerFunctionAnnotations(prepared, scope); }
    }

    private static bool CanDefineLexically(ExecutableCodeObject code)
        => code.UsesOnlyLexicalSlots && !code.ScopeFacts.RequiresClassCell;

    private static bool NeedsDefinitionContext(ExecutableCreateFunction operation, ExecutionContext scope)
        => operation.DecoratorCount != 0 || operation.DeferredAnnotations.Length != 0 ||
            operation.TypeParameters is not null || HasTypeParameterScope(scope);

    // Inline and array-backed lexical calls have the same owner and operand
    // order. Only the caller retires the storage the operands came from.
    private static PyFunctionBase CreateLexicalFunctionValue(FunctionParameterLayout layout, ExecutableCodeObject code,
        ExecutableCaptures cells, bool generator, ExecutionContext scope, LythonSourceSpan span,
        Span<ExecutableSlot> operands, PyString[]? postponedAnnotations = null,
        PreparedFunctionAnnotations preparedAnnotations = default, bool directKeywordDefault = false,
        Span<ExecutableSlot> uncapturedLocal = default)
    {
        var firstAnnotation = layout.DefaultStorageCount;
        var defaults = operands[..firstAnnotation];
        var function = CreateFunctionValue(layout, code, GetGlobalContext(scope), cells,
            ReadExecutableDefaults(layout, defaults, directKeywordDefault), generator, scope, span,
            operands[firstAnnotation..], postponedAnnotations, preparedAnnotations, directKeywordDefault, uncapturedLocal);
        defaults.Clear();
        return function;
    }

    private static bool CanDefineWithoutInvocation(ExecutableCodeObject code, ExecutableCodeObject parentCode,
        ExecutableCaptures parentCells, ExecutionContext scope, LythonSourceSpan span, out int[]? slots)
    {
        slots = null;
        return CanDefineLexically(code) && (code.ClosureNames.Count == 0 ||
            TryGetStandaloneCaptures(code, parentCode, parentCells, scope, span, out slots));
    }

    private static bool TryGetStandaloneCaptures(ExecutableCodeObject code, ExecutableCodeObject parentCode,
        ExecutableCaptures parentCells, ExecutionContext scope, LythonSourceSpan span, out int[]? slots)
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
            if (slot < 0 && parentCells.GetCell(~slot) is not { HasNamespaceOwner: false })
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
            if (MirrorsExecutableLocals || MirrorLocalStores)
                SyncExecutableLocalsFromContext(_execution.Code, _execution.Locals, this);
        }

        private static void CreateBorrowedExecutableFunction(ref ExecutableWorkState execution,
            ExecutionContext scope, ExecutableCreateFunction operation, in ExecutableInstruction instruction, int[]? captures)
        {
            var span = instruction.Span;
            var first = instruction.InputStart;
            PyFunctionBase function;
            PreparedFunctionAnnotations preparedAnnotations = default;
            try
            {
                if (operation.EagerAnnotationReads is { } reads)
                    preparedAnnotations = ReadEagerFunctionAnnotations(reads, execution.Code, operation.CodeObject, scope, span);
                var cells = CaptureExecutableCells(operation.CodeObject, scope, span, ref execution, captures);
                function = CreateLexicalFunctionValue(operation.ParameterLayout, operation.CodeObject, cells, operation.IsGenerator,
                    scope, span, execution.Stack.Slice(first, operation.InputCount).Span,
                    operation.PostponedAnnotations, preparedAnnotations, operation.DirectKeywordDefault);
                preparedAnnotations = default;
            }
            finally
            {
                try { RefundEagerFunctionAnnotations(preparedAnnotations, scope); }
                finally { execution.Stack.RetireTo(first); }
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
            PreparedFunctionAnnotations preparedAnnotations = default;
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
                function = CreateFunctionValue(layout, code, closure, cells,
                    ReadExecutableDefaults(layout, defaults, operation.DirectKeywordDefault),
                    operation.IsGenerator, context, span, annotations: annotations,
                    postponedAnnotations: operation.PostponedAnnotations, preparedAnnotations: preparedAnnotations,
                    directKeywordDefault: operation.DirectKeywordDefault);
                preparedAnnotations = default;
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
