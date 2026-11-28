using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class BuiltinsModule : PyModule
    {
        private readonly ExecutionContext.BuiltinBindings _builtins;
        private static readonly string[] PublicNames = ExecutionState.BuiltinNames
            .Append("False")
            .Append("None")
            .Append("True")
            .Where(static name => !name.StartsWith("_", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
        private static readonly string[] AllNames = PublicNames
            .Append("__debug__")
            .Append("__name__")
            .Order(StringComparer.Ordinal)
            .ToArray();

        public BuiltinsModule(ExecutionContext context) : base("builtins")
            => _builtins = context.State.BuiltinVariables;

        public override IReadOnlyList<string> ExportedNames => PublicNames;

        public override IReadOnlyList<string> MemberNames => AllNames;

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            switch (name)
            {
                case "__name__":
                    value = PyString.FromString("builtins");
                    return true;
                case "__debug__":
                case "True":
                    value = true;
                    return true;
                case "False":
                    value = false;
                    return true;
                case "None":
                    value = PyNone.Instance;
                    return true;
                default:
                    return _builtins.TryGetValue(name, out value);
            }
        }
    }
}
