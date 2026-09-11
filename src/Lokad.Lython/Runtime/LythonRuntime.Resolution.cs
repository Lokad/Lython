using System.Text.RegularExpressions;
using Lokad.Lython.Frontend;
using System.Numerics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static object ResolveIdentifier(IdentifierExpressionSyntax identifier, ExecutionContext context)
        => ResolveName(identifier.Name, identifier.Span, context);

    internal static object ResolveName(string name, LythonSourceSpan span, ExecutionContext context)
    {
        if (context.ScopeFacts.IsGlobal(name))
        {
            var globalContext = GetGlobalContext(context);
            if (globalContext.CurrentExecutableFrame is not null &&
                globalContext.CurrentExecutableFrame.TryResolveLocalOrClosure(name, out var executableValue))
            {
                return executableValue;
            }

            if (globalContext.Variables.TryGetValue(name, out var globalValue))
            {
                return globalValue;
            }

            throw RuntimeErrors.NameNotDefined(name, span);
        }

        if (context.TryGetNonlocalTarget(name, out var nonlocalContext))
        {
            if (nonlocalContext.Variables.TryGetValue(name, out var nonlocalValue))
            {
                return nonlocalValue;
            }

            throw RuntimeErrors.NameNotDefined(name, span);
        }

        for (var current = context; current is not null; current = current.ParentContext)
        {
            if (current.CurrentExecutableFrame is not null &&
                current.CurrentExecutableFrame.TryResolveLocalOrClosure(name, out var executableValue))
            {
                return executableValue;
            }

            if (current.Variables.TryGetValue(name, out var value))
            {
                return value;
            }
        }

        throw RuntimeErrors.NameNotDefined(name, span);
    }

    internal static void StoreName(string name, object value, ExecutionContext context, LythonSourceSpan span)
    {
        var storageContext = ResolveNameStorageContext(name, context, span);
        storageContext.Variables[name] = value;
        storageContext.CurrentExecutableFrame?.TryStoreLocalOrClosure(name, value);
    }

    internal static bool DeleteName(string name, ExecutionContext context, LythonSourceSpan span)
    {
        var storageContext = ResolveNameStorageContext(name, context, span);
        var removed = storageContext.Variables.Remove(name);
        removed |= storageContext.CurrentExecutableFrame?.TryDeleteLocalOrClosure(name) == true;

        return removed;
    }

    internal static ExecutionContext ResolveNameStorageContext(string name, ExecutionContext context, LythonSourceSpan span)
    {
        if (context.ScopeFacts.IsGlobal(name))
        {
            return GetGlobalContext(context);
        }

        if (context.TryGetNonlocalTarget(name, out var nonlocalContext))
        {
            return nonlocalContext;
        }

        return context;
    }

    internal static ExecutionContext GetGlobalContext(ExecutionContext context)
    {
        var current = context;
        while (current.ParentContext is not null)
        {
            current = current.ParentContext;
        }

        return current;
    }

    private static object ResolveMember(MemberExpressionSyntax member, ExecutionContext context)
    {
        var target = EvaluateExpression(member.Target, context);
        if (TryResolveRuntimeMember(target, member.MemberName, context, member.Span, out var value))
        {
            return value;
        }

        throw PyMemberAccess.CreateMissingMemberError(target, member.MemberName, member.Span, context);
    }

    internal static bool TryResolveRuntimeMember(object target, string memberName, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
    {
        if (!PyMemberAccess.TryResolve(target, memberName, context, span, out value))
        {
            return false;
        }

        // Bound engine methods report their receiver like CPython. Each
        // instance is fresh per access, so threading here covers every
        // member table at once; instances, types, supers and modules
        // keep their own binding rules (or none).
        if (value is BoundCallable unbound &&
            target is not PyInstance &&
            target is not PyType &&
            target is not PySuper &&
            target is not PyModule)
        {
            unbound.AttachReceiver(target);
        }

        return true;
    }

    private static object EvaluateSubscript(SubscriptExpressionSyntax subscript, ExecutionContext context)
    {
        var target = EvaluateExpression(subscript.Target, context);
        var index = EvaluateExpression(subscript.Index, context);
        if (target is PyDefaultDict defaultDict)
        {
            return defaultDict.GetOrCreate(ValidateDictionaryKey(index, subscript.Span), context, subscript.Span);
        }

        if (target is PyInstance instance)
        {
            return GetUserItem(instance, index, context, subscript.Span);
        }

        return PyIndexing.ReadIndex(target, CoerceIndexProtocol(index, context, subscript.Span), subscript.Span);
    }

    private static object EvaluateSlice(SliceExpressionSyntax slice, ExecutionContext context)
    {
        var target = EvaluateExpression(slice.Target, context);
        var start = slice.Start is null ? null : EvaluateExpression(slice.Start, context);
        var end = slice.End is null ? null : EvaluateExpression(slice.End, context);
        var step = slice.Step is null ? null : EvaluateExpression(slice.Step, context);

        return PyIndexing.ReadSlice(target, start, end, step, slice.Span, context);
    }

    internal static bool IsTruthy(object value) => PyTruthiness.IsTruthy(value);

    internal static bool IsTruthy(object value, ExecutionContext context, LythonSourceSpan span)
    {
        if (value is not PyInstance instance)
        {
            return PyTruthiness.IsTruthy(value);
        }

        var protocol = ResolveTruthinessProtocol(instance, context, span, out var callable);
        return protocol == TruthinessProtocol.Default
            ? true
            : InterpretTruthinessResult(protocol, callable.RequireNotNull().Invoke([], span, context), span);
    }

    internal static async ValueTask<bool> IsTruthyAsync(object value, ExecutionContext context, LythonSourceSpan span)
    {
        if (value is not PyInstance instance)
        {
            return PyTruthiness.IsTruthy(value);
        }

        var protocol = ResolveTruthinessProtocol(instance, context, span, out var callable);
        if (protocol == TruthinessProtocol.Default)
        {
            return true;
        }

        var result = await callable.RequireNotNull().InvokeAsync([], span, context).ConfigureAwait(false);
        return InterpretTruthinessResult(protocol, result, span);
    }

    private static TruthinessProtocol ResolveTruthinessProtocol(
        PyInstance instance,
        ExecutionContext context,
        LythonSourceSpan span,
        out ICallable? callable)
    {
        if (instance.TryGetAttribute("__bool__", context, span, out var boolMember) && boolMember is ICallable boolCallable)
        {
            callable = boolCallable;
            return TruthinessProtocol.Boolean;
        }

        if (instance.TryGetAttribute("__len__", context, span, out var lengthMember) && lengthMember is ICallable lengthCallable)
        {
            callable = lengthCallable;
            return TruthinessProtocol.Length;
        }

        callable = null;
        return TruthinessProtocol.Default;
    }

    private static bool InterpretTruthinessResult(TruthinessProtocol protocol, object result, LythonSourceSpan span)
    {
        if (protocol == TruthinessProtocol.Boolean)
        {
            return result is bool boolean
                ? boolean
                : throw new LythonRuntimeException("TypeError", "__bool__ should return bool", span);
        }

        if (!PyNumberOps.TryAsInteger(result, out var length))
        {
            throw new LythonRuntimeException("TypeError", "__len__() should return an integer", span);
        }

        if (length < 0)
        {
            throw new LythonRuntimeException("ValueError", "__len__() should return >= 0", span);
        }

        return length != 0;
    }

    private enum TruthinessProtocol
    {
        Default,
        Boolean,
        Length,
    }

    internal static IEnumerable<object> ToSequence(object value, LythonSourceSpan span)
        => PyIteration.ToSequence(value, span);

    internal static IEnumerable<object> ToSequence(object value, LythonSourceSpan span, ExecutionContext context)
        => PyIteration.ToSequence(value, span, context);

    internal static IAsyncEnumerable<object> ToSequenceAsync(object value, LythonSourceSpan span)
        => PyIteration.ToSequenceAsync(value, span);

    internal static IAsyncEnumerable<object> ToSequenceAsync(object value, LythonSourceSpan span, ExecutionContext context)
        => PyIteration.ToSequenceAsync(value, span, context);

    internal static bool AreEqual(object left, object right) => PyEquality.AreEqual(left, right);

    private static int Compare(object left, object right, LythonSourceSpan span) => PyComparison.Compare(left, right, span);

    private static bool CompareRelational(object left, object right, LythonSourceSpan span, Func<int, bool> predicate)
    {
        if (PyNumberOps.TryAsNumber(left, out var lhs) && PyNumberOps.TryAsNumber(right, out var rhs))
        {
            return PyNumberOps.TryCompare(lhs, rhs, out var comparison) && predicate(comparison);
        }

        return predicate(Compare(left, right, span));
    }

    private static bool Contains(object container, object candidate, LythonSourceSpan span) => PyContainment.Contains(container, candidate, span);

    private static object GetUserItem(PyInstance instance, object index, ExecutionContext context, LythonSourceSpan span)
    {
        if (!instance.TryGetAttribute("__getitem__", context, span, out var member) || member is not ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", $"'{instance.Type.Name}' object is not subscriptable", span);
        }

        return CallableInvocation.InvokeUnary(callable, index, span, context);
    }

    private static object CoerceIndexProtocol(object index, ExecutionContext context, LythonSourceSpan span)
    {
        if (index is not PyInstance instance)
        {
            return index;
        }

        if (!instance.TryGetAttribute("__index__", context, span, out var member) || member is not ICallable callable)
        {
            return index;
        }

        var converted = callable.Invoke([], span, context);
        if (!PyNumberOps.TryAsInteger(converted, out var integer))
        {
            throw new LythonRuntimeException("TypeError", "__index__ returned non-int", span);
        }

        return integer;
    }

    private static PyString CoercePathLike(object value, ExecutionContext context, LythonSourceSpan span, string owner)
    {
        if (value is PyPath path)
        {
            return path.Value;
        }

        if (value is PyDirEntryObject dirEntry)
        {
            return PyString.FromString(dirEntry.Path);
        }

        if (PyStringOps.TryAsString(value, out var text))
        {
            return text;
        }

        if (value is PyInstance instance &&
            instance.TryGetAttribute("__fspath__", context, span, out var member) &&
            member is ICallable callable)
        {
            var result = callable.Invoke([], span, context);
            if (PyStringOps.TryAsString(result, out var pathText))
            {
                return pathText;
            }

            throw new LythonRuntimeException("TypeError", "__fspath__() must return str", span);
        }

        throw new LythonRuntimeException("TypeError", owner + " expects a path-like object", span);
    }

    private static object EvaluateListComprehension(ListComprehensionExpressionSyntax comprehension, ExecutionContext context)
    {
        var result = new PyList([], context.MemoryGovernor, comprehension.Span);
        EvaluateComprehensionClauses(
            comprehension.Clauses,
            0,
            context,
            scope => result.Add(RuntimeValue(EvaluateExpression(comprehension.ItemExpression, scope))));

        context.ObserveCollectionCount(result.Count, comprehension.Span);
        return result;
    }

    private static object EvaluateSetComprehension(SetComprehensionExpressionSyntax comprehension, ExecutionContext context)
    {
        var result = new PySet(context.MemoryGovernor, comprehension.Span);
        EvaluateComprehensionClauses(
            comprehension.Clauses,
            0,
            context,
            scope =>
            {
                var item = ValidateSetItem(
                    EvaluateExpression(comprehension.ItemExpression, scope),
                    comprehension.ItemExpression.Span,
                    scope.MemoryGovernor);
                result.Add(item);
            });

        context.ObserveCollectionCount(result.Count, comprehension.Span);
        return result;
    }

    private static object EvaluateDictComprehension(DictComprehensionExpressionSyntax comprehension, ExecutionContext context)
    {
        var result = new PyDict(context.MemoryGovernor, comprehension.Span);
        EvaluateComprehensionClauses(
            comprehension.Clauses,
            0,
            context,
            scope =>
            {
                var key = ValidateDictionaryKey(EvaluateExpression(comprehension.KeyExpression, scope), comprehension.KeyExpression.Span, scope.MemoryGovernor);
                result.SetItem(key, RuntimeValue(EvaluateExpression(comprehension.ValueExpression, scope)));
            });

        context.ObserveCollectionCount(result.Count, comprehension.Span);
        return result;
    }

    private static object EvaluateGeneratorExpression(GeneratorExpressionSyntax generator, ExecutionContext context)
    {
        var clauses = generator.Clauses.Select(clause => new LoweredComprehensionClause(
            clause.Target,
            LoweredScript.LowerStandaloneExpression(clause.Iterable),
            clause.Condition is null ? null : LoweredScript.LowerStandaloneExpression(clause.Condition),
            clause.Span)).ToArray();
        // The outermost iterable is evaluated and acquired eagerly, matching
        // CPython; only element expressions, filters, and later clauses stay
        // deferred. Rebinding the source name later observes the old value.
        var outer = ToSequence(
            EvaluateLoweredExpression(clauses[0].Iterable, context),
            clauses[0].Iterable.Span,
            context);
        return new PyGeneratorExpression(
            clauses,
            LoweredScript.LowerStandaloneExpression(generator.ItemExpression),
            context,
            generator.Span,
            outer);
    }

    private static void EvaluateComprehensionClauses(
        IReadOnlyList<ComprehensionClauseSyntax> clauses,
        int index,
        ExecutionContext context,
        Action<ExecutionContext> emit)
    {
        var clause = clauses[index];
        var iterable = EvaluateExpression(clause.Iterable, context);

        foreach (var item in ToSequence(iterable, clause.Iterable.Span, context))
        {
            var scope = new ExecutionContext(context);
            AssignLoopTarget(clause.Target, item, clause.Iterable.Span, scope);

            if (clause.Condition is not null &&
                !IsTruthy(EvaluateExpression(clause.Condition, scope), scope, clause.Condition.Span))
            {
                continue;
            }

            if (index + 1 == clauses.Count)
            {
                emit(scope);
            }
            else
            {
                EvaluateComprehensionClauses(clauses, index + 1, scope, emit);
            }
        }
    }

    internal static void AssignLoopTarget(
        LoopTargetSyntax target,
        object value,
        LythonSourceSpan span,
        ExecutionContext context)
    {
        switch (target)
        {
            case LoopNameTargetSyntax name:
                StoreName(name.Name, value, context, span);
                return;
            case LoopTupleTargetSyntax tuple:
                var values = MaterializeSequenceForUnpacking(value, span, context);
                if (values.Length != tuple.Items.Count)
                {
                    throw new LythonRuntimeException("ValueError", "unpacking assignment has the wrong number of values", span);
                }

                for (var i = 0; i < tuple.Items.Count; i++)
                {
                    AssignLoopTarget(tuple.Items[i], values[i], span, context);
                }

                return;
            default:
                throw new InvalidOperationException($"Unsupported loop target type: {target.GetType().Name}");
        }
    }

    private static void PropagateComprehensionBindings(
        ExecutionContext scope,
        ExecutionContext outer,
        IEnumerable<LoopTargetSyntax> targets,
        LythonSourceSpan span)
    {
        var loopNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var target in targets)
        {
            CollectLoopTargetNames(target, loopNames);
        }

        foreach (var pair in scope.Variables)
        {
            if (!loopNames.Contains(pair.Key) && !ExecutionState.BuiltinNames.Contains(pair.Key))
            {
                StoreName(pair.Key, pair.Value, outer, span);
            }
        }
    }

    private static void CollectLoopTargetNames(LoopTargetSyntax target, HashSet<string> names)
    {
        switch (target)
        {
            case LoopNameTargetSyntax name:
                names.Add(name.Name);
                break;
            case LoopTupleTargetSyntax tuple:
                foreach (var item in tuple.Items)
                {
                    CollectLoopTargetNames(item, names);
                }
                break;
        }
    }

    private static void AssignTargets(
        IReadOnlyList<UnpackingTargetSyntax> targets,
        object value,
        LythonSourceSpan span,
        ExecutionContext context)
    {
        if (targets.Count == 1 && !targets[0].IsStarred)
        {
            StoreName(targets[0].Name, value, context, span);
            return;
        }

        var values = MaterializeSequenceForUnpacking(value, span, context);
        var layout = UnpackingLayout.FromTargets(targets);
        if (!layout.AcceptsValueCount(values.Length))
        {
            throw new LythonRuntimeException("ValueError", "unpacking assignment has the wrong number of values", span);
        }

        if (!layout.HasStarredTarget)
        {
            for (var i = 0; i < targets.Count; i++)
            {
                StoreName(targets[i].Name, values[i], context, span);
            }

            return;
        }

        for (var i = 0; i < layout.StarredTargetIndex; i++)
        {
            StoreName(targets[i].Name, values[i], context, span);
        }

        var starredCount = layout.StarredValueCount(values.Length);
        var starredItems = new object[starredCount];
        Array.Copy(values, layout.StarredTargetIndex, starredItems, 0, starredCount);
        StoreName(targets[layout.StarredTargetIndex].Name, new PyList(starredItems, context.MemoryGovernor, span), context, span);

        for (var i = layout.StarredTargetIndex + 1; i < targets.Count; i++)
        {
            var offset = layout.SourceIndexForTrailingTarget(i, values.Length);
            StoreName(targets[i].Name, values[offset], context, span);
        }
    }

    private static object[] MaterializeSequenceForUnpacking(object value, LythonSourceSpan span, ExecutionContext context)
    {
        if (value is object[] array)
        {
            return array;
        }

        if (value is PyTuple tuple)
        {
            return tuple.ToArray();
        }

        if (value is PyList list)
        {
            return list.ToArray();
        }

        // Unpack targets are fixed-arity except for one starred remainder, so
        // the drained prefix is transient scratch coexisting with live iteration
        // state. Mirror the shared asynchronous drain: charge backing growth
        // before it can allocate, observe the collection count per item, and
        // cover the final array until the starred list (or names) take over.
        using var temporary = context.MemoryGovernor.ReserveTemporary(0, span);
        var items = new List<object>();
        var chargedCapacity = 0;
        foreach (var item in ToSequence(value, span, context))
        {
            if (items.Count == items.Capacity)
            {
                var predicted = items.Capacity == 0 ? 4L : (long)items.Capacity * 2L;
                temporary.Grow(checked(16L * (predicted - chargedCapacity)), span);
            }

            items.Add(item);
            if (items.Capacity > chargedCapacity)
            {
                temporary.Grow(16L * (items.Capacity - chargedCapacity), span);
                chargedCapacity = items.Capacity;
            }

            context.ObserveCollectionCount(items.Count, span);
            if ((items.Count & 63) == 0)
            {
                context.CheckExecutionBudget(span);
            }
        }

        temporary.Grow(16L * items.Count, span);
        return [.. items];
    }

}
