namespace Lokad.Lython.Runtime;

internal sealed partial class ExecutionState
{
    // One execution owns this stack, including across async suspension. It is
    // activation state, not an ownership registry or a scalar-identity ledger.
    private NativeActivation[] _nativeActivations = [];
    private int _nativeActivationCount;
    private long _nativeActivationBytes;
    private const long NativeActivationArrayBytes = 32;
    private const long NativeActivationSlotBytes = 64;

    internal struct NativeActivation
    {
        internal LythonRuntime.ICallable? Target;
        internal LythonRuntime.ExecutionContext? Context;
        internal CallArgumentValue[]? Arguments;
        internal object[]? BoundArguments;
        internal object? WorkingStorage;
        internal object? PendingResult;
    }

    internal ReadOnlySpan<NativeActivation> NativeActivations => _nativeActivations.AsSpan(0, _nativeActivationCount);
    internal int CurrentNativeActivation => _nativeActivationCount - 1;
    internal LythonRuntime.ExecutableFrameState? ActiveExecutableFrame { get; set; }

    internal void SetNativeInvocationFrame(LythonRuntime.ExecutionContext context, object[] arguments)
    {
        if (_nativeActivationCount == 0) return;
        _nativeActivations[_nativeActivationCount - 1].Context = context;
        _nativeActivations[_nativeActivationCount - 1].BoundArguments = arguments;
    }

    internal NativeActivationScope EnterNativeActivation(
        LythonRuntime.ICallable target,
        CallArgumentValue[]? arguments,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan span)
    {
        if (_nativeActivationCount == _nativeActivations.Length)
        {
            var capacity = _nativeActivations.Length == 0 ? 1 : checked(_nativeActivations.Length * 2);
            var bytes = checked(NativeActivationArrayBytes + NativeActivationSlotBytes * capacity);
            // Both arrays coexist during growth. Fund the complete replacement,
            // then publish it before releasing the discarded backing charge.
            MemoryGovernor.Reserve(bytes, span);
            try
            {
                var replacement = new NativeActivation[capacity];
                _nativeActivations.CopyTo(replacement, 0);
                _nativeActivations = replacement;
                MemoryGovernor.Commit(bytes);
                MemoryGovernor.Release(_nativeActivationBytes);
                _nativeActivationBytes = bytes;
            }
            catch
            {
                MemoryGovernor.ReleaseReserved(bytes);
                throw;
            }
        }

        var index = _nativeActivationCount++;
        _nativeActivations[index] = new() { Target = target, Arguments = arguments, Context = context };
        return new(this, index);
    }

    internal void SetNativeBoundArguments(object[] arguments)
    {
        if (_nativeActivationCount != 0) _nativeActivations[_nativeActivationCount - 1].BoundArguments = arguments;
    }

    internal void SetNativeWorkingStorage(int index, object? storage)
    {
        if (index < 0) return; // Direct subsystem invocations have no public call activation.
        if ((uint)index >= (uint)_nativeActivationCount) throw new InvalidOperationException("Native activation is not active.");
        _nativeActivations[index].WorkingStorage = storage;
    }

    internal void TrackNativeCallResult(object result, LythonSourceSpan? span)
    {
        if (_nativeActivationCount != 0) _nativeActivations[_nativeActivationCount - 1].PendingResult = result;
        CallTemporaries.TrackCallResult(result, span);
    }

    internal readonly struct NativeActivationScope(ExecutionState state, int index) : IDisposable
    {
        internal void SetArguments(CallArgumentValue[] arguments) => state._nativeActivations[index].Arguments = arguments;
        internal void SetResult(object result) => state._nativeActivations[index].PendingResult = result;
        public void Dispose()
        {
            if (state._nativeActivationCount != index + 1) throw new InvalidOperationException("Native activation completion is out of order.");
            state._nativeActivations[index] = default;
            state._nativeActivationCount = index;
        }
    }
}
