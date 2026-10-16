namespace Lokad.Lython.Frontend;

internal sealed record ExecutableUnpackValues(UnpackingLayout Layout) : ExecutableOperation;
internal sealed record ExecutableReadRetainedSubscript : ExecutableOperation;
internal sealed record ExecutableStoreSubscript(LythonSourceSpan TargetSpan, bool ValueLast = false) : ExecutableOperation;
internal sealed record ExecutableStoreMember(string MemberName, LythonSourceSpan TargetSpan, bool ValueLast = false) : ExecutableOperation;
internal sealed record ExecutableReadRetainedSlice : ExecutableOperation;
internal sealed record ExecutableStoreSlice(LythonSourceSpan TargetSpan, bool ValueLast = false) : ExecutableOperation;

internal sealed partial class ExecutableScript
{
    private sealed partial class Builder
    {
        private int CompileLoopTarget(LoopTargetSyntax target, LythonSourceSpan span, int block)
        {
            switch (target)
            {
                case LoopNameTargetSyntax name: CompileLoopBinding(name.Name, span, block); return block;
                case LoopStarredTargetSyntax star: CompileLoopBinding(star.Name, span, block); return block;
                case LoopStoreTargetSyntax store: return CompileStoreTarget(store.Target, span, block);
                case LoopTupleTargetSyntax tuple:
                    var starIndex = -1;
                    for (var i = 0; i < tuple.Items.Count; i++)
                        if (AssignmentTargetFacts.IsStarred(tuple.Items[i])) starIndex = i;
                    EmitOperation(new ExecutableUnpackValues(new UnpackingLayout(tuple.Items.Count, starIndex)), span, block);
                    foreach (var child in tuple.Items) block = CompileLoopTarget(child, span, block);
                    return block;
                default: throw new InvalidOperationException("Unknown loop target.");
            }
        }

        private void CompileLoopBinding(string name, LythonSourceSpan span, int block)
        {
            if (_comprehensionNames.Any(names => names.Contains(name)))
            {
                // The resolved store selects inlined bindings before caller directives.
                AddInstruction(block, ExecutableInstruction.StoreGlobal(InternName(name), span));
            }
            else CompileStoreBoundName(name, span, block);
        }
    }
}
