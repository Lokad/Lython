using System.Globalization;

namespace Lokad.Lython.Frontend;

internal static partial class StaticBindingEngine
{
    public static void UpdateBindings(StatementSyntax statement, AbstractState bindings)
    {
        var mutatedReceivers = CollectMutatedReceiverNames(statement, bindings);
        // Aliases to mutable sequences are not tracked. Any known mutating path therefore invalidates
        // all sequence-shape facts before this statement establishes new bindings.
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
            // A mutating method may also change the receiver's more specific abstract kind.
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
                itemValue = (iterableValue.RequireNestedValue()).WithSpan(expression.Span);
                return true;

            case AbstractValueKind.List:
            case AbstractValueKind.Tuple:
            case AbstractValueKind.Set:
                itemValue = JoinSequenceItems(iterableValue.RequireSequenceItems(), expression.Span);
                return true;

            case AbstractValueKind.Dict:
                itemValue = JoinDictKeys(iterableValue.RequireDictionaryItems(), expression.Span);
                return true;

            case AbstractValueKind.TextFileHandle:
                if (iterableValue.RequireTextFileMode() == AbstractTextFileMode.Read)
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
                items = value.RequireSequenceItems();
                return true;

            case AbstractValueKind.String:
                {
                    var text = value.RequireText();
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
                    var bytes = value.RequireBytes();
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
            items = value.RequireSequenceItems();
            return true;
        }

        items = Array.Empty<AbstractValue>();
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

        var layout = UnpackingLayout.FromTargets(targets);
        if (!layout.AcceptsValueCount(items.Count))
        {
            return false;
        }

        if (!layout.HasStarredTarget)
        {
            for (var i = 0; i < targets.Count; i++)
            {
                bindings.Set(targets[i].Name, items[i].WithSpan(expression.Span));
            }

            return true;
        }

        for (var i = 0; i < layout.StarredTargetIndex; i++)
        {
            bindings.Set(targets[i].Name, items[i].WithSpan(expression.Span));
        }

        var starredValueCount = layout.StarredValueCount(items.Count);
        var rest = new List<AbstractValue>(starredValueCount);
        for (var i = 0; i < starredValueCount; i++)
        {
            rest.Add(items[layout.StarredTargetIndex + i].WithSpan(expression.Span));
        }

        bindings.Set(targets[layout.StarredTargetIndex].Name, AbstractValue.List(rest, expression.Span));

        for (var i = layout.StarredTargetIndex + 1; i < targets.Count; i++)
        {
            var offset = layout.SourceIndexForTrailingTarget(i, items.Count);
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
