using System.Numerics;
using BenchmarkDotNet.Attributes;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Benchmarks;

[MemoryDiagnoser]
public class RuntimeHotPathBenchmarks
{
    private readonly PyString _text = PyString.FromString("alpha beta gamma delta epsilon zeta eta theta");
    private readonly PyList _list = new(Enumerable.Range(0, 32).Select(i => (object)new BigInteger(i)));
    private readonly PyDict _dict = new();
    private readonly CallArgumentValue[] _boundBuiltinArguments;

    public RuntimeHotPathBenchmarks()
    {
        for (var i = 0; i < 16; i++)
        {
            _dict.SetItem(PyString.FromString("k" + i), new BigInteger(i));
        }

        _boundBuiltinArguments =
        [
            new CallArgumentValue((string?)null, _text),
            new CallArgumentValue("reverse", false)
        ];
    }

    [Benchmark]
    public int PyStringReplace() => _text.Replace(PyString.FromString("ta"), PyString.FromString("XX")).Length;

    [Benchmark]
    public int PyListClone() => new PyList(_list).Count;

    [Benchmark]
    public int PyDictClone() => new PyDict(_dict).Count;

    [Benchmark]
    public int PublicProjectionDictionary()
        => PublicProjection.ProjectDictionary(_dict).Count;

    [Benchmark]
    public int CallBinderNamedBuiltin()
        => CallBinder.BindNamedArguments(
            _boundBuiltinArguments,
            new LythonSourceSpan(0, 0, 0, 0),
            "sorted",
            "Builtin",
            ["iterable", "key", "reverse"],
            requiredCount: 1).Length;
}
