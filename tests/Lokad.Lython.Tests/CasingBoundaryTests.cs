using System.Security.Cryptography;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class CasingBoundaryTests
{
    // SHA-256 of independent CPython 3.13.2 lower/upper UTF-8 outputs.
    // Inputs include long ignorable runs, Unicode expansions and sigma context.
    public static IEnumerable<object[]> Cases()
    {
        yield return [string.Concat(Enumerable.Repeat(new string(Enumerable.Range(0, 128).Select(i => (char)i).ToArray()), 33)),
            "f807af3ada744cffaf7d709ff94bb4242728875a12d3bb9fe5b4468d5bd45e7a", "3bd33d91627c1db2e764ac479d294af3e4627bf47a2de9ef04062db6bb86dde2"];
        yield return ["A" + new string('.', 8192) + "Σ\u0301",
            "86a1d921a270b44c32774deb34047f998edefa665b04f90da5852f98a4d4cb82", "597fb29d130783b117b244309d1feebfe6acf18162643a952829d49e56a6547b"];
        yield return ["A" + new string(' ', 4096) + "Σ",
            "533cda03147ef9c7e70f466a597f1af2905045cfa3af1cf0960139709baffdb5", "3a5b3cd241b6b7ea115b274b2f81d4ba5841be40973d4c886006d7292dca65fc"];
        yield return ["AΣ" + new string('.', 4096) + "A",
            "5128a5978897e666701df9f84ae42267db82cac339d3f7a871dfd5bb567b8b0e", "8215f773ef0d4fa8804aebdad1c79514435204bd8fc57516d4ad19ffffd9d829"];
        yield return ["AΣ" + new string('.', 4095) + "/A",
            "6b0eff5f1ac6998cad0f17ecad3a8282fea5ed4e9d080a6d009a70cf656aeaa8", "21898fb76b9a054cb3ccc24f5c24a6f9b6abb7595b8e70dd92ce36a6743d33a9"];
        yield return [new string('A', 4095) + "\u0301ΣX",
            "9e5b37b0b3642657a85484bcabd6b9f90d36a942faed7013bf12dfd8b6648dd8", "d0e51164c8c6117b89935d67c964d7cae68304c38d7dbbcad149b65ea68bfd3f"];
        yield return [new string('A', 4095) + "İ" + new string('b', 4096) + "ßﬃ𐐀😀ΟΣ",
            "a93c837d8e3c59e7182918d2528c82da87ee27a37d20d5aa47a20e4bb5c6664c", "c1a87a6a87d7f955ca1b598ee782a7f5928bf966d41e39b6306f47d586fd88cb"];
        yield return ["Ο" + new string('^', 4096) + "Σ",
            "74587a124db6aabc95ad5a6fe07c6088a913d48dbdd2c28f9aaa59a0ad01a92e", "59bcb7c2c25a286b9b9250b55bd214a07d7db1ab997a9a51c73e8fb7c0347613"];
        yield return [string.Concat(Enumerable.Repeat("AbΣ.'\0İß𐐀", 400)) + "FINΣ",
            "e8bf35b7c6c67369fad6896b7de8c792659bfb33f2d87f4032ebbca8aca59efe", "37d0df8aa2aa557ee90424f9dfc8c8b13d5bb3236f4bb1d8df2b6ffbcdda32df"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void CaseMethodsPreservePythonForLongMixedText(string source, string lower, string upper)
    {
        var input = PyString.FromString(source);
        Assert.Equal(lower, Convert.ToHexStringLower(SHA256.HashData(PyStringOps.Lower(input).Utf8Bytes.Span)));
        Assert.Equal(upper, Convert.ToHexStringLower(SHA256.HashData(PyStringOps.Upper(input).Utf8Bytes.Span)));
        Assert.Equal(source, input.AsString());
    }
}
