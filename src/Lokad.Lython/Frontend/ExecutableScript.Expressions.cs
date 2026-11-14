using System.Globalization;
using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Frontend;

internal sealed partial class ExecutableScript
{
    private sealed partial class Builder
    {
        private int CompileExpression(LoweredExpression expression, int currentBlock)
        {
            switch (expression)
            {
                case LoweredUnpackedTypeExpression unpacked:
                    currentBlock = CompileExpression(unpacked.Value, currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.UnpackValues(new UnpackingLayout(1, -1), unpacked.Span));
                    return currentBlock;
                case LoweredYieldExpression yielded:
                    if (yielded.Value is null)
                        AddInstruction(currentBlock, ExecutableInstruction.LoadConst(InternConstant(PyNone.Instance), yielded.Span));
                    else currentBlock = CompileExpression(yielded.Value, currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.Yield(yielded.Yield.Delegated, yielded.Span));
                    return currentBlock;
                case LoweredParenthesizedExpression parenthesized:
                    currentBlock = CompileExpression(parenthesized.Inner, currentBlock);
                    return currentBlock;

                case LoweredStringLiteralExpression text:
                    AddInstruction(currentBlock, ExecutableInstruction.LoadConst(InternConstant(text.Value), text.Span));
                    return currentBlock;

                case LoweredFormattedStringExpression formatted:
                    return CompileFormattedParts(formatted.Parts, formatted.Span, currentBlock);

                case LoweredLambdaExpression lambda:
                    return CompileLambda(lambda, currentBlock);

                case LoweredBytesLiteralExpression bytes:
                    AddInstruction(currentBlock, ExecutableInstruction.LoadConst(InternConstant(bytes.Value), bytes.Span));
                    return currentBlock;

                case LoweredIntegerLiteralExpression integer:
                    AddInstruction(currentBlock, ExecutableInstruction.LoadConst(InternConstant(integer.Value), integer.Span));
                    return currentBlock;

                case LoweredImaginaryLiteralExpression imaginary:
                    AddInstruction(currentBlock, ExecutableInstruction.LoadConst(InternConstant(new PyComplex(0, PyNumberOps.ParseFloat(imaginary.Literal.ValueText[..^1]))), imaginary.Span));
                    return currentBlock;

                case LoweredFloatLiteralExpression floating:
                    AddInstruction(currentBlock, ExecutableInstruction.LoadConst(InternConstant(PyNumberOps.ParseFloat(floating.Literal.ValueText)), floating.Span));
                    return currentBlock;

                case LoweredBooleanLiteralExpression boolean:
                    AddInstruction(currentBlock, ExecutableInstruction.LoadConst(InternConstant(boolean.Literal.Value), boolean.Span));
                    return currentBlock;

                case LoweredNoneLiteralExpression none:
                    AddInstruction(currentBlock, ExecutableInstruction.LoadConst(InternConstant(PyNone.Instance), none.Span));
                    return currentBlock;

                case LoweredEllipsisLiteralExpression ellipsis:
                    AddInstruction(currentBlock, ExecutableInstruction.LoadConst(InternConstant(PyEllipsis.Instance), ellipsis.Span));
                    return currentBlock;

                case LoweredIdentifierExpression identifier:
                    CompileLoadIdentifier(identifier.Identifier.Name, identifier.Span, currentBlock);
                    return currentBlock;

                case LoweredMemberExpression member:
                    currentBlock = CompileExpression(member.Target, currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.LoadMember(InternName(member.Member.MemberName), AllocateLookupCache(), member.Span));
                    return currentBlock;

                case LoweredCallExpression call:
                    return CompileCallExpression(call, currentBlock);

                case LoweredListComprehensionExpression comprehension:
                    return CompileComprehension(comprehension.ScopeFacts, comprehension.Clauses,
                        comprehension.ItemExpression, null, ExecutableDisplayKind.List, comprehension.Span, currentBlock);

                case LoweredGeneratorExpression generator:
                    return CompileGeneratorExpression(generator, currentBlock);

                case LoweredSetComprehensionExpression comprehension:
                    return CompileComprehension(comprehension.ScopeFacts, comprehension.Clauses,
                        comprehension.ItemExpression, null, ExecutableDisplayKind.Set, comprehension.Span, currentBlock);

                case LoweredDictComprehensionExpression comprehension:
                    return CompileComprehension(comprehension.ScopeFacts, comprehension.Clauses,
                        comprehension.KeyExpression, comprehension.ValueExpression, ExecutableDisplayKind.Dictionary,
                        comprehension.Span, currentBlock);

                case LoweredListLiteralExpression list:
                    if (list.List.HasUnpacking)
                    {
                        return CompileDisplay(list.Items, ExecutableDisplayKind.List, list.Span, currentBlock);
                    }

                    foreach (var item in list.Items)
                    {
                        currentBlock = CompileExpression(item.Expression, currentBlock);
                    }
                    AddInstruction(currentBlock, ExecutableInstruction.MakeList(list.Items.Count, list.Span));
                    return currentBlock;

                case LoweredTupleLiteralExpression tuple:
                    if (tuple.Tuple.HasUnpacking)
                    {
                        return CompileDisplay(tuple.Items, ExecutableDisplayKind.Tuple, tuple.Span, currentBlock);
                    }

                    var tupleBlock = currentBlock;
                    var tupleStart = _blocks[currentBlock].Instructions.Count;
                    foreach (var item in tuple.Items)
                    {
                        currentBlock = CompileExpression(item.Expression, currentBlock);
                    }
                    if (currentBlock == tupleBlock && TryCompleteLiteralTuple(currentBlock, tupleStart, tuple.Items.Count, tuple.Span))
                        return currentBlock;
                    AddInstruction(currentBlock, ExecutableInstruction.MakeTuple(tuple.Items.Count, tuple.Span));
                    return currentBlock;

                case LoweredSetLiteralExpression set:
                    if (set.Set.HasUnpacking)
                    {
                        return CompileDisplay(set.Items, ExecutableDisplayKind.Set, set.Span, currentBlock);
                    }

                    foreach (var item in set.Items)
                    {
                        currentBlock = CompileExpression(item.Expression, currentBlock);
                    }
                    AddInstruction(currentBlock, ExecutableInstruction.MakeSet(set.Items.Count, set.Span));
                    return currentBlock;

                case LoweredDictLiteralExpression dict:
                    if (dict.Items.Any(item => item.IsUnpacking))
                    {
                        return CompileDictionaryDisplay(dict, currentBlock);
                    }

                    foreach (var item in dict.Items)
                    {
                        currentBlock = CompileExpression(item.Key, currentBlock);
                        currentBlock = CompileExpression(item.Value, currentBlock);
                    }
                    AddInstruction(currentBlock, ExecutableInstruction.MakeDict(dict.Items.Count, dict.Span));
                    return currentBlock;

                case LoweredSubscriptExpression subscript:
                    currentBlock = CompileExpression(subscript.Target, currentBlock);
                    currentBlock = CompileExpression(subscript.Index, currentBlock);
                    // Function facts survive calls; modules retain invocation
                    // facts only where a body can repeat within that invocation.
                    AddInstruction(currentBlock, ExecutableInstruction.Subscript(subscript.Span,
                        _parameterLayout is not null && !_generator || _loops.Count != 0 || _comprehensionNames.Count != 0
                            ? AllocateLookupCache() : -1));
                    return currentBlock;

                case LoweredSliceExpression slice:
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

                case LoweredSliceValueExpression sliceValue:
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

                case LoweredBinaryExpression binary:
                    if (binary.Binary.Operator is BinaryOperatorSyntax.Or or BinaryOperatorSyntax.And)
                    {
                        return CompileShortCircuitExpression(binary, currentBlock);
                    }

                    currentBlock = CompileExpression(binary.Left, currentBlock);
                    currentBlock = CompileExpression(binary.Right, currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.Binary(binary.Binary.Operator, binary.Span));
                    return currentBlock;

                case LoweredUnaryExpression unary:
                    currentBlock = CompileExpression(unary.Operand, currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.Unary(MapUnaryOperator(unary.Unary.Operator), unary.Span));
                    return currentBlock;

                case LoweredConditionalExpression conditional:
                    return CompileConditionalExpression(conditional, currentBlock);

                case LoweredChainedComparisonExpression chained:
                    return CompileChainedComparisonExpression(chained, currentBlock);

                case LoweredAssignmentExpression walrus:
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
        private int CompileShortCircuitExpression(LoweredBinaryExpression binary, int currentBlock)
        {
            currentBlock = CompileExpression(binary.Left, currentBlock);
            AddInstruction(currentBlock, ExecutableInstruction.Dup(binary.Left.Span));

            var joinBlock = CreateBlock();
            if (binary.Binary.Operator == BinaryOperatorSyntax.And)
            {
                AddInstruction(currentBlock, ExecutableInstruction.JumpIfFalse(joinBlock, binary.Left.Span));
                AddInstruction(currentBlock, ExecutableInstruction.PopTop(binary.Binary.Span));
                currentBlock = CompileExpression(binary.Right, currentBlock);
            }
            else
            {
                var rightBlock = CreateBlock();
                AddInstruction(currentBlock, ExecutableInstruction.JumpIfFalse(rightBlock, binary.Left.Span));
                AddInstruction(currentBlock, ExecutableInstruction.Jump(joinBlock, binary.Binary.Span));
                AddInstruction(rightBlock, ExecutableInstruction.PopTop(binary.Binary.Span));
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
        private int CompileChainedComparisonExpression(LoweredChainedComparisonExpression chained, int currentBlock)
        {
            var slot = InternSyntheticLocal("chain_left");

            currentBlock = CompileExpression(chained.Operands[0], currentBlock);
            AddInstruction(currentBlock, ExecutableInstruction.StoreLocal(slot, chained.Operands[0].Span));

            var joinBlock = CreateBlock();

            for (var i = 0; i < chained.ChainedComparison.Operators.Count; i++)
            {
                AddInstruction(currentBlock, ExecutableInstruction.LoadLocal(slot, chained.Span));
                currentBlock = CompileExpression(chained.Operands[i + 1], currentBlock);
                if (i < chained.ChainedComparison.Operators.Count - 1)
                {
                    AddInstruction(currentBlock, ExecutableInstruction.Dup(chained.Span));
                    AddInstruction(currentBlock, ExecutableInstruction.StoreLocal(slot, chained.Span));
                }
                AddInstruction(currentBlock, ExecutableInstruction.Binary(chained.ChainedComparison.Operators[i], chained.Span));
                if (i < chained.ChainedComparison.Operators.Count - 1)
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
        private int CompileAssignmentExpression(LoweredAssignmentExpression walrus, int currentBlock)
        {
            currentBlock = CompileExpression(walrus.Expression, currentBlock);
            AddInstruction(currentBlock, ExecutableInstruction.Dup(walrus.Expression.Span));
            CompileStoreBoundName(walrus.Assignment.Name, walrus.Span, currentBlock);
            return currentBlock;
        }

        private int CompileCallExpression(LoweredCallExpression call, int currentBlock)
        {
            if (call.Arguments.Any(argument => argument.Kind is CallArgumentKind.StarredList or CallArgumentKind.StarredDictionary))
            {
                return CompileExpandedCall(call, currentBlock);
            }

            var exitBlock = currentBlock;
            var callTarget = call.Target;
            while (callTarget is LoweredParenthesizedExpression parenthesized)
                callTarget = parenthesized.Inner;
            var hasReceiver = callTarget is LoweredMemberExpression;
            if (callTarget is LoweredMemberExpression member)
            {
                exitBlock = CompileExpression(member.Target, exitBlock);
                var nameIndex = InternName(member.Member.MemberName);
                var cacheIndex = AllocateLookupCache();
                AddInstruction(exitBlock, ExecutableInstruction.LoadCallMember(nameIndex, cacheIndex, member.Span));
            }
            else exitBlock = CompileExpression(call.Target, exitBlock);
            if (call.Call.PreloadImplicitSuperArguments)
                AddInstruction(exitBlock, ExecutableInstruction.PreloadImplicitSuperArguments(call.Span));
            foreach (var argument in call.Arguments)
            {
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
        private int CompileConditionalExpression(LoweredConditionalExpression conditional, int currentBlock)
        {
            currentBlock = CompileExpression(conditional.Condition, currentBlock);

            var consequentBlock = CreateBlock();
            var alternativeBlock = CreateBlock();
            var joinBlock = CreateBlock();

            AddInstruction(currentBlock, ExecutableInstruction.JumpIfFalse(alternativeBlock, conditional.Condition.Span));
            AddInstruction(currentBlock, ExecutableInstruction.Jump(consequentBlock, conditional.Conditional.Span));

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

        private bool TryCompleteLiteralTuple(int block, int start, int count, LythonSourceSpan span)
        {
            var instructions = _blocks[block].Instructions;
            if (instructions.Count - start != count) return false;
            for (var index = start; index < instructions.Count; index++)
            {
                var instruction = instructions[index];
                if (instruction.OpCode != ExecutableOpCode.LoadConst
                    || _constants[instruction.ConstantIndex] is not (PyNone or PyEllipsis or bool or BigInteger
                        or double or PyComplex or PyString or PyBytes or PyTuple)) return false;
            }

            // The existing coarse compilation preflight owns immutable literals
            // and instruction storage. Reuse emitted facts, without another syntax
            // walk or executing an operator. Dynamic displays keep MakeTuple.
            var items = new object[count];
            for (var index = 0; index < items.Length; index++)
                items[index] = _constants[instructions[start + index].ConstantIndex]!;
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
