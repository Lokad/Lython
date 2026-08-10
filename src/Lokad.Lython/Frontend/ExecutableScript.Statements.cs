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
        private int? CompileStatements(IReadOnlyList<LoweredStatement> statements, int entryBlock)
        {
            int? current = entryBlock;
            foreach (var statement in statements)
            {
                if (current is null)
                {
                    return null;
                }

                current = CompileStatement(statement, current.Value);
            }

            return current;
        }

        private int? CompileStatement(LoweredStatement statement, int currentBlock)
        {
            switch (statement)
            {
                case LoweredImportStatement importStatement:
                    AddInstruction(currentBlock, ExecutableInstruction.Import(InternImport(importStatement), importStatement.Span));
                    return currentBlock;

                case LoweredScopeDirectiveStatement:
                    return currentBlock;

                case LoweredFunctionDefinitionStatement functionDefinition:
                    AddInstruction(currentBlock, ExecutableInstruction.DefineFunction(InternFunction(functionDefinition), functionDefinition.Span));
                    return currentBlock;

                case LoweredAssignmentStatement assignment:
                    return CompileAssignmentStatement(assignment, currentBlock);

                case LoweredExpressionStatement expressionStatement:
                    CompileExpression(expressionStatement.Expression, currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.PopTop(expressionStatement.Span));
                    return currentBlock;

                case LoweredIfStatement ifStatement:
                    return CompileIfStatement(ifStatement, currentBlock);

                case LoweredForStatement forStatement:
                    return CompileForStatement(forStatement, currentBlock);

                case LoweredWhileStatement whileStatement:
                    return CompileWhileStatement(whileStatement, currentBlock);

                case LoweredMatchStatement matchStatement:
                    return CompileMatchStatement(matchStatement, currentBlock);

                case LoweredWithStatement withStatement:
                    return CompileWithStatement(withStatement, currentBlock);

                case LoweredTryStatement tryStatement:
                    return CompileTryStatement(tryStatement, currentBlock);

                case LoweredPassStatement:
                    return currentBlock;

                case LoweredBreakStatement breakStatement:
                    return CompileBreakStatement(breakStatement, currentBlock);

                case LoweredContinueStatement continueStatement:
                    return CompileContinueStatement(continueStatement, currentBlock);

                case LoweredReturnStatement returnStatement:
                    if (returnStatement.Expression is null)
                    {
                        AddInstruction(currentBlock, ExecutableInstruction.ReturnNone(returnStatement.Span));
                    }
                    else
                    {
                        CompileExpression(returnStatement.Expression, currentBlock);
                        AddInstruction(currentBlock, ExecutableInstruction.Return(returnStatement.Span));
                    }

                    return null;

                default:
                    AddInstruction(currentBlock, ExecutableInstruction.ExecuteFallbackStatement(InternStatementFallback(statement), statement.Span));
                    return currentBlock;
            }
        }

        private int CompileAssignmentStatement(LoweredAssignmentStatement assignment, int currentBlock)
        {
            switch (assignment)
            {
                case LoweredNameAssignmentStatement simple:
                    CompileExpression(simple.Expression, currentBlock);
                    CompileStoreBoundName(simple.Assignment.Name, simple.Span, currentBlock);
                    return currentBlock;

                case LoweredAnnotatedAssignmentStatement annotated:
                    if (annotated.Expression is null)
                    {
                        throw new ExecutableLoweringFallbackException($"Executable IR lowering does not support annotation-only assignments: {annotated.Assignment.GetType().Name}.");
                    }

                    CompileExpression(annotated.Expression, currentBlock);
                    CompileStoreBoundName(annotated.Assignment.Name, annotated.Span, currentBlock);
                    return currentBlock;

                case LoweredAugmentedAssignmentStatement augmented:
                    if (augmented.Target is not LoweredNameAugmentedAssignmentTarget augmentedName)
                    {
                        AddInstruction(currentBlock, ExecutableInstruction.ExecuteFallbackStatement(InternStatementFallback(assignment), assignment.Span));
                        return currentBlock;
                    }

                    CompileLoadIdentifier(augmentedName.Target.Name, augmented.Span, currentBlock);
                    CompileExpression(augmented.Expression, currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.Augmented(MapAugmentedAssignmentOperator(augmented.Assignment.Operator), augmented.Span));
                    CompileStoreBoundName(augmentedName.Target.Name, augmented.Span, currentBlock);
                    return currentBlock;

                case LoweredChainedAssignmentStatement chained:
                    CompileExpression(chained.Expression, currentBlock);
                    for (var i = 0; i < chained.Assignment.Targets.Count; i++)
                    {
                        if (chained.Assignment.Targets[i] is not NameAssignmentTargetSyntax name)
                        {
                            throw new ExecutableLoweringFallbackException($"Executable IR lowering does not support chained assignment target {chained.Assignment.Targets[i].GetType().Name}.");
                        }

                        if (i < chained.Assignment.Targets.Count - 1)
                        {
                            AddInstruction(currentBlock, ExecutableInstruction.Dup(chained.Span));
                        }

                        CompileStoreBoundName(name.Name, chained.Span, currentBlock);
                    }
                    return currentBlock;

                case LoweredUnpackingAssignmentStatement unpacking:
                    CompileExpression(unpacking.Expression, currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.AssignUnpackingTargets(InternUnpackingTargets(unpacking.Assignment.Targets, unpacking.Span), unpacking.Span));
                    return currentBlock;

                default:
                    AddInstruction(currentBlock, ExecutableInstruction.ExecuteFallbackStatement(InternStatementFallback(assignment), assignment.Span));
                    return currentBlock;
            }
        }

        private int CompileForStatement(LoweredForStatement statement, int currentBlock)
        {
            CompileExpression(statement.Iterable, currentBlock);
            AddInstruction(currentBlock, ExecutableInstruction.GetIter(statement.Iterable.Span));

            var headBlock = CreateBlock();
            var bodyBlock = CreateBlock();
            var elseBlock = statement.ElseStatements is null ? -1 : CreateBlock();
            var exitBlock = CreateBlock();

            AddInstruction(currentBlock, ExecutableInstruction.Jump(headBlock, statement.Span));

            AddInstruction(headBlock, ExecutableInstruction.ForNext(statement.ElseStatements is null ? exitBlock : elseBlock, statement.Iterable.Span));
            AddInstruction(headBlock, ExecutableInstruction.AssignLoopTarget(InternLoopTarget(statement.Syntax.Target, statement.Span), statement.Span));
            AddInstruction(headBlock, ExecutableInstruction.Jump(bodyBlock, statement.Span));

            _loops.Push(new LoopContext(headBlock, exitBlock));
            try
            {
                var bodyExit = CompileStatements(statement.Body, bodyBlock);
                if (bodyExit is int bodyBlockExit && !IsTerminated(bodyBlockExit))
                {
                    AddInstruction(bodyBlockExit, ExecutableInstruction.Jump(headBlock, statement.Span));
                }
            }
            finally
            {
                _loops.Pop();
            }

            if (statement.ElseStatements is not null)
            {
                var elseExit = CompileStatements(statement.ElseStatements, elseBlock);
                if (elseExit is int elseBlockExit && !IsTerminated(elseBlockExit))
                {
                    AddInstruction(elseBlockExit, ExecutableInstruction.Jump(exitBlock, statement.Span));
                }
            }

            return exitBlock;
        }

        private int CompileTryStatement(LoweredTryStatement statement, int currentBlock)
        {
            var tryBlock = CreateBlock();
            AddInstruction(currentBlock, ExecutableInstruction.Jump(tryBlock, statement.Span));

            var protectedStart = tryBlock;
            var tryExit = CompileStatements(statement.TryBody, tryBlock);
            var protectedEnd = _blocks.Count - 1;

            var exceptBlock = -1;
            var exceptEnd = -1;
            int? exceptExit = null;
            if (statement.ExceptBody is not null)
            {
                exceptBlock = CreateBlock();
                exceptExit = CompileStatements(statement.ExceptBody, exceptBlock);
                exceptEnd = _blocks.Count - 1;
            }

            var elseBlock = -1;
            var elseEnd = -1;
            int? elseExit = null;
            if (statement.ElseBody is not null)
            {
                elseBlock = CreateBlock();
                elseExit = CompileStatements(statement.ElseBody, elseBlock);
                elseEnd = _blocks.Count - 1;
            }

            var finallyBlock = -1;
            int? finallyExit = null;
            if (statement.FinallyBody is not null)
            {
                finallyBlock = CreateBlock();
                finallyExit = CompileStatements(statement.FinallyBody, finallyBlock);
            }

            var afterBlock = CreateBlock();

            _regions.Add(new ExecutableExceptionRegion(
                protectedStart,
                protectedEnd,
                statement.Syntax.ExceptionTypeNames,
                statement.Syntax.ExceptionVariableName,
                statement.ExceptBody is null ? null : exceptBlock,
                statement.FinallyBody is null ? null : finallyBlock));

            if (tryExit is int tryBlockExit && !IsTerminated(tryBlockExit))
            {
                AddInstruction(tryBlockExit, ExecutableInstruction.Jump(
                    statement.ElseBody is not null ? elseBlock : statement.FinallyBody is not null ? finallyBlock : afterBlock,
                    statement.Span));
            }

            if (statement.ExceptBody is not null)
            {
                if (statement.FinallyBody is not null)
                {
                    _regions.Add(new ExecutableExceptionRegion(
                        exceptBlock,
                        exceptEnd,
                        null,
                        null,
                        null,
                        finallyBlock));
                }

                if (exceptExit is int exceptBlockExit && !IsTerminated(exceptBlockExit))
                {
                    AddInstruction(
                        exceptBlockExit,
                        ExecutableInstruction.ClearException(
                            statement.Syntax.ExceptionVariableName is null ? -1 : InternName(statement.Syntax.ExceptionVariableName),
                            statement.Span));
                    AddInstruction(exceptBlockExit, ExecutableInstruction.Jump(statement.FinallyBody is not null ? finallyBlock : afterBlock, statement.Span));
                }
            }

            if (statement.ElseBody is not null)
            {
                if (statement.FinallyBody is not null)
                {
                    _regions.Add(new ExecutableExceptionRegion(
                        elseBlock,
                        elseEnd,
                        null,
                        null,
                        null,
                        finallyBlock));
                }

                if (elseExit is int elseBlockExit && !IsTerminated(elseBlockExit))
                {
                    AddInstruction(elseBlockExit, ExecutableInstruction.Jump(statement.FinallyBody is not null ? finallyBlock : afterBlock, statement.Span));
                }
            }

            if (statement.FinallyBody is not null)
            {
                if (finallyExit is int finallyBlockExit && !IsTerminated(finallyBlockExit))
                {
                    AddInstruction(finallyBlockExit, ExecutableInstruction.EndFinally(afterBlock, statement.Span));
                }
            }

            return afterBlock;
        }

        private int CompileWithStatement(LoweredWithStatement statement, int currentBlock)
        {
            var managerSlot = InternSyntheticLocal("with_manager");

            CompileExpression(statement.ContextExpression, currentBlock);
            AddInstruction(currentBlock, ExecutableInstruction.ResolveContextManager(statement.ContextExpression.Span));
            AddInstruction(currentBlock, ExecutableInstruction.Dup(statement.Span));
            AddInstruction(currentBlock, ExecutableInstruction.StoreLocal(managerSlot, statement.Span));
            AddInstruction(currentBlock, ExecutableInstruction.EnterContextManager(statement.ContextExpression.Span));
            if (statement.Syntax.VariableName is not null)
            {
                CompileStoreBoundName(statement.Syntax.VariableName, statement.Span, currentBlock);
            }
            else
            {
                AddInstruction(currentBlock, ExecutableInstruction.PopTop(statement.Span));
            }

            var bodyBlock = CreateBlock();
            AddInstruction(currentBlock, ExecutableInstruction.Jump(bodyBlock, statement.Span));

            var protectedStart = bodyBlock;
            var bodyExit = CompileStatements(statement.Body, bodyBlock);
            var protectedEnd = _blocks.Count - 1;

            var finallyBlock = CreateBlock();
            var afterBlock = CreateBlock();

            _regions.Add(new ExecutableExceptionRegion(
                protectedStart,
                protectedEnd,
                null,
                null,
                null,
                finallyBlock));

            if (bodyExit is int bodyBlockExit && !IsTerminated(bodyBlockExit))
            {
                AddInstruction(bodyBlockExit, ExecutableInstruction.Jump(finallyBlock, statement.Span));
            }

            AddInstruction(finallyBlock, ExecutableInstruction.LoadLocal(managerSlot, statement.Span));
            AddInstruction(finallyBlock, ExecutableInstruction.ExitContextManager(statement.Span));
            AddInstruction(finallyBlock, ExecutableInstruction.EndFinally(afterBlock, statement.Span));

            return afterBlock;
        }

        private int CompileMatchStatement(LoweredMatchStatement statement, int currentBlock)
        {
            var subjectSlot = InternSyntheticLocal("match_subject");
            CompileExpression(statement.Subject, currentBlock);
            AddInstruction(currentBlock, ExecutableInstruction.StoreLocal(subjectSlot, statement.Subject.Span));

            var afterBlock = CreateBlock();
            var nextCaseBlock = CreateBlock();
            AddInstruction(currentBlock, ExecutableInstruction.Jump(nextCaseBlock, statement.Span));

            for (var i = 0; i < statement.Cases.Count; i++)
            {
                var matchCase = statement.Cases[i];
                var bodyBlock = CreateBlock();
                var failBlock = i == statement.Cases.Count - 1 ? afterBlock : CreateBlock();

                AddInstruction(nextCaseBlock, ExecutableInstruction.LoadLocal(subjectSlot, matchCase.Syntax.Span));
                AddInstruction(nextCaseBlock, ExecutableInstruction.MatchCase(InternMatchCase(matchCase.Syntax), failBlock, matchCase.Syntax.Span));
                AddInstruction(nextCaseBlock, ExecutableInstruction.Jump(bodyBlock, matchCase.Syntax.Span));

                var bodyExit = CompileStatements(matchCase.Body, bodyBlock);
                if (bodyExit is int bodyBlockExit && !IsTerminated(bodyBlockExit))
                {
                    AddInstruction(bodyBlockExit, ExecutableInstruction.Jump(afterBlock, matchCase.Syntax.Span));
                }

                nextCaseBlock = failBlock;
            }

            return afterBlock;
        }

        private int CompileIfStatement(LoweredIfStatement statement, int currentBlock)
        {
            CompileExpression(statement.Condition, currentBlock);

            var thenBlock = CreateBlock();
            var elseBlock = statement.ElseStatements is null ? -1 : CreateBlock();
            var joinBlock = CreateBlock();

            AddInstruction(currentBlock, ExecutableInstruction.JumpIfFalse(statement.ElseStatements is null ? joinBlock : elseBlock, statement.Condition.Span));
            AddInstruction(currentBlock, ExecutableInstruction.Jump(thenBlock, statement.Span));

            var thenExit = CompileStatements(statement.ThenStatements, thenBlock);
            if (thenExit is int thenBlockExit && !IsTerminated(thenBlockExit))
            {
                AddInstruction(thenBlockExit, ExecutableInstruction.Jump(joinBlock, statement.Span));
            }

            if (statement.ElseStatements is not null)
            {
                var elseExit = CompileStatements(statement.ElseStatements, elseBlock);
                if (elseExit is int elseBlockExit && !IsTerminated(elseBlockExit))
                {
                    AddInstruction(elseBlockExit, ExecutableInstruction.Jump(joinBlock, statement.Span));
                }
            }

            return joinBlock;
        }

        private int CompileWhileStatement(LoweredWhileStatement statement, int currentBlock)
        {
            var conditionBlock = CreateBlock();
            var bodyBlock = CreateBlock();
            var elseBlock = statement.ElseStatements is null ? -1 : CreateBlock();
            var exitBlock = CreateBlock();

            AddInstruction(currentBlock, ExecutableInstruction.Jump(conditionBlock, statement.Span));

            CompileExpression(statement.Condition, conditionBlock);
            AddInstruction(conditionBlock, ExecutableInstruction.JumpIfFalse(statement.ElseStatements is null ? exitBlock : elseBlock, statement.Condition.Span));
            AddInstruction(conditionBlock, ExecutableInstruction.Jump(bodyBlock, statement.Span));

            _loops.Push(new LoopContext(conditionBlock, exitBlock));
            try
            {
                var bodyExit = CompileStatements(statement.Body, bodyBlock);
                if (bodyExit is int bodyBlockExit && !IsTerminated(bodyBlockExit))
                {
                    AddInstruction(bodyBlockExit, ExecutableInstruction.Jump(conditionBlock, statement.Span));
                }
            }
            finally
            {
                _loops.Pop();
            }

            if (statement.ElseStatements is not null)
            {
                var elseExit = CompileStatements(statement.ElseStatements, elseBlock);
                if (elseExit is int elseBlockExit && !IsTerminated(elseBlockExit))
                {
                    AddInstruction(elseBlockExit, ExecutableInstruction.Jump(exitBlock, statement.Span));
                }
            }

            return exitBlock;
        }

        private int? CompileBreakStatement(LoweredBreakStatement statement, int currentBlock)
        {
            if (!_loops.TryPeek(out var loop))
            {
                throw new ExecutableLoweringFallbackException("Executable IR lowering cannot emit break outside a loop.");
            }

            AddInstruction(currentBlock, ExecutableInstruction.Jump(loop.BreakBlockIndex, statement.Span));
            return null;
        }

        private int? CompileContinueStatement(LoweredContinueStatement statement, int currentBlock)
        {
            if (!_loops.TryPeek(out var loop))
            {
                throw new ExecutableLoweringFallbackException("Executable IR lowering cannot emit continue outside a loop.");
            }

            AddInstruction(currentBlock, ExecutableInstruction.Jump(loop.ContinueBlockIndex, statement.Span));
            return null;
        }

    }
}
