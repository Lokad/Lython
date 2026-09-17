using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;
using System.Text;
using System.Text.RegularExpressions;
using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Pooled binder value slots for the common layout widths. The consumer
    // audit shows bound Values never escape alive: frames copy refs out,
    // bodies and dataclass dunders read transiently, and the struct itself
    // never leaves its Invoke. ThreadStatic plus take-null with keep-first
    // stays safe under recursion, suspension and parallel tests; slots are
    // cleared before caching (fresh arrays start null too). Binding failures
    // drop the box instead of caching it. Only the hot user-function paths
    // return boxes; colder paths simply do not cache.
    [ThreadStatic]
    private static object[]? _pooledOneBoundValue;
    [ThreadStatic]
    private static object[]? _pooledTwoBoundValues;
    [ThreadStatic]
    private static object[]? _pooledThreeBoundValues;

    private static object[] RentBoundValues(int count)
    {
        if (count == 1)
        {
            var rented = _pooledOneBoundValue;
            if (rented is not null)
            {
                _pooledOneBoundValue = null;
                return rented;
            }
            return new object[1];
        }
        if (count == 2)
        {
            var rented = _pooledTwoBoundValues;
            if (rented is not null)
            {
                _pooledTwoBoundValues = null;
                return rented;
            }
            return new object[2];
        }
        if (count == 3)
        {
            var rentedThree = _pooledThreeBoundValues;
            if (rentedThree is not null)
            {
                _pooledThreeBoundValues = null;
                return rentedThree;
            }
            return new object[3];
        }
        return new object[count];
    }

    internal static void ReturnBoundValues(object[] values)
    {
        if (values.Length == 1)
        {
            Array.Clear(values);
            _pooledOneBoundValue ??= values;
        }
        else if (values.Length == 2)
        {
            Array.Clear(values);
            _pooledTwoBoundValues ??= values;
        }
        else if (values.Length == 3)
        {
            Array.Clear(values);
            _pooledThreeBoundValues ??= values;
        }
    }

    internal static BoundCallArguments BindFunctionArguments(
        CallArgumentValue[] arguments,
        LythonSourceSpan span,
        FunctionBindingPlan plan,
        ExecutionContext context)
    {
        context.State.NoteBoundCall();
        var pool = context.State.CallTemporaries;
        // Values travel in plan layout order (positional, keyword-only, then
        // variadic names) with presence bits, instead of a per-call name
        // dictionary. The layout arrays live on the shared plan; only the value
        // slots are per-call.
        var values = plan.LayoutParameterNames.Count == 0
            ? Array.Empty<object>()
            : RentBoundValues(plan.LayoutParameterNames.Count);
        var assigned = new ArgumentPresence(plan.LayoutParameterNames.Count);
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

                values[positionalIndex] = argument.Value;
                assigned[positionalIndex] = true;
                positionalIndex++;
                continue;
            }

            var keywordName = argument.KeywordName;
            if (!plan.LayoutParameterIndex.TryGetValue(keywordName, out var keywordIndex) ||
                keywordIndex >= plan.NamedLayoutCount)
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

            if (assigned[keywordIndex])
            {
                throw CallErrors.MultipleValues(plan.CallableKind, plan.CallableName, keywordName, span);
            }

            values[keywordIndex] = argument.Value;
            assigned[keywordIndex] = true;
        }

        for (var i = 0; i < plan.PositionalParameters.Count; i++)
        {
            if (assigned[i])
            {
                continue;
            }

            var parameterName = plan.PositionalParameters[i].Name;
            if (plan.DefaultValues.TryGetValue(parameterName, out var defaultValue))
            {
                values[i] = defaultValue;
                continue;
            }

            throw CallErrors.MissingArgument(plan.CallableKind, plan.CallableName, parameterName, span);
        }

        for (var k = 0; k < plan.KeywordOnlyParameters.Count; k++)
        {
            var i = plan.PositionalParameters.Count + k;
            if (assigned[i])
            {
                continue;
            }

            var parameterName = plan.KeywordOnlyParameters[k].Name;
            if (plan.DefaultValues.TryGetValue(parameterName, out var defaultValue))
            {
                values[i] = defaultValue;
                continue;
            }

            throw CallErrors.MissingArgument(plan.CallableKind, plan.CallableName, parameterName, span);
        }

        if (plan.VariadicList is not null)
        {
            var variadicIndex = plan.LayoutParameterIndex[plan.VariadicList.Name];
            var overflow = extraPositional;
            if (overflow is null || overflow.Count == 0)
            {
                values[variadicIndex] = CreateTuple(0, _ => PyNone.Instance, context, span);
            }
            else
            {
                ChargeReclamationPool.NotifyStorageReplaced(overflow, overflow.CommittedStorageBytes);
                var overflowTuple = CreateTuple(overflow.Count, i => overflow[i], context, span);
                pool.TrackMutable(overflowTuple, overflowTuple.CommittedStorageBytes);
                values[variadicIndex] = overflowTuple;
            }

            assigned[variadicIndex] = true;
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
            var keywordDictIndex = plan.LayoutParameterIndex[plan.VariadicDictionary.Name];
            values[keywordDictIndex] = keywordDict;
            assigned[keywordDictIndex] = true;
        }

        return new BoundCallArguments(values, assigned);
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

    // Registers a freshly constructed class for its own type record, member
    // slots and governed construction shares (name copy plus bases/mro backing;
    // member values stay aliased): dropped definitions reclaim through the pool
    // once collected, and a denied registration refunds the whole coupon so the
    // failed definition strands nothing.
    internal static void TrackClassTypeValue(PyType type, int memberCount, ExecutionContext? context, LythonSourceSpan? span)
    {
        if (context is null)
        {
            return;
        }

        context.Services.State.CallTemporaries.TrackFreshMutable(
            type,
            checked(ClassTypeBaseBytes + ClassMemberSlotBytes * (long)memberCount + type.ConstructionChargeBytes));
    }

    private static void ChargeFunctionValue(ExecutionContext? context, LythonSourceSpan? span)
    {
        context?.MemoryGovernor.Reserve(FunctionValueBytes, span);
        context?.MemoryGovernor.Commit(FunctionValueBytes);
    }

    // Registers a freshly constructed function or lambda for its own value,
    // default-map, freshly retained closure-context/cell, governed name and docstring
    // charges:
    // dropped definitions reclaim through the pool once collected, and a denied
    // registration refunds the whole coupon so the failed definition strands nothing.
    // Lambdas carry the shared ungoverned name, so only def-built functions add
    // a name share.
    internal static void TrackFunctionValue(object function, int defaultCount, long retentionBytes, long docstringBytes, ExecutionContext? context, LythonSourceSpan? span)
    {
        if (context is null)
        {
            return;
        }

        var nameBytes = function is PyFunctionBase defined ? defined.NameCommittedBytes : 0;
        context.Services.State.CallTemporaries.TrackFreshMutable(
            function,
            checked(FunctionValueBytes + DefaultArgumentSlotBytes * (long)defaultCount + retentionBytes + nameBytes + docstringBytes));
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
    // The function CLR wrapper itself stays MG04-owned. Newly charged non-module
    // retention is returned so the defining site can fold it into the function pool
    // coupon: dropped definitions then release their own contexts on sweep. Module
    // frames outlive the run, so their share stays durably committed. Sibling
    // definitions alias shared frames first-wins, so a partial sibling drop can
    // transiently undercount one chain while survivors live; the charge balances
    // once the whole group drops.
    private const long ClosureContextBaseBytes = 512;
    private const long ClosureCellSlotBytes = 32;

    internal static long ChargeClosureRetention(ExecutionContext? closure, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        var retained = 0L;
        if (closure is null || governor is null)
        {
            return retained;
        }

        for (var current = closure; current is not null; current = current.ParentContext)
        {
            var isModuleFrame = current.Frame.Parent is null;
            var owned = isModuleFrame
                ? CountOwnedModuleVariables(current)
                : current.Frame.Variables.Count;

            if (!current.ClosureRetentionCharged)
            {
                var bytes = checked(ClosureContextBaseBytes + ClosureCellSlotBytes * (long)owned);
                governor.Reserve(bytes, span);
                governor.Commit(bytes);
                current.ClosureRetentionCharged = true;
                current.ClosureChargedVariableCount = owned;
                if (!isModuleFrame)
                {
                    retained = checked(retained + bytes);
                }

                continue;
            }

            var uncharged = owned - current.ClosureChargedVariableCount;
            if (uncharged > 0)
            {
                var delta = checked(ClosureCellSlotBytes * (long)uncharged);
                governor.Reserve(delta, span);
                governor.Commit(delta);
                current.ClosureChargedVariableCount = owned;
                if (!isModuleFrame)
                {
                    retained = checked(retained + delta);
                }
            }

            // A paid context implies paid ancestors: every walk covers the whole
            // chain and chains only grow at the leaf, so the walk ends here.
            break;
        }

        return retained;
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

    // Per-literal shared-cache entry (weak handle plus table storage) retained
    // beside each literal payload: one entry per lowered literal node, matching the
    // reclamation entry rate until M03 calibrates table storage precisely.
    private const long SharedLiteralEntryBytes = 64;

    // Returns the committed bytes so a failed import can release a charge that was
    // never published to the module registry; retrying then pays once, not per attempt.
    internal static long ChargeDeferredModuleCode(
        IReadOnlyList<StatementSyntax> statements,
        MemoryGovernor? governor,
        LythonSourceSpan? span)
    {
        if (statements.Count == 0 || governor is null)
        {
            return 0;
        }

        var measured = MeasureDeferredModuleCode(statements);
        var bytes = RuntimeMemoryEstimates.SaturatingAdd(checked(measured.Nodes * RetainedSyntaxNodeBytes), measured.PayloadBytes);
        governor.Reserve(bytes, span);
        governor.Commit(bytes);
        return bytes;
    }

    // Measures deferred bodies in one module pass: definitions count their full
    // subtree once (nested definitions included); compounds, classes and plain
    // statements contribute only deferred roots found in evaluated positions, since
    // their own statements execute transiently. Nodes feed the per-node rate;
    // payloads own retained literal bytes beside them.
    internal static long CountDeferredModuleCode(IReadOnlyList<StatementSyntax> statements)
        => MeasureDeferredModuleCode(statements).Nodes;

    internal static (long Nodes, long PayloadBytes) MeasureDeferredModuleCode(
        IReadOnlyList<StatementSyntax> statements)
    {
        var nodes = 0L;
        var payload = 0L;
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
                var defined = MeasureRetainedSubtree(statement);
                nodes += defined.Nodes;
                payload = RuntimeMemoryEstimates.SaturatingAdd(payload, defined.PayloadBytes);
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
                var measured = MeasureDeferredRoots(expression);
                nodes += measured.Nodes;
                payload = RuntimeMemoryEstimates.SaturatingAdd(payload, measured.PayloadBytes);
            }

            var extra = MeasureExtraStatementDeferredRoots(statement);
            nodes += extra.Nodes;
            payload = RuntimeMemoryEstimates.SaturatingAdd(payload, extra.PayloadBytes);
        }

        return (nodes, payload);
    }

    // Measures deferred roots in evaluated positions: each outermost lambda or
    // generator expression owns its full subtree once (parameters and clauses
    // included); anything else there is transient (eager comprehension
    // scaffolding included). A live generator retains its clauses and item
    // expression like a function retains its body, so generators are roots
    // exactly like lambdas.
    private static (long Nodes, long PayloadBytes) MeasureDeferredRoots(ExpressionSyntax root)
    {
        var nodes = 0L;
        var payload = 0L;
        var pending = new Stack<ExpressionSyntax>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var expression = pending.Pop();
            if (expression is LambdaExpressionSyntax or GeneratorExpressionSyntax)
            {
                var measured = MeasureRetainedSubtree(expression);
                nodes += measured.Nodes;
                payload = RuntimeMemoryEstimates.SaturatingAdd(payload, measured.PayloadBytes);
                continue;
            }

            foreach (var child in ExpressionSyntaxTraversal.EnumerateChildren(expression))
            {
                pending.Push(child);
            }
        }

        return (nodes, payload);
    }

    // Full retained-measure over one deferred subtree: node counts twin
    // CountSyntaxNodes exactly (same children, same fail-loud shapes) while
    // literal occurrences add their retained payloads beside the node rate.
    // Each occurrence is a distinct lowered literal node with its own shared
    // cache entry, so payloads charge per occurrence, not per distinct value.
    internal static (long Nodes, long PayloadBytes) MeasureRetainedSubtree(StatementSyntax root)
        => MeasureRetainedNodes(root);

    internal static (long Nodes, long PayloadBytes) MeasureRetainedSubtree(ExpressionSyntax root)
        => MeasureRetainedNodes(root);

    private static (long Nodes, long PayloadBytes) MeasureRetainedNodes(object root)
    {
        var nodes = 0L;
        var payload = 0L;
        var pending = new Stack<object>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            switch (pending.Pop())
            {
                case StatementSyntax statement:
                    nodes++;
                    PushStatementChildren(statement, pending);
                    break;
                case ExpressionSyntax expression:
                    nodes++;
                    payload = RuntimeMemoryEstimates.SaturatingAdd(payload, RetainedLiteralBytes(expression));
                    PushExpressionChildren(expression, pending);
                    break;
                case PatternSyntax pattern:
                    nodes++;
                    PushPatternChildren(pattern, pending);
                    break;
                default:
                    throw new InvalidOperationException("Unknown retained syntax node.");
            }
        }

        return (nodes, payload);
    }

    // Retained payload per literal occurrence, mirroring construction charges:
    // strings and bytes keep their estimated payload, integers keep their
    // boxed magnitude, each beside one shared-cache entry. Floats parse fresh
    // per evaluation and singletons retain nothing, so they carry no payload.
    // Formatted-string text chunks persist inside the retained lowered parts.
    private static long RetainedLiteralBytes(ExpressionSyntax expression) => expression switch
    {
        StringLiteralExpressionSyntax text => RuntimeMemoryEstimates.SaturatingAdd(
            PyString.EstimateApproximateBytes(Encoding.UTF8.GetByteCount(text.Value)),
            SharedLiteralEntryBytes),
        BytesLiteralExpressionSyntax bytes => RuntimeMemoryEstimates.SaturatingAdd(
            PyBytes.EstimateApproximateBytes(bytes.Value.Length),
            SharedLiteralEntryBytes),
        IntegerLiteralExpressionSyntax integer => RuntimeMemoryEstimates.SaturatingAdd(
            RuntimeMemoryEstimates.EstimateBigIntegerBytes(ParseInteger(integer)),
            SharedLiteralEntryBytes),
        FormattedStringExpressionSyntax formatted => RetainedFormatTextBytes(formatted.Parts),
        _ => 0,
    };

    private static long RetainedFormatTextBytes(IReadOnlyList<FormattedStringPartSyntax>? parts)
    {
        if (parts is null)
        {
            return 0;
        }

        var bytes = 0L;
        foreach (var part in parts)
        {
            switch (part)
            {
                case FormattedStringTextPartSyntax text:
                    bytes = RuntimeMemoryEstimates.SaturatingAdd(bytes, Encoding.UTF8.GetByteCount(text.Text));
                    break;
                case FormattedStringExpressionPartSyntax expressionPart:
                    bytes = RuntimeMemoryEstimates.SaturatingAdd(bytes, RetainedFormatTextBytes(expressionPart.FormatSpecifierParts));
                    break;
                default:
                    throw new InvalidOperationException($"Unknown format part: {part.GetType().Name}");
            }
        }

        return bytes;
    }

    // Deferred-root search inside the retained references the shared traversals
    // skip: unpacking receivers, chained targets and match patterns (whose own
    // nodes count in full walks). Each is a small closed set; unknown shapes
    // fail loud below.
    private static (long Nodes, long PayloadBytes) MeasureExtraStatementDeferredRoots(StatementSyntax statement)
    {
        var nodes = 0L;
        var payload = 0L;
        foreach (var expression in EnumerateExtraStatementExpressions(statement))
        {
            var measured = MeasureDeferredRoots(expression);
            nodes += measured.Nodes;
            payload = RuntimeMemoryEstimates.SaturatingAdd(payload, measured.PayloadBytes);
        }

        if (statement is MatchStatementSyntax matchStatement)
        {
            foreach (var matchCase in matchStatement.Cases)
            {
                var measured = MeasureDeferredRootsInPattern(matchCase.Pattern);
                nodes += measured.Nodes;
                payload = RuntimeMemoryEstimates.SaturatingAdd(payload, measured.PayloadBytes);
            }
        }

        return (nodes, payload);
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
    private static (long Nodes, long PayloadBytes) MeasureDeferredRootsInPattern(PatternSyntax root)
    {
        var nodes = 0L;
        var payload = 0L;
        var pending = new Stack<PatternSyntax>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var pattern = pending.Pop();
            foreach (var expression in EnumeratePatternExpressions(pattern))
            {
                var measured = MeasureDeferredRoots(expression);
                nodes += measured.Nodes;
                payload = RuntimeMemoryEstimates.SaturatingAdd(payload, measured.PayloadBytes);
            }

            foreach (var nested in EnumeratePatternSubpatterns(pattern))
            {
                pending.Push(nested);
            }
        }

        return (nodes, payload);
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
                ReturnBoundValues(boundArguments.Values);
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
                ReturnBoundValues(boundArguments.Values);
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

        // M05: only the CLR text survives message rendering, so the transient
        // is pool-owned here and dropped renders reclaim on sweep. Already-owned
        // renders (notably PyString args rendering to themselves) dedup to a
        // no-op through the shared table instead of double-owning guest charges.
        private static string TakeMessageText(PyString rendered, ExecutionContext context, LythonSourceSpan span)
        {
            var text = rendered.AsString();
            context.Services.State.CallTemporaries.TrackFreshString(rendered, span);
            return text;
        }

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
                    TakeMessageText(PyRendering.ToReprPyString(values[0], new PyRenderingContext(context)), context, span),
                1 => TakeMessageText(PyRendering.ToInterpolatedPyString(values[0], new PyRenderingContext(context)), context, span),
                _ => TakeMessageText(PyRendering.ToReprPyString(args, new PyRenderingContext(context)), context, span),
            };
            var payload = values.Length == 0 ? PyNone.Instance : values.Length == 1 ? values[0] : args;
            var constructed = new PyException(ExceptionIdentity, message, payload, args);
            // M05: the args tuple commits at construction but no call funnel owns
            // a bare exception record, so dropped constructions (including every
            // caught raise) stranded 32+16n B. Handler rewraps share this same
            // tuple through ExplicitArgs, so the pool keys the tuple itself and
            // dropped constructions reclaim once collected.
            context.Services.State.CallTemporaries.TrackFreshMutable(args, args.CommittedStorageBytes, span);
            return constructed;
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
