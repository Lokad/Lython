using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        private bool TrySkipFilePrefixMismatches(TextFileHandle file, in ExecutableInstruction head)
        {
            if (file.BufferedSearchReader(State) is not { } reader || _execution.Stack.LiveExtent != head.ResultSlot ||
                GlobalNamespace is not null || MirrorLocalStores ||
                State.MaxRecursionDepth is int limit && State.CurrentRecursionDepth >= limit)
                return false;

            var instructions = _execution.Code.Instructions;
            var index = _execution.InstructionIndex;
            var line = instructions[index + 1].LocalSlot;
            var callable = instructions[index + 4].InputStart;
            var locals = _execution.Locals;
            ref var counter = ref locals[head.BufferedPrefixCounterSlot];
            if (!counter.TryGetInteger(out var prior) || prior > long.MaxValue - int.MaxValue ||
                locals[callable].Reference is not PyExecutableFunction function ||
                function.ReadConstantPrefix(State) is not { } prefix) return false;

            var count = reader.SkipBufferedPrefixMismatches(prefix.Utf8Span, out var last);
            if (count == 0) return false;
            locals[line].Object = last;
            counter.SetInteger(prior + count);
            return true;
        }
    }
}
