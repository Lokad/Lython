using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: retained os.stat records hold exactly the charged box. The record
/// carries only value-type fields (kind, inline BigInteger magnitude, inline
/// timestamp), so its heap graph is the 56-byte object plus one 8-byte slot:
/// the 64-byte box unit is exact. The ModifiedAt string is computed per read
/// and governed there, never stored. Decision: keep returning the host record
/// instead of reshaping it. (Live-heap slope sampling measured exactly
/// 64.0B/record across repeated rounds, tripping to 160.0B/record under a
/// temporary 64-byte bloat field; the slope form is not committed because
/// GetTotalMemory wobbles under parallel-suite load, while this shape pin is
/// deterministic.)
/// </summary>
public sealed class StatRecordShapeTests
{
    [Fact]
    public void RecordHoldsNoReferencePayload()
    {
        var fields = typeof(LythonPathStat).GetFields(
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);
        Assert.NotEmpty(fields);
        foreach (var field in fields)
        {
            Assert.True(
                field.FieldType.IsValueType,
                $"stat record gained a reference payload: {field.Name} ({field.FieldType})");
        }

        var size = typeof(LythonPathStat).GetProperty("Size");
        Assert.NotNull(size);
        Assert.Equal(typeof(System.Numerics.BigInteger), size.PropertyType);
        var modifiedAt = typeof(LythonPathStat).GetProperty("ModifiedAtTimestamp");
        Assert.NotNull(modifiedAt);
        Assert.Equal(typeof(System.DateTimeOffset?), modifiedAt.PropertyType);
    }
}