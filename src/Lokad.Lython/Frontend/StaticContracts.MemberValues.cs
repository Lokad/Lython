namespace Lokad.Lython.Frontend;

internal static partial class StaticContracts
{
    private static readonly StaticMemberValueContract[] MemberValueContracts =
    [
        new(AbstractValueKind.Path, "name", StaticReturnShape.String),
        new(AbstractValueKind.Path, "suffix", StaticReturnShape.String),
        new(AbstractValueKind.Path, "stem", StaticReturnShape.String),
        new(AbstractValueKind.Path, "parent", StaticReturnShape.Path),
        new(AbstractValueKind.SubprocessCompletedProcess, "returncode", StaticReturnShape.Integer),
        new(AbstractValueKind.SubprocessCompletedProcess, "stdout", StaticReturnShape.String),
        new(AbstractValueKind.SubprocessCompletedProcess, "stderr", StaticReturnShape.String),
        new(AbstractValueKind.DataclassField, "name", StaticReturnShape.String),
        new(AbstractValueKind.DataclassField, "default", StaticReturnShape.Unknown),
        new(AbstractValueKind.DataclassField, "default_factory", StaticReturnShape.Unknown),
        new(AbstractValueKind.DataclassField, "init", StaticReturnShape.Boolean),
        new(AbstractValueKind.DataclassField, "repr", StaticReturnShape.Boolean),
        new(AbstractValueKind.DataclassField, "hash", StaticReturnShape.Unknown),
        new(AbstractValueKind.DataclassField, "compare", StaticReturnShape.Boolean),
        new(AbstractValueKind.DataclassField, "metadata", StaticReturnShape.Unknown),
        new(AbstractValueKind.DataclassField, "kw_only", StaticReturnShape.Boolean),
    ];

    public static bool TryGetMemberValue(AbstractValue receiver, string memberName, LythonSourceSpan span, out AbstractValue value)
    {
        if (receiver.Kind == AbstractValueKind.ArgparseNamespace)
        {
            var summary = (AbstractArgparseNamespaceSummary)receiver.Value;
            if (summary.Members.TryGetValue(memberName, out value))
            {
                value = value.WithSpan(span);
                return true;
            }
        }

        if (receiver.Kind == AbstractValueKind.DataclassField &&
            string.Equals(memberName, "name", StringComparison.Ordinal))
        {
            var field = (AbstractDataclassFieldSummary)receiver.Value;
            value = AbstractValue.String(field.Name, span);
            return true;
        }

        foreach (var contract in MemberValueContracts)
        {
            if (contract.ReceiverKind == receiver.Kind &&
                contract.MemberName == memberName)
            {
                value = CreateReturnValue(contract.ValueShape, span);
                return true;
            }
        }

        value = default;
        return false;
    }
}
