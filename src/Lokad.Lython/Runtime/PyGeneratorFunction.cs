using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed class PyGeneratorFunction : PyFunctionBase
{
    private readonly ExecutableCodeObject _code;
    private readonly IReadOnlyList<LythonRuntime.ExecutableCell>? _closureCells;

    public PyGeneratorFunction(LoweredFunctionDefinitionStatement definition,
        LythonRuntime.ExecutionContext closure, Dictionary<string, object> defaults,
        IReadOnlyList<LythonRuntime.ExecutableCell>? closureCells = null)
        : this(definition.Syntax.DeclaredName, definition.Parameters, definition.GeneratorCode!,
            closure, defaults, definition.GeneratorCode!.ScopeFacts, closureCells) { }

    public PyGeneratorFunction(string name, IReadOnlyList<LoweredFunctionParameter> parameters,
        ExecutableCodeObject code, LythonRuntime.ExecutionContext closure,
        Dictionary<string, object> defaults, ScopeDirectiveFacts scopeFacts,
        IReadOnlyList<LythonRuntime.ExecutableCell>? closureCells = null,
        PythonCallableKind callableKind = PythonCallableKind.Function)
        : base(name, parameters, closure, defaults, scopeFacts, callableKind)
    {
        _code = code;
        _closureCells = closureCells;
    }

    protected override bool RequiresArgumentMirroring => true;

    protected override object ExecuteBody(LythonRuntime.ExecutionContext frame, BoundCallArguments arguments, LythonSourceSpan span)
        => LythonRuntime.PyGenerator.Create(_code, frame, span, _closureCells);

    protected override ValueTask<object> ExecuteBodyAsync(LythonRuntime.ExecutionContext frame, BoundCallArguments arguments, LythonSourceSpan span)
        => new(ExecuteBody(frame, arguments, span));
}
