using System.Net;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed class DifflibMatchObject : IPySequenceValue, IPyIndexableValue, IPyIterableValue, IPyRenderableValue, IPyHashableValue
    {
        public DifflibMatchObject(int a, int b, int size)
        {
            A = a;
            B = b;
            Size = size;
        }

        public int A { get; }

        public int B { get; }

        public int Size { get; }

        public int Count => 3;

        public int Length => 3;

        public object this[int index] => GetItem(index);

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "a" => new BigInteger(A),
                "b" => new BigInteger(B),
                "size" => new BigInteger(Size),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public object GetItem(int index)
            => index switch
            {
                0 => new BigInteger(A),
                1 => new BigInteger(B),
                2 => new BigInteger(Size),
                _ => throw new ArgumentOutOfRangeException(nameof(index))
            };

        public object CreateSlice(IEnumerable<object> items) => new PyTuple(items);

        public object GetIndex(int index) => GetItem(index);

        public object GetSlice(IEnumerable<int> indices)
            => new PyTuple(indices.Select(GetItem));

        public IEnumerator<object> GetEnumerator()
        {
            yield return new BigInteger(A);
            yield return new BigInteger(B);
            yield return new BigInteger(Size);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public IEnumerable<object> Iterate() => this;

        public int GetPyHashCode()
        {
            var hash = new HashCode();
            hash.Add(A);
            hash.Add(B);
            hash.Add(Size);
            return hash.ToHashCode();
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString($"Match(a={A}, b={B}, size={Size})");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    internal readonly record struct DiffOpcode(DiffTag Tag, int I1, int I2, int J1, int J2);

    internal enum DiffTag
    {
        Equal,
        Delete,
        Insert,
        Replace,
    }
}
