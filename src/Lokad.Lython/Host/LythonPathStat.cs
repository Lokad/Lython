using System.Numerics;

namespace Lokad.Lython;

public sealed record LythonPathStat(
    bool Exists,
    bool IsFile,
    bool IsDir,
    BigInteger Size,
    string ModifiedAt);
