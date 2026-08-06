using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class BuiltinsModule : PyModule
    {
        private readonly Dictionary<string, object> _builtins;
        private readonly string[] _exportedNames;
        private readonly string[] _memberNames;

        public BuiltinsModule(ExecutionContext context) : base("builtins")
        {
            _builtins = context.State.BuiltinVariables;
            _exportedNames = _builtins.Keys
                .Append("False")
                .Append("None")
                .Append("True")
                .Where(static name => !name.StartsWith("_", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            _memberNames = _exportedNames
                .Append("__debug__")
                .Append("__name__")
                .Order(StringComparer.Ordinal)
                .ToArray();
        }

        public override IReadOnlyList<string> ExportedNames => _exportedNames;

        public override IReadOnlyList<string> MemberNames => _memberNames;

        public override bool TryGetMember(string name, out object value)
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
                    return _builtins.TryGetValue(name, out value!);
            }
        }
    }
}
