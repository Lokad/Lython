using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static PyDict CreateDefinitionRecord<T>(ref ExecutableWorkState execution,
        ExecutionContext context, int count, int cacheIndex, T names, Func<T, int, PyString> nameAt, LythonSourceSpan span)
    {
        if (cacheIndex < 0) return new PyDict(count, context.MemoryGovernor, span);
        ref var cache = ref execution.Stack.GetLookupCaches(execution.Code.LookupCacheCount, span)[cacheIndex];
        var keys = PyDict.RecordKeys.PrepareDefinition(ref cache, count, names, nameAt, context, span);
        return keys.CreateRecord(context, span, count);
    }

    internal sealed partial class ExecutionContext
    {
        private async ValueTask ExecutePreparedClassDefinitionAsync(ExecutableCreateClass create, int first, LythonSourceSpan span, bool asynchronous)
        {
            var definition = create.Definition;
            var decoratorCount = definition.Decorators.Count;
            using var accumulator = create.ExpandedHeader
                ? (CallExpansion.CallArgumentAccumulator)_execution.Stack.TakeValue(first + decoratorCount, first + decoratorCount)
                : null;
            using var storage = MemoryGovernor.ReserveTemporary(256L + 128L * decoratorCount, span);
            var decorators = new LoweredExpression[decoratorCount];
            for (var i = decorators.Length - 1; i >= 0; i--)
                decorators[i] = new LoweredCapturedExpression(definition.Decorators[i].Syntax, _execution.Stack.TakeValue(first + i, first + i));
            definition = definition with { Decorators = decorators };
            if (accumulator is not null)
            {
                var arguments = accumulator.ToArray();
                storage.Grow(128L * arguments.Length, span);
                var headers = new LoweredCallArgument[arguments.Length];
                for (var i = 0; i < arguments.Length; i++)
                {
                    var argument = arguments[i];
                    if (argument.IsKeyword) storage.Grow(2L * argument.KeywordName.Length, span);
                    headers[i] = new LoweredCallArgument(argument.IsKeyword ? CallArgumentForm.Keyword(argument.KeywordName) : CallArgumentForm.Positional,
                        new LoweredCapturedExpression(definition.HeaderArguments[0].Expression.Syntax, argument.Value));
                }
                definition = definition with { HeaderArguments = headers };
            }
            if (asynchronous) await ExecuteLoweredClassDefinitionAsync(definition, this).ConfigureAwait(false);
            else ExecuteLoweredClassDefinition(definition, this);
            SyncExecutableLocalsFromContext(_execution.Code, _execution.Locals, this);
        }

        private static bool CanDefineWithoutInvocation(ExecutableCodeObject code)
            => code.UsesOnlyLexicalSlots && code.ClosureNames.Count == 0 && !code.ScopeFacts.RequiresClassCell;

        private static ValueTask CreateExecutableFunction(ref ExecutableWorkState execution,
            ExecutionContext context, ExecutionContext? invocation, ExecutableCreateFunction operation,
            int first, int local, bool asynchronous)
        {
            var binding = operation.Binding;
            var span = binding.Span;
            context.EnterInterpreterFrame(span);
            var transferred = false;
            try
            {
                var layout = binding.ParameterLayout;
                var endDecorators = first + binding.DecoratorCount;
                var next = endDecorators;
                PendingFunctionDefaults? defaults = null;
                if (layout.Defaults.Length != 0)
                {
                    defaults = (PendingFunctionDefaults)execution.Stack[next++];
                    foreach (var slot in layout.Defaults)
                        defaults.Add(slot, RuntimeValue(execution.Stack[next++]), span);
                    defaults.Complete();
                }

                var cells = CaptureExecutableClosures(binding.CodeObject, context, span);
                var typeScope = CreateTypeScope(binding.TypeParameters, context, span);
                var closure = invocation is null ? GetGlobalContext(context)
                    : SelectDefinitionClosure(binding.CodeObject, typeScope.Closure, cells);
                var values = defaults?.Values ?? default;
                var governor = context.MemoryGovernor;
                using var storage = governor.ReserveTemporary(FunctionValueBytes, span);
                PyFunctionBase function = binding.IsGenerator
                    ? new PyGeneratorFunction(layout, binding.CodeObject, closure, values, cells)
                    : new PyExecutableFunction(layout, binding.CodeObject, closure, cells, values);
                storage.Commit(FunctionValueBytes);
                ChargeClosureRetention(closure, governor, span);
                var metadata = ValueTask.CompletedTask;
                if (binding.Annotations.Length != 0 || binding.TypeParameters is not null || HasTypeParameterScope(context))
                {
                    var evaluated = operation.AnnotationsOnStack ? execution.Stack.Slice(next, binding.Annotations.Length) : default;
                    metadata = AttachTypeMetadataAsync(function, binding.Annotations, binding.TypeParameters is not null,
                        span, typeScope, context, asynchronous, ref execution, evaluated, binding.AnnotationRecordCache);
                }

                if (binding.DecoratorCount != 0 || !metadata.IsCompletedSuccessfully)
                {
                    var completion = invocation!.FinishExecutableFunctionAsync(binding, function, defaults, metadata, first, asynchronous);
                    transferred = true;
                    return completion;
                }

                metadata.GetAwaiter().GetResult();
                defaults?.Publish();
                if (invocation is null)
                    StoreLocalValue(execution.Code, execution.Locals, null, local, ExecutableSlot.FromObject(function), span);
                else
                    AssignExecutableBoundName(execution.Code, execution.Locals, binding.Name, function, context, span);
                return ValueTask.CompletedTask;
            }
            finally
            {
                if (!transferred) RetireExecutableDefinition(ref execution, context, first);
            }
        }

        private async ValueTask FinishExecutableFunctionAsync(ExecutableFunctionBinding binding, PyFunctionBase function,
            PendingFunctionDefaults? defaults, ValueTask metadata, int first, bool asynchronous)
        {
            var span = binding.Span;
            try
            {
                await metadata.ConfigureAwait(false);
                // Decorator callbacks can publish the original definition even
                // when they replace it or raise. Transfer the default owner first.
                defaults?.Publish();
                object decorated = function;
                for (var i = first + binding.DecoratorCount - 1; i >= first; i--)
                {
                    if (_execution.Stack[i] is not ICallable decorator)
                        throw new LythonRuntimeException("TypeError", "Decorator expression must evaluate to a callable.", span);
                    decorated = asynchronous ? await CallableInvocation.InvokeUnaryAsync(decorator, decorated, span, this).ConfigureAwait(false)
                        : CallableInvocation.InvokeUnary(decorator, decorated, span, this);
                }
                AssignExecutableBoundName(_execution.Code, _execution.Locals, binding.Name, decorated, this, span);
            }
            finally
            {
                RetireExecutableDefinition(ref _execution, this, first);
            }
        }

        private static void RetireExecutableDefinition(ref ExecutableWorkState execution, ExecutionContext context, int first)
        {
            try { execution.Stack.RetireTo(first); }
            finally { context.LeaveInterpreterFrame(); }
        }
    }
}
