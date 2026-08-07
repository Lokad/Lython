namespace Lokad.Lython.Frontend;

internal static class ImportSyntaxFacts
{
    public static bool IsStarImport(IReadOnlyList<ImportedMemberSyntax> members)
        => members.Count == 1 && members[0].Name == "*";

    public static IEnumerable<string> EnumerateBindingNames(ImportStatementSyntax statement)
    {
        if (statement.ImportedMembers is null)
        {
            yield break;
        }

        if (IsStarImport(statement.ImportedMembers))
        {
            foreach (var memberName in StaticContracts.GetModuleExportedMemberNames(statement.ModuleName))
            {
                yield return memberName;
            }

            yield break;
        }

        foreach (var member in statement.ImportedMembers)
        {
            yield return member.BindingName;
        }
    }
}
