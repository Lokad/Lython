using Lokad.Lython.Frontend;

namespace Lokad.Lython.Tests;

internal static class ExecutableDefinitionInspection
{
    internal static IReadOnlyList<ExecutableCreateFunction> FunctionBindings(this ExecutableCodeObject code)
        => code.Constants.OfType<ExecutableCreateFunction>()
            .DistinctBy(binding => binding.CodeObject).ToArray();
}
