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
        private void CompileExpression(LoweredExpression expression, int currentBlock)
        {
            switch (expression)
            {
                case LoweredParenthesizedExpression parenthesized:
                    CompileExpression(parenthesized.Inner, currentBlock);
                    return;

                case LoweredStringLiteralExpression text:
                    AddInstruction(currentBlock, ExecutableInstruction.LoadConst(InternConstant(PyString.FromString(text.Literal.Value)), text.Span));
                    return;

                case LoweredBytesLiteralExpression bytes:
                    AddInstruction(currentBlock, ExecutableInstruction.LoadConst(InternConstant(new PyBytes(bytes.Literal.Value.ToArray())), bytes.Span));
                    return;

                case LoweredIntegerLiteralExpression integer:
                    AddInstruction(currentBlock, ExecutableInstruction.LoadConst(InternConstant(PyNumberOps.ParseInteger(integer.Literal.ValueText)), integer.Span));
                    return;

                case LoweredFloatLiteralExpression floating:
                    AddInstruction(currentBlock, ExecutableInstruction.LoadConst(InternConstant(PyNumberOps.ParseFloat(floating.Literal.ValueText)), floating.Span));
                    return;

                case LoweredBooleanLiteralExpression boolean:
                    AddInstruction(currentBlock, ExecutableInstruction.LoadConst(InternConstant(boolean.Literal.Value), boolean.Span));
                    return;

                case LoweredNoneLiteralExpression none:
                    AddInstruction(currentBlock, ExecutableInstruction.LoadConst(InternConstant(PyNone.Instance), none.Span));
                    return;

                case LoweredIdentifierExpression identifier:
                    CompileLoadIdentifier(identifier.Identifier.Name, identifier.Span, currentBlock);
                    return;

                case LoweredMemberExpression member:
                    CompileExpression(member.Target, currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.LoadMember(InternName(member.Member.MemberName), AllocateMemberCache(), member.Span));
                    return;

                case LoweredCallExpression call:
                    if (!TryCompileCallExpression(call, currentBlock))
                    {
                        AddInstruction(currentBlock, ExecutableInstruction.EvaluateFallbackExpression(InternExpressionFallback(call), call.Span));
                    }
                    return;

                case LoweredListLiteralExpression list:
                    if (list.List.HasUnpacking)
                    {
                        AddInstruction(currentBlock, ExecutableInstruction.EvaluateFallbackExpression(InternExpressionFallback(list), list.Span));
                        return;
                    }

                    foreach (var item in list.Items)
                    {
                        CompileExpression(item.Expression, currentBlock);
                    }
                    AddInstruction(currentBlock, ExecutableInstruction.MakeList(list.Items.Count, list.Span));
                    return;

                case LoweredTupleLiteralExpression tuple:
                    if (tuple.Tuple.HasUnpacking)
                    {
                        AddInstruction(currentBlock, ExecutableInstruction.EvaluateFallbackExpression(InternExpressionFallback(tuple), tuple.Span));
                        return;
                    }

                    foreach (var item in tuple.Items)
                    {
                        CompileExpression(item.Expression, currentBlock);
                    }
                    AddInstruction(currentBlock, ExecutableInstruction.MakeTuple(tuple.Items.Count, tuple.Span));
                    return;

                case LoweredSetLiteralExpression set:
                    if (set.Set.HasUnpacking)
                    {
                        AddInstruction(currentBlock, ExecutableInstruction.EvaluateFallbackExpression(InternExpressionFallback(set), set.Span));
                        return;
                    }

                    foreach (var item in set.Items)
                    {
                        CompileExpression(item.Expression, currentBlock);
                    }
                    AddInstruction(currentBlock, ExecutableInstruction.MakeSet(set.Items.Count, set.Span));
                    return;

                case LoweredDictLiteralExpression dict:
                    if (dict.Items.Any(item => item.IsUnpacking))
                    {
                        AddInstruction(currentBlock, ExecutableInstruction.EvaluateFallbackExpression(InternExpressionFallback(dict), dict.Span));
                        return;
                    }

                    foreach (var item in dict.Items)
                    {
                        CompileExpression(item.Key, currentBlock);
                        CompileExpression(item.Value, currentBlock);
                    }
                    AddInstruction(currentBlock, ExecutableInstruction.MakeDict(dict.Items.Count, dict.Span));
                    return;

                case LoweredSubscriptExpression subscript:
                    CompileExpression(subscript.Target, currentBlock);
                    CompileExpression(subscript.Index, currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.Subscript(subscript.Span));
                    return;

                case LoweredSliceExpression slice:
                    CompileExpression(slice.Target, currentBlock);
                    var parts = ExecutableSliceParts.None;
                    if (slice.Start is not null)
                    {
                        CompileExpression(slice.Start, currentBlock);
                        parts |= ExecutableSliceParts.Start;
                    }
                    if (slice.End is not null)
                    {
                        CompileExpression(slice.End, currentBlock);
                        parts |= ExecutableSliceParts.End;
                    }
                    if (slice.Step is not null)
                    {
                        CompileExpression(slice.Step, currentBlock);
                        parts |= ExecutableSliceParts.Step;
                    }
                    AddInstruction(currentBlock, ExecutableInstruction.Slice(parts, slice.Span));
                    return;

                case LoweredBinaryExpression binary:
                    if (binary.Binary.Operator is BinaryOperatorSyntax.Or or BinaryOperatorSyntax.And)
                    {
                        AddInstruction(currentBlock, ExecutableInstruction.EvaluateFallbackExpression(InternExpressionFallback(binary), binary.Span));
                        return;
                    }

                    CompileExpression(binary.Left, currentBlock);
                    CompileExpression(binary.Right, currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.Binary(MapBinaryOperator(binary.Binary.Operator), binary.Span));
                    return;

                case LoweredUnaryExpression unary:
                    CompileExpression(unary.Operand, currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.Unary(MapUnaryOperator(unary.Unary.Operator), unary.Span));
                    return;

                default:
                    AddInstruction(currentBlock, ExecutableInstruction.EvaluateFallbackExpression(InternExpressionFallback(expression), expression.Span));
                    return;
            }
        }

        private bool TryCompileCallExpression(LoweredCallExpression call, int currentBlock)
        {
            foreach (var argument in call.Arguments)
            {
                if (argument.Kind is CallArgumentKind.StarredList or CallArgumentKind.StarredDictionary)
                {
                    return false;
                }
            }

            CompileExpression(call.Target, currentBlock);
            foreach (var argument in call.Arguments)
            {
                CompileExpression(argument.Expression, currentBlock);
            }

            AddInstruction(currentBlock, ExecutableInstruction.Call(InternCallSite(call), AllocateCallCache(), call.Span));
            return true;
        }

        private static ExecutableBinaryOperator MapBinaryOperator(BinaryOperatorSyntax op)
            => op switch
            {
                BinaryOperatorSyntax.Or => ExecutableBinaryOperator.Or,
                BinaryOperatorSyntax.And => ExecutableBinaryOperator.And,
                BinaryOperatorSyntax.Add => ExecutableBinaryOperator.Add,
                BinaryOperatorSyntax.Subtract => ExecutableBinaryOperator.Subtract,
                BinaryOperatorSyntax.Multiply => ExecutableBinaryOperator.Multiply,
                BinaryOperatorSyntax.Divide => ExecutableBinaryOperator.Divide,
                BinaryOperatorSyntax.FloorDivide => ExecutableBinaryOperator.FloorDivide,
                BinaryOperatorSyntax.Modulo => ExecutableBinaryOperator.Modulo,
                BinaryOperatorSyntax.Power => ExecutableBinaryOperator.Power,
                BinaryOperatorSyntax.BitwiseOr => ExecutableBinaryOperator.BitwiseOr,
                BinaryOperatorSyntax.BitwiseXor => ExecutableBinaryOperator.BitwiseXor,
                BinaryOperatorSyntax.BitwiseAnd => ExecutableBinaryOperator.BitwiseAnd,
                BinaryOperatorSyntax.LeftShift => ExecutableBinaryOperator.LeftShift,
                BinaryOperatorSyntax.RightShift => ExecutableBinaryOperator.RightShift,
                BinaryOperatorSyntax.Equal => ExecutableBinaryOperator.Equal,
                BinaryOperatorSyntax.NotEqual => ExecutableBinaryOperator.NotEqual,
                BinaryOperatorSyntax.Less => ExecutableBinaryOperator.Less,
                BinaryOperatorSyntax.LessEqual => ExecutableBinaryOperator.LessEqual,
                BinaryOperatorSyntax.Greater => ExecutableBinaryOperator.Greater,
                BinaryOperatorSyntax.GreaterEqual => ExecutableBinaryOperator.GreaterEqual,
                BinaryOperatorSyntax.Is => ExecutableBinaryOperator.Is,
                BinaryOperatorSyntax.IsNot => ExecutableBinaryOperator.IsNot,
                BinaryOperatorSyntax.In => ExecutableBinaryOperator.In,
                BinaryOperatorSyntax.NotIn => ExecutableBinaryOperator.NotIn,
                _ => throw new ExecutableLoweringFallbackException($"Executable IR lowering does not support binary operator {op}."),
            };

        private static ExecutableUnaryOperator MapUnaryOperator(UnaryOperatorSyntax op)
            => op switch
            {
                UnaryOperatorSyntax.Not => ExecutableUnaryOperator.Not,
                UnaryOperatorSyntax.Plus => ExecutableUnaryOperator.Plus,
                UnaryOperatorSyntax.Minus => ExecutableUnaryOperator.Minus,
                UnaryOperatorSyntax.BitwiseNot => ExecutableUnaryOperator.BitwiseNot,
                _ => throw new ExecutableLoweringFallbackException($"Executable IR lowering does not support unary operator {op}."),
            };

        private static ExecutableAugmentedOperator MapAugmentedAssignmentOperator(AugmentedAssignmentOperatorSyntax op)
            => op switch
            {
                AugmentedAssignmentOperatorSyntax.Add => ExecutableAugmentedOperator.Add,
                AugmentedAssignmentOperatorSyntax.Subtract => ExecutableAugmentedOperator.Subtract,
                AugmentedAssignmentOperatorSyntax.Multiply => ExecutableAugmentedOperator.Multiply,
                AugmentedAssignmentOperatorSyntax.Divide => ExecutableAugmentedOperator.Divide,
                AugmentedAssignmentOperatorSyntax.FloorDivide => ExecutableAugmentedOperator.FloorDivide,
                AugmentedAssignmentOperatorSyntax.Modulo => ExecutableAugmentedOperator.Modulo,
                AugmentedAssignmentOperatorSyntax.Power => ExecutableAugmentedOperator.Power,
                AugmentedAssignmentOperatorSyntax.BitwiseOr => ExecutableAugmentedOperator.BitwiseOr,
                AugmentedAssignmentOperatorSyntax.BitwiseXor => ExecutableAugmentedOperator.BitwiseXor,
                AugmentedAssignmentOperatorSyntax.BitwiseAnd => ExecutableAugmentedOperator.BitwiseAnd,
                AugmentedAssignmentOperatorSyntax.LeftShift => ExecutableAugmentedOperator.LeftShift,
                AugmentedAssignmentOperatorSyntax.RightShift => ExecutableAugmentedOperator.RightShift,
                _ => throw new ExecutableLoweringFallbackException($"Executable IR lowering does not support augmented assignment operator {op}."),
            };

    }
}
