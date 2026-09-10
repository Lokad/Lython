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
        private int CreateBlock()
        {
            var index = _blocks.Count;
            _blocks.Add(new BasicBlockBuilder(index));
            return index;
        }

        private void AddInstruction(int blockIndex, ExecutableInstruction instruction)
        {
            _blocks[blockIndex].Instructions.Add(instruction);
        }

        private bool IsTerminated(int blockIndex)
        {
            var instructions = _blocks[blockIndex].Instructions;
            if (instructions.Count == 0)
            {
                return false;
            }

            return instructions[^1].OpCode is ExecutableOpCode.Jump or ExecutableOpCode.Return or ExecutableOpCode.ReturnNone;
        }

        private IReadOnlyList<ExecutableBasicBlock> NormalizeBlocks(IReadOnlyList<int> ordered, IReadOnlyDictionary<int, int> indexMap)
        {
            var normalized = new List<ExecutableBasicBlock>(ordered.Count);
            foreach (var originalIndex in ordered)
            {
                var rewritten = _blocks[originalIndex].Instructions
                    .Select(instruction => RewriteInstructionTargets(instruction, indexMap))
                    .ToArray();
                normalized.Add(new ExecutableBasicBlock(indexMap[originalIndex], rewritten));
            }

            return normalized;
        }

        private IReadOnlyList<ExecutableExceptionRegion> NormalizeRegions(
            IReadOnlyList<int> orderedBlocks,
            IReadOnlyDictionary<int, int> indexMap)
        {
            var normalized = new List<ExecutableExceptionRegion>(_regions.Count);
            foreach (var region in _regions)
            {
                var firstProtected = LowerBound(orderedBlocks, region.ProtectedStartBlockIndex);
                var afterLastProtected = UpperBound(orderedBlocks, region.ProtectedEndBlockIndex);
                if (firstProtected == afterLastProtected)
                {
                    continue;
                }

                // orderedBlocks is sorted and indexMap assigns its position as the
                // normalized index, so a reachable original range remains contiguous.
                int? suiteStart = null;
                int? suiteEnd = null;
                if (region.SuiteStartBlockIndex is int suiteStartOriginal &&
                    region.SuiteEndBlockIndex is int suiteEndOriginal)
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
                    region.ExceptionTypeNames,
                    region.ExceptionVariableName,
                    region.ExceptBlockIndex is int exceptBlock ? indexMap[FinalJumpTarget(exceptBlock)] : null,
                    region.FinallyBlockIndex is int finallyBlock ? indexMap[FinalJumpTarget(finallyBlock)] : null,
                    suiteStart,
                    suiteEnd));
            }

            // Runtime unwinding consumes applicable regions from the narrowest
            // protected range outward, without rescanning the region table.
            return normalized
                .OrderBy(region => region.ProtectedEndBlockIndex - region.ProtectedStartBlockIndex)
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

        private HashSet<int> CollectReachableBlocks(int entryBlockIndex)
        {
            var reachable = new HashSet<int>();
            var pending = new Stack<int>();
            pending.Push(entryBlockIndex);

            while (pending.Count > 0)
            {
                var blockIndex = pending.Pop();
                if (!reachable.Add(blockIndex))
                {
                    continue;
                }

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
                        case ExecutableOpCode.Jump:
                        case ExecutableOpCode.JumpIfFalse:
                        case ExecutableOpCode.ForNext:
                        case ExecutableOpCode.EndFinally:
                            pending.Push(FinalJumpTarget(instruction.TargetBlockIndex));
                            break;
                        case ExecutableOpCode.MatchCase:
                            pending.Push(FinalJumpTarget(instruction.FailureBlockIndex));
                            break;
                    }
                }

                foreach (var region in _regions)
                {
                    if (blockIndex < region.ProtectedStartBlockIndex || blockIndex > region.ProtectedEndBlockIndex)
                    {
                        continue;
                    }

                    if (region.ExceptBlockIndex is int exceptBlock)
                    {
                        pending.Push(exceptBlock);
                    }

                    if (region.FinallyBlockIndex is int finallyBlock)
                    {
                        pending.Push(finallyBlock);
                    }
                }

                var last = block.Instructions[^1];
                if (last.OpCode is not ExecutableOpCode.Jump and
                    not ExecutableOpCode.Return and
                    not ExecutableOpCode.ReturnNone &&
                    blockIndex + 1 < _blocks.Count)
                {
                    pending.Push(blockIndex + 1);
                }
            }

            return reachable;
        }

        private ExecutableInstruction RewriteInstructionTargets(ExecutableInstruction instruction, IReadOnlyDictionary<int, int> indexMap)
        {
            return instruction.OpCode switch
            {
                ExecutableOpCode.Jump or
                ExecutableOpCode.JumpIfFalse or
                ExecutableOpCode.ForNext or
                ExecutableOpCode.EndFinally => instruction.WithTargetBlockIndex(indexMap[FinalJumpTarget(instruction.TargetBlockIndex)]),
                ExecutableOpCode.MatchCase => instruction.WithFailureBlockIndex(indexMap[FinalJumpTarget(instruction.FailureBlockIndex)]),
                _ => instruction
            };
        }

        private int FinalJumpTarget(int blockIndex)
        {
            var seen = new HashSet<int>();
            var current = blockIndex;
            while (seen.Add(current))
            {
                var instructions = _blocks[current].Instructions;
                if (instructions.Count != 1 || instructions[0].OpCode != ExecutableOpCode.Jump)
                {
                    break;
                }

                current = instructions[0].TargetBlockIndex;
            }

            return current;
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
            if (_scopeFacts.IsGlobal(name))
            {
                AddInstruction(currentBlock, ExecutableInstruction.LoadGlobal(InternName(name), span));
            }
            else if (_localIndexes.TryGetValue(name, out var slot))
            {
                AddInstruction(currentBlock, ExecutableInstruction.LoadLocal(slot, span));
            }
            else if (_scopeFacts.IsNonlocal(name) || _parentClosureCandidates.Contains(name))
            {
                AddInstruction(currentBlock, ExecutableInstruction.LoadClosure(InternClosure(name), span));
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
                AddInstruction(currentBlock, ExecutableInstruction.StoreClosure(InternClosure(name), span));
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

        private int InternImport(LoweredImportStatement importStatement)
        {
            _imports.Add(new ExecutableImportBinding(
                importStatement.Syntax.ModuleName,
                importStatement.Syntax.BindingName,
                importStatement.Syntax.BoundModuleName,
                importStatement.Syntax.ImportedMembers,
                importStatement.Span));
            return _imports.Count - 1;
        }

        private int AllocateMemberCache() => _memberCacheCount++;

        private int AllocateCallCache() => _callCacheCount++;

        private int InternCallSite(LoweredCallExpression call)
        {
            _callSites.Add(new ExecutableCallSite(
                call.Arguments.Count,
                call.Arguments.Select(argument => new ExecutableCallArgumentSpec(argument.Form)).ToArray(),
                call.Target.Span,
                call.Span));
            return _callSites.Count - 1;
        }

        private int InternLoopTarget(LoopTargetSyntax target, LythonSourceSpan span)
        {
            _loopTargets.Add(new ExecutableLoopTargetBinding(target, span));
            return _loopTargets.Count - 1;
        }

        private int InternUnpackingTargets(IReadOnlyList<UnpackingTargetSyntax> targets, LythonSourceSpan span)
        {
            _unpackingTargets.Add(new ExecutableUnpackingTargetBinding(targets, span));
            return _unpackingTargets.Count - 1;
        }

        private int InternFunction(LoweredFunctionDefinitionStatement functionDefinition)
        {
            ExecutableCodeObject? codeObject;
            try
            {
                codeObject = new Builder(
                    functionDefinition.Parameters,
                    _functionParameters is null ? null : _locals.Concat(_closures))
                    .CompileCodeObject(functionDefinition.Syntax.Name, functionDefinition.Body);
            }
            catch (ExecutableLoweringFallbackException)
            {
                codeObject = null;
            }

            var defaultValues = functionDefinition.Parameters
                .Where(parameter => parameter.DefaultValue is not null)
                .ToDictionary(
                    parameter => parameter.Name,
                    parameter => parameter.DefaultValue.RequireNotNull(),
                    StringComparer.Ordinal);

            _functions.Add(new ExecutableFunctionBinding(functionDefinition, codeObject, defaultValues));
            return _functions.Count - 1;
        }

        private int InternMatchCase(MatchCaseSyntax matchCase)
        {
            var localBindings = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var name in EnumeratePatternBindingNames(matchCase.Pattern))
            {
                if (_localIndexes.TryGetValue(name, out var slot))
                {
                    localBindings[name] = slot;
                }
            }

            _matchCases.Add(new ExecutableMatchCaseBinding(matchCase, localBindings));
            return _matchCases.Count - 1;
        }

        private int InternStatementFallback(LoweredStatement statement)
        {
            _statementFallbacks.Add(new ExecutableStatementFallback(statement));
            return _statementFallbacks.Count - 1;
        }

        private int InternExpressionFallback(LoweredExpression expression)
        {
            _expressionFallbacks.Add(new ExecutableExpressionFallback(expression));
            return _expressionFallbacks.Count - 1;
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

        private static IEnumerable<string> EnumeratePatternBindingNames(PatternSyntax pattern)
        {
            switch (pattern)
            {
                case MatchCapturePatternSyntax capture:
                    yield return capture.Name;
                    yield break;
                case MatchSequencePatternSyntax sequence:
                    foreach (var item in sequence.Items.SelectMany(EnumeratePatternBindingNames))
                    {
                        yield return item;
                    }
                    yield break;
                case MatchMappingPatternSyntax mapping:
                    foreach (var item in mapping.Items.SelectMany(pair => EnumeratePatternBindingNames(pair.Pattern)))
                    {
                        yield return item;
                    }
                    if (mapping.RestName is not null)
                    {
                        yield return mapping.RestName;
                    }
                    yield break;
                case MatchClassPatternSyntax classPattern:
                    foreach (var item in classPattern.PositionalPatterns.SelectMany(EnumeratePatternBindingNames))
                    {
                        yield return item;
                    }
                    foreach (var item in classPattern.KeywordPatterns.SelectMany(pair => EnumeratePatternBindingNames(pair.Pattern)))
                    {
                        yield return item;
                    }
                    yield break;
                case MatchStarPatternSyntax star when star.Name is not null:
                    yield return star.Name;
                    yield break;
                case MatchAsPatternSyntax asPattern:
                    foreach (var item in EnumeratePatternBindingNames(asPattern.Pattern))
                    {
                        yield return item;
                    }
                    yield return asPattern.Name;
                    yield break;
                case MatchOrPatternSyntax orPattern:
                    foreach (var item in orPattern.Patterns.SelectMany(EnumeratePatternBindingNames))
                    {
                        yield return item;
                    }
                    yield break;
                default:
                    yield break;
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

        private readonly record struct LoopContext(
            int ContinueBlockIndex,
            int BreakBlockIndex);
    }
}
