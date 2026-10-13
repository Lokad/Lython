using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

// Lowering may replace children with already evaluated guest values. Describe
// those actual fields, not Syntax/Kind getters or allocated syntax iterators.
// All traversal uses MemoryStorageWalk's existing funded queue and work bound.
internal static class MemoryCodeStorageEdges
{
    internal static bool TryVisit(object value, MemoryStorageWalk visitor)
    {
        switch (value)
        {
            case ExecutableCreateFunction creation:
                if (visitor.Add(creation.Definition)) visitor.Add(creation.Inputs);
                break;
            case ExecutableCreateLambda creation:
                if (visitor.Add(creation.Expression)) visitor.Add(creation.Parameters);
                break;
            case ExecutableCreateClass creation: visitor.Add(creation.Definition); break;
            case ExecutableCreateComprehension creation: visitor.Add(creation.Expression); break;
            case ExecutableAnnotationsEnabled:
            case ExecutableStartClassHeader:
            case ExecutableStartDisplay:
            case ExecutableAppendDisplay:
            case ExecutableStoreDictionaryItem:
            case ExecutableFinishTuple:
            case ExecutableStartCall:
            case ExecutableAppendCall:
            case ExecutableFinishCall:
            case ExecutableConvertFormattedValue:
            case ExecutableFormatField:
            case ExecutableJoinFormattedParts:
            case ExecutableFailAssertion:
            case ExecutableRaise:
            case ExecutableDeletePreparedTarget:
            case ExecutableStorePreparedTarget:
            case ExecutableReadPreparedTarget:
                // These records contain scalars, source names/spans or parsed
                // assignment-target syntax, not evaluated operands.
                break;
            case ExecutableFunctionBinding function:
                if (!visitor.Add(function.Function) || !visitor.Add(function.CodeObject)) break;
                visitor.Add(function.DefaultValues);
                break;
            case ExecutableStatementFallback statement: visitor.Add(statement.Statement); break;
            case ExecutableExpressionFallback expression: visitor.Add(expression.Expression); break;
            case ExecutableUnpackingTargetBinding unpacking: visitor.Add(unpacking.Lowered); break;
            case ExecutableLoopTargetBinding loop: visitor.Add(loop.Target); break;
            case ExecutableMatchCaseBinding:
                // Parsed targets/patterns/guards contain source syntax and
                // scalar lookup indices, not lowered runtime captures.
                break;
            case LoopStoreTargetSyntax target: visitor.Add(target.LoweredTarget); break;
            case LoopNameTargetSyntax:
            case LoopStarredTargetSyntax: break;
            case LoopTupleTargetSyntax tuple: visitor.Add(tuple.Items); break;
            case LoweredStoreTarget target: visitor.Add(target.Reads); break;
            case LoweredFunctionParameter parameter:
                if (visitor.Add(parameter.Annotation)) visitor.Add(parameter.DefaultValue);
                break;
            case LoweredTypeParameter parameter:
                if (visitor.Add(parameter.Bound)) visitor.Add(parameter.Default);
                break;
            case LoweredCollectionDisplayItem item: visitor.Add(item.Expression); break;
            case LoweredDictionaryDisplayItem item:
                if (visitor.Add(item.Key)) visitor.Add(item.Value);
                break;
            case LoweredCallArgument argument: visitor.Add(argument.Expression); break;
            case LoweredComprehensionClause clause:
                if (!visitor.Add(clause.Iterable) || !visitor.Add(clause.Condition)) break;
                visitor.Add(clause.Target);
                break;
            case LoweredFormattedStringTextPart: break;
            case LoweredFormattedStringExpressionPart part:
                if (visitor.Add(part.Expression)) visitor.Add(part.FormatSpecifierParts);
                break;
            case LoweredFormattedStringExpression formatted: visitor.Add(formatted.Parts); break;
            case LoweredParenthesizedExpression parentheses: visitor.Add(parentheses.Inner); break;
            case LoweredUnpackedTypeExpression unpacked: visitor.Add(unpacked.Value); break;
            case LoweredYieldExpression yielded: visitor.Add(yielded.Value); break;
            case LoweredIdentifierExpression:
            case LoweredStringLiteralExpression:
            case LoweredBytesLiteralExpression:
            case LoweredIntegerLiteralExpression:
            case LoweredImaginaryLiteralExpression:
            case LoweredFloatLiteralExpression:
            case LoweredBooleanLiteralExpression:
            case LoweredNoneLiteralExpression:
            case LoweredEllipsisLiteralExpression:
                // Parsed literal payloads are source-owned. Runtime values are
                // represented by LoweredCapturedExpression's separate contract.
                break;
            case LoweredListLiteralExpression list: visitor.Add(list.Items); break;
            case LoweredTupleLiteralExpression tuple: visitor.Add(tuple.Items); break;
            case LoweredSetLiteralExpression set: visitor.Add(set.Items); break;
            case LoweredDictLiteralExpression dictionary: visitor.Add(dictionary.Items); break;
            case LoweredListComprehensionExpression list:
                if (visitor.Add(list.ItemExpression)) visitor.Add(list.Clauses);
                break;
            case LoweredSetComprehensionExpression set:
                if (visitor.Add(set.ItemExpression)) visitor.Add(set.Clauses);
                break;
            case LoweredGeneratorExpression generator:
                if (visitor.Add(generator.ItemExpression)) visitor.Add(generator.Clauses);
                break;
            case LoweredDictComprehensionExpression dictionary:
                if (!visitor.Add(dictionary.KeyExpression) || !visitor.Add(dictionary.ValueExpression)) break;
                visitor.Add(dictionary.Clauses);
                break;
            case LoweredMemberExpression member: visitor.Add(member.Target); break;
            case LoweredCallExpression call:
                if (visitor.Add(call.Target)) visitor.Add(call.Arguments);
                break;
            case LoweredSubscriptExpression subscript:
                if (visitor.Add(subscript.Target)) visitor.Add(subscript.Index);
                break;
            case LoweredSliceExpression slice:
                if (!visitor.Add(slice.Target) || !visitor.Add(slice.Start) || !visitor.Add(slice.End)) break;
                visitor.Add(slice.Step);
                break;
            case LoweredSliceValueExpression slice:
                if (!visitor.Add(slice.Start) || !visitor.Add(slice.End)) break;
                visitor.Add(slice.Step);
                break;
            case LoweredBinaryExpression binary:
                if (visitor.Add(binary.Left)) visitor.Add(binary.Right);
                break;
            case LoweredChainedComparisonExpression comparison: visitor.Add(comparison.Operands); break;
            case LoweredUnaryExpression unary: visitor.Add(unary.Operand); break;
            case LoweredConditionalExpression conditional:
                if (!visitor.Add(conditional.Condition) || !visitor.Add(conditional.Consequent)) break;
                visitor.Add(conditional.Alternative);
                break;
            case LoweredAssignmentExpression assignment: visitor.Add(assignment.Expression); break;
            case LoweredLambdaExpression lambda:
                if (!visitor.Add(lambda.Body) || !visitor.Add(lambda.PreparedParameters)) break;
                visitor.Add(lambda.GeneratorCode);
                break;
            case LoweredNameAssignmentStatement assignment: visitor.Add(assignment.Expression); break;
            case LoweredChainedAssignmentStatement assignment:
                if (visitor.Add(assignment.Expression)) visitor.Add(assignment.Targets);
                break;
            case LoweredAnnotatedAssignmentStatement assignment:
                if (!visitor.Add(assignment.Annotation) || !visitor.Add(assignment.Expression)) break;
                visitor.Add(assignment.Target);
                break;
            case LoweredUnpackingAssignmentStatement assignment:
                if (visitor.Add(assignment.Expression)) visitor.Add(assignment.Target);
                break;
            case LoweredAugmentedAssignmentStatement assignment:
                if (visitor.Add(assignment.Target)) visitor.Add(assignment.Expression);
                break;
            case LoweredSubscriptAssignmentStatement assignment:
                if (!visitor.Add(assignment.Receiver) || !visitor.Add(assignment.Index)) break;
                visitor.Add(assignment.Expression);
                break;
            case LoweredSliceAssignmentStatement assignment:
                if (!visitor.Add(assignment.Receiver) || !visitor.Add(assignment.Start) ||
                    !visitor.Add(assignment.End) || !visitor.Add(assignment.Step)) break;
                visitor.Add(assignment.Expression);
                break;
            case LoweredMemberAssignmentStatement assignment:
                if (visitor.Add(assignment.Receiver)) visitor.Add(assignment.Expression);
                break;
            case LoweredNameAugmentedAssignmentTarget: break;
            case LoweredSubscriptAugmentedAssignmentTarget target:
                if (visitor.Add(target.Receiver)) visitor.Add(target.Index);
                break;
            case LoweredSliceAugmentedAssignmentTarget target:
                if (!visitor.Add(target.Receiver) || !visitor.Add(target.Start) || !visitor.Add(target.End)) break;
                visitor.Add(target.Step);
                break;
            case LoweredMemberAugmentedAssignmentTarget target: visitor.Add(target.Receiver); break;
            case LoweredTypeAliasStatement alias:
                if (visitor.Add(alias.Value)) visitor.Add(alias.TypeParameters);
                break;
            case LoweredFunctionDefinitionStatement function:
                if (!visitor.Add(function.Decorators) || !visitor.Add(function.Parameters) ||
                    !visitor.Add(function.ReturnAnnotation) || !visitor.Add(function.Body) ||
                    !visitor.Add(function.TypeParameters)) break;
                visitor.Add(function.GeneratorCode);
                break;
            case LoweredClassDefinitionStatement type:
                if (!visitor.Add(type.Decorators) || !visitor.Add(type.Bases) ||
                    !visitor.Add(type.KeywordArguments) || !visitor.Add(type.HeaderArguments) ||
                    !visitor.Add(type.TypeParameters)) break;
                visitor.Add(type.Body);
                break;
            case LoweredExpressionStatement statement: visitor.Add(statement.Expression); break;
            case LoweredIfStatement condition:
                if (!visitor.Add(condition.Condition) || !visitor.Add(condition.ThenStatements)) break;
                visitor.Add(condition.ElseStatements);
                break;
            case LoweredForStatement loop:
                if (!visitor.Add(loop.Iterable) || !visitor.Add(loop.Body) || !visitor.Add(loop.Syntax.Target)) break;
                visitor.Add(loop.ElseStatements);
                break;
            case LoweredWhileStatement loop:
                if (!visitor.Add(loop.Condition) || !visitor.Add(loop.Body)) break;
                visitor.Add(loop.ElseStatements);
                break;
            case LoweredMatchStatement match:
                if (visitor.Add(match.Subject)) visitor.Add(match.Cases);
                break;
            case LoweredMatchCase match:
                if (visitor.Add(match.Body)) visitor.Add(match.Guard);
                break;
            case LoweredWithStatement with:
                if (!visitor.Add(with.ContextExpression) || !visitor.Add(with.Body)) break;
                visitor.Add(with.Syntax.LoweredTarget);
                break;
            case LoweredTryStatement attempt:
                if (!visitor.Add(attempt.TryBody) || !visitor.Add(attempt.ExceptClauses) ||
                    !visitor.Add(attempt.ElseBody)) break;
                visitor.Add(attempt.FinallyBody);
                break;
            case LoweredExceptClause handler:
                if (visitor.Add(handler.ExceptionType)) visitor.Add(handler.Body);
                break;
            case LoweredAssertStatement assertion:
                if (visitor.Add(assertion.Condition)) visitor.Add(assertion.Message);
                break;
            case LoweredDeleteStatement deletion: visitor.Add(deletion.Target); break;
            case LoweredReturnStatement result: visitor.Add(result.Expression); break;
            case LoweredRaiseStatement error:
                if (visitor.Add(error.Expression)) visitor.Add(error.CauseExpression);
                break;
            case LoweredImportStatement:
            case LoweredScopeDirectiveStatement:
            case LoweredPassStatement:
            case LoweredBreakStatement:
            case LoweredContinueStatement:
                break;
            case List<LoweredStatement> list: return Visit(list, visitor);
            case List<LoweredExpression> list: return Visit(list, visitor);
            case List<LoweredFunctionParameter> list: return Visit(list, visitor);
            case List<LoweredFormattedStringPart> list: return Visit(list, visitor);
            case List<LoweredCollectionDisplayItem> list: return Visit(list, visitor);
            case List<LoweredDictionaryDisplayItem> list: return Visit(list, visitor);
            case List<LoweredCallArgument> list: return Visit(list, visitor);
            case List<LoweredComprehensionClause> list: return Visit(list, visitor);
            case List<LoweredTypeParameter> list: return Visit(list, visitor);
            case List<LoweredExceptClause> list: return Visit(list, visitor);
            case List<LoweredMatchCase> list: return Visit(list, visitor);
            case List<LoweredStoreTarget> list: return Visit(list, visitor);
            case List<LoopTargetSyntax> list: return Visit(list, visitor);
            case Dictionary<string, LoweredExpression> defaults when defaults.GetType() == typeof(Dictionary<string, LoweredExpression>):
                foreach (var pair in defaults) if (!visitor.Add(pair.Value)) break;
                break;
            case Dictionary<ExpressionSyntax, LoweredExpression> targets when targets.GetType() == typeof(Dictionary<ExpressionSyntax, LoweredExpression>):
                foreach (var pair in targets) if (!visitor.Add(pair.Value)) break;
                break;
            default: return false;
        }
        return true;
    }

    private static bool Visit<T>(List<T> values, MemoryStorageWalk visitor) where T : class
    {
        if (values.GetType() != typeof(List<T>)) return false;
        foreach (var value in values) if (!visitor.Add(value)) break;
        return true;
    }
}
