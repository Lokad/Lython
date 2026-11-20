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
            _blocks.Add([]);
            return index;
        }

        private void AddInstruction(int blockIndex, ExecutableInstruction instruction)
        {
            var instructions = _blocks[blockIndex];
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
            var instructions = _blocks[blockIndex];
            if (instructions.Count == 0)
            {
                return false;
            }

            return instructions[^1].OpCode is ExecutableOpCode.Jump or ExecutableOpCode.AbruptJump or ExecutableOpCode.Return or ExecutableOpCode.ReturnNone or ExecutableOpCode.ReraiseException;
        }

        private IReadOnlyList<ExecutableExceptionRegion> NormalizeRegions(ExecutableBlockTargets targets)
        {
            var normalized = new List<ExecutableExceptionRegion>(_regions.Count);
            foreach (var region in _regions)
            {
                var firstProtected = FirstAtOrAfter(targets.Compacted[region.ProtectedStartIndex]);
                var afterLastProtected = FirstAfter(targets.Compacted[region.ProtectedEndIndex]);
                if (firstProtected == afterLastProtected)
                {
                    continue;
                }

                int? suiteStart = null;
                int? suiteEnd = null;
                if (region.SuiteStartIndex is int suiteStartOriginal &&
                    region.SuiteEndIndex is int suiteEndOriginal)
                {
                    var firstSuite = FirstAtOrAfter(targets.Compacted[suiteStartOriginal]);
                    var afterLastSuite = FirstAfter(targets.Compacted[suiteEndOriginal]);
                    if (firstSuite != afterLastSuite)
                    {
                        suiteStart = firstSuite;
                        suiteEnd = afterLastSuite - 1;
                    }
                }

                normalized.Add(new ExecutableExceptionRegion(
                    firstProtected,
                    afterLastProtected - 1,
                    region.ExceptIndex is int exceptBlock ? targets.Follow(exceptBlock) : null,
                    region.FinallyIndex is int finallyBlock ? targets.Follow(finallyBlock) : null,
                    suiteStart,
                    suiteEnd, region.CleanupId));
            }

            // Runtime unwinding consumes applicable regions from the narrowest
            // protected range outward, without rescanning the region table.
            return normalized
                .OrderBy(region => region.ProtectedEndIndex - region.ProtectedStartIndex)
                .ToArray();

            static int FirstAtOrAfter(int index) => index >= 0 ? index : ~index;
            static int FirstAfter(int index) => index >= 0 ? index + 1 : ~index;
        }

        private int[] RetainReachableBlocks(int entryBlockIndex, int[] jumpTargets)
        {
            var indexMap = new int[_blocks.Count];
            var pending = new Stack<int>();
            pending.Push(entryBlockIndex);

            while (pending.Count > 0)
            {
                var blockIndex = pending.Pop();
                if (indexMap[blockIndex] != 0)
                {
                    continue;
                }
                indexMap[blockIndex] = 1;

                var block = _blocks[blockIndex];
                if (block.Count == 0)
                {
                    if (blockIndex + 1 < _blocks.Count)
                    {
                        pending.Push(blockIndex + 1);
                    }
                    continue;
                }

                foreach (var instruction in block)
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

                var last = block[^1];
                if (last.OpCode is not ExecutableOpCode.Jump and
                    not ExecutableOpCode.Return and
                    not ExecutableOpCode.ReturnNone and
                    not ExecutableOpCode.ReraiseException &&
                    blockIndex + 1 < _blocks.Count)
                {
                    pending.Push(blockIndex + 1);
                }
            }

            // Compact in original order so every protected range stays contiguous.
            // Reuse the visit map for destination indexes. An unreachable block
            // stores the complement of its insertion position so a range endpoint
            // still maps directly without making it a valid branch target.
            var count = 0;
            for (var original = 0; original < indexMap.Length; original++)
            {
                if (indexMap[original] == 0)
                {
                    indexMap[original] = ~count;
                    continue;
                }
                _blocks[count] = _blocks[original];
                indexMap[original] = count++;
            }

            _blocks.RemoveRange(count, _blocks.Count - count);
            return indexMap;
        }

        private int[] ResolveJumpTargets()
        {
            var successors = new int[_blocks.Count];
            for (var i = 0; i < successors.Length; i++)
            {
                var instructions = _blocks[i];
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
            else if (HasEnclosingBinding(name))
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
                AddInstruction(currentBlock, HasEnclosingBinding(name)
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
                call.Arguments.Select(argument => argument.Form).ToArray(),
                call.Target.Span,
                call.Span,
                hasReceiver));
            return _callSites.Count - 1;
        }

        private ExecutableFunctionBinding PrepareFunctionBinding(LoweredFunctionDefinitionStatement functionDefinition)
        {
            InternModuleBinding(functionDefinition.Syntax.Name);
            var typeParameterNames = functionDefinition.Syntax.PreparedTypeParameterNames;
            if (typeParameterNames is null && functionDefinition.TypeParameters is { Count: > 0 } typeParameters)
                typeParameterNames = typeParameters.Select(static p => p.Syntax.Name).ToHashSet(StringComparer.Ordinal);
            var codeObject = CompileFunctionCode(functionDefinition.Syntax.DeclaredName,
                functionDefinition.ParameterLayout, functionDefinition.Body,
                functionDefinition.ScopeFacts, typeParameterNames, functionDefinition.Syntax.IsGenerator,
                qualifiedName: functionDefinition.Syntax.PreparedQualifiedName);

            codeObject.DefinitionAnnotationKeys = PyDict.RecordKeys.PrepareDefinition(functionDefinition.Annotations.Length,
                functionDefinition.Annotations, static (inputs, index) => inputs[index].Key,
                _compilationCancellation, functionDefinition.Span);
            return new ExecutableFunctionBinding(
                functionDefinition.Syntax.Name, functionDefinition.Span,
                functionDefinition.Decorators.Count, functionDefinition.Annotations,
                functionDefinition.TypeParameters, codeObject, functionDefinition.Syntax.IsGenerator)
            {
                ParameterLayout = functionDefinition.ParameterLayout,
            };
        }

        private ExecutableCodeObject CompileFunctionCode(string name,
            FunctionParameterLayout parameterLayout, IReadOnlyList<LoweredStatement> body,
            ScopeDirectiveFacts scopeFacts, IReadOnlySet<string>? typeParameterNames = null,
            bool generator = false, PyString? qualifiedName = null)
        {
            return new Builder(parameterLayout, this, scopeFacts,
                completeLexicalParent: _completeLexicalParent && typeParameterNames is null,
                moduleCode: _moduleCode, typeParameterNames: typeParameterNames)
                { _generator = generator }
                .CompileCodeObject(name, body, qualifiedName: qualifiedName);
        }

        // A child is compiled while its parent's active comprehensions and
        // slot maps are intact. Resolve just the names the child references.
        private bool HasEnclosingBinding(string name)
        {
            for (var descendant = this; descendant._lexicalParent is { } parent; descendant = parent)
            {
                // Type parameters shadow outer bindings in the callable body.
                if (descendant._typeParameterNames?.Contains(name) == true) return false;

                // The iteration scope can bind the name even at module scope
                // or when the enclosing function declared it global.
                foreach (var names in parent._comprehensionNames)
                    if (names.Contains(name)) return true;

                if (parent._parameterLayout is null || parent._scopeFacts.IsGlobal(name)) return false;
                if (parent._localIndexes.ContainsKey(name) || parent._closureIndexes.ContainsKey(name)) return true;
            }
            return false;
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
