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

        private int? CompileStatements(IReadOnlyList<StatementSyntax> statements, int entryBlock, int firstStatement = 0)
        {
            int? current = entryBlock;
            for (var index = firstStatement; index < statements.Count; index++)
            {
                if (current is null)
                {
                    return null;
                }

                PythonTokenization.CheckCancellation(_compilation.CancellationToken, index - firstStatement);
                current = CompileStatement(statements[index], current.Value);
            }

            return current;
        }

        private int? CompileStatement(StatementSyntax statement, int currentBlock)
        {
            switch (statement)
            {
                case ImportStatementSyntax importStatement:
                    AddInstruction(currentBlock, ExecutableInstruction.Import(InternImport(importStatement), importStatement.Span));
                    return currentBlock;

                case ScopeDirectiveStatementSyntax:
                    return currentBlock;

                case ClassDefinitionStatementSyntax classDefinition:
                    return CompileClassDefinition(classDefinition, currentBlock);
                case FunctionDefinitionStatementSyntax functionDefinition:
                    return CompileFunctionDefinition(functionDefinition, currentBlock, _generator &&
                        StatementSyntaxTraversal.EnumerateDirectExpressions(functionDefinition).Any(GeneratorSyntaxFacts.ContainsYield));

                case AssertStatementSyntax assertion:
                    return CompileAssert(assertion, currentBlock);
                case RaiseStatementSyntax raised:
                    return CompileRaise(raised, currentBlock);
                case DeleteStatementSyntax deleted:
                    return CompileDelete(deleted.Target, currentBlock);

                case AssignmentStatementSyntax or AnnotatedAssignmentStatementSyntax or AugmentedAssignmentStatementSyntax
                    or ChainedAssignmentStatementSyntax or UnpackingAssignmentStatementSyntax or MemberAssignmentStatementSyntax
                    or SubscriptAssignmentStatementSyntax or SliceAssignmentStatementSyntax:
                    return CompileAssignmentStatement(statement, currentBlock);

                case ExpressionStatementSyntax expressionStatement:
                    currentBlock = CompileExpression(expressionStatement.Expression, currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.PopTop(expressionStatement.Span));
                    return currentBlock;

                case IfStatementSyntax ifStatement:
                    return CompileIfStatement(ifStatement, currentBlock);

                case ForStatementSyntax forStatement:
                    return CompileForStatement(forStatement, currentBlock);

                case WhileStatementSyntax whileStatement:
                    return CompileWhileStatement(whileStatement, currentBlock);

                case MatchStatementSyntax matchStatement:
                    return CompileMatchStatement(matchStatement, currentBlock);

                case WithStatementSyntax withStatement:
                    return CompileWithStatement(withStatement, currentBlock);

                case TryStatementSyntax tryStatement:
                    return CompileTryStatement(tryStatement, currentBlock);

                case PassStatementSyntax:
                    return currentBlock;

                case BreakStatementSyntax breakStatement:
                    return CompileBreakStatement(breakStatement, currentBlock);

                case ContinueStatementSyntax continueStatement:
                    return CompileContinueStatement(continueStatement, currentBlock);

                case ReturnStatementSyntax returnStatement:
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

                case TypeAliasStatementSyntax alias:
                    InternModuleBinding(alias.Name);
                    var binding = new LoweredTypeAlias(alias, LoweredScript.LowerExpression(alias.Value));
                    EmitDefinition(new ExecutableBindTypeAlias(binding), alias.Span, currentBlock);
                    return currentBlock;
                default:
                    throw new InvalidOperationException($"Unknown statement syntax: {statement.GetType().Name}.");
            }
        }

        private int CompileAssignmentStatement(StatementSyntax assignment, int currentBlock)
        {
            switch (assignment)
            {
                case AssignmentStatementSyntax simple:
                    currentBlock = CompileExpression(simple.Expression, currentBlock);
                    CompileStoreBoundName(simple.Name, simple.Span, currentBlock);
                    return currentBlock;

                case AnnotatedAssignmentStatementSyntax annotated:
                    if (annotated.Expression is null)
                    {
                        // Bare targets still evaluate receiver/index reads.
                        // Only module scope evaluates the annotation below.
                        foreach (var read in AssignmentTargetFacts.Reads(annotated.Target))
                        {
                            currentBlock = CompileExpression(read, currentBlock);
                            AddInstruction(currentBlock, ExecutableInstruction.PopTop(read.Span));
                        }
                    }
                    else if (annotated.Target is not NameAssignmentTargetSyntax targetName)
                    {
                        currentBlock = CompileExpression(annotated.Expression, currentBlock);
                        currentBlock = CompileStoreTarget(LoweredStoreTarget.FromSyntax(annotated.Target), annotated.Span, currentBlock);
                    }
                    else
                    {
                        currentBlock = CompileExpression(annotated.Expression, currentBlock);
                        CompileStoreBoundName(targetName.Name, annotated.Span, currentBlock);
                    }
                    return _parameterLayout is null ? CompileModuleAnnotation(annotated, currentBlock) : currentBlock;

                case AugmentedAssignmentStatementSyntax augmented:
                    var augmentedTarget = LoweredStoreTarget.FromSyntax(augmented.Target);
                    if (augmentedTarget is LoweredSliceStoreTarget augmentedSlice)
                    {
                        currentBlock = CompileExpression(augmentedSlice.Receiver, currentBlock);
                        foreach (var bound in new[] { augmentedSlice.Start, augmentedSlice.End, augmentedSlice.Step })
                        {
                            if (bound is null) AddInstruction(currentBlock, ExecutableInstruction.LoadConst(InternConstant(PyNone.Instance), augmentedSlice.Span));
                            else currentBlock = CompileExpression(bound, currentBlock);
                        }
                        AddInstruction(currentBlock, ExecutableInstruction.ReadRetainedSlice(augmentedSlice.Span));
                        currentBlock = CompileExpression(augmented.Expression, currentBlock);
                        AddInstruction(currentBlock, ExecutableInstruction.Augmented(augmented.Operator, augmented.Span));
                        AddInstruction(currentBlock, ExecutableInstruction.StoreSlice(InternConstant(augmentedSlice.Span), augmented.Span, valueLast: true));
                        return currentBlock;
                    }
                    if (augmentedTarget is LoweredSubscriptStoreTarget augmentedItem)
                    {
                        currentBlock = CompileExpression(augmentedItem.Receiver, currentBlock);
                        currentBlock = CompileExpression(augmentedItem.Index, currentBlock);
                        AddInstruction(currentBlock, ExecutableInstruction.ReadRetainedSubscript(augmentedItem.Span));
                        currentBlock = CompileExpression(augmented.Expression, currentBlock);
                        AddInstruction(currentBlock, ExecutableInstruction.Augmented(augmented.Operator, augmented.Span));
                        AddInstruction(currentBlock, ExecutableInstruction.StoreSubscript(InternConstant(augmentedItem.Span), augmented.Span, valueLast: true));
                        return currentBlock;
                    }
                    if (augmentedTarget is LoweredMemberStoreTarget augmentedMember)
                    {
                        currentBlock = CompileExpression(augmentedMember.Receiver, currentBlock);
                        AddInstruction(currentBlock, ExecutableInstruction.Dup(augmentedMember.Span));
                        AddInstruction(currentBlock, ExecutableInstruction.LoadMember(InternName(augmentedMember.MemberName),
                            AllocateLookupCache(), augmentedMember.Span));
                        currentBlock = CompileExpression(augmented.Expression, currentBlock);
                        AddInstruction(currentBlock, ExecutableInstruction.Augmented(augmented.Operator, augmented.Span));
                        AddInstruction(currentBlock, ExecutableInstruction.StoreMember(InternName(augmentedMember.MemberName), InternConstant(augmentedMember.Span), augmented.Span, valueLast: true));
                        return currentBlock;
                    }
                    if (augmentedTarget is not LoweredNameStoreTarget augmentedName)
                        throw new InvalidOperationException("Unknown augmented assignment target.");

                    CompileLoadIdentifier(augmentedName.Name, augmented.Span, currentBlock);
                    currentBlock = CompileExpression(augmented.Expression, currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.Augmented(augmented.Operator, augmented.Span));
                    CompileStoreBoundName(augmentedName.Name, augmented.Span, currentBlock);
                    return currentBlock;

                case ChainedAssignmentStatementSyntax chained:
                    currentBlock = CompileExpression(chained.Expression, currentBlock);
                    for (var i = 0; i < chained.Targets.Count; i++)
                    {
                        if (i < chained.Targets.Count - 1)
                        {
                            AddInstruction(currentBlock, ExecutableInstruction.Dup(chained.Span));
                        }

                        currentBlock = CompileStoreTarget(LoweredStoreTarget.FromSyntax(chained.Targets[i]), chained.Span, currentBlock);
                    }
                    return currentBlock;

                case UnpackingAssignmentStatementSyntax unpacking:
                    currentBlock = CompileExpression(unpacking.Expression, currentBlock);
                    return CompileStoreTarget(LoweredStoreTarget.FromUnpacking(unpacking.Targets, unpacking.Expression.Span), unpacking.Span, currentBlock);

                case MemberAssignmentStatementSyntax member:
                    currentBlock = CompileExpression(member.Expression, currentBlock);
                    currentBlock = CompileExpression(member.Target, currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.StoreMember(InternName(member.MemberName), InternConstant(member.Span), member.Span));
                    return currentBlock;
                case SubscriptAssignmentStatementSyntax subscript:
                    currentBlock = CompileExpression(subscript.Expression, currentBlock);
                    currentBlock = CompileExpression(subscript.Target, currentBlock);
                    currentBlock = CompileExpression(subscript.Index, currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.StoreSubscript(InternConstant(subscript.Span), subscript.Span));
                    return currentBlock;
                case SliceAssignmentStatementSyntax slice:
                    currentBlock = CompileExpression(slice.Expression, currentBlock);
                    currentBlock = CompileExpression(slice.Target, currentBlock);
                    foreach (var bound in new[] { slice.Start, slice.End, slice.Step })
                    {
                        if (bound is null) AddInstruction(currentBlock, ExecutableInstruction.LoadConst(InternConstant(PyNone.Instance), slice.Span));
                        else currentBlock = CompileExpression(bound, currentBlock);
                    }
                    AddInstruction(currentBlock, ExecutableInstruction.StoreSlice(InternConstant(slice.Span), slice.Span));
                    return currentBlock;
                default:
                    throw new InvalidOperationException($"Unknown assignment syntax: {assignment.GetType().Name}.");
            }
        }

        private int CompileStoreTarget(LoweredStoreTarget target, LythonSourceSpan span, int block,
            LythonSourceSpan? loopSpan = null)
        {
            if (target is LoweredNameStoreTarget name)
            {
                foreach (var names in _buffers.ComprehensionNames)
                {
                    if (!names.Contains(name.Name)) continue;
                    AddInstruction(block, ExecutableInstruction.StoreGlobal(InternName(name.Name), span));
                    return block;
                }
                CompileStoreBoundName(name.Name, span, block);
                return block;
            }
            if (target is LoweredUnpackingStoreTarget group)
            {
                AddInstruction(block, ExecutableInstruction.UnpackValues(group.Layout, span));
                // Loop destructuring shares the header's diagnostic origin;
                // ordinary assignment retains each nested target's own span.
                foreach (var child in group.Items)
                    block = CompileStoreTarget(child, loopSpan ?? child.Span, block, loopSpan);
                return block;
            }
            if (target is LoweredSubscriptStoreTarget item)
            {
                block = CompileExpression(item.Receiver, block);
                block = CompileExpression(item.Index, block);
                AddInstruction(block, ExecutableInstruction.StoreSubscript(InternConstant(item.Span), span));
                return block;
            }
            if (target is LoweredMemberStoreTarget member)
            {
                block = CompileExpression(member.Receiver, block);
                AddInstruction(block, ExecutableInstruction.StoreMember(InternName(member.MemberName), InternConstant(member.Span), span));
                return block;
            }
            if (target is LoweredSliceStoreTarget slice)
            {
                block = CompileExpression(slice.Receiver, block);
                foreach (var bound in new[] { slice.Start, slice.End, slice.Step })
                {
                    if (bound is null) AddInstruction(block, ExecutableInstruction.LoadConst(InternConstant(PyNone.Instance), span));
                    else block = CompileExpression(bound, block);
                }
                AddInstruction(block, ExecutableInstruction.StoreSlice(InternConstant(slice.Span), span));
                return block;
            }
            throw new InvalidOperationException("Unknown assignment target.");
        }

        private int CompileForStatement(ForStatementSyntax statement, int currentBlock)
        {
            currentBlock = CompileExpression(statement.Iterable, currentBlock);
            AddInstruction(currentBlock, ExecutableInstruction.GetIter(statement.Iterable.Span));

            var headBlock = CreateBlock();
            var elseBlock = statement.ElseStatements is null ? -1 : CreateBlock();
            var exitBlock = CreateBlock();

            AddInstruction(currentBlock, ExecutableInstruction.Jump(headBlock, statement.Span));

            AddInstruction(headBlock, ExecutableInstruction.ForNext(statement.ElseStatements is null ? exitBlock : elseBlock, statement.Iterable.Span));
            var targetExit = CompileStoreTarget(LoweredStoreTarget.FromLoop(statement.Target, statement.Iterable.Span), statement.Span, headBlock, statement.Span);

            _buffers.Loops.Push(new LoopContext(headBlock, exitBlock, HasIterator: true, _protectedDepth));
            try
            {
                var bodyExit = CompileStatements(statement.Body, targetExit);
                if (bodyExit is int bodyBlockExit && !IsTerminated(bodyBlockExit))
                {
                    AddInstruction(bodyBlockExit, ExecutableInstruction.Jump(headBlock, statement.Span));
                }
            }
            finally
            {
                _buffers.Loops.Pop();
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

        private int CompileTryStatement(TryStatementSyntax statement, int currentBlock)
        {
            _protectedDepth++;
            try
            {
            CompiledClause CompileClause(IReadOnlyList<StatementSyntax> body)
            {
                var startBlock = CreateBlock();
                var exitBlock = CompileStatements(body, startBlock);
                // Compiling a clause may append nested blocks; every one belongs to this protected range.
                return new CompiledClause(startBlock, _buffers.Blocks.Count - 1, exitBlock);
            }

            CompiledClause? CompileOptionalClause(IReadOnlyList<StatementSyntax>? body)
                => body is null ? null : CompileClause(body);

            void ProtectWithFinally(CompiledClause clause, CompiledClause cleanup)
            {
                _buffers.Regions.Add(new ExecutableExceptionRegion(
                    clause.StartBlock,
                    clause.EndBlock,
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

            if (statement.ExceptClauses.Any(clause => clause.ExceptionTypeExpression is not null))
            {
                // Handler selection is executable code: header expressions can
                // call/await guest protocols and eventually suspend with yield.
                var selectionStart = CreateBlock();
                var selectionBlock = selectionStart;
                foreach (var clause in statement.ExceptClauses)
                {
                    var nextHeader = CreateBlock();
                    if (clause.ExceptionTypeExpression is { } header)
                    {
                        selectionBlock = CompileExpression(header, selectionBlock);
                        AddInstruction(selectionBlock, ExecutableInstruction.MatchException(header.Span));
                        AddInstruction(selectionBlock, ExecutableInstruction.JumpIfFalse(nextHeader, header.Span));
                    }
                    if (clause.ExceptionVariableName is { } variable)
                        AddInstruction(selectionBlock, ExecutableInstruction.BindException(InternName(variable), clause.Span));

                    var handlerExit = CompileStatements(clause.Body, selectionBlock);
                    if (handlerExit is int exit && !IsTerminated(exit))
                    {
                        AddInstruction(exit, ExecutableInstruction.ClearException(
                            clause.ExceptionVariableName is null ? -1 : InternName(clause.ExceptionVariableName), clause.Span));
                        AddInstruction(exit, ExecutableInstruction.Jump(finallyClause?.StartBlock ?? afterBlock, statement.Span));
                    }
                    selectionBlock = nextHeader;
                }
                AddInstruction(selectionBlock, ExecutableInstruction.ReraiseException(statement.Span));
                var selectionEnd = _buffers.Blocks.Count - 1;
                _buffers.Regions.Add(new ExecutableExceptionRegion(tryClause.StartBlock, tryClause.EndBlock, selectionStart, null, selectionStart, selectionEnd));
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
                    _buffers.Regions.Add(new ExecutableExceptionRegion(
                        tryClause.StartBlock,
                        tryClause.EndBlock,
                        handler.StartBlock,
                        null,
                        handler.StartBlock,
                        handler.EndBlock));

                    if (finallyClause is { } handlerCleanup)
                    {
                        // Exceptions raised by a handler still execute the surrounding finally clause.
                        ProtectWithFinally(handler, handlerCleanup);
                    }

                    if (handler.ExitBlock is int handlerExit && !IsTerminated(handlerExit))
                    {
                        AddInstruction(
                            handlerExit,
                            ExecutableInstruction.ClearException(-1, statement.Span));
                        AddInstruction(handlerExit, ExecutableInstruction.Jump(finallyClause?.StartBlock ?? afterBlock, statement.Span));
                    }
                }

            }

            if (finallyClause is { } finallyRegion)
            {
                _buffers.Regions.Add(new ExecutableExceptionRegion(
                    tryClause.StartBlock,
                    tryClause.EndBlock,
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

        private int CompileWithStatement(WithStatementSyntax statement, int currentBlock)
        {
            var managerSlot = InternSyntheticLocal("with_manager");

            currentBlock = CompileExpression(statement.ContextExpression, currentBlock);
            AddInstruction(currentBlock, ExecutableInstruction.ResolveContextManager(statement.ContextExpression.Span));
            AddInstruction(currentBlock, ExecutableInstruction.Dup(statement.Span));
            AddInstruction(currentBlock, ExecutableInstruction.StoreLocal(managerSlot, statement.Span));
            AddInstruction(currentBlock, ExecutableInstruction.EnterContextManager(statement.ContextExpression.Span));
            var enteredSlot = statement.Target is null ? -1 : InternSyntheticLocal("with_value");
            AddInstruction(currentBlock, enteredSlot < 0
                ? ExecutableInstruction.PopTop(statement.Span)
                : ExecutableInstruction.StoreLocal(enteredSlot, statement.Span));
            var bodyBlock = CreateBlock();
            AddInstruction(currentBlock, ExecutableInstruction.Jump(bodyBlock, statement.Span));
            var protectedStart = bodyBlock;
            if (statement.Target is not null)
            {
                AddInstruction(bodyBlock, ExecutableInstruction.LoadLocal(enteredSlot, statement.Span));
                bodyBlock = CompileStoreTarget(LoweredStoreTarget.FromSyntax(statement.Target), statement.Span, bodyBlock);
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
            var protectedEnd = _buffers.Blocks.Count - 1;

            var finallyBlock = CreateBlock();
            var afterBlock = CreateBlock();

            _buffers.Regions.Add(new ExecutableExceptionRegion(
                protectedStart,
                protectedEnd,
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

        private int CompileMatchStatement(MatchStatementSyntax statement, int currentBlock)
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

                AddInstruction(nextCaseBlock, ExecutableInstruction.LoadLocal(subjectSlot, matchCase.Span));
                AddInstruction(nextCaseBlock, ExecutableInstruction.MatchCase(InternMatchCase(LoweredPatternPreparation.Prepare(matchCase.Pattern), matchCase.Span), failBlock, matchCase.Span));
                var successBlock = nextCaseBlock;
                if (matchCase.Guard is { } guard)
                {
                    successBlock = CompileExpression(guard, successBlock);
                    AddInstruction(successBlock, ExecutableInstruction.JumpIfFalse(failBlock, guard.Span));
                }
                AddInstruction(successBlock, ExecutableInstruction.Jump(bodyBlock, matchCase.Span));

                var bodyExit = CompileStatements(matchCase.Body, bodyBlock);
                if (bodyExit is int bodyBlockExit && !IsTerminated(bodyBlockExit))
                {
                    AddInstruction(bodyBlockExit, ExecutableInstruction.Jump(afterBlock, matchCase.Span));
                }

                nextCaseBlock = failBlock;
            }

            return afterBlock;
        }

        private int CompileIfStatement(IfStatementSyntax statement, int currentBlock)
        {
            currentBlock = CompileExpression(statement.Condition, currentBlock);

            var elseBlock = statement.ElseStatements is null ? -1 : CreateBlock();
            var joinBlock = CreateBlock();

            AddInstruction(currentBlock, ExecutableInstruction.JumpIfFalse(statement.ElseStatements is null ? joinBlock : elseBlock, statement.Condition.Span));

            var thenExit = CompileStatements(statement.ThenStatements, currentBlock);
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

        private int CompileWhileStatement(WhileStatementSyntax statement, int currentBlock)
        {
            var conditionBlock = CreateBlock();
            var elseBlock = statement.ElseStatements is null ? -1 : CreateBlock();
            var exitBlock = CreateBlock();

            AddInstruction(currentBlock, ExecutableInstruction.Jump(conditionBlock, statement.Span));

            var conditionEntry = conditionBlock;
            conditionBlock = CompileExpression(statement.Condition, conditionBlock);
            AddInstruction(conditionBlock, ExecutableInstruction.JumpIfFalse(statement.ElseStatements is null ? exitBlock : elseBlock, statement.Condition.Span));

            _buffers.Loops.Push(new LoopContext(conditionEntry, exitBlock, HasIterator: false, _protectedDepth));
            try
            {
                var bodyExit = CompileStatements(statement.Body, conditionBlock);
                if (bodyExit is int bodyBlockExit && !IsTerminated(bodyBlockExit))
                {
                    AddInstruction(bodyBlockExit, ExecutableInstruction.Jump(conditionEntry, statement.Span));
                }
            }
            finally
            {
                _buffers.Loops.Pop();
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

        private int? CompileBreakStatement(BreakStatementSyntax statement, int currentBlock)
        {
            if (!_buffers.Loops.TryPeek(out var loop))
            {
                throw new InvalidOperationException("Executable IR lowering cannot emit break outside a loop.");
            }

            // A loop wholly inside the current protected suite leaves no
            // handler or cleanup behind. Only crossing its boundary needs the
            // runtime's pending transfer and unwind machinery.
            if (_protectedDepth != loop.ProtectedDepth)
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

        private int? CompileContinueStatement(ContinueStatementSyntax statement, int currentBlock)
        {
            if (!_buffers.Loops.TryPeek(out var loop))
            {
                throw new InvalidOperationException("Executable IR lowering cannot emit continue outside a loop.");
            }

            AddInstruction(currentBlock, _protectedDepth != loop.ProtectedDepth
                ? ExecutableInstruction.AbruptJump(loop.ContinueBlockIndex, false, statement.Span)
                : ExecutableInstruction.Jump(loop.ContinueBlockIndex, statement.Span));
            return null;
        }

    }
}
