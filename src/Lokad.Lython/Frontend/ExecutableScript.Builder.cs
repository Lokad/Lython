using Lokad.Lython.Runtime;

namespace Lokad.Lython.Frontend;

internal sealed partial class ExecutableScript
{
    private ExecutableScript(
        LythonSourceSpan? annotationInitializationSpan,
        ExecutableCodeObject entryPoint)
    {
        AnnotationInitializationSpan = annotationInitializationSpan;
        EntryPoint = entryPoint;
    }

    public LythonSourceSpan? AnnotationInitializationSpan { get; }

    public ExecutableCodeObject EntryPoint { get; }

    public static ExecutableScript Compile(LoweredScript lowered)
    {
        var entryPoint = new Builder().CompileCodeObject("<module>", lowered.Statements);
        return new ExecutableScript(entryPoint.ScopeFacts.ContainsVariableAnnotations
            ? lowered.Syntax.Statements[0].Span : null, entryPoint);
    }

    internal static ExecutableCodeObject CompileGenerator(IReadOnlyList<LoweredFunctionParameter> parameters, IReadOnlyList<LoweredStatement> body,
        ScopeDirectiveFacts? scopeFacts = null)
        => new Builder(parameters, true, scopeFacts).CompileCodeObject("<generator>", body);

    internal static ExecutableCodeObject CompileClassFunction(LoweredFunctionDefinitionStatement function)
        => new Builder(function.Parameters, parentClosureCandidates: null, function.ScopeFacts)
            .CompileCodeObject(function.Syntax.DeclaredName, function.Body);

    private sealed partial class Builder
    {
        private static readonly LythonSourceSpan EmptySpan = new(0, 0, 0, 0);

        private readonly List<object?> _constants = [];
        private readonly Dictionary<object, int> _constantIndexes = new(ConstantValueComparer.Instance);
        private int? _nullConstantIndex;
        private readonly List<string> _names = [];
        private readonly List<string> _locals = [];
        private readonly List<string> _closures = [];
        private readonly Dictionary<string, int> _nameIndexes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _localIndexes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _closureIndexes = new(StringComparer.Ordinal);
        private readonly List<BasicBlockBuilder> _blocks = [];
        private readonly List<ExecutableExceptionRegion> _regions = [];
        private readonly List<ImportStatementSyntax> _imports = [];
        private readonly List<ExecutableCallSite> _callSites = [];
        private int _memberCacheCount;
        private readonly List<ExecutableFunctionBinding> _functions = [];
        private readonly List<ExecutableMatchCaseBinding> _matchCases = [];
        private readonly List<LoweredStatement> _statementFallbacks = [];
        private readonly Stack<LoopContext> _loops = [];
        private readonly Stack<IReadOnlySet<string>> _comprehensionNames = [];
        private int _protectedDepth;
        private bool _generator;
        private int _syntheticLocalCounter;

        private readonly IReadOnlyList<LoweredFunctionParameter>? _functionParameters;
        private readonly HashSet<string> _parentClosureCandidates;
        private ScopeDirectiveFacts _scopeFacts = ScopeDirectiveFacts.Empty;
        private readonly ScopeDirectiveFacts? _declaredScopeFacts;

        internal Builder() : this(null, null) { }

        internal Builder(IReadOnlyList<LoweredFunctionParameter>? functionParameters) : this(functionParameters, null) { }
        internal Builder(IReadOnlyList<LoweredFunctionParameter> functionParameters, bool generator, ScopeDirectiveFacts? scopeFacts = null)
            : this(functionParameters, null, scopeFacts) { _generator = generator; }

        internal Builder(
            IReadOnlyList<LoweredFunctionParameter>? functionParameters,
            IEnumerable<string>? parentClosureCandidates,
            ScopeDirectiveFacts? scopeFacts = null)
        {
            _declaredScopeFacts = scopeFacts;
            _functionParameters = functionParameters;
            _parentClosureCandidates = parentClosureCandidates is null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(parentClosureCandidates, StringComparer.Ordinal);
        }

        public ExecutableCodeObject CompileCodeObject(string name, IReadOnlyList<LoweredStatement> statements,
            LoweredGeneratorExpression? generatorExpression = null)
        {
            _scopeFacts = _declaredScopeFacts ?? ScopeDirectiveFactsCollector.ForLoweredStatements(_functionParameters, statements);
            // A lexical super read also requests the hidden free __class__ cell.
            // Ordinary enclosing locals must be retained like explicit reads.
            // Separately built generator functions resolve this hidden name
            // through their retained lexical chain. Generator expressions have
            // the defining builder's candidates and capture the existing cell.
            if ((!_generator || generatorExpression is not null) && _scopeFacts.RequiresClassCell && (_declaredScopeFacts is not null || _parentClosureCandidates.Contains("__class__")))
                InternClosure("__class__");
            // Match Python and FunctionBindingPlan: positional, keyword-only,
            // then variadic parameters. Collect this prefix once, before body locals.
            if (_functionParameters is not null)
            {
                foreach (var parameter in _functionParameters)
                    if (parameter.Kind is FunctionParameterKind.Positional or FunctionParameterKind.PositionalOnly or FunctionParameterKind.KeywordOnly
                        && IsLocalBindingName(parameter.Name))
                        InternLocal(parameter.Name);
                foreach (var parameter in _functionParameters)
                    if (parameter.Kind is FunctionParameterKind.VariadicList or FunctionParameterKind.VariadicDictionary
                        && IsLocalBindingName(parameter.Name))
                        InternLocal(parameter.Name);
            }
            // Function locals include bindings in dead control-flow bodies.
            // Keep the argument prefix above; no second binding walk. Modules
            // retain their dynamic namespace; actual stores intern needed slots.
            if (_functionParameters is not null)
                foreach (var localName in _scopeFacts.LocalBindingOrder)
                    InternLocal(localName);

            var entryBlock = CreateBlock();
            var exitBlock = generatorExpression is null ? CompileStatements(statements, entryBlock)
                : CompileComprehensionBody(generatorExpression.Clauses, generatorExpression.ItemExpression,
                    null, generatorExpression.Span, entryBlock, yielding: true);
            // Module completion is distinct from an executed top-level return:
            // imports reject the latter. The interpreter already ends naturally
            // at an unterminated final block. Functions still return Python None.
            if (_functionParameters is not null && exitBlock is int finalBlock && !IsTerminated(finalBlock))
            {
                AddInstruction(finalBlock, ExecutableInstruction.ReturnNone(EmptySpan));
            }

            var reachable = CollectReachableBlocks(entryBlock);
            var ordered = reachable.Order().ToArray();
            var indexMap = new Dictionary<int, int>();
            for (var i = 0; i < ordered.Length; i++)
            {
                indexMap[ordered[i]] = i;
            }

            var normalizedBlocks = NormalizeBlocks(ordered, indexMap);
            var normalizedRegions = NormalizeRegions(ordered, indexMap);
            // Annotation values can retain functions or deferred generators after
            // module slots leave. Keep their existing global namespace lifetime
            // conservatively; scalar-only declarations need no such mirror.
            var moduleAnnotationNamespace = _functionParameters is null &&
                _constants.Any(value => value is ExecutableAnnotationsEnabled) &&
                (_functions.Count != 0 || _constants.Any(value => value is ExecutableCreateLambda or ExecutableCreateGeneratorExpression));
            var requiresLocalVariableMirroring =
                moduleAnnotationNamespace || (_generator && generatorExpression is null) ||
                _statementFallbacks.Count != 0 ||
                _functions.Any(function => function.CodeObject.RequiresLocalVariableMirroring) ||
                _constants.OfType<ExecutableCreateLambda>().Any(lambda => lambda.CodeObject.RequiresLocalVariableMirroring) ||
                _constants.OfType<ExecutableCreateGeneratorExpression>().Any(generator => generator.CodeObject.RequiresLocalVariableMirroring);
            var capturedLocalSlots = CollectCapturedLocalSlots();

            var operandLayout = ExecutableStackLayout.Prepare(normalizedBlocks, _constants, _callSites,
                normalizedRegions, 0, generatorExpression is null ? 0 : 1);
            // Syntactic reachability retains handler tails even after an
            // unconditional raise. Such regions have no executable entry.
            normalizedRegions = normalizedRegions
                .Where(region => operandLayout.EntryDepths[region.ProtectedStartIndex].HasValue)
                .Select(region => region with
            {
                StackDepth = operandLayout.EntryDepths[region.ProtectedStartIndex]!.Value
            }).ToArray();

            // This builder is single-use; its completed slot maps become code metadata.
            return new ExecutableCodeObject(
                name,
                _constants.ToArray(),
                _names.ToArray(),
                _locals.ToArray(),
                _localIndexes,
                capturedLocalSlots,
                _closures.ToArray(),
                _closureIndexes,
                _scopeFacts,
                normalizedBlocks,
                0,
                normalizedRegions,
                _imports.ToArray(),
                _callSites.ToArray(),
                _memberCacheCount,
                _functions.ToArray(),
                _matchCases.ToArray(),
                _statementFallbacks.ToArray(),
                requiresLocalVariableMirroring,
                isFunctionScope: _functionParameters is not null,
                operandLayout: operandLayout);

            int[] CollectCapturedLocalSlots()
            {
                // No local-index short-circuit here: an intermediate def can own no
                // locals of its own (nested def names do not bind local slots) while
                // still needing to forward transitively free names into its closures;
                // skipping the pass-through below orphans grandchild captures.


                var captured = new HashSet<int>();
                foreach (var code in _functions.Select(function => function.CodeObject)
                    .Concat(_constants.OfType<ExecutableCreateLambda>().Select(lambda => lambda.CodeObject))
                    .Concat(_constants.OfType<ExecutableCreateGeneratorExpression>().Select(generator => generator.CodeObject)))
                {
                    if (code is null)
                    {
                        continue;
                    }

                    foreach (var closureName in code.ClosureNames)
                    {
                        CaptureName(closureName);
                    }
                }

                if (captured.Count == 0)
                {
                    return [];
                }

                var result = new int[captured.Count];
                var index = 0;
                foreach (var slot in captured.Order())
                {
                    result[index++] = slot;
                }

                return result;

                void CaptureName(string capturedName)
                {
                    if (_localIndexes.TryGetValue(capturedName, out var slot)) captured.Add(slot);
                    else if (_parentClosureCandidates.Contains(capturedName)) InternClosure(capturedName);
                }
            }
        }

    }
}
