using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed class PkgutilLoaderObject : IPyRenderableValue, IPyDynamicAttributes
    {
        public PkgutilLoaderObject(PyString name, bool isPackage, PyString? sourcePath)
        {
            Name = name;
            IsPackage = isPackage;
            SourcePath = sourcePath;
        }

        public PyString Name { get; }

        public bool IsPackage { get; }

        public PyString? SourcePath { get; }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "name" => Name,
                "fullname" => Name,
                "ispkg" => IsPackage,
                "is_package" => BoundCallable.Create((arguments, span, context) =>
                {
                    _ = context;
                    if (arguments.Length is > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "loader.is_package(fullname=None) expects zero or one argument.", span);
                    }

                    if (arguments.Length == 1 &&
                        arguments[0] is not PyNone &&
                        (!PyStringOps.TryAsString(arguments[0], out var fullname) || !fullname.Equals(Name)))
                    {
                        return false;
                    }

                    return IsPackage;
                }, "loader.is_package", ["fullname"], requiredCount: 0),
                "get_source" => BoundCallable.Create((a, s, c) => GetSource(a, s, c, false).GetAwaiter().GetResult(),
                    (a, s, c) => GetSource(a, s, c, true), "loader.get_source", ["fullname"], requiredCount: 0),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
        private async ValueTask<object> GetSource(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            if (arguments.Length != 0 && arguments[0] is not PyNone &&
                (arguments[0] is not PyString fullname || !fullname.Equals(Name))) return PyNone.Instance;
            if (SourcePath is null) return PyNone.Instance;
            using var path = context.MemoryGovernor.ReserveTemporary(checked(64L + 4L * SourcePath.Utf8Bytes.Length), span);
            var source = SourcePath.AsString();
            return asynchronous ? await ReadGovernedHostTextAsync(source, context, span).ConfigureAwait(false)
                : ReadGovernedHostText(source, context, span);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            var builder = new GovernedByteBuilder(context.Context.MemoryGovernor);
            try
            {
                builder.AppendAscii("<lython loader for ");
                builder.Append(PyRendering.ToReprPyString(Name, context));
                builder.Append((byte)'>');
                return builder.ToPyStringAndRelease();
            }
            finally { builder.Release(); }
        }
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }
}
