using System.Globalization;
using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Frontend;

internal enum ExecutableOpCode
{
    Import,
    LoadConst,
    LoadLocal,
    LoadClosure,
    LoadGlobal,
    LoadGlobalForArithmeticCall,
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
    MakeRecord,
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
    ForNextUnpack,
    StartFunctionDefaults,
    StoreFunctionDefault,
    CheckLocal,
    ReverseAssignmentValues,
    IntegerCallUpdate,
    PreparedTupleReturn,
    LoadCasedSplit,
    LoadDictionaryGetKey,
    ApplyDeferredDefinition,
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
    private const int IntegerContinuationBit = int.MinValue;

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

    internal bool TerminatesBlock => OpCode is ExecutableOpCode.Jump or ExecutableOpCode.AbruptJump or
        ExecutableOpCode.EndFinally or ExecutableOpCode.Return or ExecutableOpCode.ReturnNone or
        ExecutableOpCode.Raise or ExecutableOpCode.ReraiseException or ExecutableOpCode.FailAssertion;

    // Compiler-owned storage positions. They share the existing instruction;
    // no input-plan table or runtime shape calculation is needed.
    public int InputStart => _inputStart;
    public int SecondInput => _secondaryOperand;
    public int ThirdInput => OpCode is ExecutableOpCode.StoreSubscript or ExecutableOpCode.StoreSubscriptLast
        ? _resultSlot : throw InvalidOperand(nameof(ThirdInput));
    internal int ValueInputCount => OpCode switch
    {
        ExecutableOpCode.Binary or ExecutableOpCode.Augmented or ExecutableOpCode.Subscript or ExecutableOpCode.CompareJumpIfFalse => 2,
        ExecutableOpCode.Call when _secondaryOperand != int.MaxValue => 2,
        ExecutableOpCode.StoreSubscript or ExecutableOpCode.StoreSubscriptLast => 3,
        ExecutableOpCode.Unary or ExecutableOpCode.StoreLocal or ExecutableOpCode.Return or
            ExecutableOpCode.LoadMember or ExecutableOpCode.LoadCallMember or ExecutableOpCode.LoadCasedSplit or
            ExecutableOpCode.GetIter or ExecutableOpCode.JumpIfFalse => 1,
        _ => 0,
    };
    // A subscript store pushes no result: its output position is its live
    // extent. Reuse that otherwise redundant field for its third input, keeping
    // every instruction at the existing size, including native helper code.
    public int ResultSlot => OpCode is ExecutableOpCode.StoreSubscript or ExecutableOpCode.StoreSubscriptLast or ExecutableOpCode.CompareJumpIfFalse
        ? LiveExtent : _resultSlot;
    // A complemented prepared extent marks the adjacent result consumer.
    // Decode the original extent; physical positions never name locals.
    public int LiveExtent => HasResultConsumer ? ~_liveExtent : _liveExtent;
    public bool HasResultConsumer => _liveExtent < 0;

    internal ExecutableInstruction WithStorageSlots(int inputStart, int resultSlot, int liveExtent,
        int inputCount = 0, int secondInput = int.MaxValue, int thirdInput = int.MaxValue)
        => new(OpCode, Span, _primaryOperand,
            inputCount > 1 ? secondInput : _secondaryOperand,
            inputStart, OpCode == ExecutableOpCode.CompareJumpIfFalse ? _resultSlot
                : inputCount == 3 ? thirdInput : resultSlot,
            HasResultConsumer ? ~liveExtent : liveExtent);

    internal ExecutableInstruction WithValueInputs(int first, int second, int third = int.MaxValue)
        => new(OpCode, Span, _primaryOperand,
            ValueInputCount > 1 ? second : _secondaryOperand,
            first, ValueInputCount == 3 ? third : _resultSlot, _liveExtent);

    // The following consumer retains its source span and is also the
    // continuation when input cleanup must finish asynchronously.
    internal ExecutableInstruction WithResultConsumer()
        => new(OpCode, Span, _primaryOperand, _secondaryOperand,
            _inputStart, _resultSlot, ~_liveExtent);

    public int ImportIndex => ReadPrimaryOperand(ExecutableOpCode.Import, nameof(ImportIndex));
    public int ConstantIndex => OpCode is ExecutableOpCode.LoadConst or ExecutableOpCode.ApplyOperation
        or ExecutableOpCode.ApplyDeferredDefinition
        or ExecutableOpCode.AppendFormattedText or ExecutableOpCode.FormatField or ExecutableOpCode.MakeRecord
        or ExecutableOpCode.StartFunctionDefaults
        ? _primaryOperand : throw InvalidOperand(nameof(ConstantIndex));
    internal int ImmediatelyCalledDefinitionCapture => _secondaryOperand;
    internal ExecutableInstruction WithImmediatelyCalledDefinition(int capture)
        => new(OpCode, Span, _primaryOperand, capture, _inputStart, _resultSlot, _liveExtent);
    internal int DeferredKeywordSlot => OpCode == ExecutableOpCode.ApplyDeferredDefinition
        ? _secondaryOperand : throw InvalidOperand(nameof(DeferredKeywordSlot));
    internal ExecutableInstruction WithDeferredDefinition(int keywordSlot)
        => new(ExecutableOpCode.ApplyDeferredDefinition, Span, _primaryOperand, keywordSlot,
            _inputStart, _resultSlot, _liveExtent);
    internal int DefaultStorageDepth => _primaryOperand;
    internal int DefaultValueIndex => _secondaryOperand;
    public int LocalSlot => OpCode is ExecutableOpCode.LoadLocal or ExecutableOpCode.LoadDictionaryGetKey or
            ExecutableOpCode.StoreLocal or ExecutableOpCode.CheckLocal
            or ExecutableOpCode.IntegerCallUpdate or ExecutableOpCode.PreparedTupleReturn
            ? _primaryOperand : throw InvalidOperand(nameof(LocalSlot));
    // Before layout assigns a result slot, a LoadLocal can name its
    // same-block consumer in that field. No hint survives in the final tape.
    internal int CheckedLocalConsumer => _resultSlot;
    internal ExecutableInstruction WithCheckedLocalConsumer(int index)
        => new(ExecutableOpCode.LoadLocal, Span, _primaryOperand, resultSlot: index);
    internal ExecutableInstruction AsCheckLocal()
        => new(ExecutableOpCode.CheckLocal, Span, _primaryOperand);
    internal int DictionaryGetCallOffset => _secondaryOperand;
    internal ExecutableInstruction WithDictionaryGetKey(int offset)
        => new(ExecutableOpCode.LoadDictionaryGetKey, Span, _primaryOperand, offset,
            _inputStart, _resultSlot, _liveExtent);
    internal int IntegerUpdateCallIndex => _secondaryOperand;
    internal bool IntegerUpdateChecksLocal => LiveExtent == ResultSlot;
    internal ExecutableInstruction WithIntegerUpdateCall(int callIndex)
        => new(ExecutableOpCode.IntegerCallUpdate, Span, _primaryOperand, callIndex, _inputStart, _resultSlot, _liveExtent);
    internal int PreparedTupleIndex => _secondaryOperand;
    internal ExecutableInstruction WithPreparedTupleReturn(int tupleIndex)
        => new(ExecutableOpCode.PreparedTupleReturn, Span, _primaryOperand, tupleIndex, _inputStart, _resultSlot, _liveExtent);
    // StoreLocal has only one input, leaving its second operand field free.
    internal bool StoresCapturedLocal => _secondaryOperand != int.MaxValue;
    internal ExecutableInstruction WithCapturedLocalStore()
        => new(OpCode, Span, _primaryOperand, 0, _inputStart, _resultSlot, _liveExtent);
    public int ClosureSlot => OpCode is ExecutableOpCode.LoadClosure or ExecutableOpCode.StoreClosure
        ? _primaryOperand : throw InvalidOperand(nameof(ClosureSlot));
    internal bool HasClosureArithmetic => OpCode == ExecutableOpCode.LoadClosure && _secondaryOperand != int.MaxValue;
    internal int ClosureArithmeticConstant => OpCode == ExecutableOpCode.LoadClosure
        ? _secondaryOperand : throw InvalidOperand(nameof(ClosureArithmeticConstant));
    internal ExecutableInstruction WithClosureArithmetic(int constantIndex)
        => new(OpCode, Span, _primaryOperand, constantIndex, _inputStart, _resultSlot, _liveExtent);
    internal ExecutableInstruction WithArithmeticCall()
        => new(ExecutableOpCode.LoadGlobalForArithmeticCall, Span, _primaryOperand, _secondaryOperand,
            _inputStart, _resultSlot, _liveExtent);
    public int NameIndex => OpCode is ExecutableOpCode.LoadGlobal or ExecutableOpCode.LoadGlobalForArithmeticCall or ExecutableOpCode.LoadName
        or ExecutableOpCode.LoadMember or ExecutableOpCode.LoadCallMember or ExecutableOpCode.LoadCasedSplit
        or ExecutableOpCode.StoreGlobal or ExecutableOpCode.StoreName or ExecutableOpCode.BindException
        or ExecutableOpCode.StoreMember or ExecutableOpCode.StoreMemberLast
        or ExecutableOpCode.DeleteName or ExecutableOpCode.DeleteMember
        ? _primaryOperand : throw InvalidOperand(nameof(NameIndex));
    public int LookupCacheIndex => OpCode == ExecutableOpCode.Subscript ? _primaryOperand
        : OpCode is ExecutableOpCode.LoadMember or ExecutableOpCode.LoadCallMember or ExecutableOpCode.LoadCasedSplit or ExecutableOpCode.MakeDict
            ? _secondaryOperand : throw InvalidOperand(nameof(LookupCacheIndex));
    public bool DiscardIterator => OpCode == ExecutableOpCode.AbruptJump
        ? _secondaryOperand != 0 : throw InvalidOperand("ItemCount");
    public int ItemCount => OpCode is ExecutableOpCode.MakeList or ExecutableOpCode.MakeTuple or ExecutableOpCode.MakeSet
        ? _primaryOperand : throw InvalidOperand(nameof(ItemCount));
    public int AssignmentValueCount => ReadPrimaryOperand(ExecutableOpCode.ReverseAssignmentValues, nameof(AssignmentValueCount));
    public int PairCount => ReadPrimaryOperand(ExecutableOpCode.MakeDict, nameof(PairCount));
    public int FormattedInitialCapacity => ReadPrimaryOperand(ExecutableOpCode.StartFormattedString, nameof(FormattedInitialCapacity));
    public char FormattedConversion => (char)ReadPrimaryOperand(ExecutableOpCode.ConvertFormattedValue, nameof(FormattedConversion));
    public int MatchCaseIndex => ReadPrimaryOperand(ExecutableOpCode.MatchCase, nameof(MatchCaseIndex));
    public int FailureIndex => OpCode == ExecutableOpCode.MatchCase
        ? _secondaryOperand : throw InvalidOperand("BlockIndex");
    public int TargetIndex => OpCode is ExecutableOpCode.AbruptJump or ExecutableOpCode.Jump or ExecutableOpCode.JumpIfFalse or ExecutableOpCode.CompareJumpIfFalse
        or ExecutableOpCode.ForNext or ExecutableOpCode.ForNextDictionaryAccumulation or ExecutableOpCode.ForNextUnpack or ExecutableOpCode.EndFinally
        ? _primaryOperand : throw InvalidOperand("BlockIndex");
    internal int DictionaryLoopCallIndex => OpCode == ExecutableOpCode.ForNextDictionaryAccumulation
        ? _secondaryOperand : throw InvalidOperand(nameof(DictionaryLoopCallIndex));
    public int CallSiteIndex => ReadPrimaryOperand(ExecutableOpCode.Call, nameof(CallSiteIndex));
    public int CleanupId => OpCode == ExecutableOpCode.EndFinally ? _secondaryOperand
        : ReadPrimaryOperand(ExecutableOpCode.ExitContextManager, nameof(CleanupId));
    public int ExceptionNameIndex => ReadPrimaryOperand(ExecutableOpCode.ClearException, nameof(ExceptionNameIndex));
    public ExecutableSliceParts SliceParts => (ExecutableSliceParts)_primaryOperand;
    public BinaryOperatorSyntax BinaryOperator
        => (BinaryOperatorSyntax)(OpCode == ExecutableOpCode.CompareJumpIfFalse ? _resultSlot : _primaryOperand & ~IntegerContinuationBit);

    // A Binary instruction's operator leaves room for a continuation bit.
    // Only a physically adjacent operation in the same block can follow it.
    internal bool HasIntegerContinuation => (_primaryOperand & IntegerContinuationBit) != 0;

    internal ExecutableInstruction WithIntegerContinuation()
        => new(OpCode, Span, _primaryOperand | IntegerContinuationBit, _secondaryOperand,
            _inputStart, _resultSlot, _liveExtent);
    public ExecutableUnaryOperator UnaryOperator => (ExecutableUnaryOperator)_primaryOperand;
    public AugmentedAssignmentOperatorSyntax AugmentedOperator => (AugmentedAssignmentOperatorSyntax)_primaryOperand;

    public int TargetSpanIndex => OpCode is ExecutableOpCode.StoreMember or ExecutableOpCode.StoreMemberLast
        ? _secondaryOperand : OpCode is ExecutableOpCode.StoreSubscript or ExecutableOpCode.StoreSubscriptLast
            or ExecutableOpCode.StoreSlice or ExecutableOpCode.StoreSliceLast
            ? _primaryOperand & int.MaxValue : throw InvalidOperand(nameof(TargetSpanIndex));
    internal bool StoresBorrowableTextKey => _primaryOperand < 0;
    internal ExecutableInstruction WithBorrowableTextStore()
        => new(OpCode, Span, _primaryOperand | int.MinValue, _secondaryOperand, _inputStart, _resultSlot, _liveExtent);
    public bool ValueLast => OpCode is ExecutableOpCode.StoreMemberLast or ExecutableOpCode.StoreSubscriptLast or ExecutableOpCode.StoreSliceLast;
    public UnpackingLayout UnpackLayout => OpCode == ExecutableOpCode.UnpackValues
        ? new(_primaryOperand, _secondaryOperand) : throw InvalidOperand(nameof(UnpackLayout));
    public bool HasAssertionMessage => ReadPrimaryOperand(ExecutableOpCode.FailAssertion, nameof(HasAssertionMessage)) != 0;
    public bool HasRaiseCause => ReadPrimaryOperand(ExecutableOpCode.Raise, nameof(HasRaiseCause)) != 0;

    public static ExecutableInstruction Import(int importIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.Import, span, importIndex);

    public static ExecutableInstruction ApplyOperation(int constantIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.ApplyOperation, span, constantIndex);

    internal static ExecutableInstruction StartFunctionDefaults(int definition, LythonSourceSpan span)
        => new(ExecutableOpCode.StartFunctionDefaults, span, definition);

    internal static ExecutableInstruction StoreFunctionDefault(int depth, int index, LythonSourceSpan span)
        => new(ExecutableOpCode.StoreFunctionDefault, span, depth, index);

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

    public static ExecutableInstruction ReverseAssignmentValues(int count, LythonSourceSpan span)
        => new(ExecutableOpCode.ReverseAssignmentValues, span, count);
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

    internal ExecutableInstruction AsCasedSplit()
        => new(ExecutableOpCode.LoadCasedSplit, Span, _primaryOperand, _secondaryOperand);

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

    public static ExecutableInstruction MakeDict(int pairCount, int cacheIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.MakeDict, span, pairCount, cacheIndex);

    public static ExecutableInstruction MakeRecord(int keysIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.MakeRecord, span, keysIndex);

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

    // Applied after physical slots and the loop's exit address are known.
    internal int FixedUnpackLocalStart => _secondaryOperand;

    internal ExecutableInstruction WithFixedUnpack(int localStart)
        => new(ExecutableOpCode.ForNextUnpack, Span, _primaryOperand, localStart, _inputStart, _resultSlot, _liveExtent);

    // Final-layout preparation changes no positions or storage capacity. The
    // ordinary body remains the continuation for protocols and admission.
    internal ExecutableInstruction WithDictionaryLoop(int callIndex)
        => new(ExecutableOpCode.ForNextDictionaryAccumulation, Span, _primaryOperand,
            callIndex, _inputStart, _resultSlot, _liveExtent);


    public static ExecutableInstruction Call(int callSiteIndex, LythonSourceSpan span)
        => new(ExecutableOpCode.Call, span, callSiteIndex);

    // Single-positional calls can use the common two-operand encoding. The
    // result position also identifies the existing contiguous fallback input.
    internal bool HasForwardedCallOperands => _secondaryOperand != int.MaxValue;

    internal ExecutableInstruction WithForwardedCallOperands()
        => new(OpCode, Span, _primaryOperand, 0);

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

internal sealed record ExecutableExceptionRegion(
    int ProtectedStartIndex,
    int ProtectedEndIndex,
    int? ExceptIndex,
    int? FinallyIndex,
    int? SuiteStartIndex,
    int? SuiteEndIndex,
    int? CleanupId = null,
    int StackDepth = 0);

internal record ExecutableCallSite(
    int ArgumentCount,
    CallArgumentForm[] Arguments,
    LythonSourceSpan TargetSpan,
    LythonSourceSpan CallSpan,
    bool HasReceiver = false)
{
    public ExecutableSplitPrefix? SplitPrefix { get; init; }
    public bool ConsumedByIteration { get; init; }
    public bool ReturnedDirectly { get; init; }
    public int UnpackedReturnCount { get; init; }
    public bool AccumulatesDictionary { get; init; }
    public bool IsPositionalOnly { get; } = Arguments.All(argument => argument.Kind == CallArgumentKind.Positional);
    public int TargetOffset => HasReceiver ? 1 : 0;
    public int ArgumentOffset => TargetOffset + 1;
}

internal sealed record ExecutableNamedFormatCallSite(int ArgumentCount, CallArgumentForm[] Arguments,
    LythonSourceSpan TargetSpan, LythonSourceSpan CallSpan, bool HasReceiver, Dictionary<string, int> KeywordIndices)
    : ExecutableCallSite(ArgumentCount, Arguments, TargetSpan, CallSpan, HasReceiver);

internal sealed record ExecutableMatchCaseBinding(
    LoweredRootPattern Pattern,
    LythonSourceSpan Span,
    int[] LocalBindingSlots);

// A nonnegative index names a local cell. A complemented index names an
// incoming cell in the defining code's capture array.
internal sealed record ExecutableCaptureSources(IReadOnlyDictionary<string, int> ParentLocalSlots, int[] Slots, bool CanCaptureByValue);

internal sealed partial class ExecutableCodeObject
{
    private const int CapturedDefinitionPair = -1;
    private readonly int _definitionPairFirstCreation;
    internal const int ClosedCaptureCapacity = 4;
    internal PyDict.RecordKeys? DefinitionAnnotationKeys { get; set; }

    public ExecutableCodeObject(
        string name,
        object?[] constants,
        string[] names,
        IReadOnlyList<string> localNames,
        IReadOnlyDictionary<string, int> localNameToSlot,
        IReadOnlyList<int> capturedLocalSlots,
        IReadOnlyList<string> closureNames,
        IReadOnlyDictionary<string, int> closureNameToSlot,
        ScopeDirectiveFacts scopeFacts,
        List<List<ExecutableInstruction>> blocks,
        int entryBlockIndex,
        IReadOnlyList<ExecutableExceptionRegion> exceptionRegions,
        IReadOnlyList<ImportStatementSyntax> imports,
        ExecutableCallSite[] callSites,
        int lookupCacheCount,
        IReadOnlyList<ExecutableMatchCaseBinding> matchCases,
        bool requiresLocalVariableMirroring,
        bool usesOnlyLexicalSlots,
        bool isFunctionScope,
        int initialOperandDepth,
        PyString? functionName,
        PyString? docstring,
        MemoryGovernor? governor,
        CancellationToken cancellationToken)
    {
        Name = name;
        FunctionName = functionName;
        Docstring = docstring;
        Constants = constants;
        _machineConstants = PrepareMachineConstants(constants, governor, cancellationToken);
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
        MatchCases = matchCases;
        PatternStorageCount = matchCases.Select(item => item.Pattern.StorageCount).DefaultIfEmpty(0).Max();
        RequiresLocalVariableMirroring = requiresLocalVariableMirroring;
        IsFunctionScope = isFunctionScope;
        UsesOnlyLexicalSlots = usesOnlyLexicalSlots;
        // Prepare reachable operands and their final storage. Both execution
        // forms use this layout, including at a dynamic-operation transition.
        var regionsByEntry = exceptionRegions.Count == 0 ? [] :
            exceptionRegions.OrderBy(region => region.ProtectedStartIndex).ToArray();
        var deferredCandidates = regionsByEntry.Length == 0 && blocks.Count == 1
            ? FindDeferredDefinitionLocals(blocks[0], cancellationToken) : null;
        var operandLayout = ExecutableStackLayout.Prepare(this, blocks, callSites,
            regionsByEntry, entryBlockIndex, initialOperandDepth, cancellationToken);
        OperandCapacity = operandLayout.MaximumDepth;
        PatternStorageStart = checked(FirstTemporarySlot + Math.Max(4, OperandCapacity));
        ValueStorageCapacity = checked(PatternStorageStart + PatternStorageCount);
        LastOwnedTemporarySlot = operandLayout.LastOwnedTemporarySlot;
        Instructions = ExecutableStackLayout.Linearize(this, blocks, operandLayout, regionsByEntry.Length == 0,
            cancellationToken, out _operandReadSpans);
        var positions = operandLayout.Blocks;
        EntryIndex = positions[entryBlockIndex].Offset;
        // Original block widths preserve inner-first unwinding, including
        // ranges that become physically equal after unreachable blocks vanish.
        ExceptionRegions = regionsByEntry
            .Where(region => positions[region.ProtectedStartIndex].IsReachable)
            .OrderBy(region => region.ProtectedEndIndex - region.ProtectedStartIndex)
            .Select(region =>
            {
                // A suite may have no remaining instructions. Keep its bounds
                // paired, since runtime routing tests containment in this suite.
                var suiteStart = region.SuiteStartIndex is int start ? positions[start].Offset : (int?)null;
                var suiteEnd = region.SuiteEndIndex is int end ? positions[end + 1].Offset - 1 : (int?)null;
                if (suiteStart > suiteEnd) suiteStart = suiteEnd = null;
                return region with
                {
                    StackDepth = checked(FirstTemporarySlot + positions[region.ProtectedStartIndex].EntryDepth),
                    ProtectedStartIndex = positions[region.ProtectedStartIndex].Offset,
                    ProtectedEndIndex = positions[region.ProtectedEndIndex + 1].Offset - 1,
                    ExceptIndex = region.ExceptIndex is int handler ? ResolvedOffset(handler) : null,
                    FinallyIndex = region.FinallyIndex is int cleanup ? ResolvedOffset(cleanup) : null,
                    SuiteStartIndex = suiteStart,
                    SuiteEndIndex = suiteEnd
                };
            }).ToArray();
        // Native work can precede cleanup without owning its routing state.
        // A physical instruction frontier also covers normal fallthrough and
        // jumps: transfer once before entering any finally suite.
        NativePrefixEnd = Instructions.Length;
        foreach (var region in ExceptionRegions)
            if (region.FinallyIndex is int cleanup) NativePrefixEnd = Math.Min(NativePrefixEnd, cleanup);
        PrepareNativeSegments(callSites);
        // Lexical/storage admission only. The native loop transfers at the
        // actual operation needing a frame, without a second opcode inventory.
        CanRunNativeInstructions = IsFunctionScope && !RequiresLocalVariableMirroring && EntryIndex < NativePrefixEnd
            && (capturedLocalSlots.Count == 0 ||
                usesOnlyLexicalSlots && ValueStorageCapacity <= LythonRuntime.NativeValueCapacity);
        HasDirectOperandReturn = CanRunNativeInstructions && HasClosedOperandReturn(cancellationToken);
        if (CanRunNativeInstructions)
        {
            PrepareImmediatelyCalledDefinition(cancellationToken);
            DirectDefinitionReturn = PrepareDirectDefinitionReturn(cancellationToken, out _definitionPairFirstCreation);
            DirectCasedReturnIndex = PrepareDirectCasedReturn(cancellationToken);
            DeferredDefinitionSites = PrepareDeferredDefinitions(deferredCandidates, cancellationToken, out var valueCapacity);
            ValueStorageCapacity = valueCapacity;
        }

        int ResolvedOffset(int block) => positions[ExecutableStackLayout.ResolveTarget(blocks, positions, block, cancellationToken)].Offset;
    }

    public string Name { get; }
    internal PyString? FunctionName { get; }
    internal PyString? QualifiedName { get; init; }
    internal bool UsesOnlyLexicalSlots { get; }
    internal PyString? Docstring { get; }

    // Builder-owned arrays are immutable after preparation. Operand reads use
    // their physical bounds directly, without another collection interface.
    public object?[] Constants { get; }
    private readonly long?[]? _machineConstants;

    // The original objects retain Python-visible identity and overflow
    // behavior. Arithmetic can read exact signed literals without reclassifying
    // their immutable BigInteger objects on each execution.
    internal bool TryReadMachineConstant(int index, out long value)
    {
        if (_machineConstants is { } values && values[index] is { } integer)
        {
            value = integer;
            return true;
        }
        value = 0;
        return false;
    }

    private static long?[]? PrepareMachineConstants(object?[] constants, MemoryGovernor? governor, CancellationToken token)
    {
        for (var first = 0; first < constants.Length; first++)
        {
            PythonTokenization.CheckCancellation(token, first);
            if (!LythonRuntime.TryMachineInteger(constants[first], out var initial)) continue;
            var bytes = checked(32L + 16L * constants.Length);
            using var storage = governor?.ReserveTemporary(bytes, null);
            PythonTokenization.CheckCancellation(token, 0);
            var prepared = new long?[constants.Length];
            prepared[first] = initial;
            for (var index = first + 1; index < prepared.Length; index++)
            {
                PythonTokenization.CheckCancellation(token, index);
                if (LythonRuntime.TryMachineInteger(constants[index], out var integer))
                    prepared[index] = integer;
            }
            storage?.Commit(bytes);
            return prepared;
        }
        return null;
    }

    public string[] Names { get; }
    internal int CaptureOperandBase => checked(Constants.Length + Names.Length);

    // A location fact, never a cached guest value. Independently compiled code
    // keeps ordinary namespace resolution; active modules prove exact identity.
    internal ExecutableCodeObject? GlobalModule { get; private set; }
    internal int[] ModuleGlobalSlots { get; private set; } = [];
    internal ExecutableCaptureSources? CaptureSources { get; private set; }
    internal ExecutableCreateFunction? DirectDefinitionReturn { get; }
    // A positive index describes a defaulted pair. Captured pairs use the
    // same compiler-proved entry and need no creation index.
    internal int FirstPairedDefaultCreationIndex => _definitionPairFirstCreation;
    internal bool HasDirectDefinitionPair => _definitionPairFirstCreation != 0;
    internal bool HasDirectCapturedDefinitionPair => _definitionPairFirstCreation == CapturedDefinitionPair;
    internal int DirectCasedReturnIndex { get; } = -1;
    internal bool ForwardsCaptures { get; init; }
    internal int FirstSnapshotCell { get; init; }
    internal int[]? SnapshotTailOrder { get; init; }

    internal void PrepareModuleGlobals(ExecutableCodeObject module, CancellationToken cancellationToken)
    {
        var slots = new int[Names.Length];
        for (var i = 0; i < slots.Length; i++)
        {
            if ((i & 1023) == 0) PythonTokenization.CheckCancellation(cancellationToken, 0);
            slots[i] = module.LocalNameToSlot.TryGetValue(Names[i], out var slot) ? slot : -1;
        }
        ModuleGlobalSlots = slots;
        GlobalModule = module;
    }

    internal void PrepareCaptureSources(IReadOnlyDictionary<string, int> parentLocals,
        IReadOnlyDictionary<string, int> parentClosures, bool canCaptureByValue, MemoryGovernor? governor, CancellationToken cancellationToken)
    {
        var span = Instructions[EntryIndex].Span;
        var bytes = checked(64L + sizeof(int) * (long)ClosureNames.Count);
        using var storage = governor?.ReserveTemporary(bytes, span);
        PythonTokenization.CheckCancellation(cancellationToken, span.Start);
        var slots = new int[ClosureNames.Count];
        for (var i = 0; i < slots.Length; i++)
        {
            if ((i & 255) == 0) PythonTokenization.CheckCancellation(cancellationToken, span.Start);
            var name = ClosureNames[i];
            slots[i] = parentLocals.TryGetValue(name, out var local) ? local : ~parentClosures[name];
        }
        // The builder's local-slot map also belongs to the final parent code.
        var sources = new ExecutableCaptureSources(parentLocals, slots, canCaptureByValue);
        storage?.Commit(bytes);
        CaptureSources = sources;
    }

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


    public IReadOnlyList<ExecutableMatchCaseBinding> MatchCases { get; }

    public bool RequiresLocalVariableMirroring { get; }

    internal bool HasPlainModuleClass { get; init; }

    public bool IsFunctionScope { get; }

    public int OperandCapacity { get; }
    // The compiled local prefix is fixed; native and ordinary operands share it.
    public int FirstTemporarySlot { get; }
    public int LastOwnedTemporarySlot { get; }
    public int PatternStorageCount { get; }
    public int PatternStorageStart { get; }
    public int ValueStorageCapacity { get; }
    internal int[]? DeferredDefinitionSites { get; }

    public bool CanRunNativeInstructions { get; }

    internal bool HasDirectOperandReturn { get; }

    public int NativePrefixEnd { get; }

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


}
