using System.Collections.Frozen;
using System.Globalization;
using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Frontend;

internal sealed partial class ExecutableScript
{
    // A successor of -1 denotes a block that is not a single ordinary jump.
    // Completed targets are nonnegative; negative markers index the current path.
    internal static int[] ResolveJumpTargets(ReadOnlySpan<int> successors)
    {
        var targets = new int[successors.Length];
        Array.Fill(targets, -1);
        var path = new int[successors.Length];
        for (var start = 0; start < successors.Length; start++)
        {
            if (targets[start] >= 0)
                continue;

            var length = 0;
            var current = start;
            while (targets[current] == -1)
            {
                targets[current] = -2 - length;
                path[length++] = current;
                var next = successors[current];
                if (next == -1)
                {
                    targets[current] = current;
                    break;
                }
                current = next;
            }

            if (targets[current] < 0)
            {
                // The old walk returned its first repeated node. Preserve every
                // cycle node's own identity, and each incoming tail's entry node.
                var cycleStart = -2 - targets[current];
                for (var i = cycleStart; i < length; i++)
                    targets[path[i]] = path[i];
            }
            for (var i = length - 1; i >= 0; i--)
            {
                var block = path[i];
                if (targets[block] < 0)
                    targets[block] = targets[successors[block]];
            }
        }
        return targets;
    }

    private sealed partial class Builder
    {
        private int CreateBlock()
        {
            var index = _blocks.Count;
            _blocks.Add(new BasicBlockBuilder(index));
            return index;
        }

        private void AddInstruction(int blockIndex, ExecutableInstruction instruction)
        {
            var instructions = _blocks[blockIndex].Instructions;
            if (instruction.OpCode == ExecutableOpCode.JumpIfFalse && instructions.Count != 0
                && instructions[^1] is { OpCode: ExecutableOpCode.Binary } previous
                && LythonRuntime.IsScalarComparison(previous.BinaryOperator) && previous.Span == instruction.Span)
            {
                // The value is consumed only as truth, in the same protected
                // block/span. No escaping comparison result needs a guest box.
                instructions[^1] = ExecutableInstruction.CompareJumpIfFalse(previous.BinaryOperator, instruction.TargetIndex, instruction.Span);
                return;
            }
            instructions.Add(instruction);
        }

        private bool IsTerminated(int blockIndex)
        {
            var instructions = _blocks[blockIndex].Instructions;
            if (instructions.Count == 0)
            {
                return false;
            }

            return instructions[^1].OpCode is ExecutableOpCode.Jump or ExecutableOpCode.AbruptJump or ExecutableOpCode.Return or ExecutableOpCode.ReturnNone or ExecutableOpCode.ReraiseException;
        }

        private List<ExecutableBasicBlock> NormalizeBlocks(IReadOnlyList<int> ordered, int[] indexMap, int[] jumpTargets)
        {
            var normalized = new List<ExecutableBasicBlock>(ordered.Count);
            foreach (var originalIndex in ordered)
            {
                var instructions = _blocks[originalIndex].Instructions;
                var rewritten = new ExecutableInstruction[instructions.Count];
                for (var i = 0; i < rewritten.Length; i++)
                    rewritten[i] = RewriteInstructionTargets(instructions[i], indexMap, jumpTargets);
                normalized.Add(new ExecutableBasicBlock(NormalizedBlock(indexMap, originalIndex), rewritten));
            }

            return normalized;
        }

        private IReadOnlyList<ExecutableExceptionRegion> NormalizeRegions(
            IReadOnlyList<int> orderedBlocks,
            int[] indexMap, int[] jumpTargets)
        {
            var normalized = new List<ExecutableExceptionRegion>(_regions.Count);
            foreach (var region in _regions)
            {
                var firstProtected = LowerBound(orderedBlocks, region.ProtectedStartIndex);
                var afterLastProtected = UpperBound(orderedBlocks, region.ProtectedEndIndex);
                if (firstProtected == afterLastProtected)
                {
                    continue;
                }

                // orderedBlocks is sorted and indexMap assigns its position as the
                // normalized index, so a reachable original range remains contiguous.
                int? suiteStart = null;
                int? suiteEnd = null;
                if (region.SuiteStartIndex is int suiteStartOriginal &&
                    region.SuiteEndIndex is int suiteEndOriginal)
                {
                    var firstSuite = LowerBound(orderedBlocks, suiteStartOriginal);
                    var afterLastSuite = UpperBound(orderedBlocks, suiteEndOriginal);
                    if (firstSuite != afterLastSuite)
                    {
                        suiteStart = firstSuite;
                        suiteEnd = afterLastSuite - 1;
                    }
                }

                normalized.Add(new ExecutableExceptionRegion(
                    firstProtected,
                    afterLastProtected - 1,
                    region.ExceptIndex is int exceptBlock ? NormalizedBlock(indexMap, jumpTargets[exceptBlock]) : null,
                    region.FinallyIndex is int finallyBlock ? NormalizedBlock(indexMap, jumpTargets[finallyBlock]) : null,
                    suiteStart,
                    suiteEnd, region.CleanupId));
            }

            // Runtime unwinding consumes applicable regions from the narrowest
            // protected range outward, without rescanning the region table.
            return normalized
                .OrderBy(region => region.ProtectedEndIndex - region.ProtectedStartIndex)
                .ToArray();

            static int LowerBound(IReadOnlyList<int> values, int target)
            {
                var start = 0;
                var end = values.Count;
                while (start < end)
                {
                    var middle = start + ((end - start) / 2);
                    if (values[middle] < target)
                    {
                        start = middle + 1;
                    }
                    else
                    {
                        end = middle;
                    }
                }

                return start;
            }

            static int UpperBound(IReadOnlyList<int> values, int target)
            {
                var start = 0;
                var end = values.Count;
                while (start < end)
                {
                    var middle = start + ((end - start) / 2);
                    if (values[middle] <= target)
                    {
                        start = middle + 1;
                    }
                    else
                    {
                        end = middle;
                    }
                }

                return start;
            }
        }

        private int[] CollectReachableBlocks(int entryBlockIndex, int[] jumpTargets)
        {
            var reachable = new bool[_blocks.Count];
            var count = 0;
            var pending = new Stack<int>();
            pending.Push(entryBlockIndex);

            while (pending.Count > 0)
            {
                var blockIndex = pending.Pop();
                if (reachable[blockIndex])
                {
                    continue;
                }
                reachable[blockIndex] = true;
                count++;

                var block = _blocks[blockIndex];
                if (block.Instructions.Count == 0)
                {
                    if (blockIndex + 1 < _blocks.Count)
                    {
                        pending.Push(blockIndex + 1);
                    }
                    continue;
                }

                foreach (var instruction in block.Instructions)
                {
                    switch (instruction.OpCode)
                    {
                        // Unwinding depends on the original target's region.
                        // Threading through its jump could leave that region
                        // and run cleanup before a loop iterator is discarded.
                        case ExecutableOpCode.AbruptJump:
                            pending.Push(instruction.TargetIndex);
                            break;
                        case ExecutableOpCode.Jump:
                        case ExecutableOpCode.JumpIfFalse:
                        case ExecutableOpCode.CompareJumpIfFalse:
                        case ExecutableOpCode.ForNext:
                        case ExecutableOpCode.EndFinally:
                            pending.Push(jumpTargets[instruction.TargetIndex]);
                            break;
                        case ExecutableOpCode.MatchCase:
                            pending.Push(jumpTargets[instruction.FailureIndex]);
                            break;
                    }
                }

                foreach (var region in _regions)
                {
                    if (blockIndex < region.ProtectedStartIndex || blockIndex > region.ProtectedEndIndex)
                    {
                        continue;
                    }

                    if (region.ExceptIndex is int exceptBlock)
                    {
                        pending.Push(exceptBlock);
                    }

                    if (region.FinallyIndex is int finallyBlock)
                    {
                        pending.Push(finallyBlock);
                    }
                }

                var last = block.Instructions[^1];
                if (last.OpCode is not ExecutableOpCode.Jump and
                    not ExecutableOpCode.Return and
                    not ExecutableOpCode.ReturnNone and
                    not ExecutableOpCode.ReraiseException &&
                    blockIndex + 1 < _blocks.Count)
                {
                    pending.Push(blockIndex + 1);
                }
            }

            var ordered = new int[count];
            var next = 0;
            for (var i = 0; i < reachable.Length; i++)
                if (reachable[i])
                    ordered[next++] = i;
            return ordered;
        }

        private static ExecutableInstruction RewriteInstructionTargets(ExecutableInstruction instruction, int[] indexMap, int[] jumpTargets)
        {
            return instruction.OpCode switch
            {
                ExecutableOpCode.AbruptJump => instruction.WithTargetIndex(NormalizedBlock(indexMap, instruction.TargetIndex)),
                ExecutableOpCode.Jump or
                ExecutableOpCode.JumpIfFalse or
                ExecutableOpCode.CompareJumpIfFalse or
                ExecutableOpCode.ForNext or
                ExecutableOpCode.EndFinally => instruction.WithTargetIndex(NormalizedBlock(indexMap, jumpTargets[instruction.TargetIndex])),
                ExecutableOpCode.MatchCase => instruction.WithFailureIndex(NormalizedBlock(indexMap, jumpTargets[instruction.FailureIndex])),
                _ => instruction
            };
        }

        private static int NormalizedBlock(int[] indexMap, int originalIndex)
            => indexMap[originalIndex] is var normalized && normalized >= 0
                ? normalized
                : throw new InvalidOperationException("Executable target is unreachable.");

        private int[] ResolveJumpTargets()
        {
            var successors = new int[_blocks.Count];
            for (var i = 0; i < successors.Length; i++)
            {
                var instructions = _blocks[i].Instructions;
                successors[i] = instructions.Count == 1 && instructions[0].OpCode == ExecutableOpCode.Jump
                    ? instructions[0].TargetIndex : -1;
            }
            return ExecutableScript.ResolveJumpTargets(successors);
        }

        private int InternLocal(string name)
        {
            if (_localIndexes.TryGetValue(name, out var index))
            {
                return index;
            }

            index = _locals.Count;
            _locals.Add(name);
            _localIndexes.Add(name, index);
            return index;
        }

        private bool IsLocalBindingName(string name)
            => !_scopeFacts.IsGlobal(name) && !_scopeFacts.IsNonlocal(name);

        private void CompileLoadIdentifier(string name, LythonSourceSpan span, int currentBlock)
        {
            if (_comprehensionNames.Any(names => names.Contains(name)))
            {
                AddInstruction(currentBlock, ExecutableInstruction.LoadName(InternName(name), span));
            }
            else if (_scopeFacts.IsGlobal(name))
            {
                AddInstruction(currentBlock, ExecutableInstruction.LoadGlobal(InternName(name), span));
            }
            else if (_localIndexes.TryGetValue(name, out var slot))
            {
                AddInstruction(currentBlock, ExecutableInstruction.LoadLocal(slot, span));
            }
            else if (_parentClosureCandidates.Contains(name))
            {
                AddInstruction(currentBlock, ExecutableInstruction.LoadClosure(InternClosure(name), span));
            }
            else if (_parameterLayout is not null && _completeLexicalParent && !_scopeFacts.IsNonlocal(name))
            {
                // Locals and all lexical captures were considered above. The
                // remaining name belongs to the live module/builtin namespace;
                // no invocation-time search through function frames is needed.
                AddInstruction(currentBlock, ExecutableInstruction.LoadGlobal(InternName(name), span));
            }
            else
            {
                AddInstruction(currentBlock, ExecutableInstruction.LoadName(InternName(name), span));
            }
        }

        private void CompileStoreBoundName(string name, LythonSourceSpan span, int currentBlock)
        {
            if (_scopeFacts.IsGlobal(name))
            {
                AddInstruction(currentBlock, ExecutableInstruction.StoreGlobal(InternName(name), span));
                return;
            }

            if (_scopeFacts.IsNonlocal(name))
            {
                // Standalone class-method preparation has a retained lexical
                // namespace, not an enclosing builder's known cell slots.
                AddInstruction(currentBlock, _parentClosureCandidates.Contains(name)
                    ? ExecutableInstruction.StoreClosure(InternClosure(name), span)
                    : ExecutableInstruction.StoreName(InternName(name), span));
                return;
            }

            AddInstruction(currentBlock, ExecutableInstruction.StoreLocal(InternLocal(name), span));
        }

        private int InternSyntheticLocal(string prefix) => InternLocal($"<{prefix}:{_syntheticLocalCounter++}>");

        private int InternClosure(string name)
        {
            if (_closureIndexes.TryGetValue(name, out var index))
            {
                return index;
            }

            index = _closures.Count;
            _closures.Add(name);
            _closureIndexes.Add(name, index);
            return index;
        }

        private int InternName(string name)
        {
            if (_nameIndexes.TryGetValue(name, out var index))
            {
                return index;
            }

            index = _names.Count;
            _names.Add(name);
            _nameIndexes.Add(name, index);
            return index;
        }

        private int InternConstant(object? value)
        {
            if (value is null)
            {
                if (_nullConstantIndex is { } existingNull)
                {
                    return existingNull;
                }

                var nullIndex = _constants.Count;
                _constants.Add(null);
                _nullConstantIndex = nullIndex;
                return nullIndex;
            }

            if (_constantIndexes.TryGetValue(value, out var existing))
            {
                return existing;
            }

            var index = _constants.Count;
            _constants.Add(value);
            _constantIndexes.Add(value, index);
            return index;
        }

        private void InternModuleBinding(string name)
        {
            // Function layouts already contain their complete lexical bindings.
            // Modules register declarations only while compiling actual stores;
            // unreachable declarations need no slots. Existing namespace bridges
            // keep these slots live through StoreName and dictionary mutations.
            if (_parameterLayout is null && IsLocalBindingName(name)) InternLocal(name);
        }

        private int InternImport(LoweredImportStatement importStatement)
        {
            if (_parameterLayout is null)
                foreach (var name in ImportSyntaxFacts.EnumerateBindingNames(importStatement.Syntax))
                    InternModuleBinding(name);
            _imports.Add(importStatement.Syntax);
            return _imports.Count - 1;
        }

        private int AllocateLookupCache() => _lookupCacheCount++;

        private int InternCallSite(LoweredCallExpression call, bool hasReceiver)
        {
            _callSites.Add(new ExecutableCallSite(
                call.Arguments.Count,
                call.Arguments.Select(argument => new ExecutableCallArgument(argument.Form)).ToArray(),
                call.Target.Span,
                call.Span,
                hasReceiver));
            return _callSites.Count - 1;
        }

        private int InternFunction(LoweredFunctionDefinitionStatement functionDefinition)
        {
            InternModuleBinding(functionDefinition.Syntax.Name);
            var codeObject = CompileFunctionCode(functionDefinition.Syntax.DeclaredName,
                functionDefinition.ParameterLayout, functionDefinition.Body,
                functionDefinition.ScopeFacts, functionDefinition.TypeParameters, functionDefinition.Syntax.IsGenerator);

            _functions.Add(new ExecutableFunctionBinding(
                functionDefinition.Syntax.Name, functionDefinition.Syntax.DeclaredName, functionDefinition.Span,
                functionDefinition.Decorators, functionDefinition.Parameters, functionDefinition.ReturnAnnotation,
                functionDefinition.TypeParameters, codeObject, functionDefinition.Syntax.IsGenerator,
                functionDefinition.Docstring) { ParameterLayout = functionDefinition.ParameterLayout });
            return _functions.Count - 1;
        }

        private ExecutableCodeObject CompileFunctionCode(string name,
            FunctionParameterLayout parameterLayout, IReadOnlyList<LoweredStatement> body,
            ScopeDirectiveFacts scopeFacts, IReadOnlyList<LoweredTypeParameter>? typeParameters = null,
            bool generator = false)
        {
            // Forward transitive free names as before. Comprehension names
            // are inner bindings, even at module scope or when the caller
            // declares a same-named global; add them after that exclusion.
            var candidates = ChildClosureCandidates();
            // Type parameters shadow enclosing locals in the body. Defaults
            // still evaluate in the defining context before TypeScope exists.
            if (candidates is not null && typeParameters is not null)
                candidates = candidates.Except(typeParameters.Select(static parameter => parameter.Syntax.Name), StringComparer.Ordinal);
            return new Builder(parameterLayout, candidates, scopeFacts,
                completeLexicalParent: _completeLexicalParent && typeParameters is not { Count: > 0 })
                { _generator = generator }
                .CompileCodeObject(name, body);
        }

        private IEnumerable<string>? ChildClosureCandidates()
        {
            IEnumerable<string>? candidates = _parameterLayout is null ? null
                : _locals.Concat(_closures).Concat(_parentClosureCandidates)
                    .Except(_scopeFacts.GlobalNames, StringComparer.Ordinal);
            return _comprehensionNames.Count == 0 ? candidates
                : (candidates ?? []).Concat(_comprehensionNames.SelectMany(static names => names));
        }

        private int InternMatchCase(LoweredMatchCase matchCase)
        {
            var localBindings = matchCase.Pattern.Bindings.Select(binding =>
                _localIndexes.TryGetValue(binding.Name, out var slot) ? slot : -1).ToArray();
            _matchCases.Add(new ExecutableMatchCaseBinding(matchCase.Pattern, matchCase.Syntax.Span, localBindings));
            return _matchCases.Count - 1;
        }

        private int InternStatementFallback(LoweredStatement statement)
        {
            if (statement is LoweredClassDefinitionStatement definition) InternModuleBinding(definition.Syntax.Name);
            else if (statement is LoweredTypeAliasStatement alias) InternModuleBinding(alias.Syntax.Name);
            StatementSyntax? syntax = statement switch
            {
                LoweredClassDefinitionStatement item => item.Syntax,
                LoweredAssertStatement item => item.Syntax,
                LoweredDeleteStatement item => item.Syntax,
                LoweredRaiseStatement item => item.Syntax,
                LoweredAssignmentStatement item => item.Syntax,
                _ => null,
            };
            if (_generator && syntax is not null && StatementSyntaxTraversal.EnumerateDirectExpressions(syntax).Any(GeneratorSyntaxFacts.ContainsYield))
                throw new GeneratorLoweringException("Suspension in this statement is not supported.", statement.Span);
            _statementFallbacks.Add(statement);
            return _statementFallbacks.Count - 1;
        }

        private sealed class ConstantValueComparer : IEqualityComparer<object>
        {
            public static readonly ConstantValueComparer Instance = new();

            public new bool Equals(object? left, object? right)
            {
                if (left is byte[] leftBytes && right is byte[] rightBytes)
                {
                    return leftBytes.AsSpan().SequenceEqual(rightBytes);
                }

                return object.Equals(left, right);
            }

            public int GetHashCode(object value)
            {
                if (value is not byte[] bytes)
                {
                    return value.GetHashCode();
                }

                var hash = new HashCode();
                foreach (var item in bytes)
                {
                    hash.Add(item);
                }

                return hash.ToHashCode();
            }
        }

        private sealed class BasicBlockBuilder
        {
            public BasicBlockBuilder(int index)
            {
                Index = index;
            }

            public int Index { get; }

            public List<ExecutableInstruction> Instructions { get; } = [];
        }

        // A for-loop owns one iterator slot on the value stack above the loop-entry
        // depth; while-loops own none. A break out of a for-loop must pop that
        // slot otherwise the abandoned iterator roots charges forever.
        private readonly record struct LoopContext(
            int ContinueBlockIndex,
            int BreakBlockIndex,
            bool HasIterator,
            int ProtectedDepth);
    }
}
