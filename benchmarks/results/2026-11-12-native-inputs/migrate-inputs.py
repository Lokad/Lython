from pathlib import Path
import hashlib,json
r=Path(__file__).resolve().parent;p=Path('src/Lokad.Lython/Runtime/LythonRuntime.Executable.NativeReturn.cs')
before=json.loads((r/'intermediate-r250-source.json').read_text(encoding='utf-8'))
assert hashlib.sha256(p.read_bytes()).hexdigest()==before['sourceHashes'][p.as_posix()]
s=p.read_text(encoding='utf-8')
s=s.replace('internal sealed partial class LythonRuntime\n{','''internal sealed partial class LythonRuntime
{
    // One non-owning input convention for native proof/evaluation. Parameters
    // keep their real bounds; cells remain live in the definition-owned array.
    // The readonly stack view cannot escape or cross a suspension boundary.
    private readonly ref struct NativeInputs(ReadOnlySpan<object?> values, IReadOnlyList<ExecutableCell>? cells)
    {
        internal readonly ReadOnlySpan<object?> Values = values;
        internal readonly IReadOnlyList<ExecutableCell>? Cells = cells;
    }
''',1)
a=s.index('    // Dynamic checks precede')
entry=s[:a];tail=s[a:]
entry=entry.replace('if (!GuardNativeBody(codeObject, body, arguments, context))','var inputs = new NativeInputs(arguments, null);\n        if (!GuardNativeBody(codeObject, body, inputs, context))')
entry=entry.replace('ExecuteNativeBody(codeObject, body, context, arguments,','ExecuteNativeBody(codeObject, body, context, inputs,')
entry=entry.replace('ReadOnlySpan<object?> arguments, string name,','ReadOnlySpan<object?> arguments, IReadOnlyList<ExecutableCell> cells, string name,')
entry=entry.replace('if (!GuardNativeBody(code, body, arguments, context))','var inputs = new NativeInputs(arguments, cells);\n        if (!GuardNativeBody(code, body, inputs, context))')
entry=entry.replace('ExecuteNativeBody(code, body, context, arguments,','ExecuteNativeBody(code, body, context, inputs,')
tail=tail.replace('ReadOnlySpan<object?> arguments','in NativeInputs arguments').replace('arguments.Length','arguments.Values.Length').replace('arguments[','arguments.Values[')
tail=tail.replace('arguments.Values.Length >= body.ClosureInputCount','(body.ClosureInputCount == 0 || arguments.Cells?.Count == body.ClosureInputCount)')
tail=tail.replace('(arguments.Values.Length - code.NativeBody!.ClosureInputCount)','arguments.Values.Length').replace('(arguments.Values.Length - codeObject.NativeBody!.ClosureInputCount)','arguments.Values.Length')
needle='((ExecutableCell)arguments.Values[arguments.Values.Length - code.NativeBody!.ClosureInputCount + instruction.ClosureSlot]!).StoreThroughOwner(stored);'
assert tail.count(needle)==1
tail=tail.replace(needle,'arguments.Cells![instruction.ClosureSlot].StoreThroughOwner(stored);')
a=tail.index('    // Incoming references follow the real parameter prefix')
b=tail.index('    private static void RestoreNativeReturnException',a)
tail=tail[:a]+'''    // The compiled function owns a fixed cell collection, validated before any
    // proof/effects. Values remain live; no parameter slot can impersonate a cell.
    private static bool TryReadNativeCell(ExecutableCodeObject code, in NativeInputs arguments,
        int slot, [MaybeNullWhen(false)] out ExecutableCell cell)
    {
        cell = (uint)slot < (uint)code.NativeBody!.ClosureInputCount ? arguments.Cells?[slot] : null;
        return cell is not null;
    }

'''+tail[b:]
p.write_text(entry+tail,encoding='utf-8')
assert 'arguments.Values.Length -'not in p.read_text(encoding='utf-8')
print('Native proof/evaluation now borrows real parameter bounds and existing cell owners. Appended-cell packing and index convention removed.')
