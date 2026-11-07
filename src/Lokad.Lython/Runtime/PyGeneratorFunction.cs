using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed class PyGeneratorFunction : PyFunctionBase
{
    private readonly ExecutableCodeObject _code;
    private readonly IReadOnlyList<LythonRuntime.ExecutableCell>? _closureCells;

    public PyGeneratorFunction(LoweredFunctionDefinitionStatement definition,
        LythonRuntime.ExecutionContext closure, IReadOnlyDictionary<string, object> defaults,
        IReadOnlyList<LythonRuntime.ExecutableCell>? closureCells = null)
        : this(definition.Syntax.DeclaredName, definition.ParameterLayout, definition.GeneratorCode!,
            closure, defaults, definition.GeneratorCode!.ScopeFacts, closureCells) { }

    public PyGeneratorFunction(string name, FunctionParameterLayout parameterLayout,
        ExecutableCodeObject code, LythonRuntime.ExecutionContext closure,
        IReadOnlyDictionary<string, object> defaults, ScopeDirectiveFacts scopeFacts,
        IReadOnlyList<LythonRuntime.ExecutableCell>? closureCells = null,
        PythonCallableKind callableKind = PythonCallableKind.Function)
        : base(name, parameterLayout, closure, defaults, scopeFacts, callableKind)
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
