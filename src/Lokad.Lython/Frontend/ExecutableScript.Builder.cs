using Lokad.Lython.Runtime;

namespace Lokad.Lython.Frontend;

internal sealed partial class ExecutableScript
{
    private ExecutableScript(
        LoweredScript lowered,
        ExecutableCodeObject entryPoint)
    {
        Lowered = lowered;
        EntryPoint = entryPoint;
    }

    public LoweredScript Lowered { get; }

    public ExecutableCodeObject EntryPoint { get; }

    public static ExecutableScript Compile(LoweredScript lowered)
    {
        return new ExecutableScript(lowered, new Builder().CompileCodeObject("<module>", lowered.Statements));
    }

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
        private readonly List<ExecutableImportBinding> _imports = [];
        private readonly List<ExecutableCallSite> _callSites = [];
        private int _memberCacheCount;
        private int _callCacheCount;
        private readonly List<ExecutableLoopTargetBinding> _loopTargets = [];
        private readonly List<ExecutableUnpackingTargetBinding> _unpackingTargets = [];
        private readonly List<ExecutableFunctionBinding> _functions = [];
        private readonly List<ExecutableMatchCaseBinding> _matchCases = [];
        private readonly List<ExecutableStatementFallback> _statementFallbacks = [];
        private readonly List<ExecutableExpressionFallback> _expressionFallbacks = [];
        private readonly Stack<LoopContext> _loops = [];
        private int _syntheticLocalCounter;

        private readonly IReadOnlyList<LoweredFunctionParameter>? _functionParameters;
        private readonly HashSet<string> _parentClosureCandidates;
        private ScopeDirectiveFacts _scopeFacts = ScopeDirectiveFacts.Empty;

        internal Builder() : this(null, null) { }

        internal Builder(IReadOnlyList<LoweredFunctionParameter>? functionParameters) : this(functionParameters, null) { }

        internal Builder(
            IReadOnlyList<LoweredFunctionParameter>? functionParameters,
            IEnumerable<string>? parentClosureCandidates)
        {
            _functionParameters = functionParameters;
            _parentClosureCandidates = parentClosureCandidates is null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(parentClosureCandidates, StringComparer.Ordinal);
        }

        public ExecutableCodeObject CompileCodeObject(string name, IReadOnlyList<LoweredStatement> statements)
        {
            _scopeFacts = ScopeDirectiveFactsCollector.ForLoweredStatements(_functionParameters, statements);
            CollectLocals(statements);

            var entryBlock = CreateBlock();
            var exitBlock = CompileStatements(statements, entryBlock);
            if (exitBlock is int finalBlock && !IsTerminated(finalBlock))
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
            var requiresLocalVariableMirroring =
                _statementFallbacks.Count != 0 ||
                _functions.Any(function => function.CodeObject is null);
            var capturedLocalSlots = CollectCapturedLocalSlots();

            return new ExecutableCodeObject(
                name,
                _constants.ToArray(),
                _names.ToArray(),
                _locals.ToArray(),
                new Dictionary<string, int>(_localIndexes, StringComparer.Ordinal),
                capturedLocalSlots,
                _closures.ToArray(),
                new Dictionary<string, int>(_closureIndexes, StringComparer.Ordinal),
                _scopeFacts,
                normalizedBlocks,
                0,
                normalizedRegions,
                _imports.ToArray(),
                _callSites.ToArray(),
                _memberCacheCount,
                _callCacheCount,
                _loopTargets.ToArray(),
                _unpackingTargets.ToArray(),
                _functions.ToArray(),
                _matchCases.ToArray(),
                _statementFallbacks.ToArray(),
                _expressionFallbacks.ToArray(),
                requiresLocalVariableMirroring);

            int[] CollectCapturedLocalSlots()
            {
                if (_functions.Count == 0 || _localIndexes.Count == 0)
                {
                    return [];
                }

                var captured = new HashSet<int>();
                foreach (var function in _functions)
                {
                    if (function.CodeObject is null)
                    {
                        continue;
                    }

                    foreach (var closureName in function.CodeObject.ClosureNames)
                    {
                        if (_localIndexes.TryGetValue(closureName, out var slot))
                        {
                            captured.Add(slot);
                        }
                        else if (_parentClosureCandidates.Contains(closureName))
                        {
                            InternClosure(closureName);
                        }
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
            }
        }

        private void CollectLocals(IReadOnlyList<LoweredStatement> statements)
        {
            if (_functionParameters is not null)
            {
                foreach (var parameter in _functionParameters)
                {
                    if (parameter.Kind is FunctionParameterKind.Positional or FunctionParameterKind.KeywordOnly or FunctionParameterKind.VariadicList or FunctionParameterKind.VariadicDictionary)
                    {
                        if (IsLocalBindingName(parameter.Name))
                        {
                            InternLocal(parameter.Name);
                        }
                    }
                }
            }

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
                        if (withStatement.Syntax.VariableName is not null)
                        {
                            if (IsLocalBindingName(withStatement.Syntax.VariableName)) InternLocal(withStatement.Syntax.VariableName);
                        }
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
                    if (IsLocalBindingName(annotated.Name)) InternLocal(annotated.Name);
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
