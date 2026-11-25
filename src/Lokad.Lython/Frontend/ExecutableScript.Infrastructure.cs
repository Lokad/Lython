using System.Collections.Frozen;
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
            var index = _buffers.Blocks.Count;
            _buffers.Blocks.Add(index == 0 ? _buffers.EntryBlock : []);
            return index;
        }

        private void AddInstruction(int blockIndex, ExecutableInstruction instruction)
        {
            var instructions = _buffers.Blocks[blockIndex];
            System.Diagnostics.Debug.Assert(instructions.Count == 0 || !instructions[^1].TerminatesBlock);
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
            return instructions.Count != 0 && instructions[^1].TerminatesBlock;
        }

        private int InternLocal(string name)
        {
            if (_localIndexes.TryGetValue(name, out var index))
            {
                return index;
            }

            if (ReferenceEquals(_localIndexes, _scopeFacts.PreparedLocalSlots))
                _localIndexes = new(_localIndexes, StringComparer.Ordinal);
            index = checked(_declaredLocalNames.Length + _buffers.Locals.Count);
            _buffers.Locals.Add(name);
            _localIndexes.Add(name, index);
            return index;
        }

        private string LocalName(int slot) => slot < _declaredLocalNames.Length ? _declaredLocalNames[slot]
            : _buffers.Locals[slot - _declaredLocalNames.Length];

        private string[] CompleteLocalNames()
        {
            if (_buffers.Locals.Count == 0) return _declaredLocalNames;
            if (_declaredLocalNames.Length == 0) return _buffers.Locals.ToArray();
            var names = new string[checked(_declaredLocalNames.Length + _buffers.Locals.Count)];
            _declaredLocalNames.CopyTo(names, 0);
            _buffers.Locals.CopyTo(names, _declaredLocalNames.Length);
            return names;
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
            else if (_completeLexicalParent && !_scopeFacts.IsNonlocal(name) &&
                (_parameterLayout is not null || !_classBody && _moduleCode is not null))
            {
                // The root module and complete lexical functions have no
                // other scope to search after locals and captures. Resolve
                // the live module binding before falling back to builtins.
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

        private int InternConstant(object value)
        {
            if (_buffers.ConstantIndexes.TryGetValue(value, out var existing)) return existing;

            var index = AppendConstant(value);
            _buffers.ConstantIndexes.Add(value, index);
            return index;
        }

        private int AppendConstant(object value)
        {
            var index = _buffers.Constants.Count;
            _buffers.Constants.Add(value);
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
            ScopeDirectiveFacts scopeFacts, IReadOnlyCollection<string>? typeParameterNames = null,
            bool generator = false, PyString? qualifiedName = null, LambdaExpressionSyntax? lambda = null)
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
            var localBindings = pattern.Bindings.Select(name =>
                _localIndexes.TryGetValue(name, out var slot) ? slot : -1).ToArray();
            _buffers.MatchCases.Add(new ExecutableMatchCaseBinding(pattern, span, localBindings));
            return _buffers.MatchCases.Count - 1;
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
