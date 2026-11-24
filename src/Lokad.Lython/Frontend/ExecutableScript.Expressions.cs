using System.Globalization;
using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Frontend;

internal sealed partial class ExecutableScript
{
    private sealed partial class Builder
    {
        private PyDict.RecordKeys? PrepareLiteralRecordKeys(DictLiteralExpressionSyntax expression)
        {
            var items = expression.Items;
            if (items.Count == 0) return null;
            var names = items.Count > 1 ? new HashSet<string>(StringComparer.Ordinal) : null;
            foreach (var item in items)
            {
                PythonTokenization.CheckCancellation(_compilation.CancellationToken, expression.Span.Start);
                if (item.IsUnpacking || item.Key is not StringLiteralExpressionSyntax literal ||
                    names?.Add(literal.Value) == false) return null;
            }

            // Use the constant pool's key identities. The final key table
            // shares the compilation owner, like a declaration key table.
            return PyDict.RecordKeys.PrepareDefinition(items.Count, (Builder: this, Items: items),
                static (state, index) =>
                {
                    var literal = (StringLiteralExpressionSyntax)state.Items[index].Key;
                    var builder = state.Builder;
                    return (PyString)builder._buffers.Constants[builder.InternConstant(PyString.FromString(literal.Value))]!;
                }, _compilation.CancellationToken, expression.Span);
        }

        private static object ScalarLiteral(ExpressionSyntax expression) => expression switch
        {
            StringLiteralExpressionSyntax text => PyString.FromString(text.Value),
            BytesLiteralExpressionSyntax bytes => bytes.Value,
            IntegerLiteralExpressionSyntax integer => integer.Value,
            ImaginaryLiteralExpressionSyntax imaginary => new PyComplex(0, imaginary.Value),
            FloatLiteralExpressionSyntax floating => floating.Value,
            BooleanLiteralExpressionSyntax boolean => boolean.Value,
            NoneLiteralExpressionSyntax => PyNone.Instance,
            EllipsisLiteralExpressionSyntax => PyEllipsis.Instance,
            _ => throw new InvalidOperationException("Expected a scalar literal."),
        };

        private int CompileExpression(LoweredExpression expression, int currentBlock)
            => CompileExpression(expression.Syntax, currentBlock);

        private int CompileExpression(ExpressionSyntax expression, int currentBlock)
        {
            switch (expression)
            {
                case UnpackedTypeExpressionSyntax unpacked:
                    currentBlock = CompileExpression(unpacked.Value, currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.UnpackValues(new UnpackingLayout(1, -1), unpacked.Span));
                    return currentBlock;
                case YieldExpressionSyntax yielded:
                    if (yielded.Value is null)
                        AddInstruction(currentBlock, ExecutableInstruction.LoadConst(InternConstant(PyNone.Instance), yielded.Span));
                    else currentBlock = CompileExpression(yielded.Value, currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.Yield(yielded.Delegated, yielded.Span));
                    return currentBlock;
                case ParenthesizedExpressionSyntax parenthesized:
                    currentBlock = CompileExpression(parenthesized.Inner, currentBlock);
                    return currentBlock;

                case StringLiteralExpressionSyntax or BytesLiteralExpressionSyntax or IntegerLiteralExpressionSyntax
                    or ImaginaryLiteralExpressionSyntax or FloatLiteralExpressionSyntax or BooleanLiteralExpressionSyntax
                    or NoneLiteralExpressionSyntax or EllipsisLiteralExpressionSyntax:
                    AddInstruction(currentBlock, ExecutableInstruction.LoadConst(InternConstant(ScalarLiteral(expression)), expression.Span));
                    return currentBlock;

                case FormattedStringExpressionSyntax formatted:
                    return CompileFormattedParts(formatted.Parts, formatted.Span, currentBlock);

                case LambdaExpressionSyntax lambda:
                    return CompileLambda((LoweredLambdaExpression)LoweredScript.LowerExpression(lambda), currentBlock);

                case IdentifierExpressionSyntax identifier:
                    CompileLoadIdentifier(identifier.Name, identifier.Span, currentBlock);
                    return currentBlock;

                case MemberExpressionSyntax member:
                    currentBlock = CompileExpression(member.Target, currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.LoadMember(InternName(member.MemberName), AllocateLookupCache(), member.Span));
                    return currentBlock;

                case CallExpressionSyntax call:
                    return CompileCallExpression(call, currentBlock);

                case ListComprehensionExpressionSyntax comprehension:
                    return CompileComprehension(comprehension, comprehension.Clauses,
                        comprehension.ItemExpression, null, ExecutableDisplayKind.List, comprehension.Span, currentBlock);

                case GeneratorExpressionSyntax generator:
                    return CompileGeneratorExpression(generator, currentBlock);

                case SetComprehensionExpressionSyntax comprehension:
                    return CompileComprehension(comprehension, comprehension.Clauses,
                        comprehension.ItemExpression, null, ExecutableDisplayKind.Set, comprehension.Span, currentBlock);

                case DictComprehensionExpressionSyntax comprehension:
                    return CompileComprehension(comprehension, comprehension.Clauses,
                        comprehension.KeyExpression, comprehension.ValueExpression, ExecutableDisplayKind.Dictionary,
                        comprehension.Span, currentBlock);

                case ListLiteralExpressionSyntax list:
                    if (list.HasUnpacking)
                    {
                        return CompileDisplay(list.Items, ExecutableDisplayKind.List, list.Span, currentBlock);
                    }

                    foreach (var item in list.Items)
                    {
                        currentBlock = CompileExpression(item.Expression, currentBlock);
                    }
                    AddInstruction(currentBlock, ExecutableInstruction.MakeList(list.Items.Count, list.Span));
                    return currentBlock;

                case TupleLiteralExpressionSyntax tuple:
                    if (tuple.HasUnpacking)
                    {
                        return CompileDisplay(tuple.Items, ExecutableDisplayKind.Tuple, tuple.Span, currentBlock);
                    }

                    var tupleBlock = currentBlock;
                    var tupleStart = _buffers.Blocks[currentBlock].Count;
                    foreach (var item in tuple.Items)
                    {
                        currentBlock = CompileExpression(item.Expression, currentBlock);
                    }
                    if (currentBlock == tupleBlock && TryCompleteLiteralTuple(currentBlock, tupleStart, tuple.Items.Count, tuple.Span))
                        return currentBlock;
                    AddInstruction(currentBlock, ExecutableInstruction.MakeTuple(tuple.Items.Count, tuple.Span));
                    return currentBlock;

                case SetLiteralExpressionSyntax set:
                    if (set.HasUnpacking)
                    {
                        return CompileDisplay(set.Items, ExecutableDisplayKind.Set, set.Span, currentBlock);
                    }

                    foreach (var item in set.Items)
                    {
                        currentBlock = CompileExpression(item.Expression, currentBlock);
                    }
                    AddInstruction(currentBlock, ExecutableInstruction.MakeSet(set.Items.Count, set.Span));
                    return currentBlock;

                case DictLiteralExpressionSyntax dict:
                    if (PrepareLiteralRecordKeys(dict) is { } keys)
                    {
                        foreach (var item in dict.Items)
                            currentBlock = CompileExpression(item.Value, currentBlock);
                        AddInstruction(currentBlock, ExecutableInstruction.MakeRecord(InternConstant(keys), dict.Span));
                        return currentBlock;
                    }
                    if (dict.Items.Any(item => item.IsUnpacking))
                    {
                        return CompileDictionaryDisplay(dict, currentBlock);
                    }

                    foreach (var item in dict.Items)
                    {
                        currentBlock = CompileExpression(item.Key, currentBlock);
                        currentBlock = CompileExpression(item.Value, currentBlock);
                    }
                    // A repeated display can reuse its table if its evaluated
                    // names still supply the very same string keys.
                    var recordCache = dict.Items.Count > 1 &&
                        (_parameterLayout is not null || _buffers.Loops.Count != 0 || _buffers.ComprehensionNames.Count != 0) &&
                        dict.Items.All(item => item.Key is IdentifierExpressionSyntax or StringLiteralExpressionSyntax)
                        ? AllocateLookupCache() : -1;
                    AddInstruction(currentBlock, ExecutableInstruction.MakeDict(dict.Items.Count, recordCache, dict.Span));
                    return currentBlock;

                case SubscriptExpressionSyntax subscript:
                    currentBlock = CompileExpression(subscript.Target, currentBlock);
                    currentBlock = CompileExpression(subscript.Index, currentBlock);
                    // Functions and retained generators already own lookup
                    // storage. Generator expressions repeat within that frame;
                    // modules need facts only where their body can repeat.
                    AddInstruction(currentBlock, ExecutableInstruction.Subscript(subscript.Span,
                        _parameterLayout is not null || _buffers.Loops.Count != 0 || _buffers.ComprehensionNames.Count != 0
                            ? AllocateLookupCache() : -1));
                    return currentBlock;

                case SliceExpressionSyntax slice:
                    currentBlock = CompileExpression(slice.Target, currentBlock);
                    var parts = ExecutableSliceParts.None;
                    if (slice.Start is not null)
                    {
                        currentBlock = CompileExpression(slice.Start, currentBlock);
                        parts |= ExecutableSliceParts.Start;
                    }
                    if (slice.End is not null)
                    {
                        currentBlock = CompileExpression(slice.End, currentBlock);
                        parts |= ExecutableSliceParts.End;
                    }
                    if (slice.Step is not null)
                    {
                        currentBlock = CompileExpression(slice.Step, currentBlock);
                        parts |= ExecutableSliceParts.Step;
                    }
                    AddInstruction(currentBlock, ExecutableInstruction.Slice(parts, slice.Span));
                    return currentBlock;

                case SliceValueExpressionSyntax sliceValue:
                    var valueParts = ExecutableSliceParts.None;
                    if (sliceValue.Start is not null)
                    {
                        currentBlock = CompileExpression(sliceValue.Start, currentBlock);
                        valueParts |= ExecutableSliceParts.Start;
                    }
                    if (sliceValue.End is not null)
                    {
                        currentBlock = CompileExpression(sliceValue.End, currentBlock);
                        valueParts |= ExecutableSliceParts.End;
                    }
                    if (sliceValue.Step is not null)
                    {
                        currentBlock = CompileExpression(sliceValue.Step, currentBlock);
                        valueParts |= ExecutableSliceParts.Step;
                    }
                    AddInstruction(currentBlock, ExecutableInstruction.MakeSlice(valueParts, sliceValue.Span));
                    return currentBlock;

                case BinaryExpressionSyntax binary:
                    if (binary.Operator is BinaryOperatorSyntax.Or or BinaryOperatorSyntax.And)
                    {
                        return CompileShortCircuitExpression(binary, currentBlock);
                    }

                    currentBlock = CompileExpression(binary.Left, currentBlock);
                    currentBlock = CompileExpression(binary.Right, currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.Binary(binary.Operator, binary.Span));
                    return currentBlock;

                case UnaryExpressionSyntax unary:
                    currentBlock = CompileExpression(unary.Operand, currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.Unary(MapUnaryOperator(unary.Operator), unary.Span));
                    return currentBlock;

                case ConditionalExpressionSyntax conditional:
                    return CompileConditionalExpression(conditional, currentBlock);

                case ChainedComparisonExpressionSyntax chained:
                    return CompileChainedComparisonExpression(chained, currentBlock);

                case AssignmentExpressionSyntax walrus:
                    return CompileAssignmentExpression(walrus, currentBlock);

                default:
                    throw new InvalidOperationException($"Unknown expression during executable compilation: {expression.GetType().Name}");
            }
        }

        // and/or branch natively instead of paying one lowered-dispatch
        // state machine per evaluation. Both keep the left operand value
        // for its deciding edge (CPython returns operands, not bools) and
        // test it with the left span like the lowered path does, so both
        // join edges arrive with the same stack depth.
        private int CompileShortCircuitExpression(BinaryExpressionSyntax binary, int currentBlock)
        {
            currentBlock = CompileExpression(binary.Left, currentBlock);
            AddInstruction(currentBlock, ExecutableInstruction.Dup(binary.Left.Span));

            var joinBlock = CreateBlock();
            if (binary.Operator == BinaryOperatorSyntax.And)
            {
                AddInstruction(currentBlock, ExecutableInstruction.JumpIfFalse(joinBlock, binary.Left.Span));
                AddInstruction(currentBlock, ExecutableInstruction.PopTop(binary.Span));
                currentBlock = CompileExpression(binary.Right, currentBlock);
            }
            else
            {
                var rightBlock = CreateBlock();
                AddInstruction(currentBlock, ExecutableInstruction.JumpIfFalse(rightBlock, binary.Left.Span));
                AddInstruction(currentBlock, ExecutableInstruction.Jump(joinBlock, binary.Span));
                AddInstruction(rightBlock, ExecutableInstruction.PopTop(binary.Span));
                currentBlock = CompileExpression(binary.Right, rightBlock);
            }

            if (!IsTerminated(currentBlock))
            {
                AddInstruction(currentBlock, ExecutableInstruction.Jump(joinBlock, binary.Right.Span));
            }

            return joinBlock;
        }

        // Keep raw rich results, like and/or: only intermediate results are
        // truth-tested, and the deciding result remains on the operand stack.
        // Retain the middle operand once for the next comparison.
        private int CompileChainedComparisonExpression(ChainedComparisonExpressionSyntax chained, int currentBlock)
        {
            var slot = InternSyntheticLocal("chain_left");

            currentBlock = CompileExpression(chained.Operands[0], currentBlock);
            AddInstruction(currentBlock, ExecutableInstruction.StoreLocal(slot, chained.Operands[0].Span));

            var joinBlock = CreateBlock();

            for (var i = 0; i < chained.Operators.Count; i++)
            {
                AddInstruction(currentBlock, ExecutableInstruction.LoadLocal(slot, chained.Span));
                currentBlock = CompileExpression(chained.Operands[i + 1], currentBlock);
                if (i < chained.Operators.Count - 1)
                {
                    AddInstruction(currentBlock, ExecutableInstruction.Dup(chained.Span));
                    AddInstruction(currentBlock, ExecutableInstruction.StoreLocal(slot, chained.Span));
                }
                AddInstruction(currentBlock, ExecutableInstruction.Binary(chained.Operators[i], chained.Span));
                if (i < chained.Operators.Count - 1)
                {
                    AddInstruction(currentBlock, ExecutableInstruction.Dup(chained.Span));
                    AddInstruction(currentBlock, ExecutableInstruction.JumpIfFalse(joinBlock, chained.Span));
                    AddInstruction(currentBlock, ExecutableInstruction.PopTop(chained.Span));
                }
            }

            AddInstruction(currentBlock, ExecutableInstruction.Jump(joinBlock, chained.Span));
            return joinBlock;
        }

        // Assignment expressions duplicate the value and store one copy
        // through the same bound-name machinery as assignment statements,
        // instead of paying one lowered-dispatch state machine per
        // evaluation. Scope facts resolve the target exactly like a plain
        // store in the same position.
        private int CompileAssignmentExpression(AssignmentExpressionSyntax walrus, int currentBlock)
        {
            currentBlock = CompileExpression(walrus.Expression, currentBlock);
            AddInstruction(currentBlock, ExecutableInstruction.Dup(walrus.Expression.Span));
            CompileStoreBoundName(walrus.Name, walrus.Span, currentBlock);
            return currentBlock;
        }

        private int CompileCallExpression(CallExpressionSyntax call, int currentBlock)
        {
            if (call.Arguments.Any(argument => argument.Kind is CallArgumentKind.StarredList or CallArgumentKind.StarredDictionary))
            {
                return CompileExpandedCall(call, currentBlock);
            }

            var exitBlock = currentBlock;
            var callTarget = call.Target;
            while (callTarget is ParenthesizedExpressionSyntax parenthesized)
                callTarget = parenthesized.Inner;
            var hasReceiver = callTarget is MemberExpressionSyntax;
            if (callTarget is MemberExpressionSyntax member)
            {
                exitBlock = CompileExpression(member.Target, exitBlock);
                var nameIndex = InternName(member.MemberName);
                var cacheIndex = AllocateLookupCache();
                AddInstruction(exitBlock, ExecutableInstruction.LoadCallMember(nameIndex, cacheIndex, member.Span));
            }
            else exitBlock = CompileExpression(call.Target, exitBlock);
            if (call.PreloadImplicitSuperArguments)
                AddInstruction(exitBlock, ExecutableInstruction.PreloadImplicitSuperArguments(call.Span));
            if (callTarget is MemberExpressionSyntax { MemberName: "join" }
                && call.Arguments is [{ Kind: CallArgumentKind.Positional, Expression: GeneratorExpressionSyntax generator }]
                && IsTrimmedSplitGenerator(generator))
                exitBlock = CompileGeneratorExpression(generator, exitBlock, joinedTrimmedSplit: true);
            else
            {
                foreach (var argument in call.Arguments)
                    exitBlock = CompileExpression(argument.Expression, exitBlock);
            }

            AddInstruction(exitBlock, ExecutableInstruction.Call(InternCallSite(call, hasReceiver), call.Span));
            return exitBlock;
        }

        // Conditional expressions branch natively instead of paying one
        // lowered-dispatch state machine per evaluation. The shape mirrors
        // CompileIfStatement: the condition value feeds JumpIfFalse, and
        // exactly one side leaves its value for the join, so both edges
        // arrive with the same stack depth.
        private int CompileConditionalExpression(ConditionalExpressionSyntax conditional, int currentBlock)
        {
            var (consequentBlock, alternativeBlock) = CompileConditionalBranches(conditional, currentBlock);
            var joinBlock = CreateBlock();

            var consequentExit = CompileExpression(conditional.Consequent, consequentBlock);
            if (!IsTerminated(consequentExit))
            {
                AddInstruction(consequentExit, ExecutableInstruction.Jump(joinBlock, conditional.Consequent.Span));
            }

            var alternativeExit = CompileExpression(conditional.Alternative, alternativeBlock);
            if (!IsTerminated(alternativeExit))
            {
                AddInstruction(alternativeExit, ExecutableInstruction.Jump(joinBlock, conditional.Alternative.Span));
            }

            return joinBlock;
        }

        private (int Consequent, int Alternative) CompileConditionalBranches(ConditionalExpressionSyntax conditional, int currentBlock)
        {
            currentBlock = CompileExpression(conditional.Condition, currentBlock);
            var consequent = CreateBlock();
            var alternative = CreateBlock();
            AddInstruction(currentBlock, ExecutableInstruction.JumpIfFalse(alternative, conditional.Condition.Span));
            AddInstruction(currentBlock, ExecutableInstruction.Jump(consequent, conditional.Span));
            return (consequent, alternative);
        }

        private void CompileReturnValue(ExpressionSyntax value, int block, LythonSourceSpan span)
        {
            while (value is ParenthesizedExpressionSyntax parenthesized) value = parenthesized.Inner;
            if (value is ConditionalExpressionSyntax conditional)
            {
                // A return takes the selected value straight out of its branch.
                // Both arms retain the original return span and the active
                // protected-region cleanup.
                var (consequent, alternative) = CompileConditionalBranches(conditional, block);
                CompileReturnValue(conditional.Consequent, consequent, span);
                CompileReturnValue(conditional.Alternative, alternative, span);
                return;
            }
            block = CompileExpression(value, block);
            AddInstruction(block, ExecutableInstruction.Return(span));
        }

        private bool TryCompleteLiteralTuple(int block, int start, int count, LythonSourceSpan span)
        {
            var instructions = _buffers.Blocks[block];
            if (instructions.Count - start != count) return false;
            for (var index = start; index < instructions.Count; index++)
            {
                var instruction = instructions[index];
                if (instruction.OpCode != ExecutableOpCode.LoadConst
                    || _buffers.Constants[instruction.ConstantIndex] is not (PyNone or PyEllipsis or bool or BigInteger
                        or double or PyComplex or PyString or PyBytes or PyTuple)) return false;
            }

            // The existing coarse compilation preflight owns immutable literals
            // and instruction storage. Reuse emitted facts, without another syntax
            // walk or executing an operator. Dynamic displays keep MakeTuple.
            var items = new object[count];
            for (var index = 0; index < items.Length; index++)
                items[index] = _buffers.Constants[instructions[start + index].ConstantIndex]!;
            var value = count == 0 ? PyTuple.Empty : PyTuple.FromOwnedArray(items);
            instructions.RemoveRange(start, count);
            AddInstruction(block, ExecutableInstruction.LoadConst(InternConstant(value), span));
            return true;
        }

        private static ExecutableUnaryOperator MapUnaryOperator(UnaryOperatorSyntax op)
            => op switch
            {
                UnaryOperatorSyntax.Not => ExecutableUnaryOperator.Not,
                UnaryOperatorSyntax.Plus => ExecutableUnaryOperator.Plus,
                UnaryOperatorSyntax.Minus => ExecutableUnaryOperator.Minus,
                UnaryOperatorSyntax.BitwiseNot => ExecutableUnaryOperator.BitwiseNot,
                _ => throw new InvalidOperationException($"Executable IR lowering does not support unary operator {op}."),
            };


    }
}
