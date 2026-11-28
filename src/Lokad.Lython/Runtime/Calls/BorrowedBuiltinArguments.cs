namespace Lokad.Lython.Runtime;

// The builtin decodes evaluated values before starting any pending work.
// Neither the executable slots nor the fixed keyword buffer crosses an await.
internal readonly ref struct BorrowedBuiltinArguments
{
    private readonly Span<LythonRuntime.ExecutableSlot> _operands;
    private readonly ReadOnlySpan<object> _boundValues;

    internal BorrowedBuiltinArguments(Span<LythonRuntime.ExecutableSlot> operands) => _operands = operands;
    internal BorrowedBuiltinArguments(ReadOnlySpan<object> boundValues) => _boundValues = boundValues;

    internal int Length => _operands.IsEmpty ? _boundValues.Length : _operands.Length;

    internal object this[int index] => _operands.IsEmpty ? _boundValues[index] : _operands[index].Object.RequireNotNull();

    internal object? Optional(int index) => index < Length ? this[index] : null;
}
