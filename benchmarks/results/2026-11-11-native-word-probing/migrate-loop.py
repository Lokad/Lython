from pathlib import Path
p=Path('src/Lokad.Lython/Runtime/LythonRuntime.Executable.DictionaryAccumulation.cs');t=p.read_text(encoding='utf-8')
def replace(a,b):
 global t
 assert t.count(a)==1,(a,t.count(a));t=t.replace(a,b)
replace('''            if (execution.Stack.LiveExtent != head.ResultSlot || execution.Stack.HasPendingDisposals
                || !execution.Stack.TryAdvanceNativeIteration(head.InputStart, out var next)) return false;''','''            if (execution.Stack.LiveExtent != head.ResultSlot || execution.Stack.HasPendingDisposals) return false;''')
replace('''            if (!next.HasValue)
            {
                _ = execution.Stack.TakeValue(head.InputStart, head.InputStart);
                execution.InstructionIndex = head.TargetIndex;
                return true;
            }

''','')
replace('''            var deferTotal = hasTotal && !execution.Code.RequiresLocalVariableMirroring
                && invocation?.MirrorLocalStores != true && invocation?.GlobalNamespace is null;''','''            var privateBindings = !execution.Code.RequiresLocalVariableMirroring
                && invocation?.MirrorLocalStores != true && invocation?.GlobalNamespace is null;
            var deferTotal = hasTotal && privateBindings;
            var cursor = privateBindings && execution.LocalCells?[store.ThirdInput] is null
                ? execution.Stack[head.InputStart] as PyStringOps.CasedWhitespaceIteration : null;
            if (cursor is not null && !ReferenceEquals(cursor.MemoryGovernor, scope.MemoryGovernor)) cursor = null;
            if (!TryReadDictionaryWord(ref execution, cursor, head.InputStart, out var word, out var hasWord)) return false;
            if (!hasWord)
            {
                _ = execution.Stack.TakeValue(head.InputStart, head.InputStart);
                execution.InstructionIndex = head.TargetIndex;
                return true;
            }
            var wordChanged = false;
            PyStringOps.CompactWord pendingWord = default;''')
replace('''                    PublishInstructionResult(ref execution, invocation, head, next.Value);''','''                    var compact = word.Value is null;
                    if (compact)
                    {
                        // Keep the original binding check, even though its value
                        // stays private until a continuation can observe it.
                        execution.Stack.CheckExecution(execution.Code.Instructions[headIndex + 1].Span);
                        pendingWord = word;
                        wordChanged = true;
                    }
                    else
                    {
                        PublishInstructionResult(ref execution, invocation, head, word.Value!);
                        wordChanged = false;
                    }''')
replace('''                    var key = execution.Locals[store.ThirdInput] as PyString;''','''                    var key = compact ? null : execution.Locals[store.ThirdInput] as PyString;''')
replace('''                    if (dictionary is null || key is null || hasTotal && (priorTotal is not BigInteger integer || integer.GetByteCount() >= 8))''','''                    if (dictionary is null || !compact && key is null ||
                        hasTotal && (priorTotal is not BigInteger integer || integer.GetByteCount() >= 8))''')
replace('''                        updated = dictionary.TryAccumulateWithoutAdmission(key, fallback, increment, scope,
                            call.CallSpan, add.Span, storeSpan);''','''                        updated = compact
                            ? dictionary.TryAccumulateInlineWord(PyStringOps.CompactWordBytes(ref word), fallback, increment, scope,
                                call.CallSpan, add.Span, storeSpan)
                            : dictionary.TryAccumulateWithoutAdmission(key!, fallback, increment, scope,
                                call.CallSpan, add.Span, storeSpan);''')
replace('''                    if (!execution.Stack.TryAdvanceNativeIteration(head.InputStart, out next))
                        throw new InvalidOperationException("A native loop lost its private iteration source.");
                    if (next.HasValue) continue;''','''                    if (!TryReadDictionaryWord(ref execution, cursor, head.InputStart, out var nextWord, out hasWord))
                        throw new InvalidOperationException("A native loop lost its private iteration source.");
                    if (hasWord) { word = nextWord; continue; }''')
replace('''                if (totalChanged) execution.Locals[total.InputStart] = new BigInteger(pendingTotal);''','''                try
                {
                    if (wordChanged) execution.Locals[store.ThirdInput] = cursor!.Materialize(ref pendingWord);
                }
                finally { if (totalChanged) execution.Locals[total.InputStart] = new BigInteger(pendingTotal); }''')
at=t.index('        private static bool TryExecuteDictionaryAccumulation(')
t=t[:at]+'''        private static bool TryReadDictionaryWord(ref ExecutableWorkState execution,
            PyStringOps.CasedWhitespaceIteration? cursor, int slot, out PyStringOps.CompactWord word, out bool hasWord)
        {
            if (cursor is not null) { hasWord = cursor.TryReadWord(out word); return true; }
            word = default;
            if (!execution.Stack.TryAdvanceNativeIteration(slot, out var next)) { hasWord = false; return false; }
            hasWord = next.HasValue;
            if (hasWord) word.Value = next.Value;
            return true;
        }

'''+t[at:]
p.write_text(t,encoding='utf-8')
print('One native histogram loop now accepts compact pre-funded words, with pending binding publication on every exit. Ordinary VM call/return and cursor paths unchanged.')
