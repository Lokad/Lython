using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Frontend;

internal static class LoweredScript
{
    internal static IReadOnlyList<LoweredTypeParameter>? LowerTypeParameters(IReadOnlyList<TypeParameterSyntax>? parameters)
        => parameters?.Select(parameter => new LoweredTypeParameter(parameter, parameter.Bound is null ? null : LowerExpression(parameter.Bound),
            parameter.Default is null ? null : LowerExpression(parameter.Default))).ToArray();

    // The shared layout owns names, kinds and binding order. Lower only the
    // expressions that execute in the enclosing scope, in layout order.
    internal static LoweredExpression[] LowerDefaults(IReadOnlyList<FunctionParameterSyntax> parameters,
        FunctionParameterLayout layout)
    {
        if (layout.Defaults.Length == 0) return [];
        var expressions = new LoweredExpression[layout.Defaults.Length];
        for (var i = 0; i < expressions.Length; i++)
            expressions[i] = LowerExpression(parameters[layout.Defaults[i].ParameterIndex].DefaultValue.RequireNotNull());
        return expressions;
    }

    internal static LoweredFunctionAnnotation[] LowerFunctionAnnotations(FunctionDefinitionStatementSyntax definition)
    {
        var count = definition.ReturnAnnotation is null ? 0 : 1;
        var positionalCount = 0;
        var positionalOnlyCount = 0;
        foreach (var parameter in definition.Parameters)
        {
            if (parameter.Annotation is null) continue;
            count++;
            if (parameter.Kind == FunctionParameterKind.Positional) positionalCount++;
            else if (parameter.Kind == FunctionParameterKind.PositionalOnly) positionalOnlyCount++;
        }
        if (count == 0) return [];

        // Exchange the two positional groups for Python's annotation order.
        // The remaining parameter kinds already appear in evaluation order.
        var annotations = new LoweredFunctionAnnotation[count];
        var nextPositional = 0;
        var nextPositionalOnly = positionalCount;
        var nextOther = positionalCount + positionalOnlyCount;
        foreach (var parameter in definition.Parameters)
        {
            if (parameter.Annotation is not { } expression) continue;
            var index = parameter.Kind switch
            {
                FunctionParameterKind.Positional => nextPositional++,
                FunctionParameterKind.PositionalOnly => nextPositionalOnly++,
                _ => nextOther++,
            };
            annotations[index] = new(PyString.FromString(parameter.Name), LowerExpression(expression));
        }
        if (definition.ReturnAnnotation is { } result) annotations[^1] = new(PyString.FromString("return"), LowerExpression(result));
        return annotations;
    }

    internal static LoweredExpression LowerExpression(ExpressionSyntax expression)
    {
        return expression switch
        {
            UnpackedTypeExpressionSyntax unpacked => new LoweredUnpackedTypeExpression(unpacked, LowerExpression(unpacked.Value)),
            YieldExpressionSyntax yielded => new LoweredYieldExpression(yielded, yielded.Value is null ? null : LowerExpression(yielded.Value)),
            IdentifierExpressionSyntax identifier => new LoweredIdentifierExpression(identifier),
            StringLiteralExpressionSyntax text => new LoweredStringLiteralExpression(text),
            BytesLiteralExpressionSyntax bytes => new LoweredBytesLiteralExpression(bytes),
            IntegerLiteralExpressionSyntax integer => new LoweredIntegerLiteralExpression(integer),
            ImaginaryLiteralExpressionSyntax imaginary => new LoweredImaginaryLiteralExpression(imaginary),
            FloatLiteralExpressionSyntax floating => new LoweredFloatLiteralExpression(floating),
            BooleanLiteralExpressionSyntax boolean => new LoweredBooleanLiteralExpression(boolean),
            NoneLiteralExpressionSyntax none => new LoweredNoneLiteralExpression(none),
            EllipsisLiteralExpressionSyntax ellipsis => new LoweredEllipsisLiteralExpression(ellipsis),
            FormattedStringExpressionSyntax formatted => new LoweredFormattedStringExpression(
                formatted,
                formatted.Parts.Select(LowerFormattedStringPart).ToArray()),
            ParenthesizedExpressionSyntax parenthesized => new LoweredParenthesizedExpression(
                parenthesized,
                LowerExpression(parenthesized.Inner)),
            ListLiteralExpressionSyntax list => new LoweredListLiteralExpression(
                list,
                list.Items.Select(LowerCollectionDisplayItem).ToArray()),
            ListComprehensionExpressionSyntax comprehension => new LoweredListComprehensionExpression(
                comprehension,
                LowerExpression(comprehension.ItemExpression),
                comprehension.Clauses.Select(LowerComprehensionClause).ToArray()),
            GeneratorExpressionSyntax generator => generator.PreparedCallable ?? new LoweredGeneratorExpression(
                generator,
                LowerExpression(generator.ItemExpression),
                generator.Clauses.Select(LowerComprehensionClause).ToArray()),
            TupleLiteralExpressionSyntax tuple => new LoweredTupleLiteralExpression(
                tuple,
                tuple.Items.Select(LowerCollectionDisplayItem).ToArray()),
            SetLiteralExpressionSyntax set => new LoweredSetLiteralExpression(
                set,
                set.Items.Select(LowerCollectionDisplayItem).ToArray()),
            SetComprehensionExpressionSyntax comprehension => new LoweredSetComprehensionExpression(
                comprehension,
                LowerExpression(comprehension.ItemExpression),
                comprehension.Clauses.Select(LowerComprehensionClause).ToArray()),
            DictLiteralExpressionSyntax dict => new LoweredDictLiteralExpression(
                dict,
                dict.Items.Select(item => item switch
                {
                    DictionaryKeyValueItemSyntax pair => (LoweredDictionaryDisplayItem)new LoweredDictionaryKeyValueItem(
                        pair,
                        LowerExpression(pair.Key),
                        LowerExpression(pair.Value)),
                    DictionaryUnpackingItemSyntax unpacking => new LoweredDictionaryUnpackingItem(
                        unpacking,
                        LowerExpression(unpacking.Mapping)),
                    _ => throw new InvalidOperationException($"Unknown dictionary display item: {item.GetType().Name}")
                }).ToArray()),
            DictComprehensionExpressionSyntax comprehension => new LoweredDictComprehensionExpression(
                comprehension,
                LowerExpression(comprehension.KeyExpression),
                LowerExpression(comprehension.ValueExpression),
                comprehension.Clauses.Select(LowerComprehensionClause).ToArray()),
            MemberExpressionSyntax member => new LoweredMemberExpression(
                member,
                LowerExpression(member.Target)),
            CallExpressionSyntax call => new LoweredCallExpression(
                call,
                LowerExpression(call.Target),
                call.Arguments.Select(argument => new LoweredCallArgument(
                    argument.Form,
                    LowerExpression(argument.Expression))).ToArray()),
            SubscriptExpressionSyntax subscript => new LoweredSubscriptExpression(
                subscript,
                LowerExpression(subscript.Target),
                LowerExpression(subscript.Index)),
            SliceExpressionSyntax slice => new LoweredSliceExpression(
                slice,
                LowerExpression(slice.Target),
                slice.Start is null ? null : LowerExpression(slice.Start),
                slice.End is null ? null : LowerExpression(slice.End),
                slice.Step is null ? null : LowerExpression(slice.Step)),
            SliceValueExpressionSyntax slice => new LoweredSliceValueExpression(
                slice,
                slice.Start is null ? null : LowerExpression(slice.Start),
                slice.End is null ? null : LowerExpression(slice.End),
                slice.Step is null ? null : LowerExpression(slice.Step)),
            BinaryExpressionSyntax binary => new LoweredBinaryExpression(
                binary,
                LowerExpression(binary.Left),
                LowerExpression(binary.Right)),
            ChainedComparisonExpressionSyntax chained => new LoweredChainedComparisonExpression(
                chained,
                chained.Operands.Select(LowerExpression).ToArray()),
            UnaryExpressionSyntax unary => new LoweredUnaryExpression(
                unary,
                LowerExpression(unary.Operand)),
            ConditionalExpressionSyntax conditional => new LoweredConditionalExpression(
                conditional,
                LowerExpression(conditional.Consequent),
                LowerExpression(conditional.Condition),
                LowerExpression(conditional.Alternative)),
            AssignmentExpressionSyntax assignment => new LoweredAssignmentExpression(
                assignment,
                LowerExpression(assignment.Expression)),
            LambdaExpressionSyntax lambda => lambda.PreparedCallable ?? new LoweredLambdaExpression(
                lambda,
                LowerExpression(lambda.Body)),
            _ => throw new InvalidOperationException($"Unknown expression during lowering: {expression.GetType().Name}")
        };
    }

    private static LoweredCollectionDisplayItem LowerCollectionDisplayItem(CollectionDisplayItemSyntax item)
        => new(item, LowerExpression(item.Expression));

    private static LoweredFormattedStringPart LowerFormattedStringPart(FormattedStringPartSyntax part)
    {
        return part switch
        {
            FormattedStringTextPartSyntax text => new LoweredFormattedStringTextPart(text.Text),
            FormattedStringExpressionPartSyntax expression => new LoweredFormattedStringExpressionPart(
                LowerExpression(expression.Expression),
                expression.Conversion,
                expression.FormatSpecifier,
                expression.FormatSpecifierParts?.Select(LowerFormattedStringPart).ToArray()),
            _ => throw new InvalidOperationException($"Unknown formatted string part: {part.GetType().Name}")
        };
    }

    private static LoweredComprehensionClause LowerComprehensionClause(ComprehensionClauseSyntax clause)
    {
        return new LoweredComprehensionClause(
            clause.Target,
            LowerExpression(clause.Iterable),
            clause.Condition is null ? null : LowerExpression(clause.Condition),
            clause.Span);
    }
}
