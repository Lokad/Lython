using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Frontend;

internal abstract record ExecutableOperation;
internal enum ExecutableDisplayKind { List, Tuple, Set, Dictionary }
internal sealed record ExecutableStartDisplay(ExecutableDisplayKind Kind) : ExecutableOperation;
internal sealed record ExecutableAppendDisplay(bool Unpacking, int IteratorDepth = 0) : ExecutableOperation;
internal sealed record ExecutableStoreDictionaryItem(bool Unpacking, int IteratorDepth = 0) : ExecutableOperation;
internal sealed record ExecutableStartComprehension(ExecutableDisplayKind Kind, ScopeDirectiveFacts ScopeFacts) : ExecutableOperation;
internal sealed record ExecutableFinishComprehension : ExecutableOperation;
internal sealed record ExecutableFinishTuple : ExecutableOperation;
internal sealed record ExecutableStartCall(int ArgumentCount, bool DeferSingleStar) : ExecutableOperation;
internal sealed record ExecutableAppendCall(CallArgumentForm Form) : ExecutableOperation;
internal sealed record ExecutableFinishCall(LythonSourceSpan TargetSpan) : ExecutableOperation;
internal sealed record ExecutableConvertFormattedValue(char Conversion) : ExecutableOperation;
internal sealed record ExecutableFormatField(string? FormatSpecifier, bool DynamicSpecifier, bool Append,
    LythonRuntime.InterpolatedFormatSpecifier? PreparedFormat) : ExecutableOperation;
internal sealed record ExecutableStartFormattedString : ExecutableOperation;
internal sealed record ExecutableAppendFormattedText(PyString Text) : ExecutableOperation;
internal sealed record ExecutableFinishFormattedString : ExecutableOperation;
internal sealed record ExecutableCreateGeneratorExpression(ExecutableCodeObject CodeObject, LythonSourceSpan OuterSpan) : ExecutableOperation;

internal sealed partial class ExecutableScript
{
    private sealed partial class Builder
    {
        private void EmitOperation(ExecutableOperation operation, LythonSourceSpan span, int block)
            => AddInstruction(block, ExecutableInstruction.ApplyOperation(InternConstant(operation), span));

        private int CompileExpandedCall(LoweredCallExpression call, int block)
        {
            block = CompileExpression(call.Target, block);
            EmitOperation(new ExecutableStartCall(call.Arguments.Count,
                call.Arguments.Count(a => a.Form.Kind is CallArgumentKind.Positional or CallArgumentKind.StarredList) == 1), call.Span, block);
            foreach (var argument in call.Arguments)
            {
                block = CompileExpression(argument.Expression, block);
                EmitOperation(new ExecutableAppendCall(argument.Form), argument.Expression.Span, block);
            }
            EmitOperation(new ExecutableFinishCall(call.Target.Span), call.Span, block);
            return block;
        }

        private int CompileFormattedParts(IReadOnlyList<LoweredFormattedStringPart> parts, LythonSourceSpan span, int block)
        {
            if (parts.Count == 0)
            {
                AddInstruction(block, ExecutableInstruction.LoadConst(InternConstant(PyString.Empty), span));
                return block;
            }
            var append = parts.Count > 1;
            if (append) EmitOperation(new ExecutableStartFormattedString(), span, block);
            foreach (var part in parts)
            {
                if (part is LoweredFormattedStringTextPart text)
                {
                    var literal = PyString.FromString(text.Text);
                    if (append) EmitOperation(new ExecutableAppendFormattedText(literal), span, block);
                    else AddInstruction(block, ExecutableInstruction.LoadConst(InternConstant(literal), span));
                }
                else if (part is LoweredFormattedStringExpressionPart field)
                {
                    block = CompileExpression(field.Expression, block);
                    if (field.Conversion is { } conversion)
                        EmitOperation(new ExecutableConvertFormattedValue(conversion), span, block);
                    if (field.FormatSpecifierParts is not null)
                        block = CompileFormattedParts(field.FormatSpecifierParts, span, block);
                    EmitOperation(new ExecutableFormatField(field.FormatSpecifier, field.FormatSpecifierParts is not null, append,
                        field.FormatSpecifierParts is null ? LythonRuntime.PrepareConstantFormatSpecifier(field.FormatSpecifier, span) : null), span, block);
                }
            }
            if (append) EmitOperation(new ExecutableFinishFormattedString(), span, block);
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
