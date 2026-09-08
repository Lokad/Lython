namespace Lokad.Lython.Runtime;

/// <summary>
/// Format-independent CRC-32 (polynomial 0xEDB88320, as used by gzip, ZIP, and
/// Ethernet) shared by the gzip module and bounded archive readers. The
/// accumulator protocol is explicit: seed with <see cref="uint.MaxValue"/>,
/// advance with <see cref="Update"/>, and finalize by inverting the result.
/// <see cref="Update"/> performs no budget checks; callers must check the
/// execution budget once per chunk (see <see cref="Compute"/> for the
/// reference chunking) so long inputs remain interruptible.
/// </summary>
internal static class Crc32
{
    private const int BudgetChunkLength = 4096;

    private static readonly uint[] Table = BuildTable();

    public static uint Update(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var value in data)
        {
            crc = Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }

    public static uint Compute(ReadOnlySpan<byte> data, LythonRuntime.ExecutionContext context, LythonSourceSpan? span)
    {
        var crc = uint.MaxValue;
        for (var offset = 0; offset < data.Length; offset += BudgetChunkLength)
        {
            context.CheckExecutionBudget(span);
            var length = Math.Min(BudgetChunkLength, data.Length - offset);
            crc = Update(crc, data.Slice(offset, length));
        }

        return ~crc;
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (var index = 0; index < table.Length; index++)
        {
            var value = (uint)index;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value >> 1) ^ (0xEDB88320u & unchecked((uint)-(int)(value & 1)));
            }

            table[index] = value;
        }

        return table;
    }
}
