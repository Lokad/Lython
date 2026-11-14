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
    StoreMember,
    StoreMemberLast,
    StoreSubscript,
    StoreSubscriptLast,
    StoreSlice,
    StoreSliceLast,
    ReadRetainedSubscript,
    ReadRetainedSlice,
    UnpackValues,
    FailAssertion,
    Raise,
    DeleteName,
    DeleteMember,
    DeleteSubscript,
    DeleteSlice,
    LoadMember,
    LoadCallMember,
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
    CompareJumpIfFalse,
    MatchException,
    BindException,
    ReraiseException,
    ClearException,
    EndFinally,
    Return,
    ReturnNone,
    Complete,
    Yield,
    YieldFrom,
    AbruptJump,
    StartFormattedString,
    AppendFormattedText,
    FinishFormattedString,
    ConvertFormattedValue,
    FormatField,
    ForNextDictionaryAccumulation,
}

internal enum ExecutableUnaryOperator
{
    Not,
    Plus,
    Minus,
    BitwiseNot,
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
    // Opcode metadata and physical positions share one immutable instruction.
    // Native and resumable execution use these same final storage positions.
    private readonly int _primaryOperand;
    private readonly int _secondaryOperand;
    private readonly int _inputStart;
    private readonly int _resultSlot;
    private readonly int _liveExtent;

    private ExecutableInstruction(ExecutableOpCode opCode, LythonSourceSpan span,
        int primaryOperand = 0, int secondaryOperand = int.MaxValue,
        int inputStart = int.MaxValue, int resultSlot = -1, int liveExtent = 0)
    {
        OpCode = opCode;
        Span = span;
        _primaryOperand = primaryOperand;
        _secondaryOperand = secondaryOperand;
        _inputStart = inputStart;
        _resultSlot = resultSlot;
        _liveExtent = liveExtent;
    }

    public ExecutableOpCode OpCode { get; }

    public LythonSourceSpan Span { get; }

    // Compiler-owned storage positions. They share the existing instruction;
    // no input-plan table or runtime shape calculation is needed.
    public int InputStart => _inputStart;
    public int SecondInput => _secondaryOperand;
    public int ThirdInput => OpCode is ExecutableOpCode.StoreSubscript or ExecutableOpCode.StoreSubscriptLast
        ? _resultSlot : throw InvalidOperand(nameof(ThirdInput));
    internal int ValueInputCount => OpCode switch
    {
        ExecutableOpCode.Binary or ExecutableOpCode.Augmented or ExecutableOpCode.Subscript or ExecutableOpCode.CompareJumpIfFalse => 2,
        ExecutableOpCode.StoreSubscript or ExecutableOpCode.StoreSubscriptLast => 3,
        ExecutableOpCode.Unary or ExecutableOpCode.StoreLocal or ExecutableOpCode.Return or
            ExecutableOpCode.LoadMember or ExecutableOpCode.LoadCallMember or
            ExecutableOpCode.GetIter or ExecutableOpCode.JumpIfFalse => 1,
        _ => 0,
    };
    // A subscript store pushes no result: its output position is its live
    // extent. Reuse that otherwise redundant field for its third input, keeping
    // every instruction at the existing size, including native helper code.
    public int ResultSlot => OpCode is ExecutableOpCode.StoreSubscript or ExecutableOpCode.StoreSubscriptLast or ExecutableOpCode.CompareJumpIfFalse
        ? LiveExtent : _resultSlot;
    // A complemented prepared extent marks the adjacent local-result store.
    // Decode the original extent; physical positions never name locals.
    public int LiveExtent => HasLocalResult ? ~_liveExtent : _liveExtent;
    public bool HasLocalResult => _liveExtent < 0;

    internal ExecutableInstruction WithStorageSlots(int inputStart, int resultSlot, int liveExtent)
        => new(OpCode, Span, _primaryOperand,
            ValueInputCount > 1 ? checked(inputStart + 1) : _secondaryOperand,
            inputStart, OpCode == ExecutableOpCode.CompareJumpIfFalse ? _resultSlot
                : ValueInputCount == 3 ? checked(inputStart + 2) : resultSlot,
            HasLocalResult ? ~liveExtent : liveExtent);

    internal ExecutableInstruction WithValueInputs(int first, int second, int third = int.MaxValue)
        => new(OpCode, Span, _primaryOperand,
            ValueInputCount > 1 ? second : _secondaryOperand,
            first, ValueInputCount == 3 ? third : _resultSlot, _liveExtent);

    // The following store retains its own source span and is also the
    // continuation when input cleanup must finish asynchronously.
    internal ExecutableInstruction WithLocalResult()
        => new(OpCode, Span, _primaryOperand, _secondaryOperand,
            _inputStart, _resultSlot, ~_liveExtent);

    public int ImportIndex => ReadPrimaryOperand(ExecutableOpCode.Import, nameof(ImportIndex));
    public int FunctionIndex => ReadPrimaryOperand(ExecutableOpCode.DefineFunction, nameof(FunctionIndex));
    public int StatementFallbackIndex => ReadPrimaryOperand(ExecutableOpCode.ExecuteFallbackStatement, nameof(StatementFallbackIndex));
    public int ConstantIndex => OpCode is ExecutableOpCode.LoadConst or ExecutableOpCode.ApplyOperation
        or ExecutableOpCode.AppendFormattedText or ExecutableOpCode.FormatField
        ? _primaryOperand : throw InvalidOperand(nameof(ConstantIndex));
    public int LocalSlot => OpCode is ExecutableOpCode.LoadLocal or ExecutableOpCode.StoreLocal
            ? _primaryOperand : throw InvalidOperand(nameof(LocalSlot));
    public int ClosureSlot => OpCode is ExecutableOpCode.LoadClosure or ExecutableOpCode.StoreClosure
        ? _primaryOperand : throw InvalidOperand(nameof(ClosureSlot));
    internal bool HasClosureArithmetic => OpCode == ExecutableOpCode.LoadClosure && _secondaryOperand != int.MaxValue;
    internal int ClosureArithmeticConstant => OpCode == ExecutableOpCode.LoadClosure
        ? _secondaryOperand : throw InvalidOperand(nameof(ClosureArithmeticConstant));
    internal ExecutableInstruction WithClosureArithmetic(int constantIndex)
        => new(OpCode, Span, _primaryOperand, constantIndex, _inputStart, _resultSlot, _liveExtent);
    public int NameIndex => OpCode is ExecutableOpCode.LoadGlobal or ExecutableOpCode.LoadName
        or ExecutableOpCode.LoadMember or ExecutableOpCode.LoadCallMember
        or ExecutableOpCode.StoreGlobal or ExecutableOpCode.StoreName or ExecutableOpCode.BindException
        or ExecutableOpCode.StoreMember or ExecutableOpCode.StoreMemberLast
        or ExecutableOpCode.DeleteName or ExecutableOpCode.DeleteMember
        ? _primaryOperand : throw InvalidOperand(nameof(NameIndex));
    public int LookupCacheIndex => OpCode == ExecutableOpCode.Subscript ? _primaryOperand
        : OpCode is ExecutableOpCode.LoadMember or ExecutableOpCode.LoadCallMember
            ? _secondaryOperand : throw InvalidOperand(nameof(LookupCacheIndex));
    public bool DiscardIterator => OpCode == ExecutableOpCode.AbruptJump
        ? _secondaryOperand != 0 : throw InvalidOperand("ItemCount");
    public int ItemCount => OpCode is ExecutableOpCode.MakeList or ExecutableOpCode.MakeTuple or ExecutableOpCode.MakeSet
        ? _primaryOperand : throw InvalidOperand(nameof(ItemCount));
    public int PairCount => ReadPrimaryOperand(ExecutableOpCode.MakeDict, nameof(PairCount));
    public int FormattedInitialCapacity => ReadPrimaryOperand(ExecutableOpCode.StartFormattedString, nameof(FormattedInitialCapacity));
    public char FormattedConversion => (char)ReadPrimaryOperand(ExecutableOpCode.ConvertFormattedValue, nameof(FormattedConversion));
    public int MatchCaseIndex => ReadPrimaryOperand(ExecutableOpCode.MatchCase, nameof(MatchCaseIndex));
    public int FailureIndex => OpCode == ExecutableOpCode.MatchCase
        ? _secondaryOperand : throw InvalidOperand("BlockIndex");
    public int TargetIndex => OpCode is ExecutableOpCode.AbruptJump or ExecutableOpCode.Jump or ExecutableOpCode.JumpIfFalse or ExecutableOpCode.CompareJumpIfFalse
        or ExecutableOpCode.ForNext or ExecutableOpCode.ForNextDictionaryAccumulation or ExecutableOpCode.EndFinally
        ? _primaryOperand : throw InvalidOperand("BlockIndex");
    internal int DictionaryLoopCallIndex => OpCode == ExecutableOpCode.ForNextDictionaryAccumulation
        ? _secondaryOperand : throw InvalidOperand(nameof(DictionaryLoopCallIndex));
    public int CallSiteIndex => ReadPrimaryOperand(ExecutableOpCode.Call, nameof(CallSiteIndex));
    public int CleanupId => OpCode == ExecutableOpCode.EndFinally ? _secondaryOperand
        : ReadPrimaryOperand(ExecutableOpCode.ExitContextManager, nameof(CleanupId));
    public int ExceptionNameIndex => ReadPrimaryOperand(ExecutableOpCode.ClearException, nameof(ExceptionNameIndex));
    public ExecutableSliceParts SliceParts => (ExecutableSliceParts)_primaryOperand;
    public BinaryOperatorSyntax BinaryOperator => (BinaryOperatorSyntax)(OpCode == ExecutableOpCode.CompareJumpIfFalse ? _resultSlot : _primaryOperand);
    public ExecutableUnaryOperator UnaryOperator => (ExecutableUnaryOperator)_primaryOperand;
    public AugmentedAssignmentOperatorSyntax AugmentedOperator => (AugmentedAssignmentOperatorSyntax)_primaryOperand;

    public int TargetSpanIndex => OpCode is ExecutableOpCode.StoreMember or ExecutableOpCode.StoreMemberLast
        ? _secondaryOperand : OpCode is ExecutableOpCode.StoreSubscript or ExecutableOpCode.StoreSubscriptLast
            or ExecutableOpCode.StoreSlice or ExecutableOpCode.StoreSliceLast
            ? _primaryOperand : throw InvalidOperand(nameof(TargetSpanIndex));
    public bool ValueLast => OpCode is ExecutableOpCode.StoreMemberLast or ExecutableOpCode.StoreSubscriptLast or ExecutableOpCode.StoreSliceLast;
    public UnpackingLayout UnpackLayout => OpCode == ExecutableOpCode.UnpackValues
        ? new(_primaryOperand, _secondaryOperand) : throw InvalidOperand(nameof(UnpackLayout));
    public bool HasAssertionMessage => ReadPrimaryOperand(ExecutableOpCode.FailAssertion, nameof(HasAssertionMessage)) != 0;
    public bool HasRaiseCause => ReadPrimaryOperand(ExecutableOpCode.Raise, nameof(HasRaiseCause)) != 0;

    public static ExecutableInstruction Import(int importIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.Import, span, importIndex);

    public static ExecutableInstruction DefineFunction(int functionIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.DefineFunction, span, functionIndex);

    public static ExecutableInstruction ExecuteFallbackStatement(int statementIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.ExecuteFallbackStatement, span, statementIndex);

    public static ExecutableInstruction ApplyOperation(int constantIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.ApplyOperation, span, constantIndex);

    public static ExecutableInstruction StartFormattedString(int capacity, LythonSourceSpan span)
        => new(ExecutableOpCode.StartFormattedString, span, capacity);

    public static ExecutableInstruction AppendFormattedText(int constantIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.AppendFormattedText, span, constantIndex);

    public static ExecutableInstruction FinishFormattedString(LythonSourceSpan span)
        => new(ExecutableOpCode.FinishFormattedString, span);

    public static ExecutableInstruction ConvertFormattedValue(char conversion, LythonSourceSpan span)
        => new(ExecutableOpCode.ConvertFormattedValue, span, conversion);

    public static ExecutableInstruction FormatField(int constantIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.FormatField, span, constantIndex);

    public static ExecutableInstruction StoreMember(int nameIndex, int targetSpanIndex, LythonSourceSpan span, bool valueLast = false)
        => new(valueLast ? ExecutableOpCode.StoreMemberLast : ExecutableOpCode.StoreMember, span, nameIndex, targetSpanIndex);

    public static ExecutableInstruction StoreSubscript(int targetSpanIndex, LythonSourceSpan span, bool valueLast = false)
        => new(valueLast ? ExecutableOpCode.StoreSubscriptLast : ExecutableOpCode.StoreSubscript, span, targetSpanIndex, resultSlot: int.MaxValue);

    public static ExecutableInstruction StoreSlice(int targetSpanIndex, LythonSourceSpan span, bool valueLast = false)
        => new(valueLast ? ExecutableOpCode.StoreSliceLast : ExecutableOpCode.StoreSlice, span, targetSpanIndex);

    public static ExecutableInstruction ReadRetainedSubscript(LythonSourceSpan span) => new(ExecutableOpCode.ReadRetainedSubscript, span);
    public static ExecutableInstruction ReadRetainedSlice(LythonSourceSpan span) => new(ExecutableOpCode.ReadRetainedSlice, span);
    public static ExecutableInstruction UnpackValues(UnpackingLayout layout, LythonSourceSpan span)
        => new(ExecutableOpCode.UnpackValues, span, layout.TargetCount, layout.StarredTargetIndex);
    public static ExecutableInstruction FailAssertion(bool hasMessage, LythonSourceSpan span)
        => new(ExecutableOpCode.FailAssertion, span, hasMessage ? 1 : 0);
    public static ExecutableInstruction Raise(bool hasCause, LythonSourceSpan span)
        => new(ExecutableOpCode.Raise, span, hasCause ? 1 : 0);
    public static ExecutableInstruction DeleteName(int nameIndex, LythonSourceSpan span) => new(ExecutableOpCode.DeleteName, span, nameIndex);
    public static ExecutableInstruction DeleteMember(int nameIndex, LythonSourceSpan span) => new(ExecutableOpCode.DeleteMember, span, nameIndex);
    public static ExecutableInstruction DeleteSubscript(LythonSourceSpan span) => new(ExecutableOpCode.DeleteSubscript, span);
    public static ExecutableInstruction DeleteSlice(ExecutableSliceParts parts, LythonSourceSpan span)
        => new(ExecutableOpCode.DeleteSlice, span, (int)parts);

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

    // Final-layout preparation changes no positions or storage capacity. The
    // ordinary body remains the continuation for protocols and admission.
    internal ExecutableInstruction WithDictionaryLoop(int callIndex)
        => new(ExecutableOpCode.ForNextDictionaryAccumulation, Span, _primaryOperand,
            callIndex, _inputStart, _resultSlot, _liveExtent);


    public static ExecutableInstruction Call(int callSiteIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.Call, span, callSiteIndex);

    public static ExecutableInstruction Subscript(LythonSourceSpan span, int cacheIndex = -1)
        => new(ExecutableOpCode.Subscript, span, cacheIndex);

    public static ExecutableInstruction Slice(ExecutableSliceParts parts, LythonSourceSpan span)
        => new(ExecutableOpCode.Slice, span, (int)parts);

    public static ExecutableInstruction MakeSlice(ExecutableSliceParts parts, LythonSourceSpan span)
        => new(ExecutableOpCode.MakeSlice, span, (int)parts);

    public static ExecutableInstruction Binary(BinaryOperatorSyntax op, LythonSourceSpan span)
        => new(ExecutableOpCode.Binary, span, (int)op);

    public static ExecutableInstruction Augmented(AugmentedAssignmentOperatorSyntax op, LythonSourceSpan span)
        => new(ExecutableOpCode.Augmented, span, (int)op);

    public static ExecutableInstruction Unary(ExecutableUnaryOperator op, LythonSourceSpan span)
        => new(ExecutableOpCode.Unary, span, (int)op);

    public static ExecutableInstruction AbruptJump(int targetBlockIndex, bool discardIterator, LythonSourceSpan span)
        => new(ExecutableOpCode.AbruptJump, span, targetBlockIndex, discardIterator ? 1 : 0);

    public static ExecutableInstruction Jump(int targetBlockIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.Jump, span, targetBlockIndex);

    public static ExecutableInstruction JumpIfFalse(int targetBlockIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.JumpIfFalse, span, targetBlockIndex);

    public static ExecutableInstruction CompareJumpIfFalse(BinaryOperatorSyntax op, int targetBlockIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.CompareJumpIfFalse, span, targetBlockIndex, resultSlot: (int)op);

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

    public ExecutableInstruction WithTargetIndex(int targetBlockIndex)
        => OpCode is ExecutableOpCode.AbruptJump or ExecutableOpCode.Jump or ExecutableOpCode.JumpIfFalse or ExecutableOpCode.CompareJumpIfFalse
            or ExecutableOpCode.ForNext or ExecutableOpCode.EndFinally
            ? new(OpCode, Span, targetBlockIndex, _secondaryOperand, _inputStart, _resultSlot, _liveExtent)
            : throw new InvalidOperationException($"Instruction '{OpCode}' has no target block.");

    public ExecutableInstruction WithFailureIndex(int failureBlockIndex)
        => OpCode == ExecutableOpCode.MatchCase
            ? new(OpCode, Span, _primaryOperand, failureBlockIndex, _inputStart, _resultSlot, _liveExtent)
            : throw new InvalidOperationException($"Instruction '{OpCode}' has no failure block.");

    public static ExecutableInstruction Complete(LythonSourceSpan span)
        => new(ExecutableOpCode.Complete, span);

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
    int ProtectedStartIndex,
    int ProtectedEndIndex,
    int? ExceptIndex,
    int? FinallyIndex,
    int? SuiteStartIndex,
    int? SuiteEndIndex,
    int? CleanupId = null,
    int StackDepth = 0);

internal sealed record ExecutableCallSite(
    int ArgumentCount,
    CallArgumentForm[] Arguments,
    LythonSourceSpan TargetSpan,
    LythonSourceSpan CallSpan,
    bool HasReceiver = false)
{
    public ExecutableCasedSplit? CasedSplit { get; init; }
    public bool ConsumedByIteration { get; init; }
    public bool ReturnedDirectly { get; init; }
    public int UnpackedReturnCount { get; init; }
    public bool AccumulatesDictionary { get; init; }
    public bool IsPositionalOnly { get; } = Arguments.All(argument => argument.Kind == CallArgumentKind.Positional);
    public int TargetOffset => HasReceiver ? 1 : 0;
    public int ArgumentOffset => TargetOffset + 1;
}

// Execution retains declaration inputs, not the original syntax/body graph.
internal sealed record ExecutableFunctionBinding(
    string Name,
    string DeclaredName,
    LythonSourceSpan Span,
    IReadOnlyList<LoweredExpression> Decorators,
    IReadOnlyList<LoweredFunctionParameter> Parameters,
    LoweredExpression? ReturnAnnotation,
    IReadOnlyList<LoweredTypeParameter>? TypeParameters,
    ExecutableCodeObject CodeObject,
    bool IsGenerator,
    string? Docstring)
{
    public required FunctionParameterLayout ParameterLayout { get; init; }

    public int DefaultCount { get; } = Parameters.Count(static parameter => parameter.DefaultValue is not null);
    public bool HasAnnotations { get; } = ReturnAnnotation is not null || Parameters.Any(static parameter => parameter.Annotation is not null);
}

internal sealed record ExecutableMatchCaseBinding(
    LoweredRootPattern Pattern,
    LythonSourceSpan Span,
    int[] LocalBindingSlots);

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
        List<ExecutableBasicBlock> blocks,
        int entryBlockIndex,
        IReadOnlyList<ExecutableExceptionRegion> exceptionRegions,
        IReadOnlyList<ImportStatementSyntax> imports,
        ExecutableCallSite[] callSites,
        int lookupCacheCount,
        IReadOnlyList<ExecutableFunctionBinding> functions,
        IReadOnlyList<ExecutableMatchCaseBinding> matchCases,
        IReadOnlyList<LoweredStatement> statementFallbacks,
        bool requiresLocalVariableMirroring,
        bool isFunctionScope,
        int initialOperandDepth)
    {
        Name = name;
        Constants = constants;
        Names = names;
        LocalNames = localNames;
        FirstTemporarySlot = localNames.Count;
        LocalNameToSlot = localNameToSlot;
        CapturedLocalSlots = capturedLocalSlots;
        ClosureNames = closureNames;
        ClosureNameToSlot = closureNameToSlot;
        ScopeFacts = scopeFacts;
        Imports = imports;
        CallSites = callSites;
        LookupCacheCount = lookupCacheCount;
        Functions = functions;
        MatchCases = matchCases;
        PatternStorageCount = matchCases.Select(item => item.Pattern.StorageCount).DefaultIfEmpty(0).Max();
        StatementFallbacks = statementFallbacks;
        RequiresLocalVariableMirroring = requiresLocalVariableMirroring;
        IsFunctionScope = isFunctionScope;
        PrepareCallResults(blocks, callSites);
        var reads = ExecutableStackLayout.ForwardValues(this, blocks);
        // Assign physical positions after forwarding. Both execution forms use
        // this final layout, including at a dynamic-operation transition.
        var operandLayout = ExecutableStackLayout.Prepare(blocks, constants, callSites,
            exceptionRegions, entryBlockIndex, initialOperandDepth, FirstTemporarySlot);
        OperandCapacity = operandLayout.MaximumDepth;
        LastOwnedTemporarySlot = operandLayout.LastOwnedTemporarySlot;
        Instructions = ExecutableStackLayout.Linearize(blocks, operandLayout, out var offsets);
        _operandReadSpans = reads.Select(read => new ExecutableOperandSpan(
            ((long)(offsets[read.Block] + read.Instruction) << 2) | (uint)read.Input, read.Span)).ToArray();
        EntryIndex = offsets[entryBlockIndex];
        ExceptionRegions = exceptionRegions
            .Where(region => operandLayout.EntryDepths[region.ProtectedStartIndex].HasValue)
            .Select(region => region with
        {
            StackDepth = checked(FirstTemporarySlot + operandLayout.EntryDepths[region.ProtectedStartIndex]!.Value),
            ProtectedStartIndex = offsets[region.ProtectedStartIndex],
            ProtectedEndIndex = offsets[region.ProtectedEndIndex + 1] - 1,
            ExceptIndex = region.ExceptIndex is int handler ? offsets[handler] : null,
            FinallyIndex = region.FinallyIndex is int cleanup ? offsets[cleanup] : null,
            SuiteStartIndex = region.SuiteStartIndex is int start ? offsets[start] : null,
            SuiteEndIndex = region.SuiteEndIndex is int end ? offsets[end + 1] - 1 : null
        }).ToArray();
        PrepareNativeSegments(callSites);
        var integerStorageCapacity = 0;
        CanRunNativeInstructions = IsFunctionScope && !RequiresLocalVariableMirroring && capturedLocalSlots.Count == 0
            && exceptionRegions.Count == 0
            && PrepareInlineStorage(out integerStorageCapacity);
        NativeIntegerStorageCapacity = integerStorageCapacity;
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

    public ExecutableInstruction[] Instructions { get; }

    public int EntryIndex { get; }

    public IReadOnlyList<ExecutableExceptionRegion> ExceptionRegions { get; }

    public IReadOnlyList<ImportStatementSyntax> Imports { get; }

    public IReadOnlyList<ExecutableCallSite> CallSites { get; }

    public int LookupCacheCount { get; }

    public IReadOnlyList<ExecutableFunctionBinding> Functions { get; }

    public IReadOnlyList<ExecutableMatchCaseBinding> MatchCases { get; }

    public IReadOnlyList<LoweredStatement> StatementFallbacks { get; }

    public bool RequiresLocalVariableMirroring { get; }

    public bool IsFunctionScope { get; }

    public int OperandCapacity { get; }
    // The compiled local prefix is fixed; native and ordinary operands share it.
    public int FirstTemporarySlot { get; }
    public int LastOwnedTemporarySlot { get; }
    public int PatternStorageCount { get; }
    public int PatternStorageStart => checked(FirstTemporarySlot + Math.Max(4, OperandCapacity));
    public int ValueStorageCapacity => checked(PatternStorageStart + PatternStorageCount);

    public bool CanRunNativeInstructions { get; }

    public int NativeIntegerStorageCapacity { get; }

    private bool PrepareInlineStorage(out int integerStorageCapacity)
    {
        integerStorageCapacity = 0;
        var writesIntegers = false;
        foreach (ref readonly var instruction in Instructions.AsSpan())
        {
            if (!IsInlineInstruction(instruction)) return false;
            writesIntegers |= instruction.OpCode == ExecutableOpCode.Binary
                && instruction.BinaryOperator is BinaryOperatorSyntax.Add or BinaryOperatorSyntax.Subtract
                    or BinaryOperatorSyntax.Multiply or BinaryOperatorSyntax.FloorDivide or BinaryOperatorSyntax.Modulo
                || instruction.OpCode == ExecutableOpCode.Augmented
                && instruction.AugmentedOperator is AugmentedAssignmentOperatorSyntax.Add or AugmentedAssignmentOperatorSyntax.Subtract
                    or AugmentedAssignmentOperatorSyntax.Multiply or AugmentedAssignmentOperatorSyntax.FloorDivide
                    or AugmentedAssignmentOperatorSyntax.Modulo;
        }
        // Reuse the eligibility pass: helpers which never write this private
        // representation need neither its working buffer nor an admission.
        integerStorageCapacity = writesIntegers ? ValueStorageCapacity : 0;
        return true;
    }

    private bool CrossesExceptionBoundary(int start, int end)
    {
        // A fused segment must select the same handler at every original PC.
        foreach (var region in ExceptionRegions)
            if (Inside(region.ProtectedStartIndex) || Inside(region.ProtectedEndIndex + 1)
                || Inside(region.ExceptIndex) || Inside(region.FinallyIndex)
                || Inside(region.SuiteStartIndex) || Inside(region.SuiteEndIndex is int last ? last + 1 : null)) return true;
        return false;
        bool Inside(int? position) => position > start && position <= end;
    }

    private static bool IsInlineInstruction(ExecutableInstruction instruction) => instruction.OpCode is
        ExecutableOpCode.LoadConst or ExecutableOpCode.LoadLocal or ExecutableOpCode.LoadGlobal
        or ExecutableOpCode.LoadClosure or ExecutableOpCode.StoreLocal or ExecutableOpCode.StoreClosure
        or ExecutableOpCode.LoadCallMember or ExecutableOpCode.Call or ExecutableOpCode.Unary
        or ExecutableOpCode.Binary or ExecutableOpCode.Augmented or ExecutableOpCode.Subscript
        or ExecutableOpCode.CompareJumpIfFalse or ExecutableOpCode.Jump or ExecutableOpCode.JumpIfFalse
        or ExecutableOpCode.GetIter or ExecutableOpCode.ForNext or ExecutableOpCode.AbruptJump
        or ExecutableOpCode.StartFormattedString or ExecutableOpCode.AppendFormattedText
        or ExecutableOpCode.FinishFormattedString or ExecutableOpCode.ConvertFormattedValue or ExecutableOpCode.FormatField
        or ExecutableOpCode.MakeList or ExecutableOpCode.MakeTuple or ExecutableOpCode.Return
        or ExecutableOpCode.ReturnNone or ExecutableOpCode.Complete or ExecutableOpCode.Dup or ExecutableOpCode.PopTop;
}
