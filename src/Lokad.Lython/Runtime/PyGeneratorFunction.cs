using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed class PyGeneratorFunction(LoweredFunctionDefinitionStatement definition,
    LythonRuntime.ExecutionContext closure, Dictionary<string, object> defaults, IReadOnlyList<LythonRuntime.ExecutableCell>? closureCells = null)
    : PyFunctionBase(definition.Syntax.DeclaredName, definition.Parameters, closure, defaults,
        ScopeDirectiveFactsCollector.ForFunction(definition.Syntax))
{
    protected override bool RequiresArgumentMirroring => true;

    protected override object ExecuteBody(LythonRuntime.ExecutionContext frame, BoundCallArguments arguments, LythonSourceSpan span)
        => LythonRuntime.PyGenerator.Create(definition.GeneratorCode!, frame, span, closureCells);

    protected override ValueTask<object> ExecuteBodyAsync(LythonRuntime.ExecutionContext frame, BoundCallArguments arguments, LythonSourceSpan span)
        => new(ExecuteBody(frame, arguments, span));
}
