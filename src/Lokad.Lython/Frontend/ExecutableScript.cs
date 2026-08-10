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
    private enum OperandKind
    {
        None,
        ImportIndex,
        FunctionIndex,
        StatementFallbackIndex,
        ConstantIndex,
        LocalSlot,
        ClosureSlot,
        NameIndex,
        ExpressionFallbackIndex,
        MemberCacheIndex,
        ItemCount,
        PairCount,
        MatchCaseIndex,
        BlockIndex,
        LoopTargetIndex,
        UnpackingTargetIndex,
        CallSiteIndex,
        CallCacheIndex,
        ExceptionNameIndex,
    }

    private readonly record struct Operand(OperandKind Kind, int Value)
    {
        public static Operand None => new(OperandKind.None, 0);
    }

    private readonly Operand _primaryOperand;
    private readonly Operand _secondaryOperand;
    private readonly ExecutableBinaryOperator _binaryOperator;
    private readonly ExecutableUnaryOperator _unaryOperator;
    private readonly ExecutableAugmentedOperator _augmentedOperator;
    private readonly ExecutableSliceParts _sliceParts;

    private ExecutableInstruction(
        ExecutableOpCode opCode,
        LythonSourceSpan span,
        Operand primaryOperand,
        Operand secondaryOperand,
        ExecutableBinaryOperator binaryOperator,
        ExecutableUnaryOperator unaryOperator,
        ExecutableAugmentedOperator augmentedOperator,
        ExecutableSliceParts sliceParts)
    {
        OpCode = opCode;
        Span = span;
        _primaryOperand = primaryOperand;
        _secondaryOperand = secondaryOperand;
        _binaryOperator = binaryOperator;
        _unaryOperator = unaryOperator;
        _augmentedOperator = augmentedOperator;
        _sliceParts = sliceParts;
    }

    public ExecutableOpCode OpCode { get; }

    public LythonSourceSpan Span { get; }

    public int ImportIndex => ReadOperand(_primaryOperand, OperandKind.ImportIndex);
    public int FunctionIndex => ReadOperand(_primaryOperand, OperandKind.FunctionIndex);
    public int StatementFallbackIndex => ReadOperand(_primaryOperand, OperandKind.StatementFallbackIndex);
    public int ConstantIndex => ReadOperand(_primaryOperand, OperandKind.ConstantIndex);
    public int LocalSlot => ReadOperand(_primaryOperand, OperandKind.LocalSlot);
    public int ClosureSlot => ReadOperand(_primaryOperand, OperandKind.ClosureSlot);
    public int NameIndex => ReadOperand(_primaryOperand, OperandKind.NameIndex);
    public int ExpressionFallbackIndex => ReadOperand(_primaryOperand, OperandKind.ExpressionFallbackIndex);
    public int MemberCacheIndex => ReadOperand(_secondaryOperand, OperandKind.MemberCacheIndex);
    public int ItemCount => ReadOperand(_primaryOperand, OperandKind.ItemCount);
    public int PairCount => ReadOperand(_primaryOperand, OperandKind.PairCount);
    public int MatchCaseIndex => ReadOperand(_primaryOperand, OperandKind.MatchCaseIndex);
    public int FailureBlockIndex => ReadOperand(_secondaryOperand, OperandKind.BlockIndex);
    public int TargetBlockIndex => ReadOperand(_primaryOperand, OperandKind.BlockIndex);
    public int LoopTargetIndex => ReadOperand(_primaryOperand, OperandKind.LoopTargetIndex);
    public int UnpackingTargetIndex => ReadOperand(_primaryOperand, OperandKind.UnpackingTargetIndex);
    public int CallSiteIndex => ReadOperand(_primaryOperand, OperandKind.CallSiteIndex);
    public int CallCacheIndex => ReadOperand(_secondaryOperand, OperandKind.CallCacheIndex);
    public int ExceptionNameIndex => ReadOperand(_primaryOperand, OperandKind.ExceptionNameIndex);
    public ExecutableSliceParts SliceParts => _sliceParts;
    public ExecutableBinaryOperator BinaryOperator => _binaryOperator;
    public ExecutableUnaryOperator UnaryOperator => _unaryOperator;
    public ExecutableAugmentedOperator AugmentedOperator => _augmentedOperator;

    public static ExecutableInstruction Import(int importIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.Import, new Operand(OperandKind.ImportIndex, importIndex), span);

    public static ExecutableInstruction DefineFunction(int functionIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.DefineFunction, new Operand(OperandKind.FunctionIndex, functionIndex), span);

    public static ExecutableInstruction ExecuteFallbackStatement(int statementIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.ExecuteFallbackStatement, new Operand(OperandKind.StatementFallbackIndex, statementIndex), span);

    public static ExecutableInstruction LoadConst(int constantIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.LoadConst, new Operand(OperandKind.ConstantIndex, constantIndex), span);

    public static ExecutableInstruction LoadLocal(int slot, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.LoadLocal, new Operand(OperandKind.LocalSlot, slot), span);

    public static ExecutableInstruction LoadClosure(int slot, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.LoadClosure, new Operand(OperandKind.ClosureSlot, slot), span);

    public static ExecutableInstruction LoadGlobal(int nameIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.LoadGlobal, new Operand(OperandKind.NameIndex, nameIndex), span);

    public static ExecutableInstruction LoadName(int nameIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.LoadName, new Operand(OperandKind.NameIndex, nameIndex), span);

    public static ExecutableInstruction EvaluateFallbackExpression(int expressionIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.EvaluateFallbackExpression, new Operand(OperandKind.ExpressionFallbackIndex, expressionIndex), span);

    public static ExecutableInstruction LoadMember(int nameIndex, int cacheIndex, LythonSourceSpan span)
        => CreateIndexed(
            ExecutableOpCode.LoadMember,
            new Operand(OperandKind.NameIndex, nameIndex),
            new Operand(OperandKind.MemberCacheIndex, cacheIndex),
            span);

    public static ExecutableInstruction StoreLocal(int slot, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.StoreLocal, new Operand(OperandKind.LocalSlot, slot), span);

    public static ExecutableInstruction StoreClosure(int slot, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.StoreClosure, new Operand(OperandKind.ClosureSlot, slot), span);

    public static ExecutableInstruction StoreGlobal(int nameIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.StoreGlobal, new Operand(OperandKind.NameIndex, nameIndex), span);

    public static ExecutableInstruction StoreName(int nameIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.StoreName, new Operand(OperandKind.NameIndex, nameIndex), span);

    public static ExecutableInstruction Dup(LythonSourceSpan span)
        => Create(ExecutableOpCode.Dup, span);

    public static ExecutableInstruction PopTop(LythonSourceSpan span)
        => Create(ExecutableOpCode.PopTop, span);

    public static ExecutableInstruction MakeList(int count, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.MakeList, new Operand(OperandKind.ItemCount, count), span);

    public static ExecutableInstruction MakeTuple(int count, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.MakeTuple, new Operand(OperandKind.ItemCount, count), span);

    public static ExecutableInstruction MakeSet(int count, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.MakeSet, new Operand(OperandKind.ItemCount, count), span);

    public static ExecutableInstruction MakeDict(int pairCount, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.MakeDict, new Operand(OperandKind.PairCount, pairCount), span);

    public static ExecutableInstruction ResolveContextManager(LythonSourceSpan span)
        => Create(ExecutableOpCode.ResolveContextManager, span);

    public static ExecutableInstruction EnterContextManager(LythonSourceSpan span)
        => Create(ExecutableOpCode.EnterContextManager, span);

    public static ExecutableInstruction ExitContextManager(LythonSourceSpan span)
        => Create(ExecutableOpCode.ExitContextManager, span);

    public static ExecutableInstruction MatchCase(int caseIndex, int failBlockIndex, LythonSourceSpan span)
        => CreateIndexed(
            ExecutableOpCode.MatchCase,
            new Operand(OperandKind.MatchCaseIndex, caseIndex),
            new Operand(OperandKind.BlockIndex, failBlockIndex),
            span);

    public static ExecutableInstruction GetIter(LythonSourceSpan span)
        => Create(ExecutableOpCode.GetIter, span);

    public static ExecutableInstruction ForNext(int targetBlockIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.ForNext, new Operand(OperandKind.BlockIndex, targetBlockIndex), span);

    public static ExecutableInstruction AssignLoopTarget(int loopTargetIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.AssignLoopTarget, new Operand(OperandKind.LoopTargetIndex, loopTargetIndex), span);

    public static ExecutableInstruction AssignUnpackingTargets(int unpackingTargetIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.AssignUnpackingTargets, new Operand(OperandKind.UnpackingTargetIndex, unpackingTargetIndex), span);

    public static ExecutableInstruction Call(int callSiteIndex, int cacheIndex, LythonSourceSpan span)
        => CreateIndexed(
            ExecutableOpCode.Call,
            new Operand(OperandKind.CallSiteIndex, callSiteIndex),
            new Operand(OperandKind.CallCacheIndex, cacheIndex),
            span);

    public static ExecutableInstruction Subscript(LythonSourceSpan span)
        => Create(ExecutableOpCode.Subscript, span);

    public static ExecutableInstruction Slice(ExecutableSliceParts parts, LythonSourceSpan span)
        => new(ExecutableOpCode.Slice, span, Operand.None, Operand.None, default, default, default, parts);

    public static ExecutableInstruction Binary(ExecutableBinaryOperator op, LythonSourceSpan span)
        => new(ExecutableOpCode.Binary, span, Operand.None, Operand.None, op, default, default, default);

    public static ExecutableInstruction Augmented(ExecutableAugmentedOperator op, LythonSourceSpan span)
        => new(ExecutableOpCode.Augmented, span, Operand.None, Operand.None, default, default, op, default);

    public static ExecutableInstruction Unary(ExecutableUnaryOperator op, LythonSourceSpan span)
        => new(ExecutableOpCode.Unary, span, Operand.None, Operand.None, default, op, default, default);

    public static ExecutableInstruction Jump(int targetBlockIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.Jump, new Operand(OperandKind.BlockIndex, targetBlockIndex), span);

    public static ExecutableInstruction JumpIfFalse(int targetBlockIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.JumpIfFalse, new Operand(OperandKind.BlockIndex, targetBlockIndex), span);

    public static ExecutableInstruction ClearException(int exceptionNameIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.ClearException, new Operand(OperandKind.ExceptionNameIndex, exceptionNameIndex), span);

    public static ExecutableInstruction EndFinally(int targetBlockIndex, LythonSourceSpan span)
        => CreateIndexed(ExecutableOpCode.EndFinally, new Operand(OperandKind.BlockIndex, targetBlockIndex), span);

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
        => new(opCode, span, Operand.None, Operand.None, default, default, default, default);

    private static ExecutableInstruction CreateIndexed(ExecutableOpCode opCode, Operand primaryOperand, LythonSourceSpan span)
        => new(opCode, span, primaryOperand, Operand.None, default, default, default, default);

    private static ExecutableInstruction CreateIndexed(ExecutableOpCode opCode, Operand primaryOperand, Operand secondaryOperand, LythonSourceSpan span)
        => new(opCode, span, primaryOperand, secondaryOperand, default, default, default, default);

    private int ReadOperand(Operand operand, OperandKind expectedKind)
        => operand.Kind == expectedKind
            ? operand.Value
            : throw new InvalidOperationException($"Instruction '{OpCode}' has no {expectedKind} operand.");
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
