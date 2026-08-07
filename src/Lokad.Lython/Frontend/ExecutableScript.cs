using System.Globalization;
using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Frontend;

// Signals that a valid lowered program must use the general interpreter instead of executable IR.
internal sealed class ExecutableLoweringFallbackException(string message) : Exception(message);

internal enum ExecutableOpCode
{
    Import,
    DefineFunction,
    ExecuteFallbackStatement,
    LoadConst,
    LoadLocal,
    LoadClosure,
    LoadGlobal,
    LoadName,
    EvaluateFallbackExpression,
    LoadMember,
    StoreLocal,
    StoreClosure,
    StoreGlobal,
    StoreName,
    Dup,
    PopTop,
    MakeList,
    MakeTuple,
    MakeSet,
    MakeDict,
    ResolveContextManager,
    EnterContextManager,
    ExitContextManager,
    MatchCase,
    GetIter,
    ForNext,
    AssignLoopTarget,
    AssignUnpackingTargets,
    Call,
    Subscript,
    Slice,
    Binary,
    Augmented,
    Unary,
    Jump,
    JumpIfFalse,
    ClearException,
    EndFinally,
    Return,
    ReturnNone,
}

internal enum ExecutableBinaryOperator
{
    Or,
    And,
    Add,
    Subtract,
    Multiply,
    Divide,
    FloorDivide,
    Modulo,
    Power,
    BitwiseOr,
    BitwiseXor,
    BitwiseAnd,
    LeftShift,
    RightShift,
    Equal,
    NotEqual,
    Less,
    LessEqual,
    Greater,
    GreaterEqual,
    Is,
    IsNot,
    In,
    NotIn,
}

internal enum ExecutableUnaryOperator
{
    Not,
    Plus,
    Minus,
    BitwiseNot,
}

internal enum ExecutableAugmentedOperator
{
    Add,
    Subtract,
    Multiply,
    Divide,
    FloorDivide,
    Modulo,
    Power,
    BitwiseOr,
    BitwiseXor,
    BitwiseAnd,
    LeftShift,
    RightShift,
}

[Flags]
internal enum ExecutableSliceParts
{
    None = 0,
    Start = 1,
    End = 2,
    Step = 4,
}

internal readonly record struct ExecutableInstruction
{
    private readonly int _primaryIndex;
    private readonly int _secondaryIndex;
    private readonly ExecutableBinaryOperator _binaryOperator;
    private readonly ExecutableUnaryOperator _unaryOperator;
    private readonly ExecutableAugmentedOperator _augmentedOperator;
    private readonly ExecutableSliceParts _sliceParts;

    private ExecutableInstruction(
        ExecutableOpCode opCode,
        LythonSourceSpan span,
        int primaryIndex,
        int secondaryIndex,
        ExecutableBinaryOperator binaryOperator,
        ExecutableUnaryOperator unaryOperator,
        ExecutableAugmentedOperator augmentedOperator,
        ExecutableSliceParts sliceParts)
    {
        OpCode = opCode;
        Span = span;
        _primaryIndex = primaryIndex;
        _secondaryIndex = secondaryIndex;
        _binaryOperator = binaryOperator;
        _unaryOperator = unaryOperator;
        _augmentedOperator = augmentedOperator;
        _sliceParts = sliceParts;
    }

    public ExecutableOpCode OpCode { get; }

    public LythonSourceSpan Span { get; }

    // Named projections keep the VM independent of the compact, index-based storage layout.
    public int ImportIndex => _primaryIndex;
    public int FunctionIndex => _primaryIndex;
    public int StatementFallbackIndex => _primaryIndex;
    public int ConstantIndex => _primaryIndex;
    public int LocalSlot => _primaryIndex;
    public int ClosureSlot => _primaryIndex;
    public int NameIndex => _primaryIndex;
    public int ExpressionFallbackIndex => _primaryIndex;
    public int MemberCacheIndex => _secondaryIndex;
    public int ItemCount => _primaryIndex;
    public int PairCount => _primaryIndex;
    public int MatchCaseIndex => _primaryIndex;
    public int FailureBlockIndex => _secondaryIndex;
    public int TargetBlockIndex => _primaryIndex;
    public int LoopTargetIndex => _primaryIndex;
    public int UnpackingTargetIndex => _primaryIndex;
    public int CallSiteIndex => _primaryIndex;
    public int CallCacheIndex => _secondaryIndex;
    public int ExceptionNameIndex => _primaryIndex;
    public ExecutableSliceParts SliceParts => _sliceParts;
    public ExecutableBinaryOperator BinaryOperator => _binaryOperator;
    public ExecutableUnaryOperator UnaryOperator => _unaryOperator;
    public ExecutableAugmentedOperator AugmentedOperator => _augmentedOperator;

    public static ExecutableInstruction Import(int importIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.Import, importIndex, span);

    public static ExecutableInstruction DefineFunction(int functionIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.DefineFunction, functionIndex, span);

    public static ExecutableInstruction ExecuteFallbackStatement(int statementIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.ExecuteFallbackStatement, statementIndex, span);

    public static ExecutableInstruction LoadConst(int constantIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.LoadConst, constantIndex, span);

    public static ExecutableInstruction LoadLocal(int slot, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.LoadLocal, slot, span);

    public static ExecutableInstruction LoadClosure(int slot, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.LoadClosure, slot, span);

    public static ExecutableInstruction LoadGlobal(int nameIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.LoadGlobal, nameIndex, span);

    public static ExecutableInstruction LoadName(int nameIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.LoadName, nameIndex, span);

    public static ExecutableInstruction EvaluateFallbackExpression(int expressionIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.EvaluateFallbackExpression, expressionIndex, span);

    public static ExecutableInstruction LoadMember(int nameIndex, int cacheIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.LoadMember, nameIndex, cacheIndex, span);

    public static ExecutableInstruction StoreLocal(int slot, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.StoreLocal, slot, span);

    public static ExecutableInstruction StoreClosure(int slot, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.StoreClosure, slot, span);

    public static ExecutableInstruction StoreGlobal(int nameIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.StoreGlobal, nameIndex, span);

    public static ExecutableInstruction StoreName(int nameIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.StoreName, nameIndex, span);

    public static ExecutableInstruction Dup(LythonSourceSpan span)
        => Create(ExecutableOpCode.Dup, span);

    public static ExecutableInstruction PopTop(LythonSourceSpan span)
        => Create(ExecutableOpCode.PopTop, span);

    public static ExecutableInstruction MakeList(int count, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.MakeList, count, span);

    public static ExecutableInstruction MakeTuple(int count, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.MakeTuple, count, span);

    public static ExecutableInstruction MakeSet(int count, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.MakeSet, count, span);

    public static ExecutableInstruction MakeDict(int pairCount, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.MakeDict, pairCount, span);

    public static ExecutableInstruction ResolveContextManager(LythonSourceSpan span)
        => Create(ExecutableOpCode.ResolveContextManager, span);

    public static ExecutableInstruction EnterContextManager(LythonSourceSpan span)
        => Create(ExecutableOpCode.EnterContextManager, span);

    public static ExecutableInstruction ExitContextManager(LythonSourceSpan span)
        => Create(ExecutableOpCode.ExitContextManager, span);

    public static ExecutableInstruction MatchCase(int caseIndex, int failBlockIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.MatchCase, caseIndex, failBlockIndex, span);

    public static ExecutableInstruction GetIter(LythonSourceSpan span)
        => Create(ExecutableOpCode.GetIter, span);

    public static ExecutableInstruction ForNext(int targetBlockIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.ForNext, targetBlockIndex, span);

    public static ExecutableInstruction AssignLoopTarget(int loopTargetIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.AssignLoopTarget, loopTargetIndex, span);

    public static ExecutableInstruction AssignUnpackingTargets(int unpackingTargetIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.AssignUnpackingTargets, unpackingTargetIndex, span);

    public static ExecutableInstruction Call(int callSiteIndex, int cacheIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.Call, callSiteIndex, cacheIndex, span);

    public static ExecutableInstruction Subscript(LythonSourceSpan span)
        => Create(ExecutableOpCode.Subscript, span);

    public static ExecutableInstruction Slice(ExecutableSliceParts parts, LythonSourceSpan span)
        => new(ExecutableOpCode.Slice, span, 0, 0, default, default, default, parts);

    public static ExecutableInstruction Binary(ExecutableBinaryOperator op, LythonSourceSpan span)
        => new(ExecutableOpCode.Binary, span, 0, 0, op, default, default, default);

    public static ExecutableInstruction Augmented(ExecutableAugmentedOperator op, LythonSourceSpan span)
        => new(ExecutableOpCode.Augmented, span, 0, 0, default, default, op, default);

    public static ExecutableInstruction Unary(ExecutableUnaryOperator op, LythonSourceSpan span)
        => new(ExecutableOpCode.Unary, span, 0, 0, default, op, default, default);

    public static ExecutableInstruction Jump(int targetBlockIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.Jump, targetBlockIndex, span);

    public static ExecutableInstruction JumpIfFalse(int targetBlockIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.JumpIfFalse, targetBlockIndex, span);

    public static ExecutableInstruction ClearException(int exceptionNameIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.ClearException, exceptionNameIndex, span);

    public static ExecutableInstruction EndFinally(int targetBlockIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.EndFinally, targetBlockIndex, span);

    public static ExecutableInstruction Return(LythonSourceSpan span)
        => Create(ExecutableOpCode.Return, span);

    public static ExecutableInstruction ReturnNone(LythonSourceSpan span)
        => Create(ExecutableOpCode.ReturnNone, span);

    public ExecutableInstruction WithTargetBlockIndex(int targetBlockIndex)
        => OpCode switch
        {
            ExecutableOpCode.Jump => Jump(targetBlockIndex, Span),
            ExecutableOpCode.JumpIfFalse => JumpIfFalse(targetBlockIndex, Span),
            ExecutableOpCode.ForNext => ForNext(targetBlockIndex, Span),
            ExecutableOpCode.EndFinally => EndFinally(targetBlockIndex, Span),
            _ => throw new InvalidOperationException($"Instruction '{OpCode}' has no target block.")
        };

    public ExecutableInstruction WithFailureBlockIndex(int failureBlockIndex)
        => OpCode == ExecutableOpCode.MatchCase
            ? MatchCase(MatchCaseIndex, failureBlockIndex, Span)
            : throw new InvalidOperationException($"Instruction '{OpCode}' has no failure block.");

    private static ExecutableInstruction Create(ExecutableOpCode opCode, LythonSourceSpan span)
        => new(opCode, span, 0, 0, default, default, default, default);

    private static ExecutableInstruction CreateIndexed(ExecutableOpCode opCode, int primaryIndex, LythonSourceSpan span)
        => new(opCode, span, primaryIndex, 0, default, default, default, default);

    private static ExecutableInstruction CreateIndexed(ExecutableOpCode opCode, int primaryIndex, int secondaryIndex, LythonSourceSpan span)
        => new(opCode, span, primaryIndex, secondaryIndex, default, default, default, default);
}

internal sealed record ExecutableBasicBlock(
    int Index,
    IReadOnlyList<ExecutableInstruction> Instructions);

internal sealed record ExecutableExceptionRegion(
    int ProtectedStartBlockIndex,
    int ProtectedEndBlockIndex,
    IReadOnlyList<string>? ExceptionTypeNames,
    string? ExceptionVariableName,
    int? ExceptBlockIndex,
    int? FinallyBlockIndex);

internal sealed record ExecutableImportBinding(
    string ModuleName,
    string BindingName,
    string BoundModuleName,
    IReadOnlyList<ImportedMemberSyntax>? ImportedMembers,
    LythonSourceSpan Span);

internal sealed record ExecutableCallArgumentSpec(
    CallArgumentForm Form)
{
    public CallArgumentKind Kind => Form.Kind;

    public string KeywordName => Form.KeywordName;
}

internal sealed record ExecutableCallSite(
    int ArgumentCount,
    IReadOnlyList<ExecutableCallArgumentSpec> Arguments,
    LythonSourceSpan TargetSpan,
    LythonSourceSpan CallSpan);

internal sealed record ExecutableLoopTargetBinding(
    LoopTargetSyntax Target,
    LythonSourceSpan Span);

internal sealed record ExecutableUnpackingTargetBinding(
    IReadOnlyList<UnpackingTargetSyntax> Targets,
    LythonSourceSpan Span);

internal sealed record ExecutableFunctionBinding(
    LoweredFunctionDefinitionStatement Function,
    ExecutableCodeObject? CodeObject,
    IReadOnlyDictionary<string, LoweredExpression> DefaultValues);

internal sealed record ExecutableMatchCaseBinding(
    MatchCaseSyntax Case,
    IReadOnlyDictionary<string, int> LocalBindingSlots);

internal sealed record ExecutableStatementFallback(
    LoweredStatement Statement);

internal sealed record ExecutableExpressionFallback(
    LoweredExpression Expression);

internal sealed class ExecutableCodeObject
{
    public ExecutableCodeObject(
        string name,
        IReadOnlyList<object?> constants,
        IReadOnlyList<string> names,
        IReadOnlyList<string> localNames,
        IReadOnlyDictionary<string, int> localNameToSlot,
        IReadOnlyList<int> capturedLocalSlots,
        IReadOnlyList<string> closureNames,
        IReadOnlyDictionary<string, int> closureNameToSlot,
        ScopeDirectiveFacts scopeFacts,
        IReadOnlyList<ExecutableBasicBlock> blocks,
        int entryBlockIndex,
        IReadOnlyList<ExecutableExceptionRegion> exceptionRegions,
        IReadOnlyList<ExecutableImportBinding> imports,
        IReadOnlyList<ExecutableCallSite> callSites,
        int memberCacheCount,
        int callCacheCount,
        IReadOnlyList<ExecutableLoopTargetBinding> loopTargets,
        IReadOnlyList<ExecutableUnpackingTargetBinding> unpackingTargets,
        IReadOnlyList<ExecutableFunctionBinding> functions,
        IReadOnlyList<ExecutableMatchCaseBinding> matchCases,
        IReadOnlyList<ExecutableStatementFallback> statementFallbacks,
        IReadOnlyList<ExecutableExpressionFallback> expressionFallbacks,
        bool requiresLocalVariableMirroring)
    {
        Name = name;
        Constants = constants;
        Names = names;
        LocalNames = localNames;
        LocalNameToSlot = localNameToSlot;
        CapturedLocalSlots = capturedLocalSlots;
        ClosureNames = closureNames;
        ClosureNameToSlot = closureNameToSlot;
        ScopeFacts = scopeFacts;
        Blocks = blocks;
        EntryBlockIndex = entryBlockIndex;
        ExceptionRegions = exceptionRegions;
        Imports = imports;
        CallSites = callSites;
        MemberCacheCount = memberCacheCount;
        CallCacheCount = callCacheCount;
        LoopTargets = loopTargets;
        UnpackingTargets = unpackingTargets;
        Functions = functions;
        MatchCases = matchCases;
        StatementFallbacks = statementFallbacks;
        ExpressionFallbacks = expressionFallbacks;
        RequiresLocalVariableMirroring = requiresLocalVariableMirroring;
    }

    public string Name { get; }

    public IReadOnlyList<object?> Constants { get; }

    public IReadOnlyList<string> Names { get; }

    public IReadOnlyList<string> LocalNames { get; }

    public IReadOnlyDictionary<string, int> LocalNameToSlot { get; }

    public IReadOnlyList<int> CapturedLocalSlots { get; }

    public IReadOnlyList<string> ClosureNames { get; }

    public IReadOnlyDictionary<string, int> ClosureNameToSlot { get; }

    public ScopeDirectiveFacts ScopeFacts { get; }

    public IReadOnlyList<ExecutableBasicBlock> Blocks { get; }

    public int EntryBlockIndex { get; }

    public IReadOnlyList<ExecutableExceptionRegion> ExceptionRegions { get; }

    public IReadOnlyList<ExecutableImportBinding> Imports { get; }

    public IReadOnlyList<ExecutableCallSite> CallSites { get; }

    public int MemberCacheCount { get; }

    public int CallCacheCount { get; }

    public IReadOnlyList<ExecutableLoopTargetBinding> LoopTargets { get; }

    public IReadOnlyList<ExecutableUnpackingTargetBinding> UnpackingTargets { get; }

    public IReadOnlyList<ExecutableFunctionBinding> Functions { get; }

    public IReadOnlyList<ExecutableMatchCaseBinding> MatchCases { get; }

    public IReadOnlyList<ExecutableStatementFallback> StatementFallbacks { get; }

    public IReadOnlyList<ExecutableExpressionFallback> ExpressionFallbacks { get; }

    public bool RequiresLocalVariableMirroring { get; }
}

internal sealed class ExecutableScript
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

    private sealed class Builder
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
            var normalizedRegions = NormalizeRegions(indexMap);
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
        }

        private int[] CollectCapturedLocalSlots()
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

        private int? CompileStatements(IReadOnlyList<LoweredStatement> statements, int entryBlock)
        {
            int? current = entryBlock;
            foreach (var statement in statements)
            {
                if (current is null)
                {
                    return null;
                }

                current = CompileStatement(statement, current.Value);
            }

            return current;
        }

        private int? CompileStatement(LoweredStatement statement, int currentBlock)
        {
            switch (statement)
            {
                case LoweredImportStatement importStatement:
                    AddInstruction(currentBlock, ExecutableInstruction.Import(InternImport(importStatement), importStatement.Span));
                    return currentBlock;

                case LoweredScopeDirectiveStatement:
                    return currentBlock;

                case LoweredFunctionDefinitionStatement functionDefinition:
                    AddInstruction(currentBlock, ExecutableInstruction.DefineFunction(InternFunction(functionDefinition), functionDefinition.Span));
                    return currentBlock;

                case LoweredAssignmentStatement assignment:
                    return CompileAssignmentStatement(assignment, currentBlock);

                case LoweredExpressionStatement expressionStatement:
                    CompileExpression(expressionStatement.Expression, currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.PopTop(expressionStatement.Span));
                    return currentBlock;

                case LoweredIfStatement ifStatement:
                    return CompileIfStatement(ifStatement, currentBlock);

                case LoweredForStatement forStatement:
                    return CompileForStatement(forStatement, currentBlock);

                case LoweredWhileStatement whileStatement:
                    return CompileWhileStatement(whileStatement, currentBlock);

                case LoweredMatchStatement matchStatement:
                    return CompileMatchStatement(matchStatement, currentBlock);

                case LoweredWithStatement withStatement:
                    return CompileWithStatement(withStatement, currentBlock);

                case LoweredTryStatement tryStatement:
                    return CompileTryStatement(tryStatement, currentBlock);

                case LoweredPassStatement:
                    return currentBlock;

                case LoweredBreakStatement breakStatement:
                    return CompileBreakStatement(breakStatement, currentBlock);

                case LoweredContinueStatement continueStatement:
                    return CompileContinueStatement(continueStatement, currentBlock);

                case LoweredReturnStatement returnStatement:
                    if (returnStatement.Expression is null)
                    {
                        AddInstruction(currentBlock, ExecutableInstruction.ReturnNone(returnStatement.Span));
                    }
                    else
                    {
                        CompileExpression(returnStatement.Expression, currentBlock);
                        AddInstruction(currentBlock, ExecutableInstruction.Return(returnStatement.Span));
                    }

                    return null;

                default:
                    AddInstruction(currentBlock, ExecutableInstruction.ExecuteFallbackStatement(InternStatementFallback(statement), statement.Span));
                    return currentBlock;
            }
        }

        private int CompileAssignmentStatement(LoweredAssignmentStatement assignment, int currentBlock)
        {
            switch (assignment.Syntax)
            {
                case AssignmentStatementSyntax simple:
                    CompileExpression(assignment.Expression.RequireNotNull(), currentBlock);
                    CompileStoreBoundName(simple.Name, assignment.Span, currentBlock);
                    return currentBlock;

                case AnnotatedAssignmentStatementSyntax annotated:
                    if (assignment.Expression is null)
                    {
                        throw new ExecutableLoweringFallbackException($"Executable IR lowering does not support annotation-only assignments: {assignment.Syntax.GetType().Name}.");
                    }

                    CompileExpression(assignment.Expression, currentBlock);
                    CompileStoreBoundName(annotated.Name, assignment.Span, currentBlock);
                    return currentBlock;

                case AugmentedAssignmentStatementSyntax augmented:
                    if (augmented.Target is not NameAssignmentTargetSyntax augmentedName)
                    {
                        AddInstruction(currentBlock, ExecutableInstruction.ExecuteFallbackStatement(InternStatementFallback(assignment), assignment.Span));
                        return currentBlock;
                    }

                    CompileLoadIdentifier(augmentedName.Name, assignment.Span, currentBlock);
                    CompileExpression(assignment.Expression.RequireNotNull(), currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.Augmented(MapAugmentedAssignmentOperator(augmented.Operator), assignment.Span));
                    CompileStoreBoundName(augmentedName.Name, assignment.Span, currentBlock);
                    return currentBlock;

                case ChainedAssignmentStatementSyntax chained:
                    CompileExpression(assignment.Expression.RequireNotNull(), currentBlock);
                    for (var i = 0; i < chained.Targets.Count; i++)
                    {
                        if (chained.Targets[i] is not NameAssignmentTargetSyntax name)
                        {
                            throw new ExecutableLoweringFallbackException($"Executable IR lowering does not support chained assignment target {chained.Targets[i].GetType().Name}.");
                        }

                        if (i < chained.Targets.Count - 1)
                        {
                            AddInstruction(currentBlock, ExecutableInstruction.Dup(assignment.Span));
                        }

                        CompileStoreBoundName(name.Name, assignment.Span, currentBlock);
                    }
                    return currentBlock;

                case UnpackingAssignmentStatementSyntax unpacking:
                    CompileExpression(assignment.Expression.RequireNotNull(), currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.AssignUnpackingTargets(InternUnpackingTargets(unpacking.Targets, assignment.Span), assignment.Span));
                    return currentBlock;

                default:
                    AddInstruction(currentBlock, ExecutableInstruction.ExecuteFallbackStatement(InternStatementFallback(assignment), assignment.Span));
                    return currentBlock;
            }
        }

        private int CompileForStatement(LoweredForStatement statement, int currentBlock)
        {
            CompileExpression(statement.Iterable, currentBlock);
            AddInstruction(currentBlock, ExecutableInstruction.GetIter(statement.Iterable.Span));

            var headBlock = CreateBlock();
            var bodyBlock = CreateBlock();
            var elseBlock = statement.ElseStatements is null ? -1 : CreateBlock();
            var exitBlock = CreateBlock();

            AddInstruction(currentBlock, ExecutableInstruction.Jump(headBlock, statement.Span));

            AddInstruction(headBlock, ExecutableInstruction.ForNext(statement.ElseStatements is null ? exitBlock : elseBlock, statement.Iterable.Span));
            AddInstruction(headBlock, ExecutableInstruction.AssignLoopTarget(InternLoopTarget(statement.Syntax.Target, statement.Span), statement.Span));
            AddInstruction(headBlock, ExecutableInstruction.Jump(bodyBlock, statement.Span));

            _loops.Push(new LoopContext(headBlock, exitBlock));
            try
            {
                var bodyExit = CompileStatements(statement.Body, bodyBlock);
                if (bodyExit is int bodyBlockExit && !IsTerminated(bodyBlockExit))
                {
                    AddInstruction(bodyBlockExit, ExecutableInstruction.Jump(headBlock, statement.Span));
                }
            }
            finally
            {
                _loops.Pop();
            }

            if (statement.ElseStatements is not null)
            {
                var elseExit = CompileStatements(statement.ElseStatements, elseBlock);
                if (elseExit is int elseBlockExit && !IsTerminated(elseBlockExit))
                {
                    AddInstruction(elseBlockExit, ExecutableInstruction.Jump(exitBlock, statement.Span));
                }
            }

            return exitBlock;
        }

        private int CompileTryStatement(LoweredTryStatement statement, int currentBlock)
        {
            var tryBlock = CreateBlock();
            AddInstruction(currentBlock, ExecutableInstruction.Jump(tryBlock, statement.Span));

            var protectedStart = tryBlock;
            var tryExit = CompileStatements(statement.TryBody, tryBlock);
            var protectedEnd = _blocks.Count - 1;

            var exceptBlock = -1;
            var exceptEnd = -1;
            int? exceptExit = null;
            if (statement.ExceptBody is not null)
            {
                exceptBlock = CreateBlock();
                exceptExit = CompileStatements(statement.ExceptBody, exceptBlock);
                exceptEnd = _blocks.Count - 1;
            }

            var elseBlock = -1;
            var elseEnd = -1;
            int? elseExit = null;
            if (statement.ElseBody is not null)
            {
                elseBlock = CreateBlock();
                elseExit = CompileStatements(statement.ElseBody, elseBlock);
                elseEnd = _blocks.Count - 1;
            }

            var finallyBlock = -1;
            int? finallyExit = null;
            if (statement.FinallyBody is not null)
            {
                finallyBlock = CreateBlock();
                finallyExit = CompileStatements(statement.FinallyBody, finallyBlock);
            }

            var afterBlock = CreateBlock();

            _regions.Add(new ExecutableExceptionRegion(
                protectedStart,
                protectedEnd,
                statement.Syntax.ExceptionTypeNames,
                statement.Syntax.ExceptionVariableName,
                statement.ExceptBody is null ? null : exceptBlock,
                statement.FinallyBody is null ? null : finallyBlock));

            if (tryExit is int tryBlockExit && !IsTerminated(tryBlockExit))
            {
                AddInstruction(tryBlockExit, ExecutableInstruction.Jump(
                    statement.ElseBody is not null ? elseBlock : statement.FinallyBody is not null ? finallyBlock : afterBlock,
                    statement.Span));
            }

            if (statement.ExceptBody is not null)
            {
                if (statement.FinallyBody is not null)
                {
                    _regions.Add(new ExecutableExceptionRegion(
                        exceptBlock,
                        exceptEnd,
                        null,
                        null,
                        null,
                        finallyBlock));
                }

                if (exceptExit is int exceptBlockExit && !IsTerminated(exceptBlockExit))
                {
                    AddInstruction(
                        exceptBlockExit,
                        ExecutableInstruction.ClearException(
                            statement.Syntax.ExceptionVariableName is null ? -1 : InternName(statement.Syntax.ExceptionVariableName),
                            statement.Span));
                    AddInstruction(exceptBlockExit, ExecutableInstruction.Jump(statement.FinallyBody is not null ? finallyBlock : afterBlock, statement.Span));
                }
            }

            if (statement.ElseBody is not null)
            {
                if (statement.FinallyBody is not null)
                {
                    _regions.Add(new ExecutableExceptionRegion(
                        elseBlock,
                        elseEnd,
                        null,
                        null,
                        null,
                        finallyBlock));
                }

                if (elseExit is int elseBlockExit && !IsTerminated(elseBlockExit))
                {
                    AddInstruction(elseBlockExit, ExecutableInstruction.Jump(statement.FinallyBody is not null ? finallyBlock : afterBlock, statement.Span));
                }
            }

            if (statement.FinallyBody is not null)
            {
                if (finallyExit is int finallyBlockExit && !IsTerminated(finallyBlockExit))
                {
                    AddInstruction(finallyBlockExit, ExecutableInstruction.EndFinally(afterBlock, statement.Span));
                }
            }

            return afterBlock;
        }

        private int CompileWithStatement(LoweredWithStatement statement, int currentBlock)
        {
            var managerSlot = InternSyntheticLocal("with_manager");

            CompileExpression(statement.ContextExpression, currentBlock);
            AddInstruction(currentBlock, ExecutableInstruction.ResolveContextManager(statement.ContextExpression.Span));
            AddInstruction(currentBlock, ExecutableInstruction.Dup(statement.Span));
            AddInstruction(currentBlock, ExecutableInstruction.StoreLocal(managerSlot, statement.Span));
            AddInstruction(currentBlock, ExecutableInstruction.EnterContextManager(statement.ContextExpression.Span));
            if (statement.Syntax.VariableName is not null)
            {
                CompileStoreBoundName(statement.Syntax.VariableName, statement.Span, currentBlock);
            }
            else
            {
                AddInstruction(currentBlock, ExecutableInstruction.PopTop(statement.Span));
            }

            var bodyBlock = CreateBlock();
            AddInstruction(currentBlock, ExecutableInstruction.Jump(bodyBlock, statement.Span));

            var protectedStart = bodyBlock;
            var bodyExit = CompileStatements(statement.Body, bodyBlock);
            var protectedEnd = _blocks.Count - 1;

            var finallyBlock = CreateBlock();
            var afterBlock = CreateBlock();

            _regions.Add(new ExecutableExceptionRegion(
                protectedStart,
                protectedEnd,
                null,
                null,
                null,
                finallyBlock));

            if (bodyExit is int bodyBlockExit && !IsTerminated(bodyBlockExit))
            {
                AddInstruction(bodyBlockExit, ExecutableInstruction.Jump(finallyBlock, statement.Span));
            }

            AddInstruction(finallyBlock, ExecutableInstruction.LoadLocal(managerSlot, statement.Span));
            AddInstruction(finallyBlock, ExecutableInstruction.ExitContextManager(statement.Span));
            AddInstruction(finallyBlock, ExecutableInstruction.EndFinally(afterBlock, statement.Span));

            return afterBlock;
        }

        private int CompileMatchStatement(LoweredMatchStatement statement, int currentBlock)
        {
            var subjectSlot = InternSyntheticLocal("match_subject");
            CompileExpression(statement.Subject, currentBlock);
            AddInstruction(currentBlock, ExecutableInstruction.StoreLocal(subjectSlot, statement.Subject.Span));

            var afterBlock = CreateBlock();
            var nextCaseBlock = CreateBlock();
            AddInstruction(currentBlock, ExecutableInstruction.Jump(nextCaseBlock, statement.Span));

            for (var i = 0; i < statement.Cases.Count; i++)
            {
                var matchCase = statement.Cases[i];
                var bodyBlock = CreateBlock();
                var failBlock = i == statement.Cases.Count - 1 ? afterBlock : CreateBlock();

                AddInstruction(nextCaseBlock, ExecutableInstruction.LoadLocal(subjectSlot, matchCase.Syntax.Span));
                AddInstruction(nextCaseBlock, ExecutableInstruction.MatchCase(InternMatchCase(matchCase.Syntax), failBlock, matchCase.Syntax.Span));
                AddInstruction(nextCaseBlock, ExecutableInstruction.Jump(bodyBlock, matchCase.Syntax.Span));

                var bodyExit = CompileStatements(matchCase.Body, bodyBlock);
                if (bodyExit is int bodyBlockExit && !IsTerminated(bodyBlockExit))
                {
                    AddInstruction(bodyBlockExit, ExecutableInstruction.Jump(afterBlock, matchCase.Syntax.Span));
                }

                nextCaseBlock = failBlock;
            }

            return afterBlock;
        }

        private int CompileIfStatement(LoweredIfStatement statement, int currentBlock)
        {
            CompileExpression(statement.Condition, currentBlock);

            var thenBlock = CreateBlock();
            var elseBlock = statement.ElseStatements is null ? -1 : CreateBlock();
            var joinBlock = CreateBlock();

            AddInstruction(currentBlock, ExecutableInstruction.JumpIfFalse(statement.ElseStatements is null ? joinBlock : elseBlock, statement.Condition.Span));
            AddInstruction(currentBlock, ExecutableInstruction.Jump(thenBlock, statement.Span));

            var thenExit = CompileStatements(statement.ThenStatements, thenBlock);
            if (thenExit is int thenBlockExit && !IsTerminated(thenBlockExit))
            {
                AddInstruction(thenBlockExit, ExecutableInstruction.Jump(joinBlock, statement.Span));
            }

            if (statement.ElseStatements is not null)
            {
                var elseExit = CompileStatements(statement.ElseStatements, elseBlock);
                if (elseExit is int elseBlockExit && !IsTerminated(elseBlockExit))
                {
                    AddInstruction(elseBlockExit, ExecutableInstruction.Jump(joinBlock, statement.Span));
                }
            }

            return joinBlock;
        }

        private int CompileWhileStatement(LoweredWhileStatement statement, int currentBlock)
        {
            var conditionBlock = CreateBlock();
            var bodyBlock = CreateBlock();
            var elseBlock = statement.ElseStatements is null ? -1 : CreateBlock();
            var exitBlock = CreateBlock();

            AddInstruction(currentBlock, ExecutableInstruction.Jump(conditionBlock, statement.Span));

            CompileExpression(statement.Condition, conditionBlock);
            AddInstruction(conditionBlock, ExecutableInstruction.JumpIfFalse(statement.ElseStatements is null ? exitBlock : elseBlock, statement.Condition.Span));
            AddInstruction(conditionBlock, ExecutableInstruction.Jump(bodyBlock, statement.Span));

            _loops.Push(new LoopContext(conditionBlock, exitBlock));
            try
            {
                var bodyExit = CompileStatements(statement.Body, bodyBlock);
                if (bodyExit is int bodyBlockExit && !IsTerminated(bodyBlockExit))
                {
                    AddInstruction(bodyBlockExit, ExecutableInstruction.Jump(conditionBlock, statement.Span));
                }
            }
            finally
            {
                _loops.Pop();
            }

            if (statement.ElseStatements is not null)
            {
                var elseExit = CompileStatements(statement.ElseStatements, elseBlock);
                if (elseExit is int elseBlockExit && !IsTerminated(elseBlockExit))
                {
                    AddInstruction(elseBlockExit, ExecutableInstruction.Jump(exitBlock, statement.Span));
                }
            }

            return exitBlock;
        }

        private int? CompileBreakStatement(LoweredBreakStatement statement, int currentBlock)
        {
            if (!_loops.TryPeek(out var loop))
            {
                throw new ExecutableLoweringFallbackException("Executable IR lowering cannot emit break outside a loop.");
            }

            AddInstruction(currentBlock, ExecutableInstruction.Jump(loop.BreakBlockIndex, statement.Span));
            return null;
        }

        private int? CompileContinueStatement(LoweredContinueStatement statement, int currentBlock)
        {
            if (!_loops.TryPeek(out var loop))
            {
                throw new ExecutableLoweringFallbackException("Executable IR lowering cannot emit continue outside a loop.");
            }

            AddInstruction(currentBlock, ExecutableInstruction.Jump(loop.ContinueBlockIndex, statement.Span));
            return null;
        }

        private void CompileExpression(LoweredExpression expression, int currentBlock)
        {
            switch (expression)
            {
                case LoweredParenthesizedExpression parenthesized:
                    CompileExpression(parenthesized.Inner, currentBlock);
                    return;

                case LoweredStringLiteralExpression text:
                    AddInstruction(currentBlock, ExecutableInstruction.LoadConst(InternConstant(PyString.FromString(text.Literal.Value)), text.Span));
                    return;

                case LoweredBytesLiteralExpression bytes:
                    AddInstruction(currentBlock, ExecutableInstruction.LoadConst(InternConstant(new PyBytes(bytes.Literal.Value.ToArray())), bytes.Span));
                    return;

                case LoweredIntegerLiteralExpression integer:
                    AddInstruction(currentBlock, ExecutableInstruction.LoadConst(InternConstant(PyNumberOps.ParseInteger(integer.Literal.ValueText)), integer.Span));
                    return;

                case LoweredFloatLiteralExpression floating:
                    AddInstruction(currentBlock, ExecutableInstruction.LoadConst(InternConstant(PyNumberOps.ParseFloat(floating.Literal.ValueText)), floating.Span));
                    return;

                case LoweredBooleanLiteralExpression boolean:
                    AddInstruction(currentBlock, ExecutableInstruction.LoadConst(InternConstant(boolean.Literal.Value), boolean.Span));
                    return;

                case LoweredNoneLiteralExpression none:
                    AddInstruction(currentBlock, ExecutableInstruction.LoadConst(InternConstant(PyNone.Instance), none.Span));
                    return;

                case LoweredIdentifierExpression identifier:
                    CompileLoadIdentifier(identifier.Identifier.Name, identifier.Span, currentBlock);
                    return;

                case LoweredMemberExpression member:
                    CompileExpression(member.Target, currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.LoadMember(InternName(member.Member.MemberName), AllocateMemberCache(), member.Span));
                    return;

                case LoweredCallExpression call:
                    if (!TryCompileCallExpression(call, currentBlock))
                    {
                        AddInstruction(currentBlock, ExecutableInstruction.EvaluateFallbackExpression(InternExpressionFallback(call), call.Span));
                    }
                    return;

                case LoweredListLiteralExpression list:
                    if (list.List.UnpackingFlags.Any(flag => flag))
                    {
                        AddInstruction(currentBlock, ExecutableInstruction.EvaluateFallbackExpression(InternExpressionFallback(list), list.Span));
                        return;
                    }

                    foreach (var item in list.Items)
                    {
                        CompileExpression(item, currentBlock);
                    }
                    AddInstruction(currentBlock, ExecutableInstruction.MakeList(list.Items.Count, list.Span));
                    return;

                case LoweredTupleLiteralExpression tuple:
                    if (tuple.Tuple.UnpackingFlags.Any(flag => flag))
                    {
                        AddInstruction(currentBlock, ExecutableInstruction.EvaluateFallbackExpression(InternExpressionFallback(tuple), tuple.Span));
                        return;
                    }

                    foreach (var item in tuple.Items)
                    {
                        CompileExpression(item, currentBlock);
                    }
                    AddInstruction(currentBlock, ExecutableInstruction.MakeTuple(tuple.Items.Count, tuple.Span));
                    return;

                case LoweredSetLiteralExpression set:
                    if (set.Set.UnpackingFlags.Any(flag => flag))
                    {
                        AddInstruction(currentBlock, ExecutableInstruction.EvaluateFallbackExpression(InternExpressionFallback(set), set.Span));
                        return;
                    }

                    foreach (var item in set.Items)
                    {
                        CompileExpression(item, currentBlock);
                    }
                    AddInstruction(currentBlock, ExecutableInstruction.MakeSet(set.Items.Count, set.Span));
                    return;

                case LoweredDictLiteralExpression dict:
                    if (dict.Items.Any(item => item.IsUnpacking))
                    {
                        AddInstruction(currentBlock, ExecutableInstruction.EvaluateFallbackExpression(InternExpressionFallback(dict), dict.Span));
                        return;
                    }

                    foreach (var item in dict.Items)
                    {
                        CompileExpression(item.Key, currentBlock);
                        CompileExpression(item.Value, currentBlock);
                    }
                    AddInstruction(currentBlock, ExecutableInstruction.MakeDict(dict.Items.Count, dict.Span));
                    return;

                case LoweredSubscriptExpression subscript:
                    CompileExpression(subscript.Target, currentBlock);
                    CompileExpression(subscript.Index, currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.Subscript(subscript.Span));
                    return;

                case LoweredSliceExpression slice:
                    CompileExpression(slice.Target, currentBlock);
                    var parts = ExecutableSliceParts.None;
                    if (slice.Start is not null)
                    {
                        CompileExpression(slice.Start, currentBlock);
                        parts |= ExecutableSliceParts.Start;
                    }
                    if (slice.End is not null)
                    {
                        CompileExpression(slice.End, currentBlock);
                        parts |= ExecutableSliceParts.End;
                    }
                    if (slice.Step is not null)
                    {
                        CompileExpression(slice.Step, currentBlock);
                        parts |= ExecutableSliceParts.Step;
                    }
                    AddInstruction(currentBlock, ExecutableInstruction.Slice(parts, slice.Span));
                    return;

                case LoweredBinaryExpression binary:
                    if (binary.Binary.Operator is BinaryOperatorSyntax.Or or BinaryOperatorSyntax.And)
                    {
                        AddInstruction(currentBlock, ExecutableInstruction.EvaluateFallbackExpression(InternExpressionFallback(binary), binary.Span));
                        return;
                    }

                    CompileExpression(binary.Left, currentBlock);
                    CompileExpression(binary.Right, currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.Binary(MapBinaryOperator(binary.Binary.Operator), binary.Span));
                    return;

                case LoweredUnaryExpression unary:
                    CompileExpression(unary.Operand, currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.Unary(MapUnaryOperator(unary.Unary.Operator), unary.Span));
                    return;

                default:
                    AddInstruction(currentBlock, ExecutableInstruction.EvaluateFallbackExpression(InternExpressionFallback(expression), expression.Span));
                    return;
            }
        }

        private bool TryCompileCallExpression(LoweredCallExpression call, int currentBlock)
        {
            foreach (var argument in call.Arguments)
            {
                if (argument.Kind is CallArgumentKind.StarredList or CallArgumentKind.StarredDictionary)
                {
                    return false;
                }
            }

            CompileExpression(call.Target, currentBlock);
            foreach (var argument in call.Arguments)
            {
                CompileExpression(argument.Expression, currentBlock);
            }

            AddInstruction(currentBlock, ExecutableInstruction.Call(InternCallSite(call), AllocateCallCache(), call.Span));
            return true;
        }

        private static ExecutableBinaryOperator MapBinaryOperator(BinaryOperatorSyntax op)
            => op switch
            {
                BinaryOperatorSyntax.Or => ExecutableBinaryOperator.Or,
                BinaryOperatorSyntax.And => ExecutableBinaryOperator.And,
                BinaryOperatorSyntax.Add => ExecutableBinaryOperator.Add,
                BinaryOperatorSyntax.Subtract => ExecutableBinaryOperator.Subtract,
                BinaryOperatorSyntax.Multiply => ExecutableBinaryOperator.Multiply,
                BinaryOperatorSyntax.Divide => ExecutableBinaryOperator.Divide,
                BinaryOperatorSyntax.FloorDivide => ExecutableBinaryOperator.FloorDivide,
                BinaryOperatorSyntax.Modulo => ExecutableBinaryOperator.Modulo,
                BinaryOperatorSyntax.Power => ExecutableBinaryOperator.Power,
                BinaryOperatorSyntax.BitwiseOr => ExecutableBinaryOperator.BitwiseOr,
                BinaryOperatorSyntax.BitwiseXor => ExecutableBinaryOperator.BitwiseXor,
                BinaryOperatorSyntax.BitwiseAnd => ExecutableBinaryOperator.BitwiseAnd,
                BinaryOperatorSyntax.LeftShift => ExecutableBinaryOperator.LeftShift,
                BinaryOperatorSyntax.RightShift => ExecutableBinaryOperator.RightShift,
                BinaryOperatorSyntax.Equal => ExecutableBinaryOperator.Equal,
                BinaryOperatorSyntax.NotEqual => ExecutableBinaryOperator.NotEqual,
                BinaryOperatorSyntax.Less => ExecutableBinaryOperator.Less,
                BinaryOperatorSyntax.LessEqual => ExecutableBinaryOperator.LessEqual,
                BinaryOperatorSyntax.Greater => ExecutableBinaryOperator.Greater,
                BinaryOperatorSyntax.GreaterEqual => ExecutableBinaryOperator.GreaterEqual,
                BinaryOperatorSyntax.Is => ExecutableBinaryOperator.Is,
                BinaryOperatorSyntax.IsNot => ExecutableBinaryOperator.IsNot,
                BinaryOperatorSyntax.In => ExecutableBinaryOperator.In,
                BinaryOperatorSyntax.NotIn => ExecutableBinaryOperator.NotIn,
                _ => throw new ExecutableLoweringFallbackException($"Executable IR lowering does not support binary operator {op}."),
            };

        private static ExecutableUnaryOperator MapUnaryOperator(UnaryOperatorSyntax op)
            => op switch
            {
                UnaryOperatorSyntax.Not => ExecutableUnaryOperator.Not,
                UnaryOperatorSyntax.Plus => ExecutableUnaryOperator.Plus,
                UnaryOperatorSyntax.Minus => ExecutableUnaryOperator.Minus,
                UnaryOperatorSyntax.BitwiseNot => ExecutableUnaryOperator.BitwiseNot,
                _ => throw new ExecutableLoweringFallbackException($"Executable IR lowering does not support unary operator {op}."),
            };

        private static ExecutableAugmentedOperator MapAugmentedAssignmentOperator(AugmentedAssignmentOperatorSyntax op)
            => op switch
            {
                AugmentedAssignmentOperatorSyntax.Add => ExecutableAugmentedOperator.Add,
                AugmentedAssignmentOperatorSyntax.Subtract => ExecutableAugmentedOperator.Subtract,
                AugmentedAssignmentOperatorSyntax.Multiply => ExecutableAugmentedOperator.Multiply,
                AugmentedAssignmentOperatorSyntax.Divide => ExecutableAugmentedOperator.Divide,
                AugmentedAssignmentOperatorSyntax.FloorDivide => ExecutableAugmentedOperator.FloorDivide,
                AugmentedAssignmentOperatorSyntax.Modulo => ExecutableAugmentedOperator.Modulo,
                AugmentedAssignmentOperatorSyntax.Power => ExecutableAugmentedOperator.Power,
                AugmentedAssignmentOperatorSyntax.BitwiseOr => ExecutableAugmentedOperator.BitwiseOr,
                AugmentedAssignmentOperatorSyntax.BitwiseXor => ExecutableAugmentedOperator.BitwiseXor,
                AugmentedAssignmentOperatorSyntax.BitwiseAnd => ExecutableAugmentedOperator.BitwiseAnd,
                AugmentedAssignmentOperatorSyntax.LeftShift => ExecutableAugmentedOperator.LeftShift,
                AugmentedAssignmentOperatorSyntax.RightShift => ExecutableAugmentedOperator.RightShift,
                _ => throw new ExecutableLoweringFallbackException($"Executable IR lowering does not support augmented assignment operator {op}."),
            };

        private int CreateBlock()
        {
            var index = _blocks.Count;
            _blocks.Add(new BasicBlockBuilder(index));
            return index;
        }

        private void AddInstruction(int blockIndex, ExecutableInstruction instruction)
        {
            _blocks[blockIndex].Instructions.Add(instruction);
        }

        private bool IsTerminated(int blockIndex)
        {
            var instructions = _blocks[blockIndex].Instructions;
            if (instructions.Count == 0)
            {
                return false;
            }

            return instructions[^1].OpCode is ExecutableOpCode.Jump or ExecutableOpCode.Return or ExecutableOpCode.ReturnNone;
        }

        private IReadOnlyList<ExecutableBasicBlock> NormalizeBlocks(IReadOnlyList<int> ordered, IReadOnlyDictionary<int, int> indexMap)
        {
            var normalized = new List<ExecutableBasicBlock>(ordered.Count);
            foreach (var originalIndex in ordered)
            {
                var rewritten = _blocks[originalIndex].Instructions
                    .Select(instruction => RewriteInstructionTargets(instruction, indexMap))
                    .ToArray();
                normalized.Add(new ExecutableBasicBlock(indexMap[originalIndex], rewritten));
            }

            return normalized;
        }

        private IReadOnlyList<ExecutableExceptionRegion> NormalizeRegions(IReadOnlyDictionary<int, int> indexMap)
        {
            return _regions
                .Select(region =>
                {
                    var protectedBlocks = indexMap
                        .Where(pair => pair.Key >= region.ProtectedStartBlockIndex && pair.Key <= region.ProtectedEndBlockIndex)
                        .Select(pair => pair.Value)
                        .Order()
                        .ToArray();
                    return protectedBlocks.Length == 0
                        ? null
                        : new ExecutableExceptionRegion(
                            protectedBlocks[0],
                            protectedBlocks[^1],
                            region.ExceptionTypeNames,
                            region.ExceptionVariableName,
                            region.ExceptBlockIndex is int exceptBlock ? indexMap[FinalJumpTarget(exceptBlock)] : null,
                            region.FinallyBlockIndex is int finallyBlock ? indexMap[FinalJumpTarget(finallyBlock)] : null);
                })
                .Where(region => region is not null)
                .Select(region => region.RequireNotNull())
                .ToArray();
        }

        private HashSet<int> CollectReachableBlocks(int entryBlockIndex)
        {
            var reachable = new HashSet<int>();
            var pending = new Stack<int>();
            pending.Push(entryBlockIndex);

            while (pending.Count > 0)
            {
                var blockIndex = pending.Pop();
                if (!reachable.Add(blockIndex))
                {
                    continue;
                }

                var block = _blocks[blockIndex];
                if (block.Instructions.Count == 0)
                {
                    if (blockIndex + 1 < _blocks.Count)
                    {
                        pending.Push(blockIndex + 1);
                    }
                    continue;
                }

                foreach (var instruction in block.Instructions)
                {
                    switch (instruction.OpCode)
                    {
                        case ExecutableOpCode.Jump:
                        case ExecutableOpCode.JumpIfFalse:
                        case ExecutableOpCode.ForNext:
                        case ExecutableOpCode.EndFinally:
                            pending.Push(FinalJumpTarget(instruction.TargetBlockIndex));
                            break;
                        case ExecutableOpCode.MatchCase:
                            pending.Push(FinalJumpTarget(instruction.FailureBlockIndex));
                            break;
                    }
                }

                foreach (var region in _regions)
                {
                    if (blockIndex < region.ProtectedStartBlockIndex || blockIndex > region.ProtectedEndBlockIndex)
                    {
                        continue;
                    }

                    if (region.ExceptBlockIndex is int exceptBlock)
                    {
                        pending.Push(exceptBlock);
                    }

                    if (region.FinallyBlockIndex is int finallyBlock)
                    {
                        pending.Push(finallyBlock);
                    }
                }

                var last = block.Instructions[^1];
                if (last.OpCode is not ExecutableOpCode.Jump and
                    not ExecutableOpCode.Return and
                    not ExecutableOpCode.ReturnNone &&
                    blockIndex + 1 < _blocks.Count)
                {
                    pending.Push(blockIndex + 1);
                }
            }

            return reachable;
        }

        private ExecutableInstruction RewriteInstructionTargets(ExecutableInstruction instruction, IReadOnlyDictionary<int, int> indexMap)
        {
            return instruction.OpCode switch
            {
                ExecutableOpCode.Jump or
                ExecutableOpCode.JumpIfFalse or
                ExecutableOpCode.ForNext or
                ExecutableOpCode.EndFinally => instruction.WithTargetBlockIndex(indexMap[FinalJumpTarget(instruction.TargetBlockIndex)]),
                ExecutableOpCode.MatchCase => instruction.WithFailureBlockIndex(indexMap[FinalJumpTarget(instruction.FailureBlockIndex)]),
                _ => instruction
            };
        }

        private int FinalJumpTarget(int blockIndex)
        {
            var seen = new HashSet<int>();
            var current = blockIndex;
            while (seen.Add(current))
            {
                var instructions = _blocks[current].Instructions;
                if (instructions.Count != 1 || instructions[0].OpCode != ExecutableOpCode.Jump)
                {
                    break;
                }

                current = instructions[0].TargetBlockIndex;
            }

            return current;
        }

        private int InternLocal(string name)
        {
            if (_localIndexes.TryGetValue(name, out var index))
            {
                return index;
            }

            index = _locals.Count;
            _locals.Add(name);
            _localIndexes.Add(name, index);
            return index;
        }

        private bool IsLocalBindingName(string name)
            => !_scopeFacts.IsGlobal(name) && !_scopeFacts.IsNonlocal(name);

        private void CompileLoadIdentifier(string name, LythonSourceSpan span, int currentBlock)
        {
            if (_scopeFacts.IsGlobal(name))
            {
                AddInstruction(currentBlock, ExecutableInstruction.LoadGlobal(InternName(name), span));
            }
            else if (_localIndexes.TryGetValue(name, out var slot))
            {
                AddInstruction(currentBlock, ExecutableInstruction.LoadLocal(slot, span));
            }
            else if (_scopeFacts.IsNonlocal(name) || _parentClosureCandidates.Contains(name))
            {
                AddInstruction(currentBlock, ExecutableInstruction.LoadClosure(InternClosure(name), span));
            }
            else
            {
                AddInstruction(currentBlock, ExecutableInstruction.LoadName(InternName(name), span));
            }
        }

        private void CompileStoreBoundName(string name, LythonSourceSpan span, int currentBlock)
        {
            if (_scopeFacts.IsGlobal(name))
            {
                AddInstruction(currentBlock, ExecutableInstruction.StoreGlobal(InternName(name), span));
                return;
            }

            if (_scopeFacts.IsNonlocal(name))
            {
                AddInstruction(currentBlock, ExecutableInstruction.StoreClosure(InternClosure(name), span));
                return;
            }

            AddInstruction(currentBlock, ExecutableInstruction.StoreLocal(InternLocal(name), span));
        }

        private int InternSyntheticLocal(string prefix) => InternLocal($"<{prefix}:{_syntheticLocalCounter++}>");

        private int InternClosure(string name)
        {
            if (_closureIndexes.TryGetValue(name, out var index))
            {
                return index;
            }

            index = _closures.Count;
            _closures.Add(name);
            _closureIndexes.Add(name, index);
            return index;
        }

        private int InternName(string name)
        {
            if (_nameIndexes.TryGetValue(name, out var index))
            {
                return index;
            }

            index = _names.Count;
            _names.Add(name);
            _nameIndexes.Add(name, index);
            return index;
        }

        private int InternConstant(object? value)
        {
            if (value is null)
            {
                if (_nullConstantIndex is { } existingNull)
                {
                    return existingNull;
                }

                var nullIndex = _constants.Count;
                _constants.Add(null);
                _nullConstantIndex = nullIndex;
                return nullIndex;
            }

            if (_constantIndexes.TryGetValue(value, out var existing))
            {
                return existing;
            }

            var index = _constants.Count;
            _constants.Add(value);
            _constantIndexes.Add(value, index);
            return index;
        }

        private int InternImport(LoweredImportStatement importStatement)
        {
            _imports.Add(new ExecutableImportBinding(
                importStatement.Syntax.ModuleName,
                importStatement.Syntax.BindingName,
                importStatement.Syntax.BoundModuleName,
                importStatement.Syntax.ImportedMembers,
                importStatement.Span));
            return _imports.Count - 1;
        }

        private int AllocateMemberCache() => _memberCacheCount++;

        private int AllocateCallCache() => _callCacheCount++;

        private int InternCallSite(LoweredCallExpression call)
        {
            _callSites.Add(new ExecutableCallSite(
                call.Arguments.Count,
                call.Arguments.Select(argument => new ExecutableCallArgumentSpec(argument.Form)).ToArray(),
                call.Target.Span,
                call.Span));
            return _callSites.Count - 1;
        }

        private int InternLoopTarget(LoopTargetSyntax target, LythonSourceSpan span)
        {
            _loopTargets.Add(new ExecutableLoopTargetBinding(target, span));
            return _loopTargets.Count - 1;
        }

        private int InternUnpackingTargets(IReadOnlyList<UnpackingTargetSyntax> targets, LythonSourceSpan span)
        {
            _unpackingTargets.Add(new ExecutableUnpackingTargetBinding(targets, span));
            return _unpackingTargets.Count - 1;
        }

        private int InternFunction(LoweredFunctionDefinitionStatement functionDefinition)
        {
            ExecutableCodeObject? codeObject;
            try
            {
                codeObject = new Builder(
                    functionDefinition.Parameters,
                    _functionParameters is null ? null : _locals.Concat(_closures))
                    .CompileCodeObject(functionDefinition.Syntax.Name, functionDefinition.Body);
            }
            catch (ExecutableLoweringFallbackException)
            {
                codeObject = null;
            }

            var defaultValues = functionDefinition.Parameters
                .Where(parameter => parameter.DefaultValue is not null)
                .ToDictionary(
                    parameter => parameter.Name,
                    parameter => parameter.DefaultValue.RequireNotNull(),
                    StringComparer.Ordinal);

            _functions.Add(new ExecutableFunctionBinding(functionDefinition, codeObject, defaultValues));
            return _functions.Count - 1;
        }

        private int InternMatchCase(MatchCaseSyntax matchCase)
        {
            var localBindings = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var name in EnumeratePatternBindingNames(matchCase.Pattern))
            {
                if (_localIndexes.TryGetValue(name, out var slot))
                {
                    localBindings[name] = slot;
                }
            }

            _matchCases.Add(new ExecutableMatchCaseBinding(matchCase, localBindings));
            return _matchCases.Count - 1;
        }

        private int InternStatementFallback(LoweredStatement statement)
        {
            _statementFallbacks.Add(new ExecutableStatementFallback(statement));
            return _statementFallbacks.Count - 1;
        }

        private int InternExpressionFallback(LoweredExpression expression)
        {
            _expressionFallbacks.Add(new ExecutableExpressionFallback(expression));
            return _expressionFallbacks.Count - 1;
        }

        private sealed class ConstantValueComparer : IEqualityComparer<object>
        {
            public static readonly ConstantValueComparer Instance = new();

            public new bool Equals(object? left, object? right)
            {
                if (left is byte[] leftBytes && right is byte[] rightBytes)
                {
                    return leftBytes.AsSpan().SequenceEqual(rightBytes);
                }

                return object.Equals(left, right);
            }

            public int GetHashCode(object value)
            {
                if (value is not byte[] bytes)
                {
                    return value.GetHashCode();
                }

                var hash = new HashCode();
                foreach (var item in bytes)
                {
                    hash.Add(item);
                }

                return hash.ToHashCode();
            }
        }

        private static IEnumerable<string> EnumeratePatternBindingNames(PatternSyntax pattern)
        {
            switch (pattern)
            {
                case MatchCapturePatternSyntax capture:
                    yield return capture.Name;
                    yield break;
                case MatchSequencePatternSyntax sequence:
                    foreach (var item in sequence.Items.SelectMany(EnumeratePatternBindingNames))
                    {
                        yield return item;
                    }
                    yield break;
                case MatchMappingPatternSyntax mapping:
                    foreach (var item in mapping.Items.SelectMany(pair => EnumeratePatternBindingNames(pair.Pattern)))
                    {
                        yield return item;
                    }
                    if (mapping.RestName is not null)
                    {
                        yield return mapping.RestName;
                    }
                    yield break;
                case MatchClassPatternSyntax classPattern:
                    foreach (var item in classPattern.PositionalPatterns.SelectMany(EnumeratePatternBindingNames))
                    {
                        yield return item;
                    }
                    foreach (var item in classPattern.KeywordPatterns.SelectMany(pair => EnumeratePatternBindingNames(pair.Pattern)))
                    {
                        yield return item;
                    }
                    yield break;
                case MatchStarPatternSyntax star when star.Name is not null:
                    yield return star.Name;
                    yield break;
                case MatchAsPatternSyntax asPattern:
                    foreach (var item in EnumeratePatternBindingNames(asPattern.Pattern))
                    {
                        yield return item;
                    }
                    yield return asPattern.Name;
                    yield break;
                case MatchOrPatternSyntax orPattern:
                    foreach (var item in orPattern.Patterns.SelectMany(EnumeratePatternBindingNames))
                    {
                        yield return item;
                    }
                    yield break;
                default:
                    yield break;
            }
        }

        private sealed class BasicBlockBuilder
        {
            public BasicBlockBuilder(int index)
            {
                Index = index;
            }

            public int Index { get; }

            public List<ExecutableInstruction> Instructions { get; } = [];
        }

        private readonly record struct LoopContext(
            int ContinueBlockIndex,
            int BreakBlockIndex);

    }
}
