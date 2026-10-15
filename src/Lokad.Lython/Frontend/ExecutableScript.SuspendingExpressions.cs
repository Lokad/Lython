using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Frontend;

internal abstract record ExecutableOperation;
internal enum ExecutableDisplayKind { List, Tuple, Set, Dictionary }
internal sealed record ExecutableStartDisplay(ExecutableDisplayKind Kind) : ExecutableOperation;
internal sealed record ExecutableAppendDisplay(bool Unpacking) : ExecutableOperation;
internal sealed record ExecutableStoreDictionaryItem(bool Unpacking) : ExecutableOperation;
internal sealed record ExecutableFinishTuple : ExecutableOperation;
internal sealed record ExecutableStartCall(int ArgumentCount, bool DeferSingleStar) : ExecutableOperation;
internal sealed record ExecutableAppendCall(CallArgumentForm Form) : ExecutableOperation;
internal sealed record ExecutableFinishCall(LythonSourceSpan TargetSpan) : ExecutableOperation;
internal sealed record ExecutableConvertFormattedValue(char Conversion) : ExecutableOperation;
internal sealed record ExecutableFormatField(string? FormatSpecifier, bool DynamicSpecifier) : ExecutableOperation;
internal sealed record ExecutableJoinFormattedParts(int Count) : ExecutableOperation;
internal sealed record ExecutableCreateComprehension(LoweredExpression Expression) : ExecutableOperation;

internal sealed partial class ExecutableScript
{
    private sealed partial class Builder
    {
        private void EmitOperation(ExecutableOperation operation, LythonSourceSpan span, int block)
            => AddInstruction(block, ExecutableInstruction.ApplyOperation(InternConstant(operation), span));

        private bool TryCompileSuspendingExpression(LoweredExpression expression, int block, out int result)
        {
            result = block;
            switch (expression)
            {
                case LoweredLambdaExpression lambda:
                    result = CompileSuspendingLambda(lambda, block);
                    return true;
                case LoweredFormattedStringExpression formatted:
                    result = CompileFormattedParts(formatted.Parts, formatted.Span, block);
                    return true;
                case LoweredCallExpression call when call.Arguments.Any(a => a.Form.Kind is CallArgumentKind.StarredList or CallArgumentKind.StarredDictionary):
                    result = CompileExpression(call.Target, block);
                    EmitOperation(new ExecutableStartCall(call.Arguments.Count, call.Arguments.Count(a => a.Form.Kind is CallArgumentKind.Positional or CallArgumentKind.StarredList) == 1), call.Span, result);
                    foreach (var argument in call.Arguments)
                    {
                        result = CompileExpression(argument.Expression, result);
                        EmitOperation(new ExecutableAppendCall(argument.Form), argument.Expression.Span, result);
                    }
                    EmitOperation(new ExecutableFinishCall(call.Target.Span), call.Span, result);
                    return true;
                case LoweredListLiteralExpression list when list.List.HasUnpacking:
                    result = CompileDisplay(list.Items, ExecutableDisplayKind.List, list.Span, block);
                    return true;
                case LoweredTupleLiteralExpression tuple when tuple.Tuple.HasUnpacking:
                    result = CompileDisplay(tuple.Items, ExecutableDisplayKind.Tuple, tuple.Span, block);
                    return true;
                case LoweredSetLiteralExpression set when set.Set.HasUnpacking:
                    result = CompileDisplay(set.Items, ExecutableDisplayKind.Set, set.Span, block);
                    return true;
                case LoweredDictLiteralExpression dict when dict.Items.Any(i => i.IsUnpacking):
                    result = CompileDictionaryDisplay(dict, result);
                    return true;
            }
            var clauses = expression switch
            {
                LoweredListComprehensionExpression c => c.Clauses,
                LoweredSetComprehensionExpression c => c.Clauses,
                LoweredDictComprehensionExpression c => c.Clauses,
                LoweredGeneratorExpression c => c.Clauses,
                _ => null,
            };
            if (clauses is null) return false;
            result = CompileExpression(clauses[0].Iterable, block);
            EmitOperation(new ExecutableCreateComprehension(expression), expression.Span, result);
            return true;
        }

        private int CompileFormattedParts(IReadOnlyList<LoweredFormattedStringPart> parts, LythonSourceSpan span, int block)
        {
            foreach (var part in parts)
            {
                if (part is LoweredFormattedStringTextPart text)
                    AddInstruction(block, ExecutableInstruction.LoadConst(InternConstant(PyString.FromString(text.Text)), span));
                else if (part is LoweredFormattedStringExpressionPart field)
                {
                    block = CompileExpression(field.Expression, block);
                    if (field.Conversion is { } conversion)
                        EmitOperation(new ExecutableConvertFormattedValue(conversion), span, block);
                    if (field.FormatSpecifierParts is not null)
                        block = CompileFormattedParts(field.FormatSpecifierParts, span, block);
                    EmitOperation(new ExecutableFormatField(field.FormatSpecifier, field.FormatSpecifierParts is not null), span, block);
                }
            }
            EmitOperation(new ExecutableJoinFormattedParts(parts.Count), span, block);
            return block;
        }

        private int CompileDictionaryDisplay(LoweredDictLiteralExpression dict, int block)
        {
            EmitOperation(new ExecutableStartDisplay(ExecutableDisplayKind.Dictionary), dict.Span, block);
            foreach (var item in dict.Items)
            {
                block = CompileExpression(item.Key, block);
                if (!item.IsUnpacking) block = CompileExpression(item.Value, block);
                EmitOperation(new ExecutableStoreDictionaryItem(item.IsUnpacking),
                    item.IsUnpacking ? item.Syntax.Span : item.Key.Span, block);
            }
            return block;
        }

        private int CompileDisplay(IReadOnlyList<LoweredCollectionDisplayItem> items, ExecutableDisplayKind kind, LythonSourceSpan span, int block)
        {
            EmitOperation(new ExecutableStartDisplay(kind), span, block);
            foreach (var item in items)
            {
                block = CompileExpression(item.Expression, block);
                EmitOperation(new ExecutableAppendDisplay(item.IsUnpacking), item.Span, block);
            }
            if (kind == ExecutableDisplayKind.Tuple) EmitOperation(new ExecutableFinishTuple(), span, block);
            return block;
        }
    }
}
