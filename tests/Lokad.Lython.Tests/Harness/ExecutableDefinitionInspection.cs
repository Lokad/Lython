using Lokad.Lython.Frontend;

namespace Lokad.Lython.Tests;

internal static class ExecutableDefinitionInspection
{
    internal static IReadOnlyList<ExecutableFunctionBinding> FunctionBindings(this ExecutableCodeObject code)
        => code.Constants.OfType<ExecutableCreateFunction>().Select(operation => operation.Binding)
            .DistinctBy(binding => binding.CodeObject).ToArray();
}
