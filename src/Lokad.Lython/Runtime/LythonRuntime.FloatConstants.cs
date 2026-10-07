namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Python constructors use a positive quiet NaN. The CLR NaN constant's
    // sign differs, and is visible through copysign and binary serialization.
    private static readonly double PythonNaN = BitConverter.Int64BitsToDouble(0x7ff8_0000_0000_0000L);
}
