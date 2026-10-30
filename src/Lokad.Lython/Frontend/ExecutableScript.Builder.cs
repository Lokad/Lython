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
        private readonly List<LoweredExpression> _expressionFallbacks = [];
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
            CollectLocals(statements);
            if (_functionParameters is not null)
            {
                // Binding anywhere in a function makes the name local throughout
                // that function, including bindings in unexecuted loop/try bodies.
                // Append after parameter collection to preserve argument slots.
                foreach (var localName in _scopeFacts.LocalNames.Order(StringComparer.Ordinal))
                    InternLocal(localName);
            }

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
                moduleAnnotationNamespace || (_generator && generatorExpression is null) || _expressionFallbacks.Count != 0 ||
                _statementFallbacks.Count != 0 ||
                _functions.Any(function => function.CodeObject.RequiresLocalVariableMirroring) ||
                _constants.OfType<ExecutableCreateLambda>().Any(lambda => lambda.CodeObject.RequiresLocalVariableMirroring) ||
                _constants.OfType<ExecutableCreateGeneratorExpression>().Any(generator => generator.CodeObject.RequiresLocalVariableMirroring);
            var capturedLocalSlots = CollectCapturedLocalSlots();

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
                _expressionFallbacks.ToArray(),
                requiresLocalVariableMirroring,
                isFunctionScope: _functionParameters is not null);

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

        private void CollectLocals(IReadOnlyList<LoweredStatement> statements)
        {
            foreach (var statement in statements)
            {
                switch (statement)
                {
                    case LoweredAssignmentStatement assignment:
                        CollectAssignmentLocals(assignment.Syntax);
                        break;
                    case LoweredIfStatement ifStatement:
                        CollectLocals(ifStatement.ThenStatements);
                        if (ifStatement.ElseStatements is not null)
                        {
                            CollectLocals(ifStatement.ElseStatements);
                        }
                        break;
                    case LoweredWhileStatement whileStatement:
                        CollectLocals(whileStatement.Body);
                        if (whileStatement.ElseStatements is not null)
                        {
                            CollectLocals(whileStatement.ElseStatements);
                        }
                        break;
                    case LoweredMatchStatement matchStatement:
                        foreach (var matchCase in matchStatement.Cases)
                        {
                            CollectPatternLocals(matchCase.Syntax.Pattern);
                            CollectLocals(matchCase.Body);
                        }
                        break;
                    case LoweredWithStatement withStatement:
                        foreach (var name in withStatement.Syntax.BoundNames)
                            if (IsLocalBindingName(name)) InternLocal(name);
                        CollectLocals(withStatement.Body);
                        break;
                }
            }
        }

        private void CollectAssignmentLocals(StatementSyntax syntax)
        {
            switch (syntax)
            {
                case AssignmentStatementSyntax assignment:
                    if (IsLocalBindingName(assignment.Name)) InternLocal(assignment.Name);
                    break;
                case AnnotatedAssignmentStatementSyntax annotated:
                    if (annotated.Target is NameAssignmentTargetSyntax targetName && IsLocalBindingName(targetName.Name)) InternLocal(targetName.Name);
                    break;
                case AugmentedAssignmentStatementSyntax { Target: NameAssignmentTargetSyntax nameTarget }:
                    if (IsLocalBindingName(nameTarget.Name)) InternLocal(nameTarget.Name);
                    break;
                case ChainedAssignmentStatementSyntax chained:
                    foreach (var target in chained.Targets)
                    {
                        if (target is NameAssignmentTargetSyntax name)
                        {
                            if (IsLocalBindingName(name.Name)) InternLocal(name.Name);
                        }
                    }
                    break;
            }
        }

        private void CollectPatternLocals(PatternSyntax pattern)
        {
            switch (pattern)
            {
                case MatchCapturePatternSyntax capture:
                    if (IsLocalBindingName(capture.Name)) InternLocal(capture.Name);
                    break;
                case MatchSequencePatternSyntax sequence:
                    foreach (var item in sequence.Items)
                    {
                        CollectPatternLocals(item);
                    }
                    break;
                case MatchMappingPatternSyntax mapping:
                    foreach (var item in mapping.Items)
                    {
                        CollectPatternLocals(item.Pattern);
                    }
                    if (mapping.RestName is not null)
                    {
                        if (IsLocalBindingName(mapping.RestName)) InternLocal(mapping.RestName);
                    }
                    break;
                case MatchClassPatternSyntax classPattern:
                    foreach (var item in classPattern.PositionalPatterns)
                    {
                        CollectPatternLocals(item);
                    }
                    foreach (var item in classPattern.KeywordPatterns)
                    {
                        CollectPatternLocals(item.Pattern);
                    }
                    break;
                case MatchStarPatternSyntax star when star.Name is not null:
                    if (IsLocalBindingName(star.Name)) InternLocal(star.Name);
                    break;
                case MatchAsPatternSyntax asPattern:
                    CollectPatternLocals(asPattern.Pattern);
                    if (IsLocalBindingName(asPattern.Name)) InternLocal(asPattern.Name);
                    break;
                case MatchOrPatternSyntax orPattern:
                    foreach (var item in orPattern.Patterns)
                    {
                        CollectPatternLocals(item);
                    }
                    break;
            }
        }
    }
}
