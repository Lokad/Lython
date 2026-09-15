using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;
using System.Text.RegularExpressions;
using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static Dictionary<string, object> BindFunctionArguments(
        CallArgumentValue[] arguments,
        LythonSourceSpan span,
        FunctionBindingPlan plan,
        ExecutionContext context)
    {
        context.State.NoteBoundCall();
        var pool = context.State.CallTemporaries;
        // Exact upper bound of bound entries (each parameter binds once, plus the
        // variadic names): capacity is only a hint, but the exact size avoids both
        // upfront waste and growth resizes on the common arities.
        var bound = new Dictionary<string, object>(plan.PositionalParameters.Count + plan.KeywordOnlyParameters.Count + (plan.VariadicList is null ? 0 : 1) + (plan.VariadicDictionary is null ? 0 : 1), StringComparer.Ordinal);
        // The overflow list is scratch: most calls never spill positionals,
        // so materialize it only on the first spill, tracked in the call pool
        // invocation for a list that is dropped before return.
        PyList? extraPositional = null;
        // Keyword overflow likewise materializes only for **kwargs functions
        // receiving unknown names; every other call skips the dictionary.
        Dictionary<string, object>? extraKeywords = null;
        var positionalIndex = 0;

        foreach (var argument in arguments)
        {
            if (argument.IsPositional)
            {
                if (positionalIndex >= plan.PositionalParameters.Count)
                {
                    if (plan.VariadicList is null)
                    {
                        throw CallErrors.TooManyPositional(plan.CallableKind, plan.CallableName, span);
                    }

                    if (extraPositional is null)
                    {
                        extraPositional = new PyList([], context.MemoryGovernor, span);
                        pool.TrackMutable(extraPositional, extraPositional.CommittedStorageBytes);
                    }

                    extraPositional.Add(argument.Value);
                    continue;
                }

                var parameter = plan.PositionalParameters[positionalIndex++];
                bound[parameter.Name] = argument.Value;
                continue;
            }

            var keywordName = argument.KeywordName;
            if (!plan.NamedParameters.TryGetValue(keywordName, out var named))
            {
                if (plan.VariadicDictionary is null)
                {
                    throw CallErrors.UnexpectedKeyword(plan.CallableKind, plan.CallableName, keywordName, span);
                }

                extraKeywords ??= new Dictionary<string, object>(StringComparer.Ordinal);
                if (!extraKeywords.TryAdd(keywordName, argument.Value))
                {
                    throw CallErrors.MultipleValues(plan.CallableKind, plan.CallableName, keywordName, span);
                }

                continue;
            }

            if (!bound.TryAdd(named.Name, argument.Value))
            {
                throw CallErrors.MultipleValues(plan.CallableKind, plan.CallableName, keywordName, span);
            }
        }

        foreach (var parameter in plan.PositionalParameters)
        {
            if (bound.ContainsKey(parameter.Name))
            {
                continue;
            }

            if (plan.DefaultValues.TryGetValue(parameter.Name, out var defaultValue))
            {
                bound[parameter.Name] = defaultValue;
                continue;
            }

            throw CallErrors.MissingArgument(plan.CallableKind, plan.CallableName, parameter.Name, span);
        }

        foreach (var parameter in plan.KeywordOnlyParameters)
        {
            if (bound.ContainsKey(parameter.Name))
            {
                continue;
            }

            if (plan.DefaultValues.TryGetValue(parameter.Name, out var defaultValue))
            {
                bound[parameter.Name] = defaultValue;
                continue;
            }

            throw CallErrors.MissingArgument(plan.CallableKind, plan.CallableName, parameter.Name, span);
        }

        if (plan.VariadicList is not null)
        {
            var overflow = extraPositional;
            if (overflow is null || overflow.Count == 0)
            {
                bound[plan.VariadicList.Name] = CreateTuple(0, _ => PyNone.Instance, context, span);
            }
            else
            {
                ChargeReclamationPool.NotifyStorageReplaced(overflow, overflow.CommittedStorageBytes);
                var overflowTuple = CreateTuple(overflow.Count, i => overflow[i], context, span);
                pool.TrackMutable(overflowTuple, overflowTuple.CommittedStorageBytes);
                bound[plan.VariadicList.Name] = overflowTuple;
            }
        }

        if (plan.VariadicDictionary is not null)
        {
            var keywordDict = new PyDict(context.MemoryGovernor, span);
            pool.TrackMutable(keywordDict, keywordDict.CommittedStorageBytes);
            if (extraKeywords is not null)
            {
                foreach (var pair in extraKeywords)
                {
                    var keyword = PyString.FromString(pair.Key, context.MemoryGovernor, span);
                    pool.TrackString(keyword);
                    keywordDict.SetItem(keyword, pair.Value);
                }
            }

            ChargeReclamationPool.NotifyStorageReplaced(keywordDict, keywordDict.CommittedStorageBytes);
            bound[plan.VariadicDictionary.Name] = keywordDict;
        }

        return bound;
    }

    // Constructed function objects retain a wrapper plus binding plan and
    // default map per evaluation. Defs and lambdas execute per evaluation,
    // so each constructed value charges once built.
    private const long FunctionValueBytes = 128;

    // Constructed class objects retain a type record plus one namespace slot per
    // member; charge the base plus per-member slots once built. Member values
    // stay owned by their own construction.
    private const long ClassTypeBaseBytes = 128;
    private const long ClassMemberSlotBytes = 64;

    internal static void ChargeClassTypeValue(int memberCount, MemoryGovernor governor, LythonSourceSpan? span)
    {
        var bytes = checked(ClassTypeBaseBytes + ClassMemberSlotBytes * (long)memberCount);
        governor.Reserve(bytes, span);
        governor.Commit(bytes);
    }

    private static void ChargeFunctionValue(ExecutionContext? context, LythonSourceSpan? span)
    {
        context?.MemoryGovernor.Reserve(FunctionValueBytes, span);
        context?.MemoryGovernor.Commit(FunctionValueBytes);
    }

    // MG11: default-argument maps survive with the function value. The 128B
    // constructed-value unit covers the wrapper, binding plan and an empty map,
    // so each defaulted parameter owns one 64B table slot beside it.
    private const long DefaultArgumentSlotBytes = 64;

    internal static void ChargeDefaultArguments(int defaultCount, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        if (defaultCount <= 0 || governor is null)
        {
            return;
        }

        var bytes = checked(DefaultArgumentSlotBytes * (long)defaultCount);
        governor.Reserve(bytes, span);
        governor.Commit(bytes);
    }

    // A function value retains its defining context chain for as long as the
    // function is retained: intermediate invocation contexts plus the variable
    // tables mirroring their locals stay alive through the closure, so each
    // executed def or lambda owns the so-far-unowned storage of every context
    // it retains, module frames included (imported modules keep per-module
    // scopes alive through their functions). Shared frames pay once (first-wins
    // aliasing), with later definitions paying only for variables bound since.
    // The function CLR wrapper itself stays MG04-owned.
    private const long ClosureContextBaseBytes = 512;
    private const long ClosureCellSlotBytes = 32;

    internal static void ChargeClosureRetention(ExecutionContext? closure, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        if (closure is null || governor is null)
        {
            return;
        }

        for (var current = closure; current is not null; current = current.ParentContext)
        {
            var owned = current.Frame.Parent is null
                ? CountOwnedModuleVariables(current)
                : current.Frame.Variables.Count;

            if (!current.ClosureRetentionCharged)
            {
                var bytes = checked(ClosureContextBaseBytes + ClosureCellSlotBytes * (long)owned);
                governor.Reserve(bytes, span);
                governor.Commit(bytes);
                current.ClosureRetentionCharged = true;
                current.ClosureChargedVariableCount = owned;
                continue;
            }

            var uncharged = owned - current.ClosureChargedVariableCount;
            if (uncharged > 0)
            {
                var delta = checked(ClosureCellSlotBytes * (long)uncharged);
                governor.Reserve(delta, span);
                governor.Commit(delta);
                current.ClosureChargedVariableCount = owned;
            }

            // A paid context implies paid ancestors: every walk covers the whole
            // chain and chains only grow at the leaf, so the walk ends here.
            break;
        }
    }

    // Module frames start as copies of the run builtins table; those aliases
    // stay owned by the run, so only genuinely module-owned entries count here.
    // Shadowing assignments replace the shared reference and count normally.
    private static int CountOwnedModuleVariables(ExecutionContext context)
    {
        var builtins = context.Services.State.BuiltinVariables;
        var owned = 0;
        foreach (var pair in context.Frame.Variables)
        {
            if (!builtins.TryGetValue(pair.Key, out var shared) || !ReferenceEquals(shared, pair.Value))
            {
                owned++;
            }
        }

        return owned;
    }

    // MG22: imported definitions retain their lowered body graphs (lowered
    // nodes plus syntax and constant nodes) for as long as the defined value
    // is retained, even when bodies never execute. Module slots, scope tables
    // and source text do not cover that graph, so each module owns its
    // deferred code once at import preparation: deep syntax-node counts over
    // every retained function, class-nested, lambda and generator body. Re-imports hit
    // the registry and pay nothing, nested bodies pay once inside their outer
    // walk instead of again at execution, and host-owned entry scripts never
    // flow through import preparation, so no name check exists to spoof.
    // Top-level executed statements are transient and stay owned through
    // their values; conditional definitions over-count conservatively.
    private const long RetainedSyntaxNodeBytes = 64;

    internal static void ChargeDeferredModuleCode(
        IReadOnlyList<StatementSyntax> statements,
        MemoryGovernor? governor,
        LythonSourceSpan? span)
    {
        if (statements.Count == 0 || governor is null)
        {
            return;
        }

        var bytes = checked(CountDeferredModuleCode(statements) * RetainedSyntaxNodeBytes);
        governor.Reserve(bytes, span);
        governor.Commit(bytes);
    }

    // Counts deferred bodies in one module pass: definitions count their full
    // subtree once (nested definitions included); compounds, classes and plain
    // statements contribute only lambdas found in evaluated positions, since
    // their own statements execute transiently.
    internal static long CountDeferredModuleCode(IReadOnlyList<StatementSyntax> statements)
    {
        var count = 0L;
        var search = new Stack<StatementSyntax>();
        for (var i = statements.Count - 1; i >= 0; i--)
        {
            search.Push(statements[i]);
        }

        while (search.Count > 0)
        {
            var statement = search.Pop();
            if (statement is FunctionDefinitionStatementSyntax)
            {
                count += CountSyntaxSubtree(statement);
                continue;
            }

            foreach (var body in StatementSyntaxTraversal.EnumerateChildBodies(statement))
            {
                for (var i = body.Count - 1; i >= 0; i--)
                {
                    search.Push(body[i]);
                }
            }

            foreach (var expression in StatementSyntaxTraversal.EnumerateDirectExpressions(statement))
            {
                count += CountDeferredRoots(expression);
            }

            count += CountExtraStatementDeferredRoots(statement);
        }

        return count;
    }

    // Counts deferred roots in evaluated positions: each outermost lambda or generator
    // expression owns its full subtree once (parameters and clauses included);
    // anything else there is transient (eager comprehension scaffolding included).
    // A live generator retains its clauses and item expression like a function
    // retains its body, so generators are roots exactly like lambdas.
    private static long CountDeferredRoots(ExpressionSyntax root)
    {
        var count = 0L;
        var pending = new Stack<ExpressionSyntax>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var expression = pending.Pop();
            if (expression is LambdaExpressionSyntax or GeneratorExpressionSyntax)
            {
                count += CountSyntaxSubtree(expression);
                continue;
            }

            foreach (var child in ExpressionSyntaxTraversal.EnumerateChildren(expression))
            {
                pending.Push(child);
            }
        }

        return count;
    }

    // Deferred-root search inside the retained references the shared traversals
    // skip: unpacking receivers, chained targets and match patterns (whose own
    // nodes count in full walks). Each is a small closed set; unknown shapes
    // fail loud below.
    private static long CountExtraStatementDeferredRoots(StatementSyntax statement)
    {
        var count = 0L;
        foreach (var expression in EnumerateExtraStatementExpressions(statement))
        {
            count += CountDeferredRoots(expression);
        }

        if (statement is MatchStatementSyntax matchStatement)
        {
            foreach (var matchCase in matchStatement.Cases)
            {
                count += CountDeferredRootsInPattern(matchCase.Pattern);
            }
        }

        return count;
    }

    // Retained expression references beyond the shared direct-expression
    // traversal: unpacking receivers and chained targets. Annotated,
    // augmented, subscript, slice and member targets ride the traversal.
    private static IEnumerable<ExpressionSyntax> EnumerateExtraStatementExpressions(StatementSyntax statement)
    {
        switch (statement)
        {
            case UnpackingAssignmentStatementSyntax unpacking:
                foreach (var target in unpacking.Targets)
                {
                    foreach (var expression in EnumerateUnpackingTargetExpressions(target))
                    {
                        yield return expression;
                    }
                }

                break;
            case ChainedAssignmentStatementSyntax chained:
                foreach (var target in chained.Targets)
                {
                    foreach (var expression in EnumerateAssignmentTargetExpressions(target))
                    {
                        yield return expression;
                    }
                }

                break;
            default:
                break;
        }
    }

    private static IEnumerable<ExpressionSyntax> EnumerateAssignmentTargetExpressions(AssignmentTargetSyntax target)
    {
        var pending = new Stack<AssignmentTargetSyntax>();
        pending.Push(target);
        while (pending.Count > 0)
        {
            switch (pending.Pop())
            {
                case NameAssignmentTargetSyntax:
                    break;
                case SubscriptAssignmentTargetSyntax subscript:
                    yield return subscript.Target;
                    yield return subscript.Index;
                    break;
                case SliceAssignmentTargetSyntax slice:
                    yield return slice.Target;
                    if (slice.Start is not null)
                    {
                        yield return slice.Start;
                    }

                    if (slice.End is not null)
                    {
                        yield return slice.End;
                    }

                    if (slice.Step is not null)
                    {
                        yield return slice.Step;
                    }

                    break;
                case MemberAssignmentTargetSyntax member:
                    yield return member.Target;
                    break;
                case UnpackingAssignmentTargetGroupSyntax group:
                    foreach (var nested in group.Targets)
                    {
                        foreach (var expression in EnumerateUnpackingTargetExpressions(nested))
                        {
                            yield return expression;
                        }
                    }

                    break;
                default:
                    throw new InvalidOperationException($"Unknown assignment target: {target.GetType().Name}");
            }
        }
    }

    private static IEnumerable<ExpressionSyntax> EnumerateUnpackingTargetExpressions(UnpackingTargetSyntax target)
    {
        switch (target)
        {
            case UnpackingNameTargetSyntax:
                break;
            case UnpackingSubscriptTargetSyntax subscript:
                yield return subscript.Target;
                yield return subscript.Index;
                break;
            case UnpackingSliceTargetSyntax slice:
                yield return slice.Target;
                if (slice.Start is not null)
                {
                    yield return slice.Start;
                }

                if (slice.End is not null)
                {
                    yield return slice.End;
                }

                if (slice.Step is not null)
                {
                    yield return slice.Step;
                }

                break;
            case UnpackingMemberTargetSyntax member:
                yield return member.Target;
                break;
            case UnpackingNestedTargetSyntax nested:
                foreach (var item in nested.Items)
                {
                    foreach (var expression in EnumerateUnpackingTargetExpressions(item))
                    {
                        yield return expression;
                    }
                }

                break;
            default:
                throw new InvalidOperationException($"Unknown unpacking target: {target.GetType().Name}");
        }
    }

    // Deferred-root search inside match patterns, whose nodes count only in full
    // walks. Embedded expressions route through the same root scan.
    private static long CountDeferredRootsInPattern(PatternSyntax root)
    {
        var count = 0L;
        var pending = new Stack<PatternSyntax>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var pattern = pending.Pop();
            foreach (var expression in EnumeratePatternExpressions(pattern))
            {
                count += CountDeferredRoots(expression);
            }

            foreach (var nested in EnumeratePatternSubpatterns(pattern))
            {
                pending.Push(nested);
            }
        }

        return count;
    }

    private static IEnumerable<ExpressionSyntax> EnumeratePatternExpressions(PatternSyntax pattern)
    {
        switch (pattern)
        {
            case MatchValuePatternSyntax value:
                yield return value.Expression;
                break;
            case MatchMappingPatternSyntax mapping:
                foreach (var item in mapping.Items)
                {
                    yield return item.Key;
                }

                break;
            case MatchClassPatternSyntax @class:
                yield return @class.ClassExpression;
                break;
            default:
                break;
        }
    }

    private static IEnumerable<PatternSyntax> EnumeratePatternSubpatterns(PatternSyntax pattern)
    {
        switch (pattern)
        {
            case MatchSequencePatternSyntax sequence:
                foreach (var item in sequence.Items)
                {
                    yield return item;
                }

                break;
            case MatchMappingPatternSyntax mapping:
                foreach (var item in mapping.Items)
                {
                    yield return item.Pattern;
                }

                break;
            case MatchClassPatternSyntax @class:
                foreach (var positional in @class.PositionalPatterns)
                {
                    yield return positional;
                }

                foreach (var keyword in @class.KeywordPatterns)
                {
                    yield return keyword.Pattern;
                }

                break;
            case MatchAsPatternSyntax @as:
                yield return @as.Pattern;
                break;
            case MatchOrPatternSyntax or:
                foreach (var alternative in or.Patterns)
                {
                    yield return alternative;
                }

                break;
            case MatchValuePatternSyntax:
            case MatchSingletonPatternSyntax:
            case MatchCapturePatternSyntax:
            case MatchWildcardPatternSyntax:
            case MatchStarPatternSyntax:
                break;
            default:
                throw new InvalidOperationException($"Unknown match pattern: {pattern.GetType().Name}");
        }
    }

    // Full retained-node count over one deferred subtree: every statement,
    // expression, pattern and target reference the lowered graph keeps alive
    // through the defined value. Single pass, iterative for deep nesting.
    // Full retained-node count over one deferred subtree: every statement,
    // expression, pattern and target reference the lowered graph keeps alive
    // through the defined value. Single pass, iterative for deep nesting;
    // unknown shapes fail loud instead of silently undercounting.
    internal static long CountSyntaxSubtree(StatementSyntax root)
        => CountSyntaxNodes(root);

    internal static long CountSyntaxSubtree(ExpressionSyntax root)
        => CountSyntaxNodes(root);

    private static long CountSyntaxNodes(object root)
    {
        var count = 0L;
        var pending = new Stack<object>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            switch (pending.Pop())
            {
                case StatementSyntax statement:
                    count++;
                    PushStatementChildren(statement, pending);
                    break;
                case ExpressionSyntax expression:
                    count++;
                    PushExpressionChildren(expression, pending);
                    break;
                case PatternSyntax pattern:
                    count++;
                    PushPatternChildren(pattern, pending);
                    break;
                default:
                    throw new InvalidOperationException("Unknown retained syntax node.");
            }
        }

        return count;
    }

    private static void PushStatementChildren(StatementSyntax statement, Stack<object> pending)
    {
        foreach (var expression in StatementSyntaxTraversal.EnumerateDirectExpressions(statement))
        {
            pending.Push(expression);
        }

        foreach (var expression in EnumerateExtraStatementExpressions(statement))
        {
            pending.Push(expression);
        }

        if (statement is MatchStatementSyntax matchStatement)
        {
            foreach (var matchCase in matchStatement.Cases)
            {
                pending.Push(matchCase.Pattern);
            }
        }

        foreach (var body in StatementSyntaxTraversal.EnumerateChildBodies(statement))
        {
            foreach (var nested in body)
            {
                pending.Push(nested);
            }
        }
    }

    private static void PushExpressionChildren(ExpressionSyntax expression, Stack<object> pending)
    {
        if (expression is LambdaExpressionSyntax lambda)
        {
            foreach (var parameter in lambda.Parameters)
            {
                if (parameter.Annotation is not null)
                {
                    pending.Push(parameter.Annotation);
                }

                if (parameter.DefaultValue is not null)
                {
                    pending.Push(parameter.DefaultValue);
                }
            }
        }

        foreach (var child in ExpressionSyntaxTraversal.EnumerateChildren(expression))
        {
            pending.Push(child);
        }
    }

    private static void PushPatternChildren(PatternSyntax pattern, Stack<object> pending)
    {
        foreach (var expression in EnumeratePatternExpressions(pattern))
        {
            pending.Push(expression);
        }

        foreach (var nested in EnumeratePatternSubpatterns(pattern))
        {
            pending.Push(nested);
        }
    }

    internal static Dictionary<string, object> BuildDefaultArgumentMap(
        IReadOnlyList<LoweredFunctionParameter> parameters,
        Func<LoweredExpression, object> evaluate)
    {
        var defaults = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var parameter in parameters)
        {
            if (parameter.DefaultValue is not null)
            {
                defaults[parameter.Name] = RuntimeValue(evaluate(parameter.DefaultValue));
            }
        }

        return defaults;
    }

    internal static async ValueTask<Dictionary<string, object>> BuildDefaultArgumentMapAsync(
        IReadOnlyList<LoweredFunctionParameter> parameters,
        Func<LoweredExpression, ValueTask<object>> evaluate)
    {
        var defaults = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var parameter in parameters)
        {
            if (parameter.DefaultValue is not null)
            {
                defaults[parameter.Name] = RuntimeValue(await evaluate(parameter.DefaultValue).ConfigureAwait(false));
            }
        }

        return defaults;
    }

    private sealed class LambdaFunction : ICallable, IPyDynamicAttributes
    {
        // The lambda name is fixed vocabulary shared across all instances,
        // so reads alias stably like CPython instead of rebuilding per read.
        private static readonly PyString LambdaName = PyString.FromString("<lambda>");

        private readonly LoweredExpression _body;
        private readonly FunctionBindingPlan _bindingPlan;
        private readonly ExecutionContext _closure;

        public LambdaFunction(IReadOnlyList<LoweredFunctionParameter> parameters, LoweredExpression body, ExecutionContext closure, Dictionary<string, object> defaultValues)
        {
            _body = body;
            _closure = closure;
            _bindingPlan = new FunctionBindingPlan("<lambda>", PythonCallableKind.Lambda, parameters, defaultValues);
        }

        // Lambdas report CPython-style names from their binding plan and defining
        // scope: <lambda>, the nested qualname and the defining module.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name == "__name__")
            {
                value = LambdaName;
                return true;
            }

            if (name == "__qualname__")
            {
                value = PyFunctionBinding.EnclosingFunctionPath(_closure) is { } path
                    ? PyString.FromString(path + ".<locals>.<lambda>")
                    : LambdaName;
                return true;
            }

            // Lambdas never carry docstrings, so __doc__ reports None like CPython.
            if (name == "__doc__")
            {
                value = PyNone.Instance;
                return true;
            }

            if (name == "__module__" && PyFunctionBase.TryGetModuleName(_closure, out var moduleName))
            {
                value = moduleName;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var boundArguments = BindFunctionArguments(arguments, span, _bindingPlan, context);

            var frame = PyFunctionBinding.EnterInvocationFrame(
                _closure,
                ScopeDirectiveFacts.Empty,
                _bindingPlan,
                boundArguments,
                mirrorBoundArguments: true,
                ownerType: null,
                span);
            try
            {
                return EvaluateLoweredExpression(_body, frame);
            }
            catch (LythonRuntimeException ex)
            {
                PyFunctionBinding.AnnotateException(ex, frame, "<lambda>", span);
                throw;
            }
            finally
            {
                frame.LeaveFunctionCall();
            }
        }

        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var boundArguments = BindFunctionArguments(arguments, span, _bindingPlan, context);

            var frame = PyFunctionBinding.EnterInvocationFrame(
                _closure,
                ScopeDirectiveFacts.Empty,
                _bindingPlan,
                boundArguments,
                mirrorBoundArguments: true,
                ownerType: null,
                span);
            try
            {
                return await EvaluateLoweredExpressionAsync(_body, frame).ConfigureAwait(false);
            }
            catch (LythonRuntimeException ex)
            {
                PyFunctionBinding.AnnotateException(ex, frame, "<lambda>", span);
                throw;
            }
            finally
            {
                frame.LeaveFunctionCall();
            }
        }
    }

    internal sealed class ExceptionTypeValue : ICallable, IPyDynamicAttributes, IPyRenderableValue, IPythonExceptionType, IEquatable<ExceptionTypeValue>
    {
        // Builtin module labels form a fixed vocabulary (the exception modules
        // plus every top-level module owning builtin callables), so every known
        // module shares one constant forever and per-access __module__ reads alias
        // stably like CPython; nested or unknown module names keep building
        // fresh labels.
        private static readonly PyString BuiltinsModuleName = PyString.FromString("builtins");
        private static readonly PyString ArgparseModuleName = PyString.FromString("argparse");
        private static readonly PyString CopyModuleName = PyString.FromString("copy");
        private static readonly PyString CollectionsModuleName = PyString.FromString("collections");
        private static readonly PyString CsvModuleName = PyString.FromString("csv");
        private static readonly PyString DataclassesModuleName = PyString.FromString("dataclasses");
        private static readonly PyString DatetimeModuleName = PyString.FromString("datetime");
        private static readonly PyString DecimalModuleName = PyString.FromString("decimal");
        private static readonly PyString DifflibModuleName = PyString.FromString("difflib");
        private static readonly PyString FilecmpModuleName = PyString.FromString("filecmp");
        private static readonly PyString FnmatchModuleName = PyString.FromString("fnmatch");
        private static readonly PyString FunctoolsModuleName = PyString.FromString("functools");
        private static readonly PyString GlobModuleName = PyString.FromString("glob");
        private static readonly PyString GzipModuleName = PyString.FromString("gzip");
        private static readonly PyString HashlibModuleName = PyString.FromString("hashlib");
        private static readonly PyString ImportlibModuleName = PyString.FromString("importlib");
        private static readonly PyString ItertoolsModuleName = PyString.FromString("itertools");
        private static readonly PyString JsonModuleName = PyString.FromString("json");
        private static readonly PyString MathModuleName = PyString.FromString("math");
        private static readonly PyString OpenPyxlExceptionsModuleName = PyString.FromString("openpyxl.utils.exceptions");
        private static readonly PyString OperatorModuleName = PyString.FromString("operator");
        private static readonly PyString OsModuleName = PyString.FromString("os");
        private static readonly PyString PathlibModuleName = PyString.FromString("pathlib");
        private static readonly PyString PkgutilModuleName = PyString.FromString("pkgutil");
        private static readonly PyString RandomModuleName = PyString.FromString("random");
        private static readonly PyString ReModuleName = PyString.FromString("re");
        private static readonly PyString ShlexModuleName = PyString.FromString("shlex");
        private static readonly PyString ShutilModuleName = PyString.FromString("shutil");
        private static readonly PyString StatisticsModuleName = PyString.FromString("statistics");
        private static readonly PyString SysModuleName = PyString.FromString("sys");
        private static readonly PyString TimeModuleName = PyString.FromString("time");
        private static readonly PyString TypingModuleName = PyString.FromString("typing");
        private static readonly PyString SubprocessModuleName = PyString.FromString("subprocess");
        private static readonly PyString ZipfileModuleName = PyString.FromString("zipfile");

        internal static PyString SharedModuleLabel(string moduleName) => moduleName switch
        {
            "builtins" => BuiltinsModuleName,
            "argparse" => ArgparseModuleName,
            "copy" => CopyModuleName,
            "collections" => CollectionsModuleName,
            "csv" => CsvModuleName,
            "dataclasses" => DataclassesModuleName,
            "datetime" => DatetimeModuleName,
            "decimal" => DecimalModuleName,
            "difflib" => DifflibModuleName,
            "filecmp" => FilecmpModuleName,
            "fnmatch" => FnmatchModuleName,
            "functools" => FunctoolsModuleName,
            "glob" => GlobModuleName,
            "gzip" => GzipModuleName,
            "hashlib" => HashlibModuleName,
            "importlib" => ImportlibModuleName,
            "itertools" => ItertoolsModuleName,
            "json" => JsonModuleName,
            "math" => MathModuleName,
            "openpyxl.utils.exceptions" => OpenPyxlExceptionsModuleName,
            "operator" => OperatorModuleName,
            "os" => OsModuleName,
            "pathlib" => PathlibModuleName,
            "pkgutil" => PkgutilModuleName,
            "random" => RandomModuleName,
            "re" => ReModuleName,
            "shlex" => ShlexModuleName,
            "shutil" => ShutilModuleName,
            "statistics" => StatisticsModuleName,
            "sys" => SysModuleName,
            "time" => TimeModuleName,
            "typing" => TypingModuleName,
            "subprocess" => SubprocessModuleName,
            "zipfile" => ZipfileModuleName,
            _ => PyString.FromString(moduleName),
        };

        public ExceptionTypeValue(string typeName)
            : this(PythonExceptionIdentity.Builtin(typeName))
        {
        }

        public ExceptionTypeValue(PythonExceptionIdentity identity)
        {
            ExceptionIdentity = identity;
        }

        public PythonExceptionIdentity ExceptionIdentity { get; }

        public string TypeName => ExceptionIdentity.TypeName;

        internal TypeNewMethod? NewSlot { get; set; }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "__name__" => PyString.FromString(TypeName),
                "__module__" => SharedModuleLabel(ExceptionIdentity.ModuleName),
                "type" => PyString.FromString(TypeName),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<class '" + ExceptionIdentity.QualifiedName + "'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public bool Equals(ExceptionTypeValue? other)
            => other is not null && ExceptionIdentity == other.ExceptionIdentity;

        public override bool Equals(object? obj) => obj is ExceptionTypeValue other && Equals(other);

        public override int GetHashCode() => ExceptionIdentity.GetHashCode();

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            for (var i = 0; i < arguments.Length; i++)
            {
                if (arguments[i].IsKeyword)
                {
                    throw new LythonRuntimeException("TypeError", $"{TypeName}(message) does not accept keyword arguments.", span);
                }
            }

            if (TypeName == "SystemExit")
            {
                if (arguments.Length > 1)
                {
                    throw new LythonRuntimeException("TypeError", "SystemExit([code]) expects zero or one argument.", span);
                }

                var value = arguments.Length == 0 ? PyNone.Instance : arguments[0].Value;
                return new PyException(ExceptionIdentity, FormatSystemExitMessage(value), value);
            }

            var values = arguments.Select(argument => argument.Value).ToArray();
            var args = new PyTuple(values, context.MemoryGovernor, span);
            var message = values.Length switch
            {
                0 => string.Empty,
                // Like CPython, a single KeyError argument renders through
                // repr instead of str; every other single argument uses str.
                1 when ExceptionIdentity.IsBuiltin && string.Equals(TypeName, "KeyError", StringComparison.Ordinal) =>
                    PyRendering.ToReprPyString(values[0], new PyRenderingContext(context)).AsString(),
                1 => PyRendering.ToInterpolatedString(values[0], new PyRenderingContext(context)),
                _ => PyRendering.ToReprPyString(args, new PyRenderingContext(context)).AsString(),
            };
            var payload = values.Length == 0 ? PyNone.Instance : values.Length == 1 ? values[0] : args;
            return new PyException(ExceptionIdentity, message, payload, args);
        }
    }

    private static string FormatSystemExitMessage(object value)
        => value switch
        {
            PyNone => string.Empty,
            BigInteger integer => integer.ToString(),
            bool boolean => boolean ? "True" : "False",
            _ when PyStringOps.TryAsString(value, out var text) => text.AsString(),
            _ => value.ToString() ?? string.Empty
        };
}
