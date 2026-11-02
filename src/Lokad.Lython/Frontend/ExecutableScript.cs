using System.Globalization;
using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Frontend;

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
    ApplyOperation,
    LoadMember,
    LoadCallMember,
    LoadCallReceiver,
    PreloadImplicitSuperArguments,
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
    Call,
    Subscript,
    Slice,
    MakeSlice,
    Binary,
    Augmented,
    Unary,
    Jump,
    JumpIfFalse,
    MatchException,
    BindException,
    ReraiseException,
    ClearException,
    EndFinally,
    Return,
    ReturnNone,
    Yield,
    YieldFrom,
    AbruptJump,
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
    MatrixMultiply,
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
    // Operand meaning is fixed by the opcode. Indexes and the mutually
    // exclusive operator families share these two slots; no duplicate domain
    // tags or unused per-family fields travel with every instruction.
    private readonly int _primaryOperand;
    private readonly int _secondaryOperand;

    private ExecutableInstruction(ExecutableOpCode opCode, LythonSourceSpan span,
        int primaryOperand = 0, int secondaryOperand = 0)
    {
        OpCode = opCode;
        Span = span;
        _primaryOperand = primaryOperand;
        _secondaryOperand = secondaryOperand;
    }

    public ExecutableOpCode OpCode { get; }

    public LythonSourceSpan Span { get; }

    public int ImportIndex => ReadPrimaryOperand(ExecutableOpCode.Import, nameof(ImportIndex));
    public int FunctionIndex => ReadPrimaryOperand(ExecutableOpCode.DefineFunction, nameof(FunctionIndex));
    public int StatementFallbackIndex => ReadPrimaryOperand(ExecutableOpCode.ExecuteFallbackStatement, nameof(StatementFallbackIndex));
    public int ConstantIndex => OpCode is ExecutableOpCode.LoadConst or ExecutableOpCode.ApplyOperation
        ? _primaryOperand : throw InvalidOperand(nameof(ConstantIndex));
    public int LocalSlot => OpCode is ExecutableOpCode.LoadLocal or ExecutableOpCode.StoreLocal
        ? _primaryOperand : throw InvalidOperand(nameof(LocalSlot));
    public int ClosureSlot => OpCode is ExecutableOpCode.LoadClosure or ExecutableOpCode.StoreClosure
        ? _primaryOperand : throw InvalidOperand(nameof(ClosureSlot));
    public int NameIndex => OpCode is ExecutableOpCode.LoadGlobal or ExecutableOpCode.LoadName
        or ExecutableOpCode.LoadMember or ExecutableOpCode.LoadCallMember or ExecutableOpCode.LoadCallReceiver
        or ExecutableOpCode.StoreGlobal or ExecutableOpCode.StoreName or ExecutableOpCode.BindException
        ? _primaryOperand : throw InvalidOperand(nameof(NameIndex));
    public int MemberCacheIndex => OpCode is ExecutableOpCode.LoadMember or ExecutableOpCode.LoadCallMember or ExecutableOpCode.LoadCallReceiver
        ? _secondaryOperand : throw InvalidOperand(nameof(MemberCacheIndex));
    public bool DiscardIterator => OpCode == ExecutableOpCode.AbruptJump
        ? _secondaryOperand != 0 : throw InvalidOperand("ItemCount");
    public int ItemCount => OpCode is ExecutableOpCode.MakeList or ExecutableOpCode.MakeTuple or ExecutableOpCode.MakeSet
        ? _primaryOperand : throw InvalidOperand(nameof(ItemCount));
    public int PairCount => ReadPrimaryOperand(ExecutableOpCode.MakeDict, nameof(PairCount));
    public int MatchCaseIndex => ReadPrimaryOperand(ExecutableOpCode.MatchCase, nameof(MatchCaseIndex));
    public int FailureBlockIndex => OpCode == ExecutableOpCode.MatchCase
        ? _secondaryOperand : throw InvalidOperand("BlockIndex");
    public int TargetBlockIndex => OpCode is ExecutableOpCode.AbruptJump or ExecutableOpCode.Jump or ExecutableOpCode.JumpIfFalse
        or ExecutableOpCode.ForNext or ExecutableOpCode.EndFinally
        ? _primaryOperand : throw InvalidOperand("BlockIndex");
    public int CallSiteIndex => ReadPrimaryOperand(ExecutableOpCode.Call, nameof(CallSiteIndex));
    public int CleanupId => OpCode == ExecutableOpCode.EndFinally ? _secondaryOperand
        : ReadPrimaryOperand(ExecutableOpCode.ExitContextManager, nameof(CleanupId));
    public int ExceptionNameIndex => ReadPrimaryOperand(ExecutableOpCode.ClearException, nameof(ExceptionNameIndex));
    public ExecutableSliceParts SliceParts => (ExecutableSliceParts)_primaryOperand;
    public BinaryOperatorSyntax BinaryOperator => (BinaryOperatorSyntax)_primaryOperand;
    public ExecutableUnaryOperator UnaryOperator => (ExecutableUnaryOperator)_primaryOperand;
    public ExecutableAugmentedOperator AugmentedOperator => (ExecutableAugmentedOperator)_primaryOperand;

    public static ExecutableInstruction Import(int importIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.Import, span, importIndex);

    public static ExecutableInstruction DefineFunction(int functionIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.DefineFunction, span, functionIndex);

    public static ExecutableInstruction ExecuteFallbackStatement(int statementIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.ExecuteFallbackStatement, span, statementIndex);

    public static ExecutableInstruction ApplyOperation(int constantIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.ApplyOperation, span, constantIndex);

    public static ExecutableInstruction LoadConst(int constantIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.LoadConst, span, constantIndex);

    public static ExecutableInstruction LoadLocal(int slot, LythonSourceSpan span)
        => new(ExecutableOpCode.LoadLocal, span, slot);

    public static ExecutableInstruction LoadClosure(int slot, LythonSourceSpan span)
        => new(ExecutableOpCode.LoadClosure, span, slot);

    public static ExecutableInstruction LoadGlobal(int nameIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.LoadGlobal, span, nameIndex);

    public static ExecutableInstruction LoadName(int nameIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.LoadName, span, nameIndex);

    public static ExecutableInstruction LoadMember(int nameIndex, int cacheIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.LoadMember, span, nameIndex, cacheIndex);

    public static ExecutableInstruction LoadCallMember(int nameIndex, int cacheIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.LoadCallMember, span, nameIndex, cacheIndex);

    public static ExecutableInstruction LoadCallReceiver(int nameIndex, int cacheIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.LoadCallReceiver, span, nameIndex, cacheIndex);

    public static ExecutableInstruction PreloadImplicitSuperArguments(LythonSourceSpan span)
        => new(ExecutableOpCode.PreloadImplicitSuperArguments, span);

    public static ExecutableInstruction StoreLocal(int slot, LythonSourceSpan span)
        => new(ExecutableOpCode.StoreLocal, span, slot);

    public static ExecutableInstruction StoreClosure(int slot, LythonSourceSpan span)
        => new(ExecutableOpCode.StoreClosure, span, slot);

    public static ExecutableInstruction StoreGlobal(int nameIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.StoreGlobal, span, nameIndex);

    public static ExecutableInstruction StoreName(int nameIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.StoreName, span, nameIndex);

    public static ExecutableInstruction Dup(LythonSourceSpan span)
        => new(ExecutableOpCode.Dup, span);

    public static ExecutableInstruction PopTop(LythonSourceSpan span)
        => new(ExecutableOpCode.PopTop, span);

    public static ExecutableInstruction MakeList(int count, LythonSourceSpan span)
        => new(ExecutableOpCode.MakeList, span, count);

    public static ExecutableInstruction MakeTuple(int count, LythonSourceSpan span)
        => new(ExecutableOpCode.MakeTuple, span, count);

    public static ExecutableInstruction MakeSet(int count, LythonSourceSpan span)
        => new(ExecutableOpCode.MakeSet, span, count);

    public static ExecutableInstruction MakeDict(int pairCount, LythonSourceSpan span)
        => new(ExecutableOpCode.MakeDict, span, pairCount);

    public static ExecutableInstruction ResolveContextManager(LythonSourceSpan span)
        => new(ExecutableOpCode.ResolveContextManager, span);

    public static ExecutableInstruction EnterContextManager(LythonSourceSpan span)
        => new(ExecutableOpCode.EnterContextManager, span);

    public static ExecutableInstruction ExitContextManager(LythonSourceSpan span, int cleanupId = -1)
        => new(ExecutableOpCode.ExitContextManager, span, cleanupId);

    public static ExecutableInstruction MatchCase(int caseIndex, int failBlockIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.MatchCase, span, caseIndex, failBlockIndex);

    public static ExecutableInstruction GetIter(LythonSourceSpan span)
        => new(ExecutableOpCode.GetIter, span);

    public static ExecutableInstruction ForNext(int targetBlockIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.ForNext, span, targetBlockIndex);


    public static ExecutableInstruction Call(int callSiteIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.Call, span, callSiteIndex);

    public static ExecutableInstruction Subscript(LythonSourceSpan span)
        => new(ExecutableOpCode.Subscript, span);

    public static ExecutableInstruction Slice(ExecutableSliceParts parts, LythonSourceSpan span)
        => new(ExecutableOpCode.Slice, span, (int)parts);

    public static ExecutableInstruction MakeSlice(ExecutableSliceParts parts, LythonSourceSpan span)
        => new(ExecutableOpCode.MakeSlice, span, (int)parts);

    public static ExecutableInstruction Binary(BinaryOperatorSyntax op, LythonSourceSpan span)
        => new(ExecutableOpCode.Binary, span, (int)op);

    public static ExecutableInstruction Augmented(ExecutableAugmentedOperator op, LythonSourceSpan span)
        => new(ExecutableOpCode.Augmented, span, (int)op);

    public static ExecutableInstruction Unary(ExecutableUnaryOperator op, LythonSourceSpan span)
        => new(ExecutableOpCode.Unary, span, (int)op);

    public static ExecutableInstruction AbruptJump(int targetBlockIndex, bool discardIterator, LythonSourceSpan span)
        => new(ExecutableOpCode.AbruptJump, span, targetBlockIndex, discardIterator ? 1 : 0);

    public static ExecutableInstruction Jump(int targetBlockIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.Jump, span, targetBlockIndex);

    public static ExecutableInstruction JumpIfFalse(int targetBlockIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.JumpIfFalse, span, targetBlockIndex);

    public static ExecutableInstruction ClearException(int exceptionNameIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.ClearException, span, exceptionNameIndex);

    public static ExecutableInstruction MatchException(LythonSourceSpan span)
        => new(ExecutableOpCode.MatchException, span);

    public static ExecutableInstruction BindException(int nameIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.BindException, span, nameIndex);

    public static ExecutableInstruction ReraiseException(LythonSourceSpan span)
        => new(ExecutableOpCode.ReraiseException, span);

    public static ExecutableInstruction EndFinally(int targetBlockIndex, LythonSourceSpan span, int cleanupId = -1)
        => new(ExecutableOpCode.EndFinally, span, targetBlockIndex, cleanupId);

    public static ExecutableInstruction Return(LythonSourceSpan span)
        => new(ExecutableOpCode.Return, span);

    public static ExecutableInstruction ReturnNone(LythonSourceSpan span)
        => new(ExecutableOpCode.ReturnNone, span);

    public static ExecutableInstruction Yield(bool delegated, LythonSourceSpan span)
        => new(delegated ? ExecutableOpCode.YieldFrom : ExecutableOpCode.Yield, span);

    public ExecutableInstruction WithTargetBlockIndex(int targetBlockIndex)
        => OpCode switch
        {
            ExecutableOpCode.AbruptJump => AbruptJump(targetBlockIndex, DiscardIterator, Span),
            ExecutableOpCode.Jump => Jump(targetBlockIndex, Span),
            ExecutableOpCode.JumpIfFalse => JumpIfFalse(targetBlockIndex, Span),
            ExecutableOpCode.ForNext => ForNext(targetBlockIndex, Span),
            ExecutableOpCode.EndFinally => EndFinally(targetBlockIndex, Span, CleanupId),
            _ => throw new InvalidOperationException($"Instruction '{OpCode}' has no target block.")
        };

    public ExecutableInstruction WithFailureBlockIndex(int failureBlockIndex)
        => OpCode switch
        {
            ExecutableOpCode.MatchCase => MatchCase(MatchCaseIndex, failureBlockIndex, Span),
            _ => throw new InvalidOperationException($"Instruction '{OpCode}' has no failure block.")
        };

    private int ReadPrimaryOperand(ExecutableOpCode expectedOpCode, string name)
        => OpCode == expectedOpCode ? _primaryOperand : throw InvalidOperand(name);

    private InvalidOperationException InvalidOperand(string name)
        => new($"Instruction '{OpCode}' has no {name} operand.");
}

internal sealed record ExecutableBasicBlock(
    int Index,
    ExecutableInstruction[] InstructionArray)
{
    public IReadOnlyList<ExecutableInstruction> Instructions => InstructionArray;
}

internal sealed record ExecutableExceptionRegion(
    int ProtectedStartBlockIndex,
    int ProtectedEndBlockIndex,
    int? ExceptBlockIndex,
    int? FinallyBlockIndex,
    int? SuiteStartBlockIndex,
    int? SuiteEndBlockIndex,
    int? CleanupId = null);

internal sealed record ExecutableCallSite(
    int ArgumentCount,
    IReadOnlyList<CallArgumentForm> Arguments,
    LythonSourceSpan TargetSpan,
    LythonSourceSpan CallSpan,
    bool HasReceiver = false)
{
    public bool IsPositionalOnly { get; } = Arguments.All(argument => argument.Kind == CallArgumentKind.Positional);
    public int TargetOffset => HasReceiver ? 1 : 0;
    public int ArgumentOffset => TargetOffset + 1;
}

internal sealed record ExecutableFunctionBinding(
    LoweredFunctionDefinitionStatement Function,
    ExecutableCodeObject CodeObject,
    IReadOnlyDictionary<string, LoweredExpression> DefaultValues,
    string? Docstring);

internal sealed record ExecutableMatchCaseBinding(
    MatchCaseSyntax Case,
    IReadOnlyDictionary<string, int> LocalBindingSlots);

internal sealed partial class ExecutableCodeObject
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
        IReadOnlyList<ImportStatementSyntax> imports,
        IReadOnlyList<ExecutableCallSite> callSites,
        int memberCacheCount,
        IReadOnlyList<ExecutableFunctionBinding> functions,
        IReadOnlyList<ExecutableMatchCaseBinding> matchCases,
        IReadOnlyList<LoweredStatement> statementFallbacks,
        bool requiresLocalVariableMirroring,
        bool isFunctionScope)
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
        Functions = functions;
        MatchCases = matchCases;
        StatementFallbacks = statementFallbacks;
        RequiresLocalVariableMirroring = requiresLocalVariableMirroring;
        IsFunctionScope = isFunctionScope;
        NativeReturn = PrepareNativeReturn();
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

    public IReadOnlyList<ImportStatementSyntax> Imports { get; }

    public IReadOnlyList<ExecutableCallSite> CallSites { get; }

    public int MemberCacheCount { get; }

    public IReadOnlyList<ExecutableFunctionBinding> Functions { get; }

    public IReadOnlyList<ExecutableMatchCaseBinding> MatchCases { get; }

    public IReadOnlyList<LoweredStatement> StatementFallbacks { get; }

    public bool RequiresLocalVariableMirroring { get; }

    public bool IsFunctionScope { get; }

    public NativeReturnNode[]? NativeReturn { get; }
}
