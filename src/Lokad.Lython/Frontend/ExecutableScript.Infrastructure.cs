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
        // These mappings are immutable after construction. A lone block can
        // target only itself and is always reachable from its entry.
        private static readonly int[] SingleEntryBlock = [0];

        private ExecutableBlockTargets PrepareBlockTargets(int entryBlock)
        {
            if (_buffers.Blocks.Count == 1) return new(SingleEntryBlock, SingleEntryBlock);

            var jumpTargets = ResolveJumpTargets();
            return new(RetainReachableBlocks(entryBlock, jumpTargets), jumpTargets);
        }

        private int CreateBlock()
        {
            var index = _buffers.Blocks.Count;
            _buffers.Blocks.Add(index == 0 ? _buffers.EntryBlock : []);
            return index;
        }

        private void AddInstruction(int blockIndex, ExecutableInstruction instruction)
        {
            var instructions = _buffers.Blocks[blockIndex];
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
            var instructions = _buffers.Blocks[blockIndex];
            if (instructions.Count == 0)
            {
                return false;
            }

            return instructions[^1].OpCode is ExecutableOpCode.Jump or ExecutableOpCode.AbruptJump or ExecutableOpCode.Return or ExecutableOpCode.ReturnNone or ExecutableOpCode.ReraiseException;
        }

        private IReadOnlyList<ExecutableExceptionRegion> NormalizeRegions(ExecutableBlockTargets targets)
        {
            if (_buffers.Regions.Count == 0) return [];
            var normalized = new List<ExecutableExceptionRegion>(_buffers.Regions.Count);
            foreach (var region in _buffers.Regions)
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
            var indexMap = new int[_buffers.Blocks.Count];
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

                var block = _buffers.Blocks[blockIndex];
                if (block.Count == 0)
                {
                    if (blockIndex + 1 < _buffers.Blocks.Count)
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

                foreach (var region in _buffers.Regions)
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
                    blockIndex + 1 < _buffers.Blocks.Count)
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
                _buffers.Blocks[count] = _buffers.Blocks[original];
                indexMap[original] = count++;
            }

            _buffers.Blocks.RemoveRange(count, _buffers.Blocks.Count - count);
            return indexMap;
        }

        private int[] ResolveJumpTargets()
        {
            var successors = new int[_buffers.Blocks.Count];
            for (var i = 0; i < successors.Length; i++)
            {
                var instructions = _buffers.Blocks[i];
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

            index = _buffers.Locals.Count;
            _buffers.Locals.Add(name);
            _localIndexes.Add(name, index);
            return index;
        }

        private bool IsLocalBindingName(string name)
            => !_scopeFacts.IsGlobal(name) && !_scopeFacts.IsNonlocal(name);

        private void CompileLoadIdentifier(string name, LythonSourceSpan span, int currentBlock)
        {
            if (_buffers.ComprehensionNames.Any(names => names.Contains(name)))
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

            AddInstruction(currentBlock, _classBody
                ? ExecutableInstruction.StoreName(InternName(name), span)
                : ExecutableInstruction.StoreLocal(InternLocal(name), span));
        }

        private int InternSyntheticLocal(string prefix) => InternLocal($"<{prefix}:{_syntheticLocalCounter++}>");

        private int InternClosure(string name)
        {
            if (_closureIndexes.TryGetValue(name, out var index))
            {
                return index;
            }

            index = _buffers.Closures.Count;
            _buffers.Closures.Add(name);
            _closureIndexes.Add(name, index);
            return index;
        }

        private int InternName(string name)
        {
            if (_buffers.NameIndexes.TryGetValue(name, out var index))
            {
                return index;
            }

            index = _buffers.Names.Count;
            _buffers.Names.Add(name);
            _buffers.NameIndexes.Add(name, index);
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

                var nullIndex = _buffers.Constants.Count;
                _buffers.Constants.Add(null);
                _nullConstantIndex = nullIndex;
                return nullIndex;
            }

            if (_buffers.ConstantIndexes.TryGetValue(value, out var existing))
            {
                return existing;
            }

            var index = _buffers.Constants.Count;
            _buffers.Constants.Add(value);
            _buffers.ConstantIndexes.Add(value, index);
            return index;
        }

        private void InternModuleBinding(string name)
        {
            // Function layouts already contain their complete lexical bindings.
            // Modules register declarations only while compiling actual stores;
            // unreachable declarations need no slots. Existing namespace bridges
            // keep these slots live through StoreName and dictionary mutations.
            if (_parameterLayout is null && !_classBody && IsLocalBindingName(name)) InternLocal(name);
        }

        private int InternImport(ImportStatementSyntax importStatement)
        {
            if (_parameterLayout is null)
                foreach (var name in ImportSyntaxFacts.EnumerateBindingNames(importStatement))
                    InternModuleBinding(name);
            _buffers.Imports.Add(importStatement);
            return _buffers.Imports.Count - 1;
        }

        private int AllocateLookupCache() => _lookupCacheCount++;

        private int InternCallSite(CallExpressionSyntax call, bool hasReceiver)
        {
            var arguments = call.Arguments.Select(argument => argument.Form).ToArray();
            var formatKeywords = call.Target is MemberExpressionSyntax { MemberName: "format" }
                ? PrepareFormatKeywordIndices(arguments, call.Span) : null;
            _buffers.CallSites.Add(formatKeywords is null
                ? new ExecutableCallSite(arguments.Length, arguments, call.Target.Span, call.Span, hasReceiver)
                : new ExecutableNamedFormatCallSite(arguments.Length, arguments, call.Target.Span, call.Span, hasReceiver, formatKeywords));
            return _buffers.CallSites.Count - 1;
        }

        private Dictionary<string, int>? PrepareFormatKeywordIndices(CallArgumentForm[] arguments, LythonSourceSpan span)
        {
            var firstKeyword = 0;
            while (firstKeyword < arguments.Length && arguments[firstKeyword].Kind == CallArgumentKind.Positional)
            {
                PythonTokenization.CheckCancellation(_compilation.CancellationToken, firstKeyword);
                firstKeyword++;
            }
            if (firstKeyword == arguments.Length) return null;

            // The code owner funds one index for ordinary named format calls.
            // The runtime borrows it only when the actual receiver is a string.
            var bytes = checked(64L + 64L * (arguments.Length - firstKeyword));
            using var storage = _compilation.Governor?.ReserveTemporary(bytes, span);
            PythonTokenization.CheckCancellation(_compilation.CancellationToken, 0);
            var indices = new Dictionary<string, int>(arguments.Length - firstKeyword, StringComparer.Ordinal);
            for (var i = firstKeyword; i < arguments.Length; i++)
            {
                PythonTokenization.CheckCancellation(_compilation.CancellationToken, i - firstKeyword);
                var argument = arguments[i];
                if (argument.Kind != CallArgumentKind.Keyword || !indices.TryAdd(argument.KeywordName!, i)) return null;
            }
            storage?.Commit(bytes);
            return indices;
        }

        private ExecutableCodeObject PrepareFunctionCode(FunctionDefinitionStatementSyntax functionDefinition)
        {
            InternModuleBinding(functionDefinition.Name);
            var typeParameterNames = functionDefinition.PreparedTypeParameterNames;
            if (typeParameterNames is null && functionDefinition.TypeParameters is { Count: > 0 } typeParameters)
                typeParameterNames = typeParameters.Select(static p => p.Name).ToHashSet(StringComparer.Ordinal);
            return CompileFunctionCode(functionDefinition.DeclaredName,
                functionDefinition.ParameterLayout, functionDefinition.Body,
                ScopeDirectiveFactsCollector.ForFunction(functionDefinition), typeParameterNames, functionDefinition.IsGenerator,
                qualifiedName: functionDefinition.PreparedQualifiedName);
        }

        private ExecutableCodeObject CompileFunctionCode(string name,
            FunctionParameterLayout parameterLayout, IReadOnlyList<StatementSyntax> body,
            ScopeDirectiveFacts scopeFacts, IReadOnlySet<string>? typeParameterNames = null,
            bool generator = false, PyString? qualifiedName = null, LoweredLambdaExpression? lambda = null)
        {
            return new Builder(_compilation, parameterLayout, this, scopeFacts,
                completeLexicalParent: _completeLexicalParent && typeParameterNames is null,
                moduleCode: _moduleCode, typeParameterNames: typeParameterNames, generator: generator)
                .CompileCodeObject(name, body, qualifiedName: qualifiedName, lambda: lambda);
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
                foreach (var names in parent._buffers.ComprehensionNames)
                    if (names.Contains(name)) return true;

                if (parent._parameterLayout is null || parent._scopeFacts.IsGlobal(name)) return false;
                if (parent._localIndexes.ContainsKey(name) || parent._closureIndexes.ContainsKey(name)) return true;
            }
            return false;
        }

        private int InternMatchCase(LoweredRootPattern pattern, LythonSourceSpan span)
        {
            var localBindings = pattern.Bindings.Select(binding =>
                _localIndexes.TryGetValue(binding.Name, out var slot) ? slot : -1).ToArray();
            _buffers.MatchCases.Add(new ExecutableMatchCaseBinding(pattern, span, localBindings));
            return _buffers.MatchCases.Count - 1;
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
        internal readonly record struct LoopContext(
            int ContinueBlockIndex,
            int BreakBlockIndex,
            bool HasIterator,
            int ProtectedDepth);
    }
}
