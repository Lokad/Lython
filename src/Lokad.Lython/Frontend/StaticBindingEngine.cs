using System.Globalization;

namespace Lokad.Lython.Frontend;

internal static class StaticBindingEngine
{
    public static void UpdateBindings(StatementSyntax statement, AbstractState bindings)
    {
        var mutatedReceivers = CollectMutatedReceiverNames(statement, bindings);

        switch (statement)
        {
            case ImportStatementSyntax importStatement:
                if (importStatement.ImportedMembers is null)
                {
                    if (StaticContracts.IsKnownBuiltinModule(importStatement.ModuleName))
                    {
                        bindings.Set(importStatement.BindingName, AbstractValue.Module(importStatement.ModuleName, importStatement.Span));
                    }
                    else
                    {
                        bindings.Remove(importStatement.BindingName);
                    }
                }
                else
                {
                    if (IsStarImport(importStatement.ImportedMembers))
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

    private static bool IsStarImport(IReadOnlyList<ImportedMemberSyntax> members)
        => members.Count == 1 && members[0].Name == "*";

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

        var initFields = summary.Fields.Where(static field => field.IncludeInInit).ToArray();
        var positionalFields = initFields.Where(static field => !field.KeywordOnly).ToArray();
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
            var field = initFields.FirstOrDefault(candidate => string.Equals(candidate.Name, keyword, StringComparison.Ordinal));
            if (field.Name is null || !consumedFields.Add(keyword))
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

        var initFields = summary.Fields.Where(static field => field.IncludeInInit).ToArray();
        var positionalFields = initFields.Where(static field => !field.KeywordOnly).ToArray();
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
            var field = initFields.FirstOrDefault(candidate => string.Equals(candidate.Name, keyword, StringComparison.Ordinal));
            if (field.Name is null)
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

    private static bool TryApplyArgparseParserMutation(ExpressionSyntax expression, AbstractState bindings)
    {
        if (expression is not CallExpressionSyntax
            {
                Target: MemberExpressionSyntax
                {
                    Target: IdentifierExpressionSyntax { Name: var receiverName },
                    MemberName: "add_argument"
                }
            } call ||
            !TryResolveArgparseParserMutationTarget(receiverName, bindings, out var parserName, out var parserValue) ||
            !StaticCallArguments.TryGetConcreteArguments(call, bindings, out var arguments) ||
            !TryInferArgparseDestination(arguments, bindings, out var destination))
        {
            return false;
        }

        var parser = (AbstractArgparseParserSummary)parserValue.Value;
        var members = new Dictionary<string, AbstractValue>(parser.Members, StringComparer.Ordinal)
        {
            [destination] = InferArgparseMemberValue(arguments, bindings, call.Span)
        };

        bindings.Set(parserName, AbstractValue.ArgparseParser(new AbstractArgparseParserSummary(members, parser.IsSealed), call.Span));
        return true;
    }

    private static bool TryResolveArgparseParserMutationTarget(
        string receiverName,
        AbstractState bindings,
        out string parserName,
        out AbstractValue parserValue)
    {
        if (bindings.TryGet(receiverName, out parserValue) &&
            parserValue.Kind == AbstractValueKind.ArgparseParser)
        {
            parserName = receiverName;
            return true;
        }

        if (bindings.TryGet(receiverName, out var groupValue) &&
            groupValue.Kind == AbstractValueKind.ArgparseMutuallyExclusiveGroup)
        {
            var group = (AbstractArgparseGroupSummary)groupValue.Value;
            if (!string.IsNullOrEmpty(group.ParserName) &&
                bindings.TryGet(group.ParserName, out parserValue) &&
                parserValue.Kind == AbstractValueKind.ArgparseParser)
            {
                parserName = group.ParserName;
                return true;
            }
        }

        parserName = string.Empty;
        parserValue = default;
        return false;
    }

    private static bool TryInferArgparseDestination(ConcreteCallArguments arguments, AbstractState bindings, out string destination)
    {
        if (arguments.Keywords.TryGetValue("dest", out var destExpression) &&
            StaticAbstractValueResolver.TryResolveKnownString(destExpression, bindings, out var explicitDestination) &&
            !string.IsNullOrWhiteSpace(explicitDestination))
        {
            destination = explicitDestination;
            return true;
        }

        var optionNames = new List<string>(arguments.Positional.Count);
        foreach (var optionExpression in arguments.Positional)
        {
            if (StaticAbstractValueResolver.TryResolveKnownString(optionExpression, bindings, out var optionName))
            {
                optionNames.Add(optionName);
                continue;
            }

            destination = string.Empty;
            return false;
        }

        for (var i = optionNames.Count - 1; i >= 0; i--)
        {
            var optionName = optionNames[i];
            if (optionName.StartsWith("--", StringComparison.Ordinal) && optionName.Length > 2)
            {
                destination = NormalizeArgparseDestination(optionName);
                return true;
            }
        }

        for (var i = optionNames.Count - 1; i >= 0; i--)
        {
            var optionName = optionNames[i];
            if (optionName.StartsWith("-", StringComparison.Ordinal) && optionName.Length > 1)
            {
                destination = NormalizeArgparseDestination(optionName);
                return true;
            }
        }

        foreach (var optionName in optionNames)
        {
            if (!optionName.StartsWith("-", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(optionName))
            {
                destination = optionName;
                return true;
            }
        }

        destination = string.Empty;
        return false;
    }

    private static string NormalizeArgparseDestination(string optionName)
        => optionName.TrimStart('-').Replace('-', '_');

    private static AbstractValue InferArgparseMemberValue(
        ConcreteCallArguments arguments,
        AbstractState bindings,
        LythonSourceSpan span)
    {
        var action = TryGetKnownStringKeyword(arguments, bindings, "action", out var actionText)
            ? actionText
            : "store";

        if (action == "store_true" || action == "store_false")
        {
            return AbstractValue.BooleanType(span);
        }

        var isOptional = IsArgparseOptional(arguments, bindings);
        var required = TryGetKnownBooleanKeyword(arguments, "required", out var requiredValue) && requiredValue;
        var hasDefault = TryResolveArgparseDefault(arguments, bindings, out var defaultValue);
        var scalarValue = ResolveArgparseScalarValue(arguments, span);

        if (action == "append")
        {
            var listValue = AbstractValue.ListOf(scalarValue, span);
            if (hasDefault)
            {
                return AbstractValue.Join(listValue, defaultValue, span);
            }

            return isOptional && !required ? AbstractValue.Unknown(span) : listValue;
        }

        if (TryGetKnownStringKeyword(arguments, bindings, "nargs", out var nargs) && nargs is "*" or "+")
        {
            var listValue = AbstractValue.ListOf(scalarValue, span);
            if (hasDefault)
            {
                return AbstractValue.Join(listValue, defaultValue, span);
            }

            return isOptional && !required && nargs == "*" ? AbstractValue.Unknown(span) : listValue;
        }

        if (action == "store_const")
        {
            if (arguments.Keywords.TryGetValue("const", out var constExpression))
            {
                var constValue = StaticAbstractValueResolver.ResolveOrUnknown(constExpression, bindings).WithSpan(span);
                return hasDefault ? AbstractValue.Join(constValue, defaultValue, span) : constValue;
            }

            return hasDefault ? defaultValue.WithSpan(span) : AbstractValue.Unknown(span);
        }

        if (hasDefault)
        {
            return AbstractValue.Join(scalarValue, defaultValue, span);
        }

        return isOptional && !required ? AbstractValue.Unknown(span) : scalarValue;
    }

    private static AbstractValue ResolveArgparseScalarValue(ConcreteCallArguments arguments, LythonSourceSpan span)
    {
        if (!arguments.Keywords.TryGetValue("type", out var typeExpression))
        {
            return AbstractValue.StringType(span);
        }

        while (typeExpression is ParenthesizedExpressionSyntax parenthesized)
        {
            typeExpression = parenthesized.Inner;
        }

        return typeExpression switch
        {
            IdentifierExpressionSyntax { Name: "int" } => AbstractValue.IntegerType(span),
            IdentifierExpressionSyntax { Name: "float" } => AbstractValue.FloatType(span),
            IdentifierExpressionSyntax { Name: "str" } => AbstractValue.StringType(span),
            IdentifierExpressionSyntax { Name: "bool" } => AbstractValue.BooleanType(span),
            _ => AbstractValue.Unknown(span)
        };
    }

    private static bool TryResolveArgparseDefault(ConcreteCallArguments arguments, AbstractState bindings, out AbstractValue value)
    {
        if (arguments.Keywords.TryGetValue("default", out var defaultExpression) &&
            defaultExpression is not NoneLiteralExpressionSyntax)
        {
            value = StaticAbstractValueResolver.ResolveOrUnknown(defaultExpression, bindings);
            return true;
        }

        value = default;
        return false;
    }

    private static bool IsArgparseOptional(ConcreteCallArguments arguments, AbstractState bindings)
    {
        foreach (var optionExpression in arguments.Positional)
        {
            if (StaticAbstractValueResolver.TryResolveKnownString(optionExpression, bindings, out var optionName) &&
                optionName.StartsWith("-", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetKnownStringKeyword(ConcreteCallArguments arguments, AbstractState bindings, string keyword, out string value)
    {
        if (arguments.Keywords.TryGetValue(keyword, out var expression))
        {
            return StaticAbstractValueResolver.TryResolveKnownString(expression, bindings, out value);
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetKnownBooleanKeyword(ConcreteCallArguments arguments, string keyword, out bool value)
    {
        if (arguments.Keywords.TryGetValue(keyword, out var expression))
        {
            while (expression is ParenthesizedExpressionSyntax parenthesized)
            {
                expression = parenthesized.Inner;
            }

            if (expression is BooleanLiteralExpressionSyntax boolean)
            {
                value = boolean.Value;
                return true;
            }
        }

        value = false;
        return false;
    }

    private static HashSet<string> CollectMutatedReceiverNames(StatementSyntax statement, AbstractState bindings)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        switch (statement)
        {
            case AssignmentStatementSyntax assignment:
                CollectMutatedReceiverNames(assignment.Expression, bindings, names);
                break;

            case ChainedAssignmentStatementSyntax chained:
                CollectMutatedReceiverNames(chained.Expression, bindings, names);
                break;

            case AnnotatedAssignmentStatementSyntax annotated:
                CollectMutatedReceiverNames(annotated.Annotation, bindings, names);
                CollectMutatedReceiverNames(annotated.Expression, bindings, names);
                break;

            case SubscriptAssignmentStatementSyntax subscript:
                CollectMutatedReceiverNames(subscript.Target, bindings, names);
                CollectMutatedReceiverNames(subscript.Index, bindings, names);
                CollectMutatedReceiverNames(subscript.Expression, bindings, names);
                break;

            case SliceAssignmentStatementSyntax slice:
                CollectMutatedReceiverNames(slice.Target, bindings, names);
                CollectMutatedReceiverNames(slice.Start, bindings, names);
                CollectMutatedReceiverNames(slice.End, bindings, names);
                CollectMutatedReceiverNames(slice.Step, bindings, names);
                CollectMutatedReceiverNames(slice.Expression, bindings, names);
                break;

            case MemberAssignmentStatementSyntax member:
                CollectMutatedReceiverNames(member.Target, bindings, names);
                CollectMutatedReceiverNames(member.Expression, bindings, names);
                break;

            case AugmentedAssignmentStatementSyntax augmented:
                CollectMutatedReceiverNames(augmented.Target, bindings, names);
                CollectMutatedReceiverNames(augmented.Expression, bindings, names);
                break;

            case UnpackingAssignmentStatementSyntax unpacking:
                CollectMutatedReceiverNames(unpacking.Expression, bindings, names);
                break;

            case ExpressionStatementSyntax expressionStatement:
                CollectMutatedReceiverNames(expressionStatement.Expression, bindings, names);
                break;

            case WithStatementSyntax withStatement:
                CollectMutatedReceiverNames(withStatement.ContextExpression, bindings, names);
                break;

            case IfStatementSyntax ifStatement:
                CollectMutatedReceiverNames(ifStatement.Condition, bindings, names);
                break;

            case ForStatementSyntax forStatement:
                CollectMutatedReceiverNames(forStatement.Iterable, bindings, names);
                break;

            case WhileStatementSyntax whileStatement:
                CollectMutatedReceiverNames(whileStatement.Condition, bindings, names);
                break;

            case MatchStatementSyntax matchStatement:
                CollectMutatedReceiverNames(matchStatement.Subject, bindings, names);
                foreach (var matchCase in matchStatement.Cases)
                {
                    CollectMutatedReceiverNames(matchCase.Guard, bindings, names);
                }
                break;

            case AssertStatementSyntax assertStatement:
                CollectMutatedReceiverNames(assertStatement.Condition, bindings, names);
                CollectMutatedReceiverNames(assertStatement.Message, bindings, names);
                break;

            case DeleteStatementSyntax deleteStatement:
                CollectMutatedReceiverNames(deleteStatement.Target, bindings, names);
                break;

            case FunctionDefinitionStatementSyntax functionDefinition:
                foreach (var decorator in functionDefinition.Decorators)
                {
                    CollectMutatedReceiverNames(decorator, bindings, names);
                }

                foreach (var parameter in functionDefinition.Parameters)
                {
                    CollectMutatedReceiverNames(parameter.Annotation, bindings, names);
                    CollectMutatedReceiverNames(parameter.DefaultValue, bindings, names);
                }

                CollectMutatedReceiverNames(functionDefinition.ReturnAnnotation, bindings, names);
                break;

            case ClassDefinitionStatementSyntax classDefinition:
                foreach (var decorator in classDefinition.Decorators)
                {
                    CollectMutatedReceiverNames(decorator, bindings, names);
                }

                foreach (var @base in classDefinition.Bases)
                {
                    CollectMutatedReceiverNames(@base, bindings, names);
                }

                foreach (var keywordArgument in classDefinition.KeywordArguments)
                {
                    CollectMutatedReceiverNames(keywordArgument.Value, bindings, names);
                }
                break;

            case ReturnStatementSyntax returnStatement:
                CollectMutatedReceiverNames(returnStatement.Expression, bindings, names);
                break;

            case RaiseStatementSyntax raiseStatement:
                CollectMutatedReceiverNames(raiseStatement.Expression, bindings, names);
                break;
        }

        return names;
    }

    private static void CollectMutatedReceiverNames(AssignmentTargetSyntax target, AbstractState bindings, HashSet<string> names)
    {
        switch (target)
        {
            case SubscriptAssignmentTargetSyntax subscript:
                CollectMutatedReceiverNames(subscript.Target, bindings, names);
                CollectMutatedReceiverNames(subscript.Index, bindings, names);
                break;

            case SliceAssignmentTargetSyntax slice:
                CollectMutatedReceiverNames(slice.Target, bindings, names);
                CollectMutatedReceiverNames(slice.Start, bindings, names);
                CollectMutatedReceiverNames(slice.End, bindings, names);
                CollectMutatedReceiverNames(slice.Step, bindings, names);
                break;

            case MemberAssignmentTargetSyntax member:
                CollectMutatedReceiverNames(member.Target, bindings, names);
                break;
        }
    }

    private static void CollectMutatedReceiverNames(ExpressionSyntax? expression, AbstractState bindings, HashSet<string> names)
    {
        if (expression is null)
        {
            return;
        }

        switch (expression)
        {
            case FormattedStringExpressionSyntax formatted:
                foreach (var part in formatted.Parts)
                {
                    if (part is FormattedStringExpressionPartSyntax expressionPart)
                    {
                        CollectMutatedReceiverNames(expressionPart.Expression, bindings, names);
                    }
                }
                break;

            case ListLiteralExpressionSyntax list:
                CollectMutatedReceiverNames(list.Items, bindings, names);
                break;

            case ListComprehensionExpressionSyntax listComprehension:
                foreach (var clause in listComprehension.Clauses)
                {
                    CollectMutatedReceiverNames(clause.Iterable, bindings, names);
                    CollectMutatedReceiverNames(clause.Condition, bindings, names);
                }
                CollectMutatedReceiverNames(listComprehension.ItemExpression, bindings, names);
                break;

            case GeneratorExpressionSyntax generator:
                foreach (var clause in generator.Clauses)
                {
                    CollectMutatedReceiverNames(clause.Iterable, bindings, names);
                    CollectMutatedReceiverNames(clause.Condition, bindings, names);
                }
                CollectMutatedReceiverNames(generator.ItemExpression, bindings, names);
                break;

            case DictComprehensionExpressionSyntax dictComprehension:
                foreach (var clause in dictComprehension.Clauses)
                {
                    CollectMutatedReceiverNames(clause.Iterable, bindings, names);
                    CollectMutatedReceiverNames(clause.Condition, bindings, names);
                }
                CollectMutatedReceiverNames(dictComprehension.KeyExpression, bindings, names);
                CollectMutatedReceiverNames(dictComprehension.ValueExpression, bindings, names);
                break;

            case DictLiteralExpressionSyntax dict:
                foreach (var item in dict.Items)
                {
                    CollectMutatedReceiverNames(item.Key, bindings, names);
                    CollectMutatedReceiverNames(item.Value, bindings, names);
                }
                break;

            case SetLiteralExpressionSyntax set:
                CollectMutatedReceiverNames(set.Items, bindings, names);
                break;

            case TupleLiteralExpressionSyntax tuple:
                CollectMutatedReceiverNames(tuple.Items, bindings, names);
                break;

            case ParenthesizedExpressionSyntax parenthesized:
                CollectMutatedReceiverNames(parenthesized.Inner, bindings, names);
                break;

            case MemberExpressionSyntax member:
                CollectMutatedReceiverNames(member.Target, bindings, names);
                break;

            case CallExpressionSyntax call:
                CollectMutatingCallReceiverName(call, bindings, names);
                CollectMutatedReceiverNames(call.Target, bindings, names);
                foreach (var argument in call.Arguments)
                {
                    CollectMutatedReceiverNames(argument.Expression, bindings, names);
                }
                break;

            case SubscriptExpressionSyntax subscript:
                CollectMutatedReceiverNames(subscript.Target, bindings, names);
                CollectMutatedReceiverNames(subscript.Index, bindings, names);
                break;

            case SliceExpressionSyntax slice:
                CollectMutatedReceiverNames(slice.Target, bindings, names);
                CollectMutatedReceiverNames(slice.Start, bindings, names);
                CollectMutatedReceiverNames(slice.End, bindings, names);
                CollectMutatedReceiverNames(slice.Step, bindings, names);
                break;

            case BinaryExpressionSyntax binary:
                CollectMutatedReceiverNames(binary.Left, bindings, names);
                CollectMutatedReceiverNames(binary.Right, bindings, names);
                break;

            case ChainedComparisonExpressionSyntax chained:
                CollectMutatedReceiverNames(chained.Operands, bindings, names);
                break;

            case UnaryExpressionSyntax unary:
                CollectMutatedReceiverNames(unary.Operand, bindings, names);
                break;

            case ConditionalExpressionSyntax conditional:
                CollectMutatedReceiverNames(conditional.Condition, bindings, names);
                CollectMutatedReceiverNames(conditional.Consequent, bindings, names);
                CollectMutatedReceiverNames(conditional.Alternative, bindings, names);
                break;

            case AssignmentExpressionSyntax assignment:
                CollectMutatedReceiverNames(assignment.Expression, bindings, names);
                break;
        }
    }

    private static void CollectMutatedReceiverNames(IReadOnlyList<ExpressionSyntax> expressions, AbstractState bindings, HashSet<string> names)
    {
        foreach (var expression in expressions)
        {
            CollectMutatedReceiverNames(expression, bindings, names);
        }
    }

    private static void CollectMutatingCallReceiverName(CallExpressionSyntax call, AbstractState bindings, HashSet<string> names)
    {
        if (call.Target is MemberExpressionSyntax
            {
                Target: IdentifierExpressionSyntax { Name: "operator" },
                MemberName: "setitem"
            } &&
            StaticCallArguments.TryGetConcreteArguments(call, bindings, out var operatorArguments) &&
            operatorArguments.Positional.Count > 0 &&
            operatorArguments.Positional[0] is IdentifierExpressionSyntax operatorReceiver)
        {
            names.Add(operatorReceiver.Name);
            return;
        }

        if (call.Target is not MemberExpressionSyntax
            {
                Target: IdentifierExpressionSyntax receiverIdentifier,
                MemberName: var memberName
            } ||
            !bindings.TryGet(receiverIdentifier.Name, out var receiver) ||
            !StaticContracts.IsMutatingMember(receiver, memberName))
        {
            return;
        }

        if (StaticCallArguments.TryGetConcreteArguments(call, bindings, out var arguments) &&
            StaticContracts.TryGetCallableContract(receiver, memberName, out var contract) &&
            !contract.AcceptsArgumentShape(arguments))
        {
            return;
        }

        names.Add(receiverIdentifier.Name);
    }

    private static bool TryBuildSimpleClassSummary(
        ClassDefinitionStatementSyntax classDefinition,
        AbstractState bindings,
        out AbstractClassSummary summary)
    {
        if (classDefinition.DataclassDecorator is null ||
            classDefinition.Bases.Count != 0 ||
            classDefinition.KeywordArguments.Count != 0)
        {
            summary = default!;
            return false;
        }

        var fields = new List<AbstractClassFieldSummary>();
        var methods = new Dictionary<string, AbstractFunctionSummary>(StringComparer.Ordinal);
        foreach (var statement in classDefinition.Body)
        {
            switch (statement)
            {
                case PassStatementSyntax:
                    break;

                case FunctionDefinitionStatementSyntax methodDefinition:
                    if (TryBuildSimpleFunctionSummary(methodDefinition, bindings, out var methodSummary))
                    {
                        methods[methodDefinition.Name] = methodSummary;
                    }
                    break;

                case AnnotatedAssignmentStatementSyntax annotated:
                    if (IsClassOnlyDataclassField(annotated.Annotation))
                    {
                        break;
                    }

                    var initOnly = IsInitOnlyDataclassField(annotated.Annotation);
                    var hasDefault = TryGetDataclassFieldDefault(annotated.Expression, bindings, out var defaultValue);
                    var includeInInit = TryGetDataclassFieldInit(annotated.Expression, out var init) ? init : true;
                    var keywordOnly = classDefinition.DataclassDecorator!.KwOnly ||
                        (TryGetDataclassFieldKeywordOnly(annotated.Expression, out var kwOnly) && kwOnly);
                    fields.Add(new AbstractClassFieldSummary(
                        annotated.Name,
                        hasDefault ? defaultValue : AbstractValue.Unknown(annotated.Span),
                        hasDefault,
                        includeInInit,
                        keywordOnly,
                        StoreOnInstance: !initOnly,
                        annotated.Span));
                    break;

                default:
                    summary = default!;
                    return false;
            }
        }

        if (fields.Count == 0)
        {
            summary = default!;
            return false;
        }

        summary = new AbstractClassSummary(classDefinition.Name, fields, methods, IsDataclass: true, classDefinition.Span);
        return true;
    }

    private static bool TryGetDataclassFieldDefault(ExpressionSyntax? expression, AbstractState bindings, out AbstractValue value)
    {
        if (expression is null)
        {
            value = default;
            return false;
        }

        if (TryGetDataclassFieldArgument(expression, 0, "default", out var defaultExpression))
        {
            value = StaticAbstractValueResolver.ResolveOrUnknown(defaultExpression, bindings);
            return true;
        }

        if (TryGetDataclassFieldArgument(expression, 1, "default_factory", out var defaultFactoryExpression))
        {
            return TryGetDataclassDefaultFactoryValue(defaultFactoryExpression, expression.Span, out value);
        }

        if (IsDataclassFieldCall(expression))
        {
            value = default;
            return false;
        }

        value = StaticAbstractValueResolver.ResolveOrUnknown(expression, bindings);
        return true;
    }

    private static bool TryGetDataclassFieldInit(ExpressionSyntax? expression, out bool init)
    {
        init = true;
        if (!TryGetDataclassFieldArgument(expression, 2, "init", out var initExpression))
        {
            return false;
        }

        return TryGetBooleanLiteral(initExpression, out init);
    }

    private static bool TryGetDataclassFieldKeywordOnly(ExpressionSyntax? expression, out bool keywordOnly)
    {
        keywordOnly = false;
        if (!TryGetDataclassFieldArgument(expression, 7, "kw_only", out var keywordOnlyExpression))
        {
            return false;
        }

        return TryGetBooleanLiteral(keywordOnlyExpression, out keywordOnly);
    }

    private static bool TryGetDataclassFieldArgument(ExpressionSyntax? expression, int position, string keyword, out ExpressionSyntax argument)
    {
        if (expression is CallExpressionSyntax call &&
            IsDataclassFieldCall(call) &&
            StaticCallArguments.TryGetConcreteArguments(call, out var arguments) &&
            arguments.TryGetValue(position, keyword, out argument))
        {
            return true;
        }

        argument = default!;
        return false;
    }

    private static bool IsDataclassFieldCall(ExpressionSyntax expression)
        => expression is CallExpressionSyntax call && IsDataclassFieldCall(call);

    private static bool IsDataclassFieldCall(CallExpressionSyntax call)
        => call.Target is IdentifierExpressionSyntax { Name: "field" } or
            MemberExpressionSyntax { Target: IdentifierExpressionSyntax { Name: "dataclasses" }, MemberName: "field" };

    private static bool TryGetDataclassDefaultFactoryValue(ExpressionSyntax expression, LythonSourceSpan span, out AbstractValue value)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Inner;
        }

        switch (expression)
        {
            case IdentifierExpressionSyntax { Name: "list" }:
                value = new AbstractValue(AbstractValueKind.List, Array.Empty<AbstractValue>(), span);
                return true;
            case IdentifierExpressionSyntax { Name: "tuple" }:
                value = new AbstractValue(AbstractValueKind.Tuple, Array.Empty<AbstractValue>(), span);
                return true;
            case IdentifierExpressionSyntax { Name: "dict" }:
                value = AbstractValue.Dict(Array.Empty<KeyValuePair<AbstractValue, AbstractValue>>(), span);
                return true;
            case IdentifierExpressionSyntax { Name: "set" }:
                value = new AbstractValue(AbstractValueKind.Set, Array.Empty<AbstractValue>(), span);
                return true;
            case IdentifierExpressionSyntax { Name: "str" }:
                value = AbstractValue.StringType(span);
                return true;
            case IdentifierExpressionSyntax { Name: "bytes" }:
                value = AbstractValue.BytesType(span);
                return true;
            case IdentifierExpressionSyntax { Name: "int" }:
                value = AbstractValue.IntegerType(span);
                return true;
            case IdentifierExpressionSyntax { Name: "float" }:
                value = AbstractValue.FloatType(span);
                return true;
            case IdentifierExpressionSyntax { Name: "bool" }:
                value = AbstractValue.BooleanType(span);
                return true;
            default:
                value = AbstractValue.Unknown(span);
                return true;
        }
    }

    private static bool TryGetBooleanLiteral(ExpressionSyntax expression, out bool value)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Inner;
        }

        if (expression is BooleanLiteralExpressionSyntax boolean)
        {
            value = boolean.Value;
            return true;
        }

        value = false;
        return false;
    }

    private static bool IsClassOnlyDataclassField(ExpressionSyntax annotation)
    {
        while (annotation is ParenthesizedExpressionSyntax parenthesized)
        {
            annotation = parenthesized.Inner;
        }

        if (annotation is SubscriptExpressionSyntax subscript)
        {
            annotation = subscript.Target;
        }

        return annotation switch
        {
            IdentifierExpressionSyntax { Name: "ClassVar" or "KW_ONLY" } => true,
            MemberExpressionSyntax { Target: IdentifierExpressionSyntax { Name: "typing" }, MemberName: "ClassVar" } => true,
            MemberExpressionSyntax { Target: IdentifierExpressionSyntax { Name: "dataclasses" }, MemberName: "KW_ONLY" } => true,
            _ => false
        };
    }

    private static bool IsInitOnlyDataclassField(ExpressionSyntax annotation)
    {
        while (annotation is ParenthesizedExpressionSyntax parenthesized)
        {
            annotation = parenthesized.Inner;
        }

        if (annotation is SubscriptExpressionSyntax subscript)
        {
            annotation = subscript.Target;
        }

        return annotation switch
        {
            IdentifierExpressionSyntax { Name: "InitVar" } => true,
            MemberExpressionSyntax { Target: IdentifierExpressionSyntax { Name: "dataclasses" }, MemberName: "InitVar" } => true,
            _ => false
        };
    }

    private static bool TryBuildSimpleFunctionSummary(
        FunctionDefinitionStatementSyntax functionDefinition,
        AbstractState bindings,
        out AbstractFunctionSummary summary)
    {
        if (functionDefinition.Decorators.Count != 0 ||
            functionDefinition.Parameters.Any(static parameter => parameter.Kind is FunctionParameterKind.VariadicList or FunctionParameterKind.VariadicDictionary) ||
            ScopeDirectiveFactsCollector.ContainsScopeDirective(functionDefinition.Body) ||
            !IsStraightLineSummaryBody(functionDefinition.Body))
        {
            summary = default!;
            return false;
        }

        var capturedBindings = bindings.Clone();
        capturedBindings.Remove(functionDefinition.Name);
        summary = new AbstractFunctionSummary(functionDefinition.Parameters, functionDefinition.Body, capturedBindings, functionDefinition.Span);
        return true;
    }

    private static bool IsStraightLineSummaryBody(IReadOnlyList<StatementSyntax> statements)
        => IsStraightLineSummaryBody(statements, out var hasReturn) && hasReturn;

    private static bool IsStraightLineSummaryBody(IReadOnlyList<StatementSyntax> statements, out bool hasReturn)
    {
        hasReturn = false;
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case AssignmentStatementSyntax:
                case ChainedAssignmentStatementSyntax:
                case AnnotatedAssignmentStatementSyntax:
                case ExpressionStatementSyntax:
                case PassStatementSyntax:
                    break;

                case WithStatementSyntax withStatement:
                    if (!IsStraightLineSummaryBody(withStatement.Body, out var withHasReturn))
                    {
                        return false;
                    }

                    hasReturn |= withHasReturn;
                    break;

                case IfStatementSyntax ifStatement:
                    if (!IsStraightLineSummaryBody(ifStatement.ThenStatements, out var thenHasReturn))
                    {
                        return false;
                    }

                    if (ifStatement.ElseStatements is not null)
                    {
                        if (!IsStraightLineSummaryBody(ifStatement.ElseStatements, out var elseHasReturn))
                        {
                            return false;
                        }

                        hasReturn |= thenHasReturn && elseHasReturn;
                    }
                    else
                    {
                        hasReturn |= thenHasReturn;
                    }
                    break;

                case ReturnStatementSyntax:
                    hasReturn = true;
                    break;

                default:
                    return false;
            }
        }

        return true;
    }

    private static bool TryGetParameterCallShapeFailure(
        IReadOnlyList<FunctionParameterSyntax> parameters,
        int firstParameterIndex,
        ConcreteCallArguments arguments,
        out string reason,
        out ExpressionSyntax? offendingExpression)
    {
        if (parameters.Any(static parameter => parameter.Kind is FunctionParameterKind.VariadicList or FunctionParameterKind.VariadicDictionary))
        {
            reason = string.Empty;
            offendingExpression = null;
            return false;
        }

        if (firstParameterIndex > parameters.Count)
        {
            reason = "method has no receiver parameter";
            offendingExpression = null;
            return true;
        }

        var effectiveParameters = parameters.Skip(firstParameterIndex).ToArray();
        var positionalParameters = effectiveParameters.Where(static parameter => parameter.Kind == FunctionParameterKind.Positional).ToArray();
        if (arguments.Positional.Count > positionalParameters.Length)
        {
            reason = "too many positional arguments";
            offendingExpression = null;
            return true;
        }

        var assigned = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < arguments.Positional.Count; i++)
        {
            assigned.Add(positionalParameters[i].Name);
        }

        foreach (var (keyword, expression) in arguments.Keywords)
        {
            var parameter = effectiveParameters.FirstOrDefault(candidate => string.Equals(candidate.Name, keyword, StringComparison.Ordinal));
            if (parameter is null)
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

        foreach (var parameter in effectiveParameters)
        {
            if (parameter.DefaultValue is null && !assigned.Contains(parameter.Name))
            {
                reason = $"missing required argument '{parameter.Name}'";
                offendingExpression = null;
                return true;
            }
        }

        reason = string.Empty;
        offendingExpression = null;
        return false;
    }

    private static bool TryBindFunctionArguments(
        AbstractFunctionSummary summary,
        ConcreteCallArguments arguments,
        AbstractState callBindings,
        out AbstractState functionBindings)
    {
        functionBindings = summary.CapturedBindings.Clone();
        var consumedKeywords = new HashSet<string>(StringComparer.Ordinal);
        var positionalIndex = 0;

        foreach (var parameter in summary.Parameters)
        {
            if (parameter.Kind == FunctionParameterKind.KeywordOnly)
            {
                if (arguments.Keywords.TryGetValue(parameter.Name, out var keywordExpression))
                {
                    consumedKeywords.Add(parameter.Name);
                    functionBindings.Set(parameter.Name, arguments.ResolveKeywordValue(parameter.Name, callBindings));
                }
                else if (parameter.DefaultValue is not null)
                {
                    functionBindings.Set(parameter.Name, ResolveArgumentValue(parameter.DefaultValue, summary.CapturedBindings));
                }
                else
                {
                    return false;
                }

                continue;
            }

            if (parameter.Kind != FunctionParameterKind.Positional)
            {
                return false;
            }

            if (positionalIndex < arguments.Positional.Count)
            {
                if (arguments.Keywords.ContainsKey(parameter.Name))
                {
                    return false;
                }

                functionBindings.Set(parameter.Name, arguments.ResolvePositionalValue(positionalIndex, callBindings));
                positionalIndex++;
            }
            else if (arguments.Keywords.TryGetValue(parameter.Name, out var keywordExpression))
            {
                consumedKeywords.Add(parameter.Name);
                functionBindings.Set(parameter.Name, arguments.ResolveKeywordValue(parameter.Name, callBindings));
            }
            else if (parameter.DefaultValue is not null)
            {
                functionBindings.Set(parameter.Name, ResolveArgumentValue(parameter.DefaultValue, summary.CapturedBindings));
            }
            else
            {
                return false;
            }
        }

        if (positionalIndex != arguments.Positional.Count)
        {
            return false;
        }

        foreach (var keyword in arguments.Keywords.Keys)
        {
            if (!consumedKeywords.Contains(keyword))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryBindInstanceMethodArguments(
        AbstractFunctionSummary summary,
        AbstractValue selfValue,
        ConcreteCallArguments arguments,
        AbstractState callBindings,
        out AbstractState methodBindings)
    {
        methodBindings = summary.CapturedBindings.Clone();
        if (summary.Parameters.Count == 0 || summary.Parameters[0].Kind != FunctionParameterKind.Positional)
        {
            return false;
        }

        methodBindings.Set(summary.Parameters[0].Name, selfValue.WithSpan(summary.Span));
        var consumedKeywords = new HashSet<string>(StringComparer.Ordinal);
        var positionalIndex = 0;

        for (var parameterIndex = 1; parameterIndex < summary.Parameters.Count; parameterIndex++)
        {
            var parameter = summary.Parameters[parameterIndex];
            if (parameter.Kind == FunctionParameterKind.KeywordOnly)
            {
                if (arguments.Keywords.TryGetValue(parameter.Name, out var keywordExpression))
                {
                    consumedKeywords.Add(parameter.Name);
                    methodBindings.Set(parameter.Name, arguments.ResolveKeywordValue(parameter.Name, callBindings));
                }
                else if (parameter.DefaultValue is not null)
                {
                    methodBindings.Set(parameter.Name, ResolveArgumentValue(parameter.DefaultValue, summary.CapturedBindings));
                }
                else
                {
                    return false;
                }

                continue;
            }

            if (parameter.Kind != FunctionParameterKind.Positional)
            {
                return false;
            }

            if (positionalIndex < arguments.Positional.Count)
            {
                if (arguments.Keywords.ContainsKey(parameter.Name))
                {
                    return false;
                }

                methodBindings.Set(parameter.Name, arguments.ResolvePositionalValue(positionalIndex, callBindings));
                positionalIndex++;
            }
            else if (arguments.Keywords.TryGetValue(parameter.Name, out var keywordExpression))
            {
                consumedKeywords.Add(parameter.Name);
                methodBindings.Set(parameter.Name, arguments.ResolveKeywordValue(parameter.Name, callBindings));
            }
            else if (parameter.DefaultValue is not null)
            {
                methodBindings.Set(parameter.Name, ResolveArgumentValue(parameter.DefaultValue, summary.CapturedBindings));
            }
            else
            {
                return false;
            }
        }

        if (positionalIndex != arguments.Positional.Count)
        {
            return false;
        }

        foreach (var keyword in arguments.Keywords.Keys)
        {
            if (!consumedKeywords.Contains(keyword))
            {
                return false;
            }
        }

        return true;
    }

    private static AbstractValue ResolveArgumentValue(ExpressionSyntax expression, AbstractState bindings)
        => StaticAbstractValueResolver.ResolveOrUnknown(expression, bindings);

    private static bool TryInferStraightLineReturn(
        IReadOnlyList<StatementSyntax> statements,
        AbstractState bindings,
        out AbstractValue returnValue)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
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

                case ExpressionStatementSyntax expressionStatement:
                    TryApplyArgparseParserMutation(expressionStatement.Expression, bindings);
                    break;

                case PassStatementSyntax:
                    break;

                case WithStatementSyntax withStatement:
                {
                    var withBindings = bindings.Clone();
                    if (withStatement.VariableName is not null &&
                        StaticAbstractValueResolver.TryResolve(withStatement.ContextExpression, bindings, out var contextValue) &&
                        contextValue.Kind == AbstractValueKind.TextFileHandle)
                    {
                        withBindings.Set(withStatement.VariableName, contextValue);
                    }

                    if (TryInferStraightLineReturn(withStatement.Body, withBindings, out returnValue))
                    {
                        return true;
                    }

                    bindings.ReplaceWith(withBindings);
                    if (withStatement.VariableName is not null)
                    {
                        bindings.Remove(withStatement.VariableName);
                    }

                    break;
                }

                case IfStatementSyntax ifStatement:
                {
                    var thenBindings = bindings.Clone();
                    StaticConditionRefinements.Apply(ifStatement.Condition, assumedTruth: true, thenBindings);
                    var thenReturned = TryInferStraightLineReturn(ifStatement.ThenStatements, thenBindings, out var thenReturnValue);
                    var elseBindings = bindings.Clone();
                    StaticConditionRefinements.Apply(ifStatement.Condition, assumedTruth: false, elseBindings);
                    AbstractValue elseReturnValue = default;
                    var elseReturned = ifStatement.ElseStatements is not null &&
                                       TryInferStraightLineReturn(ifStatement.ElseStatements, elseBindings, out elseReturnValue);

                    if (thenReturned && elseReturned)
                    {
                        returnValue = AbstractValue.Join(thenReturnValue, elseReturnValue, statement.Span);
                        return true;
                    }

                    if (thenReturned)
                    {
                        bindings.ReplaceWith(elseBindings);
                    }
                    else if (elseReturned)
                    {
                        bindings.ReplaceWith(thenBindings);
                    }
                    else
                    {
                        bindings.MergeFrom(thenBindings, elseBindings);
                    }

                    break;
                }

                case ReturnStatementSyntax { Expression: null }:
                    returnValue = AbstractValue.None(statement.Span);
                    return true;

                case ReturnStatementSyntax { Expression: var expression }:
                    return StaticAbstractValueResolver.TryResolve(expression, bindings, out returnValue);

                default:
                    returnValue = default;
                    return false;
            }
        }

        returnValue = default;
        return false;
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
