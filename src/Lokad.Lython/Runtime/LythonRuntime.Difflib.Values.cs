using System.Net;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal readonly record struct DiffOpcode(DiffTag Tag, int I1, int I2, int J1, int J2);

    internal enum DiffTag
    {
        Equal,
        Delete,
        Insert,
        Replace,
    }
}
