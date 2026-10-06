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
        private readonly record struct CompiledClause(int StartBlock, int EndBlock, int? ExitBlock);

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

                case LoweredFunctionDefinitionStatement functionDefinition when _generator &&
                    StatementSyntaxTraversal.EnumerateDirectExpressions(functionDefinition.Syntax).Any(GeneratorSyntaxFacts.ContainsYield):
                    return CompileSuspendingFunction(functionDefinition, currentBlock);
                case LoweredClassDefinitionStatement classDefinition when _generator &&
                    StatementSyntaxTraversal.EnumerateDirectExpressions(classDefinition.Syntax).Any(GeneratorSyntaxFacts.ContainsYield):
                    return CompileSuspendingClass(classDefinition, currentBlock);
                case LoweredFunctionDefinitionStatement functionDefinition when functionDefinition.TypeParameters is not null:
                    AddInstruction(currentBlock, ExecutableInstruction.ExecuteFallbackStatement(InternStatementFallback(functionDefinition), functionDefinition.Span));
                    return currentBlock;
                case LoweredFunctionDefinitionStatement functionDefinition:
                    AddInstruction(currentBlock, ExecutableInstruction.DefineFunction(InternFunction(functionDefinition), functionDefinition.Span));
                    return currentBlock;

                case LoweredAssertStatement assertion when _generator:
                    return CompileSuspendingAssert(assertion, currentBlock);
                case LoweredRaiseStatement raised when _generator:
                    return CompileSuspendingRaise(raised, currentBlock);
                case LoweredDeleteStatement deleted when _generator:
                    return CompileSuspendingDelete(deleted.Target.Syntax, currentBlock);

                case LoweredAssignmentStatement assignment:
                    return CompileAssignmentStatement(assignment, currentBlock);

                case LoweredExpressionStatement expressionStatement:
                    currentBlock = CompileExpression(expressionStatement.Expression, currentBlock);
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
                        currentBlock = CompileExpression(returnStatement.Expression, currentBlock);
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
                    currentBlock = CompileExpression(simple.Expression, currentBlock);
                    CompileStoreBoundName(simple.Assignment.Name, simple.Span, currentBlock);
                    return currentBlock;

                case LoweredAnnotatedAssignmentStatement annotated:
                    if (annotated.Expression is null)
                    {
                        if (!_generator) throw new ExecutableLoweringFallbackException($"Executable IR lowering does not support annotation-only assignments: {annotated.Assignment.GetType().Name}.");
                        // Function-local annotations do not evaluate. Attribute
                        // and item annotation targets still evaluate their reads.
                        foreach (var read in AssignmentTargetFacts.Reads(annotated.Assignment.Target))
                        {
                            currentBlock = CompileExpression(LoweredScript.LowerExpression(read), currentBlock);
                            AddInstruction(currentBlock, ExecutableInstruction.PopTop(read.Span));
                        }
                        return currentBlock;
                    }

                    if (annotated.Assignment.Target is not NameAssignmentTargetSyntax targetName)
                    {
                        currentBlock = CompileExpression(annotated.Expression, currentBlock);
                        currentBlock = CompileStoreTarget(annotated.Assignment.Target, annotated.Span, currentBlock);
                        return currentBlock;
                    }

                    currentBlock = CompileExpression(annotated.Expression, currentBlock);
                    CompileStoreBoundName(targetName.Name, annotated.Span, currentBlock);
                    return currentBlock;

                case LoweredAugmentedAssignmentStatement augmented:
                    if (augmented.Target is not LoweredNameAugmentedAssignmentTarget augmentedName)
                    {
                        if (_generator && (GeneratorSyntaxFacts.ContainsYield(augmented.Expression.Syntax) ||
                            AssignmentTargetFacts.Reads(augmented.Target.Syntax).Any(GeneratorSyntaxFacts.ContainsYield)))
                            return CompileSuspendingAugmentedAssignment(augmented, currentBlock);
                        AddInstruction(currentBlock, ExecutableInstruction.ExecuteFallbackStatement(InternStatementFallback(assignment), assignment.Span));
                        return currentBlock;
                    }

                    CompileLoadIdentifier(augmentedName.Target.Name, augmented.Span, currentBlock);
                    currentBlock = CompileExpression(augmented.Expression, currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.Augmented(MapAugmentedAssignmentOperator(augmented.Assignment.Operator), augmented.Span));
                    CompileStoreBoundName(augmentedName.Target.Name, augmented.Span, currentBlock);
                    return currentBlock;

                case LoweredChainedAssignmentStatement chained:
                    currentBlock = CompileExpression(chained.Expression, currentBlock);
                    for (var i = 0; i < chained.Assignment.Targets.Count; i++)
                    {
                        if (i < chained.Assignment.Targets.Count - 1)
                        {
                            AddInstruction(currentBlock, ExecutableInstruction.Dup(chained.Span));
                        }

                        currentBlock = CompileStoreTarget(chained.Assignment.Targets[i], chained.Span, currentBlock);
                    }
                    return currentBlock;

                case LoweredUnpackingAssignmentStatement unpacking:
                    currentBlock = CompileExpression(unpacking.Expression, currentBlock);
                    if (_generator && unpacking.Assignment.Targets.SelectMany(t => AssignmentTargetFacts.Reads(AssignmentTargetFacts.FromUnpacking(t))).Any(GeneratorSyntaxFacts.ContainsYield))
                        return CompileStoreTarget(new UnpackingAssignmentTargetGroupSyntax(unpacking.Assignment.Targets, unpacking.Span), unpacking.Span, currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.AssignUnpackingTargets(InternUnpackingTargets(unpacking.Assignment.Targets, unpacking.Span), unpacking.Span));
                    return currentBlock;

                case LoweredMemberAssignmentStatement member:
                    currentBlock = CompileExpression(member.Expression, currentBlock);
                    currentBlock = CompileStoreTarget(new MemberAssignmentTargetSyntax(member.Assignment.Target, member.Assignment.MemberName, member.Span), member.Span, currentBlock);
                    return currentBlock;
                case LoweredSubscriptAssignmentStatement subscript:
                    currentBlock = CompileExpression(subscript.Expression, currentBlock);
                    currentBlock = CompileStoreTarget(new SubscriptAssignmentTargetSyntax(subscript.Assignment.Target, subscript.Assignment.Index, subscript.Span), subscript.Span, currentBlock);
                    return currentBlock;
                case LoweredSliceAssignmentStatement slice:
                    currentBlock = CompileExpression(slice.Expression, currentBlock);
                    currentBlock = CompileStoreTarget(new SliceAssignmentTargetSyntax(slice.Assignment.Target, slice.Assignment.Start, slice.Assignment.End, slice.Assignment.Step, slice.Span), slice.Span, currentBlock);
                    return currentBlock;
                default:
                    AddInstruction(currentBlock, ExecutableInstruction.ExecuteFallbackStatement(InternStatementFallback(assignment), assignment.Span));
                    return currentBlock;
            }
        }

        private int CompileStoreTarget(AssignmentTargetSyntax target, LythonSourceSpan span, int block)
        {
            if (target is NameAssignmentTargetSyntax name)
            {
                CompileStoreBoundName(name.Name, span, block);
                return block;
            }
            if (_generator && AssignmentTargetFacts.Reads(target).Any(GeneratorSyntaxFacts.ContainsYield))
                return CompileSuspendingStoreTarget(target, span, block);
            AddInstruction(block, ExecutableInstruction.AssignLoopTarget(InternLoopTarget(AssignmentTargetFacts.ToLoop(target), span), span));
            return block;
        }

        private int CompileForStatement(LoweredForStatement statement, int currentBlock)
        {
            currentBlock = CompileExpression(statement.Iterable, currentBlock);
            AddInstruction(currentBlock, ExecutableInstruction.GetIter(statement.Iterable.Span));

            var headBlock = CreateBlock();
            var bodyBlock = CreateBlock();
            var elseBlock = statement.ElseStatements is null ? -1 : CreateBlock();
            var exitBlock = CreateBlock();

            AddInstruction(currentBlock, ExecutableInstruction.Jump(headBlock, statement.Span));

            AddInstruction(headBlock, ExecutableInstruction.ForNext(statement.ElseStatements is null ? exitBlock : elseBlock, statement.Iterable.Span));
            var suspendingTarget = _generator && AssignmentTargetFacts.Reads(statement.Syntax.Target).Any(GeneratorSyntaxFacts.ContainsYield);
            var targetExit = suspendingTarget
                ? CompileSuspendingLoopTarget(statement.Syntax.Target, statement.Span, headBlock) : headBlock;
            if (!suspendingTarget)
                AddInstruction(headBlock, ExecutableInstruction.AssignLoopTarget(InternLoopTarget(statement.Syntax.Target, statement.Span), statement.Span));
            AddInstruction(targetExit, ExecutableInstruction.Jump(bodyBlock, statement.Span));

            _loops.Push(new LoopContext(headBlock, exitBlock, HasIterator: true));
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
            _protectedDepth++;
            try
            {
            CompiledClause CompileClause(IReadOnlyList<LoweredStatement> body)
            {
                var startBlock = CreateBlock();
                var exitBlock = CompileStatements(body, startBlock);
                // Compiling a clause may append nested blocks; every one belongs to this protected range.
                return new CompiledClause(startBlock, _blocks.Count - 1, exitBlock);
            }

            CompiledClause? CompileOptionalClause(IReadOnlyList<LoweredStatement>? body)
                => body is null ? null : CompileClause(body);

            void ProtectWithFinally(CompiledClause clause, CompiledClause cleanup)
            {
                _regions.Add(new ExecutableExceptionRegion(
                    clause.StartBlock,
                    clause.EndBlock,
                    null,
                    null,
                    null,
                    cleanup.StartBlock,
                    cleanup.StartBlock,
                    cleanup.EndBlock, CleanupId: cleanup.StartBlock));
            }

            var tryClause = CompileClause(statement.TryBody);
            AddInstruction(currentBlock, ExecutableInstruction.Jump(tryClause.StartBlock, statement.Span));

            var elseClause = CompileOptionalClause(statement.ElseBody);
            var finallyClause = CompileOptionalClause(statement.FinallyBody);

            var afterBlock = CreateBlock();

            if (statement.ExceptClauses.Any(clause => clause.ExceptionType is not null))
            {
                // Handler selection is executable code: header expressions can
                // call/await guest protocols and eventually suspend with yield.
                var selectionStart = CreateBlock();
                var selectionBlock = selectionStart;
                foreach (var clause in statement.ExceptClauses)
                {
                    var nextHeader = CreateBlock();
                    if (clause.ExceptionType is { } header)
                    {
                        selectionBlock = CompileExpression(header, selectionBlock);
                        AddInstruction(selectionBlock, ExecutableInstruction.MatchException(header.Span));
                        AddInstruction(selectionBlock, ExecutableInstruction.JumpIfFalse(nextHeader, header.Span));
                    }
                    if (clause.Syntax.ExceptionVariableName is { } variable)
                        AddInstruction(selectionBlock, ExecutableInstruction.BindException(InternName(variable), clause.Syntax.Span));

                    var handlerExit = CompileStatements(clause.Body, selectionBlock);
                    if (handlerExit is int exit && !IsTerminated(exit))
                    {
                        AddInstruction(exit, ExecutableInstruction.ClearException(
                            clause.Syntax.ExceptionVariableName is null ? -1 : InternName(clause.Syntax.ExceptionVariableName), clause.Syntax.Span));
                        AddInstruction(exit, ExecutableInstruction.Jump(finallyClause?.StartBlock ?? afterBlock, statement.Span));
                    }
                    selectionBlock = nextHeader;
                }
                AddInstruction(selectionBlock, ExecutableInstruction.ReraiseException(statement.Span));
                var selectionEnd = _blocks.Count - 1;
                _regions.Add(new ExecutableExceptionRegion(tryClause.StartBlock, tryClause.EndBlock,
                    null, null, selectionStart, null, selectionStart, selectionEnd));
                if (finallyClause is { } cleanup)
                    ProtectWithFinally(new CompiledClause(selectionStart, selectionEnd, null), cleanup);
            }
            else
            {
                // Each handler guards the same range in order; a separate
                // finally-only region propagates uncaught abrupt completions.
                foreach (var exceptClause in statement.ExceptClauses)
                {
                    var handler = CompileClause(exceptClause.Body);
                    _regions.Add(new ExecutableExceptionRegion(
                        tryClause.StartBlock,
                        tryClause.EndBlock,
                        exceptClause.Syntax.ExceptionTypeNames,
                        exceptClause.Syntax.ExceptionVariableName,
                        handler.StartBlock,
                        null,
                        handler.StartBlock,
                        handler.EndBlock,
                        exceptClause.Syntax.ExceptionTypesAreTuple));

                    if (finallyClause is { } handlerCleanup)
                    {
                        // Exceptions raised by a handler still execute the surrounding finally clause.
                        ProtectWithFinally(handler, handlerCleanup);
                    }

                    if (handler.ExitBlock is int handlerExit && !IsTerminated(handlerExit))
                    {
                        AddInstruction(
                            handlerExit,
                            ExecutableInstruction.ClearException(
                                exceptClause.Syntax.ExceptionVariableName is null ? -1 : InternName(exceptClause.Syntax.ExceptionVariableName),
                                statement.Span));
                        AddInstruction(handlerExit, ExecutableInstruction.Jump(finallyClause?.StartBlock ?? afterBlock, statement.Span));
                    }
                }

            }

            if (finallyClause is { } finallyRegion)
            {
                _regions.Add(new ExecutableExceptionRegion(
                    tryClause.StartBlock,
                    tryClause.EndBlock,
                    null,
                    null,
                    null,
                    finallyRegion.StartBlock,
                    finallyRegion.StartBlock,
                    finallyRegion.EndBlock, CleanupId: finallyRegion.StartBlock));
            }

            if (tryClause.ExitBlock is int tryExit && !IsTerminated(tryExit))
            {
                AddInstruction(tryExit, ExecutableInstruction.Jump(
                    elseClause?.StartBlock ?? finallyClause?.StartBlock ?? afterBlock,
                    statement.Span));
            }

            if (elseClause is { } success)
            {
                if (finallyClause is { } cleanup)
                {
                    // The else suite is outside the except handler but remains inside finally protection.
                    ProtectWithFinally(success, cleanup);
                }

                if (success.ExitBlock is int successExit && !IsTerminated(successExit))
                {
                    AddInstruction(successExit, ExecutableInstruction.Jump(finallyClause?.StartBlock ?? afterBlock, statement.Span));
                }
            }

            if (finallyClause is { ExitBlock: int finallyExit } && !IsTerminated(finallyExit))
            {
                AddInstruction(finallyExit, ExecutableInstruction.EndFinally(afterBlock, statement.Span, finallyClause!.Value.StartBlock));
            }

            return afterBlock;
            }
            finally
            {
                _protectedDepth--;
            }
        }

        private int CompileWithStatement(LoweredWithStatement statement, int currentBlock)
        {
            var managerSlot = InternSyntheticLocal("with_manager");

            currentBlock = CompileExpression(statement.ContextExpression, currentBlock);
            AddInstruction(currentBlock, ExecutableInstruction.ResolveContextManager(statement.ContextExpression.Span));
            AddInstruction(currentBlock, ExecutableInstruction.Dup(statement.Span));
            AddInstruction(currentBlock, ExecutableInstruction.StoreLocal(managerSlot, statement.Span));
            AddInstruction(currentBlock, ExecutableInstruction.EnterContextManager(statement.ContextExpression.Span));
            var enteredSlot = statement.Syntax.Target is null ? -1 : InternSyntheticLocal("with_value");
            AddInstruction(currentBlock, enteredSlot < 0
                ? ExecutableInstruction.PopTop(statement.Span)
                : ExecutableInstruction.StoreLocal(enteredSlot, statement.Span));
            var bodyBlock = CreateBlock();
            AddInstruction(currentBlock, ExecutableInstruction.Jump(bodyBlock, statement.Span));
            var protectedStart = bodyBlock;
            if (statement.Syntax.Target is not null)
            {
                AddInstruction(bodyBlock, ExecutableInstruction.LoadLocal(enteredSlot, statement.Span));
                bodyBlock = CompileStoreTarget(statement.Syntax.Target!, statement.Span, bodyBlock);
            }
            _protectedDepth++;
            int? bodyExit;
            try
            {
                bodyExit = CompileStatements(statement.Body, bodyBlock);
            }
            finally
            {
                _protectedDepth--;
            }
            var protectedEnd = _blocks.Count - 1;

            var finallyBlock = CreateBlock();
            var afterBlock = CreateBlock();

            _regions.Add(new ExecutableExceptionRegion(
                protectedStart,
                protectedEnd,
                null,
                null,
                null,
                finallyBlock,
                protectedStart,
                protectedEnd, CleanupId: finallyBlock));

            if (bodyExit is int bodyBlockExit && !IsTerminated(bodyBlockExit))
            {
                AddInstruction(bodyBlockExit, ExecutableInstruction.Jump(finallyBlock, statement.Span));
            }

            AddInstruction(finallyBlock, ExecutableInstruction.LoadLocal(managerSlot, statement.Span));
            AddInstruction(finallyBlock, ExecutableInstruction.ExitContextManager(statement.Span, finallyBlock));
            AddInstruction(finallyBlock, ExecutableInstruction.EndFinally(afterBlock, statement.Span, finallyBlock));

            return afterBlock;
        }

        private int CompileMatchStatement(LoweredMatchStatement statement, int currentBlock)
        {
            var subjectSlot = InternSyntheticLocal("match_subject");
            currentBlock = CompileExpression(statement.Subject, currentBlock);
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
                var successBlock = nextCaseBlock;
                if (matchCase.Guard is { } guard)
                {
                    successBlock = CompileExpression(guard, successBlock);
                    AddInstruction(successBlock, ExecutableInstruction.JumpIfFalse(failBlock, guard.Span));
                }
                AddInstruction(successBlock, ExecutableInstruction.Jump(bodyBlock, matchCase.Syntax.Span));

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
            currentBlock = CompileExpression(statement.Condition, currentBlock);

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

            var conditionEntry = conditionBlock;
            conditionBlock = CompileExpression(statement.Condition, conditionBlock);
            AddInstruction(conditionBlock, ExecutableInstruction.JumpIfFalse(statement.ElseStatements is null ? exitBlock : elseBlock, statement.Condition.Span));
            AddInstruction(conditionBlock, ExecutableInstruction.Jump(bodyBlock, statement.Span));

            _loops.Push(new LoopContext(conditionEntry, exitBlock, HasIterator: false));
            try
            {
                var bodyExit = CompileStatements(statement.Body, bodyBlock);
                if (bodyExit is int bodyBlockExit && !IsTerminated(bodyBlockExit))
                {
                    AddInstruction(bodyBlockExit, ExecutableInstruction.Jump(conditionEntry, statement.Span));
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
            if (_protectedDepth > 0 && !_generator)
            {
                throw new ExecutableLoweringFallbackException("Executable IR lowering does not support break inside a protected with/try region; using the lowered execution path.");
            }

            if (!_loops.TryPeek(out var loop))
            {
                throw new ExecutableLoweringFallbackException("Executable IR lowering cannot emit break outside a loop.");
            }

            if (_protectedDepth > 0)
            {
                AddInstruction(currentBlock, ExecutableInstruction.AbruptJump(loop.BreakBlockIndex, loop.HasIterator, statement.Span));
                return null;
            }

            if (loop.HasIterator)
            {
                AddInstruction(currentBlock, ExecutableInstruction.PopTop(statement.Span));
            }

            AddInstruction(currentBlock, ExecutableInstruction.Jump(loop.BreakBlockIndex, statement.Span));
            return null;
        }

        private int? CompileContinueStatement(LoweredContinueStatement statement, int currentBlock)
        {
            if (_protectedDepth > 0 && !_generator)
            {
                throw new ExecutableLoweringFallbackException("Executable IR lowering does not support continue inside a protected with/try region; using the lowered execution path.");
            }

            if (!_loops.TryPeek(out var loop))
            {
                throw new ExecutableLoweringFallbackException("Executable IR lowering cannot emit continue outside a loop.");
            }

            AddInstruction(currentBlock, _protectedDepth > 0
                ? ExecutableInstruction.AbruptJump(loop.ContinueBlockIndex, false, statement.Span)
                : ExecutableInstruction.Jump(loop.ContinueBlockIndex, statement.Span));
            return null;
        }

    }
}
