using Lokad.Lython.Runtime.Text;
using System.Runtime.CompilerServices;

namespace Lokad.Lython.Runtime;

internal sealed class PyInstance : IPyRenderableValue, IPyHashableValue
{
    private readonly Dictionary<string, object> _attributes = new(StringComparer.Ordinal);

    public PyInstance(PyType type)
    {
        Type = type;
    }

    public PyType Type { get; }

    public bool TryGetOwnAttribute(string name, out object value) => _attributes.TryGetValue(name, out value!);

    public IEnumerable<KeyValuePair<string, object>> EnumerateOwnAttributes() => _attributes;

    public bool TryGetAttribute(string name, LythonRuntime.ExecutionContext context, LythonSourceSpan span, out object value)
        => PyAttributeLookup.TryResolveInstanceMember(this, name, context, span, out value);

    public void SetAttribute(string name, object value) => _attributes[name] = value;

    public bool RemoveAttribute(string name) => _attributes.Remove(name);

    public int GetPyHashCode()
    {
        if (Type.DataclassFields is { } fields)
        {
            return Type.DataclassHashMode switch
            {
                DataclassHashMode.Generated => GetGeneratedDataclassHashCode(fields),
                DataclassHashMode.Unhashable => throw new InvalidOperationException("unhashable value"),
                _ => RuntimeHelpers.GetHashCode(this)
            };
        }

        return RuntimeHelpers.GetHashCode(this);
    }

    public PyString RenderPython(PyRenderingContext context)
    {
        if (Type.DataclassReprEnabled && Type.DataclassFields is { } fields)
        {
            var renderedFields = fields.Where(field => field.Repr).ToArray();
            var builder = new Utf8ValueBuilder();
            builder.AppendString(Type.Name);
            builder.AppendAscii("(");
            for (var i = 0; i < renderedFields.Length; i++)
            {
                if (i > 0)
                {
                    builder.AppendAscii(", ");
                }

                var field = renderedFields[i];
                builder.AppendString(field.Name);
                builder.AppendAscii("=");
                _ = TryGetOwnAttribute(field.Name, out var value);
                builder.Append(PyRendering.ToPythonPyString(value ?? PyNone.Instance, context));
            }

            builder.AppendAscii(")");
            return builder.ToPyString();
        }

        return PyString.FromString($"<{Type.Name} object>");
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    public override string ToString() => $"<{Type.Name} object>";

    private int GetGeneratedDataclassHashCode(IReadOnlyList<DataclassFieldSpec> fields)
    {
        var hash = new HashCode();
        foreach (var field in fields)
        {
            if (!PyDataclass.ShouldIncludeInGeneratedHash(field))
            {
                continue;
            }

            _ = TryGetOwnAttribute(field.Name, out var value);
            hash.Add(PyValueComparer.Instance.GetHashCode(value ?? PyNone.Instance));
        }

        return hash.ToHashCode();
    }
}
