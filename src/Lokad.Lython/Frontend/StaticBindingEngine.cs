using System.Globalization;

namespace Lokad.Lython.Frontend;

internal static partial class StaticBindingEngine
{
    public static void UpdateBindings(StatementSyntax statement, AbstractState bindings)
    {
        var mutatedReceivers = CollectMutatedReceiverNames(statement, bindings);
        if (mutatedReceivers.Any(bindings.IsKnownMutableSequence) ||
            MutatesKnownMutableSequenceThroughAssignment(statement, bindings))
        {
            bindings.InvalidateMutableSequenceFacts();
        }

        switch (statement)
        {
            case ImportStatementSyntax importStatement:
                if (importStatement.ImportedMembers is null)
                {
                    if (StaticContracts.IsKnownBuiltinModule(importStatement.BoundModuleName))
                    {
                        bindings.Set(importStatement.BindingName, AbstractValue.Module(importStatement.BoundModuleName, importStatement.Span));
                    }
                    else
                    {
                        bindings.Remove(importStatement.BindingName);
                    }
                }
                else
                {
                    if (ImportSyntaxFacts.IsStarImport(importStatement.ImportedMembers))
                    {
                        foreach (var memberName in StaticContracts.GetModuleExportedMemberNames(importStatement.ModuleName))
                        {
                            if (StaticContracts.TryGetModuleMemberValue(importStatement.ModuleName, memberName, importStatement.Span, out var memberValue))
                            {
                                bindings.Set(memberName, memberValue);
                            }
                            else
                            {
                                bindings.Remove(memberName);
                            }
                        }

                        break;
                    }

                    foreach (var member in importStatement.ImportedMembers)
                    {
                        if (StaticContracts.TryGetModuleMemberValue(importStatement.ModuleName, member.Name, importStatement.Span, out var memberValue))
                        {
                            bindings.Set(member.BindingName, memberValue);
                        }
                        else
                        {
                            bindings.Remove(member.BindingName);
                        }
                    }
                }
                break;

            case AssignmentStatementSyntax assignment:
                UpdateBinding(assignment.Name, assignment.Expression, bindings);
                break;

            case ChainedAssignmentStatementSyntax chained:
                foreach (var target in chained.Targets)
                {
                    if (target is NameAssignmentTargetSyntax nameTarget)
                    {
                        UpdateBinding(nameTarget.Name, chained.Expression, bindings);
                    }
                }
                break;

            case AnnotatedAssignmentStatementSyntax annotated when annotated.Expression is not null:
                UpdateBinding(annotated.Name, annotated.Expression, bindings);
                break;

            case UnpackingAssignmentStatementSyntax unpacking:
                if (!TryBindUnpackingTargets(unpacking.Targets, unpacking.Expression, bindings))
                {
                    foreach (var target in unpacking.Targets)
                    {
                        bindings.Remove(target.Name);
                    }
                }
                break;

            case AugmentedAssignmentStatementSyntax augmented:
                RemoveAugmentedAssignmentBindings(augmented.Target, bindings);
                break;

            case DeleteStatementSyntax { Target: IdentifierExpressionSyntax identifier }:
                bindings.Remove(identifier.Name);
                break;

            case FunctionDefinitionStatementSyntax functionDefinition:
                if (TryBuildSimpleFunctionSummary(functionDefinition, bindings, out var summary))
                {
                    bindings.Set(functionDefinition.Name, AbstractValue.Function(summary, functionDefinition.Span));
                }
                else
                {
                    bindings.Remove(functionDefinition.Name);
                }
                break;

            case ClassDefinitionStatementSyntax classDefinition:
                if (TryBuildSimpleClassSummary(classDefinition, bindings, out var classSummary))
                {
                    bindings.Set(classDefinition.Name, AbstractValue.UserClass(classSummary, classDefinition.Span));
                }
                else
                {
                    bindings.Remove(classDefinition.Name);
                }
                break;

            case SubscriptAssignmentStatementSyntax subscript when subscript.Target is IdentifierExpressionSyntax subscriptIdentifier:
                bindings.Remove(subscriptIdentifier.Name);
                break;

            case SliceAssignmentStatementSyntax slice when slice.Target is IdentifierExpressionSyntax sliceIdentifier:
                bindings.Remove(sliceIdentifier.Name);
                break;

            case MemberAssignmentStatementSyntax member when member.Target is IdentifierExpressionSyntax memberIdentifier:
                bindings.Remove(memberIdentifier.Name);
                break;

            case ExpressionStatementSyntax expressionStatement:
                TryApplyArgparseParserMutation(expressionStatement.Expression, bindings);
                break;
        }

        foreach (var receiver in mutatedReceivers)
        {
            bindings.Remove(receiver);
        }
    }

    private static bool MutatesKnownMutableSequenceThroughAssignment(StatementSyntax statement, AbstractState bindings)
    {
        return statement switch
        {
            SubscriptAssignmentStatementSyntax { Target: IdentifierExpressionSyntax identifier } =>
                bindings.IsKnownMutableSequence(identifier.Name),
            SliceAssignmentStatementSyntax { Target: IdentifierExpressionSyntax identifier } =>
                bindings.IsKnownMutableSequence(identifier.Name),
            AugmentedAssignmentStatementSyntax { Target: NameAssignmentTargetSyntax identifier } =>
                bindings.IsKnownMutableSequence(identifier.Name),
            AugmentedAssignmentStatementSyntax
            {
                Target: SubscriptAssignmentTargetSyntax { Target: IdentifierExpressionSyntax identifier }
            } => bindings.IsKnownMutableSequence(identifier.Name),
            AugmentedAssignmentStatementSyntax
            {
                Target: SliceAssignmentTargetSyntax { Target: IdentifierExpressionSyntax identifier }
            } => bindings.IsKnownMutableSequence(identifier.Name),
            _ => false
        };
    }

    private static void RemoveAugmentedAssignmentBindings(AssignmentTargetSyntax target, AbstractState bindings)
    {
        switch (target)
        {
            case NameAssignmentTargetSyntax name:
                bindings.Remove(name.Name);
                break;

            case SubscriptAssignmentTargetSyntax { Target: IdentifierExpressionSyntax subscriptIdentifier }:
                bindings.Remove(subscriptIdentifier.Name);
                break;

            case SliceAssignmentTargetSyntax { Target: IdentifierExpressionSyntax sliceIdentifier }:
                bindings.Remove(sliceIdentifier.Name);
                break;

            case MemberAssignmentTargetSyntax { Target: IdentifierExpressionSyntax memberIdentifier }:
                bindings.Remove(memberIdentifier.Name);
                break;
        }
    }

    public static AbstractState BindComprehensionClauses(IReadOnlyList<ComprehensionClauseSyntax> clauses, AbstractState bindings)
    {
        var comprehensionBindings = bindings.Clone();
        foreach (var clause in clauses)
        {
            BindLoopTargetFromIterable(clause.Target, clause.Iterable, comprehensionBindings);
            if (clause.Condition is not null)
            {
                StaticConditionRefinements.Apply(clause.Condition, assumedTruth: true, comprehensionBindings);
            }
        }

        return comprehensionBindings;
    }

    public static void BindFunctionParametersUnknown(IReadOnlyList<FunctionParameterSyntax> parameters, AbstractState bindings)
    {
        foreach (var parameter in parameters)
        {
            bindings.Set(parameter.Name, AbstractValue.Unknown());
        }
    }

    public static void BindLoopTargetFromIterable(LoopTargetSyntax target, ExpressionSyntax iterableExpression, AbstractState bindings)
    {
        if (TryGetIterableElementAbstractValue(iterableExpression, bindings, out var itemValue))
        {
            BindLoopTargetValue(target, itemValue, bindings);
        }
        else
        {
            BindLoopTargetUnknown(target, bindings);
        }
    }

    public static bool TryGetIterableElementAbstractValue(ExpressionSyntax expression, AbstractState bindings, out AbstractValue itemValue)
    {
        if (!StaticAbstractValueResolver.TryResolve(expression, bindings, out var iterableValue))
        {
            itemValue = default;
            return false;
        }

        switch (iterableValue.Kind)
        {
            case AbstractValueKind.String:
            case AbstractValueKind.StringType:
                itemValue = AbstractValue.StringType(expression.Span);
                return true;

            case AbstractValueKind.Bytes:
            case AbstractValueKind.BytesType:
                itemValue = AbstractValue.IntegerType(expression.Span);
                return true;

            case AbstractValueKind.ListType:
            case AbstractValueKind.SetType:
                itemValue = ((AbstractValue)iterableValue.Value).WithSpan(expression.Span);
                return true;

            case AbstractValueKind.List:
            case AbstractValueKind.Tuple:
            case AbstractValueKind.Set:
                itemValue = JoinSequenceItems((IReadOnlyList<AbstractValue>)iterableValue.Value, expression.Span);
                return true;

            case AbstractValueKind.Dict:
                itemValue = JoinDictKeys((IReadOnlyList<KeyValuePair<AbstractValue, AbstractValue>>)iterableValue.Value, expression.Span);
                return true;

            case AbstractValueKind.TextFileHandle:
                if ((AbstractTextFileMode)iterableValue.Value == AbstractTextFileMode.Read)
                {
                    itemValue = AbstractValue.StringType(expression.Span);
                    return true;
                }

                itemValue = default;
                return false;

            case AbstractValueKind.CsvReader:
                itemValue = AbstractValue.ListOf(AbstractValue.StringType(expression.Span), expression.Span);
                return true;

            case AbstractValueKind.CsvDictReader:
                itemValue = AbstractValue.Unknown(expression.Span);
                return true;

            default:
                itemValue = default;
                return false;
        }
    }

    public static bool TryGetOrderedUnpackingItems(
        ExpressionSyntax expression,
        AbstractState bindings,
        out IReadOnlyList<AbstractValue> items)
    {
        if (!StaticAbstractValueResolver.TryResolve(expression, bindings, out var value))
        {
            items = Array.Empty<AbstractValue>();
            return false;
        }

        switch (value.Kind)
        {
            case AbstractValueKind.List:
            case AbstractValueKind.Tuple:
                items = (IReadOnlyList<AbstractValue>)value.Value;
                return true;

            case AbstractValueKind.String:
                {
                    var text = (string)value.Value;
                    var chars = new List<AbstractValue>(text.Length);
                    for (var i = 0; i < text.Length; i++)
                    {
                        chars.Add(AbstractValue.String(text.Substring(i, 1), expression.Span));
                    }

                    items = chars;
                    return true;
                }

            case AbstractValueKind.Bytes:
                {
                    var bytes = (byte[])value.Value;
                    var integers = new List<AbstractValue>(bytes.Length);
                    foreach (var item in bytes)
                    {
                        integers.Add(AbstractValue.Integer(item.ToString(CultureInfo.InvariantCulture), expression.Span));
                    }

                    items = integers;
                    return true;
                }

            default:
                items = Array.Empty<AbstractValue>();
                return false;
        }
    }

    public static AbstractValue JoinSequenceItems(IReadOnlyList<AbstractValue> items, LythonSourceSpan span)
    {
        var result = AbstractValue.Never(span);
        foreach (var item in items)
        {
            result = AbstractValue.Join(result, item, span);
        }

        return result.Kind == AbstractValueKind.Never ? AbstractValue.Unknown(span) : result;
    }

    public static bool TryGetFixedSequenceItems(AbstractValue value, out IReadOnlyList<AbstractValue> items)
    {
        if (value.Kind is AbstractValueKind.List or AbstractValueKind.Tuple)
        {
            items = (IReadOnlyList<AbstractValue>)value.Value;
            return true;
        }

        items = Array.Empty<AbstractValue>();
        return false;
    }

    public static bool TryResolveFunctionCallReturn(
        AbstractFunctionSummary summary,
        ConcreteCallArguments arguments,
        AbstractState callBindings,
        LythonSourceSpan span,
        out AbstractValue value)
    {
        if (!TryBindFunctionArguments(summary, arguments, callBindings, out var functionBindings) ||
            !TryInferStraightLineReturn(summary.Body, functionBindings, out var returnValue))
        {
            value = default;
            return false;
        }

        value = returnValue.WithSpan(span);
        return true;
    }

    public static bool TryInstantiateUserClass(
        AbstractClassSummary summary,
        ConcreteCallArguments arguments,
        AbstractState bindings,
        LythonSourceSpan span,
        out AbstractValue value)
    {
        if (!summary.IsDataclass)
        {
            value = default;
            return false;
        }

        var initFields = summary.InitFields;
        var positionalFields = summary.PositionalInitFields;
        if (arguments.Positional.Count > positionalFields.Length)
        {
            value = default;
            return false;
        }

        var fields = new Dictionary<string, AbstractValue>(StringComparer.Ordinal);
        var consumedFields = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < positionalFields.Length; i++)
        {
            if (i < arguments.Positional.Count)
            {
                var field = positionalFields[i];
                if (field.StoreOnInstance)
                {
                    fields[field.Name] = arguments.ResolvePositionalValue(i, bindings).WithSpan(span);
                }

                consumedFields.Add(field.Name);
            }
        }

        foreach (var (keyword, expression) in arguments.Keywords)
        {
            if (!summary.FieldsByName.TryGetValue(keyword, out var field) ||
                !field.IncludeInInit ||
                !consumedFields.Add(keyword))
            {
                value = default;
                return false;
            }

            if (field.StoreOnInstance)
            {
                fields[keyword] = arguments.ResolveKeywordValue(keyword, bindings).WithSpan(span);
            }
        }

        foreach (var field in summary.Fields)
        {
            if (fields.ContainsKey(field.Name))
            {
                continue;
            }

            if (field.StoreOnInstance)
            {
                fields[field.Name] = field.HasDefault
                    ? field.DefaultValue.WithSpan(span)
                    : AbstractValue.Unknown(span);
            }
        }

        value = AbstractValue.UserInstance(new AbstractInstanceSummary(summary, fields, span), span);
        return true;
    }

    public static bool TryGetUserInstanceMemberValue(AbstractValue instanceValue, string memberName, LythonSourceSpan span, out AbstractValue value)
    {
        var instance = (AbstractInstanceSummary)instanceValue.Value;
        if (instance.Fields.TryGetValue(memberName, out value))
        {
            value = value.WithSpan(span);
            return true;
        }

        value = default;
        return false;
    }

    public static bool TryResolveUserInstanceMethodReturn(
        AbstractValue instanceValue,
        string methodName,
        ConcreteCallArguments arguments,
        AbstractState callBindings,
        LythonSourceSpan span,
        out AbstractValue value)
    {
        var instance = (AbstractInstanceSummary)instanceValue.Value;
        if (!instance.Class.Methods.TryGetValue(methodName, out var methodSummary) ||
            !TryBindInstanceMethodArguments(methodSummary, instanceValue, arguments, callBindings, out var methodBindings) ||
            !TryInferStraightLineReturn(methodSummary.Body, methodBindings, out value))
        {
            value = default;
            return false;
        }

        value = value.WithSpan(span);
        return true;
    }

    public static bool TryGetFunctionCallShapeFailure(
        AbstractFunctionSummary summary,
        ConcreteCallArguments arguments,
        out string reason,
        out ExpressionSyntax? offendingExpression)
        => TryGetParameterCallShapeFailure(
            summary.Parameters,
            firstParameterIndex: 0,
            arguments,
            out reason,
            out offendingExpression);

    public static bool TryGetUserInstanceMethodCallShapeFailure(
        AbstractValue instanceValue,
        string methodName,
        ConcreteCallArguments arguments,
        out string reason,
        out ExpressionSyntax? offendingExpression)
    {
        var instance = (AbstractInstanceSummary)instanceValue.Value;
        if (!instance.Class.Methods.TryGetValue(methodName, out var methodSummary))
        {
            reason = string.Empty;
            offendingExpression = null;
            return false;
        }

        return TryGetParameterCallShapeFailure(
            methodSummary.Parameters,
            firstParameterIndex: 1,
            arguments,
            out reason,
            out offendingExpression);
    }

    public static bool TryGetDataclassConstructorShapeFailure(
        AbstractClassSummary summary,
        ConcreteCallArguments arguments,
        out string reason,
        out ExpressionSyntax? offendingExpression)
    {
        if (!summary.IsDataclass)
        {
            reason = string.Empty;
            offendingExpression = null;
            return false;
        }

        var initFields = summary.InitFields;
        var positionalFields = summary.PositionalInitFields;
        if (arguments.Positional.Count > positionalFields.Length)
        {
            reason = "too many positional arguments";
            offendingExpression = null;
            return true;
        }

        var assigned = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < arguments.Positional.Count; i++)
        {
            assigned.Add(positionalFields[i].Name);
        }

        foreach (var (keyword, expression) in arguments.Keywords)
        {
            if (!summary.FieldsByName.TryGetValue(keyword, out var field) || !field.IncludeInInit)
            {
                reason = $"unexpected keyword '{keyword}'";
                offendingExpression = expression;
                return true;
            }

            if (!assigned.Add(keyword))
            {
                reason = $"duplicate binding for '{keyword}'";
                offendingExpression = expression;
                return true;
            }
        }

        foreach (var field in initFields)
        {
            if (!field.HasDefault && !assigned.Contains(field.Name))
            {
                reason = $"missing required argument '{field.Name}'";
                offendingExpression = null;
                return true;
            }
        }

        reason = string.Empty;
        offendingExpression = null;
        return false;
    }

    private static void UpdateBinding(string name, ExpressionSyntax expression, AbstractState bindings)
    {
        if (StaticAbstractValueResolver.TryResolve(expression, bindings, out var value))
        {
            bindings.Set(name, value);
        }
        else
        {
            bindings.Remove(name);
        }
    }

    private static bool TryBindUnpackingTargets(
        IReadOnlyList<UnpackingTargetSyntax> targets,
        ExpressionSyntax expression,
        AbstractState bindings)
    {
        if (targets.Count == 1 && !targets[0].IsStarred)
        {
            UpdateBinding(targets[0].Name, expression, bindings);
            return true;
        }

        if (!TryGetOrderedUnpackingItems(expression, bindings, out var items))
        {
            return false;
        }

        var starredIndex = -1;
        for (var i = 0; i < targets.Count; i++)
        {
            if (targets[i].IsStarred)
            {
                starredIndex = i;
                break;
            }
        }

        if (starredIndex < 0)
        {
            if (items.Count != targets.Count)
            {
                return false;
            }

            for (var i = 0; i < targets.Count; i++)
            {
                bindings.Set(targets[i].Name, items[i].WithSpan(expression.Span));
            }

            return true;
        }

        var required = targets.Count - 1;
        if (items.Count < required)
        {
            return false;
        }

        for (var i = 0; i < starredIndex; i++)
        {
            bindings.Set(targets[i].Name, items[i].WithSpan(expression.Span));
        }

        var rest = new List<AbstractValue>(items.Count - required);
        for (var i = starredIndex; i <= items.Count - (targets.Count - starredIndex); i++)
        {
            rest.Add(items[i].WithSpan(expression.Span));
        }

        bindings.Set(targets[starredIndex].Name, new AbstractValue(AbstractValueKind.List, rest, expression.Span));

        for (var i = starredIndex + 1; i < targets.Count; i++)
        {
            var offset = items.Count - (targets.Count - i);
            bindings.Set(targets[i].Name, items[offset].WithSpan(expression.Span));
        }

        return true;
    }

    private static void BindLoopTargetValue(LoopTargetSyntax target, AbstractValue value, AbstractState bindings)
    {
        switch (target)
        {
            case LoopNameTargetSyntax nameTarget:
                bindings.Set(nameTarget.Name, value);
                break;

            case LoopTupleTargetSyntax tupleTarget:
                if (TryGetFixedSequenceItems(value, out var items) &&
                    items.Count == tupleTarget.Items.Count)
                {
                    for (var i = 0; i < tupleTarget.Items.Count; i++)
                    {
                        BindLoopTargetValue(tupleTarget.Items[i], items[i], bindings);
                    }
                }
                else
                {
                    BindLoopTargetUnknown(target, bindings);
                }
                break;
        }
    }

    private static void BindLoopTargetUnknown(LoopTargetSyntax target, AbstractState bindings)
    {
        switch (target)
        {
            case LoopNameTargetSyntax nameTarget:
                bindings.Set(nameTarget.Name, AbstractValue.Unknown());
                break;
            case LoopTupleTargetSyntax tupleTarget:
                foreach (var item in tupleTarget.Items)
                {
                    BindLoopTargetUnknown(item, bindings);
                }
                break;
        }
    }

    private static AbstractValue JoinDictKeys(IReadOnlyList<KeyValuePair<AbstractValue, AbstractValue>> pairs, LythonSourceSpan span)
    {
        var result = AbstractValue.Never(span);
        foreach (var pair in pairs)
        {
            result = AbstractValue.Join(result, pair.Key, span);
        }

        return result.Kind == AbstractValueKind.Never ? AbstractValue.Unknown(span) : result;
    }
}
