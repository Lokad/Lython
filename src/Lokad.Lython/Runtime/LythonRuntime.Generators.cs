using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static LythonRuntimeException GeneratorRaisedStopIteration(
        LythonRuntimeException exception, ExecutionContext context, LythonSourceSpan span)
    {
        var cause = CreatePythonExceptionInstance(exception);
        return new LythonRuntimeException("RuntimeError", "generator raised StopIteration", span, exception)
        { PythonCause = cause, PythonContext = cause, SuppressPythonContext = true };
    }

    internal sealed class PyGenerator : IPyAsyncIteratorValue, IPyDynamicAttributes, IPyTruthyValue
    {
        private ExecutionContext? _frame;
        private readonly LythonSourceSpan _span;
        private bool _started, _completed, _running;
        private PyException? _suspendedException;
        public object ReturnValue { get; private set; } = PyNone.Instance;
        public MemoryGovernor? OwnerMemoryGovernor { get; }
        public LythonSourceSpan? AllocationSpan => _span;

        private static long EstimateStorage(ExecutableCodeObject code)
            => checked(512L + 96L * (code.LocalNames.Count + code.LookupCacheCount + code.ExceptionRegions.Count));

        internal static async ValueTask<PyGenerator> CreateExpressionAsync(ExecutableCodeObject code,
            ExecutionContext context, object outerValue, LythonSourceSpan outerSpan, LythonSourceSpan span, bool asynchronous)
        {
            // Fund the retained frame, namespace/nonlocal map and native cursor
            // before their allocation or the effectful outer __iter__ call.
            var bytes = checked(EstimateStorage(code) + 256L + 96L * code.ScopeFacts.NonlocalNames.Count);
            var governor = context.MemoryGovernor;
            governor.Reserve(bytes, span);
            governor.Commit(bytes);
            PyIteration.Cursor? cursor = null;
            PyGenerator? generator = null;
            try
            {
                // A class suite's new __class__ cell belongs to its function
                // closure. Ordinary suite lookup deliberately skips that cell
                // and could otherwise select an enclosing class instead.
                var closure = context.IsClassBody ? context.FunctionClosureContext : context;
                var cells = CaptureExecutableClosures(code, closure, span);
                var frame = new ExecutionContext(context.FunctionClosureContext, code.ScopeFacts)
                { FunctionName = "<genexpr>", FirstPositionalParameterName = ".0" };
                generator = new PyGenerator(code, frame, span, cells, default);
                var outer = asynchronous
                    ? await PyIteration.AcquireGeneratorOuterAsync(outerValue, outerSpan, context).ConfigureAwait(false)
                    : PyIteration.AcquireGeneratorOuter(outerValue, outerSpan, context);
                cursor = PyIteration.Cursor.FromGeneratorOuter(outer, outerSpan, context);
                // Invalid outer acquisition creates no escaping closure. Admit
                // retention before publication, after the cursor can be cleaned up.
                ChargeClosureRetention(context.FunctionClosureContext, governor, span);
                if (code.RequiresLocalVariableMirroring) RetainLocalsForLambda(context.FunctionClosureContext);
                frame.Variables[".0"] = outer.Iterator;
                frame.SeedGeneratorOuter(outer.Iterator, cursor);
                cursor = null; // ownership transferred to the retained frame
                return generator;
            }
            catch
            {
                try
                {
                    try { cursor?.Dispose(); }
                    finally { generator?.Complete(); }
                }
                finally { governor.Release(bytes); }
                throw;
            }
        }

        internal static PyGenerator Create(ExecutableCodeObject code, ExecutionContext frame, LythonSourceSpan span,
            ExecutableCaptures closureCells = default, BoundFunctionArguments arguments = default)
        {
            // Fund locals, cells, caches and handler/delegation state. The fixed
            // operand workspace is admitted separately before construction.
            var bytes = EstimateStorage(code);
            frame.MemoryGovernor.Reserve(bytes, span);
            frame.MemoryGovernor.Commit(bytes);
            PyGenerator generator;
            try { generator = new PyGenerator(code, frame, span, closureCells, arguments); }
            catch { frame.MemoryGovernor.Release(bytes); throw; }
            return generator;
        }

        private PyGenerator(ExecutableCodeObject code, ExecutionContext frame, LythonSourceSpan span,
            ExecutableCaptures closureCells, BoundFunctionArguments arguments)
        {
            _frame = frame;
            // Mirrored namespaces remain authoritative for retained cells while
            // the executable frame is suspended, without retaining an active frame.
            frame.MirrorLocalStores |= code.RequiresLocalVariableMirroring;
            _span = span;
            OwnerMemoryGovernor = frame.MemoryGovernor;
            var execution = CreateExecutableWorkState(code, frame, arguments, closureCells);
            frame.AdoptGeneratorExecution(ref execution, arguments.StorageOwner);
        }

        public bool IsTruthy() => true;
        public IEnumerable<object> Iterate() => PyIteration.EnumerateIterator(this);
        public PyIterationResult TryMoveNext() => Advance(PyNone.Instance, null);
        public ValueTask<PyIterationResult> TryMoveNextAsync() => AdvanceAsync(PyNone.Instance, null);

        // A foreign source retains its own authority. Its consumer must keep
        // checking the active command between ordinary, unbuffered pulls.
        internal bool CanBufferFor(ExecutionContext consumer)
            => ReferenceEquals(_frame?.State, consumer.State);

        // Only private drains borrow this admitted capacity. The last value
        // still crosses the ordinary advancement boundary before publication.
        internal bool AppendBatch(List<object> items, int? maximumItems)
        {
            var limit = Math.Min(items.Capacity, maximumItems ?? int.MaxValue);
            return items.Count < limit && AppendBatchCore(items, limit);
        }

        internal bool AppendBatch(PyList items)
            => items.Count < items.Capacity && AppendBatchCore(items, items.Capacity);

        private bool AppendBatchCore(object items, int limit)
        {
            var next = AdvanceBuffered(items, limit);
            if (!next.HasValue) return true;
            // Guest code may change a live PyList's capacity. Its final Add
            // can admit growth only after the frame and buffer borrow end.
            if (items is PyList list) list.Add(next.Value);
            else ((List<object>)items).Add(next.Value);
            return false;
        }

        internal ValueTask<bool> AppendBatchAsync(List<object> items, int? maximumItems)
        {
            var limit = Math.Min(items.Capacity, maximumItems ?? int.MaxValue);
            return items.Count < limit ? AppendBatchCoreAsync(items, limit) : new(false);
        }

        internal ValueTask<bool> AppendBatchAsync(PyList items)
            => items.Count < items.Capacity ? AppendBatchCoreAsync(items, items.Capacity) : new(false);

        private async ValueTask<bool> AppendBatchCoreAsync(object items, int limit)
        {
            var next = await AdvanceBufferedAsync(items, limit).ConfigureAwait(false);
            if (!next.HasValue) return true;
            if (items is PyList list) list.Add(next.Value);
            else ((List<object>)items).Add(next.Value);
            return false;
        }

        // Only same-command consumers borrow the existing yield destination.
        // Unsupported values leave the running frame before consumer protocols.
        internal PyIterationResult AdvanceBuffered(object destination, int limit)
            => Advance(PyNone.Instance, null, destination, limit);

        internal ValueTask<PyIterationResult> AdvanceBufferedAsync(object destination, int limit)
            => AdvanceAsync(PyNone.Instance, null, destination, limit);

        // Private callers must transfer a staged yield before starting the
        // callback. The ordinary result remains empty until that transfer.
        internal PyIterationResult AdvanceListYield(int arity)
            => Advance(PyNone.Instance, null, listYieldArity: arity);

        internal ValueTask<PyIterationResult> AdvanceListYieldAsync(int arity)
            => AdvanceAsync(PyNone.Instance, null, listYieldArity: arity);

        internal bool HasStagedListYield => _frame?.HasStagedListYield == true;

        internal void TransferStagedListYield(Span<ExecutableSlot> destination)
            => _frame!.TransferStagedListYield(destination);

        private PyIterationResult Advance(object sent, LythonRuntimeException? injected,
            object? destination = null, int limit = 0, int listYieldArity = 0)
        {
            if (!CanAdvance(sent, injected)) return PyIterationResult.End;
            if (!_started && injected is not null) { Complete(); throw injected; }
            var frame = _frame!;
            var previousException = frame.State.CurrentException;
            var entered = false;
            try
            {
                EnterFrame(frame, previousException, destination, limit, listYieldArity);
                entered = true;
                frame.Resume(sent, injected);
                frame.Execute();
                var result = ReadResult(frame);
                if (!frame.HasYield) Complete();
                return result;
            }
            catch (LythonRuntimeException exception)
            {
                Complete();
                PyFunctionBinding.AnnotateException(exception, frame, frame.FunctionName ?? "<generator>", _span);
                if (exception.Identity == PythonExceptionIdentity.Builtin("StopIteration"))
                    throw GeneratorRaisedStopIteration(exception, frame, _span);
                throw;
            }
            catch { Complete(); throw; }
            finally { if (entered) LeaveFrame(frame, previousException, destination is not null, listYieldArity); }
        }

        private async ValueTask<PyIterationResult> AdvanceAsync(object sent, LythonRuntimeException? injected,
            object? destination = null, int limit = 0, int listYieldArity = 0)
        {
            if (!CanAdvance(sent, injected)) return PyIterationResult.End;
            if (!_started && injected is not null) { Complete(); throw injected; }
            var frame = _frame!;
            var previousException = frame.State.CurrentException;
            var entered = false;
            try
            {
                EnterFrame(frame, previousException, destination, limit, listYieldArity);
                entered = true;
                frame.Resume(sent, injected);
                await frame.ExecuteAsync().ConfigureAwait(false);
                var result = ReadResult(frame);
                if (!frame.HasYield) Complete();
                return result;
            }
            catch (LythonRuntimeException exception)
            {
                Complete();
                PyFunctionBinding.AnnotateException(exception, frame, frame.FunctionName ?? "<generator>", _span);
                if (exception.Identity == PythonExceptionIdentity.Builtin("StopIteration"))
                    throw GeneratorRaisedStopIteration(exception, frame, _span);
                throw;
            }
            catch { Complete(); throw; }
            finally { if (entered) LeaveFrame(frame, previousException, destination is not null, listYieldArity); }
        }

        private bool CanAdvance(object sent, LythonRuntimeException? injected)
        {
            if (_running) throw new LythonRuntimeException("ValueError", "generator already executing", _span);
            if (_completed) { ReturnValue = PyNone.Instance; if (injected is not null) throw injected; return false; }
            if (!_started && sent is not PyNone)
                throw new LythonRuntimeException("TypeError", "can't send non-None value to a just-started generator", _span);
            return true;
        }

        private void EnterFrame(ExecutionContext frame, PyException? previousException, object? destination, int limit, int listYieldArity)
        {
            frame.EnterFunctionCall(_span);
            try { frame.EnterInterpreterFrame(_span); }
            catch { frame.LeaveFunctionCall(); throw; }
            _running = true;
            frame.EnterExecutableSlots();
            frame.SetCallerException(previousException);
            frame.State.SetCurrentException(frame.HasActiveHandler ? _suspendedException : previousException);
            _started = true;
            // Reentry and stack admission have succeeded. A rejected nested
            // pull must never overwrite the current advance's destination.
            if (destination is not null) frame.BufferYields(destination, limit);
            if (listYieldArity != 0) frame.RequestListYield(listYieldArity);
        }

        private void LeaveFrame(ExecutionContext frame, PyException? previousException, bool buffered, int listYieldArity)
        {
            if (buffered) frame.StopBufferingYields();
            if (listYieldArity != 0) frame.RequestListYield(0);
            _running = false;
            frame.State.SetCurrentException(previousException);
            frame.LeaveExecutableSlots();
            frame.LeaveInterpreterFrame();
            frame.LeaveFunctionCall();
        }

        private PyIterationResult ReadResult(ExecutionContext frame)
        {
            _suspendedException = frame.State.CurrentException;
            if (frame.HasYield) return PyIterationResult.Yield(frame.TakeYieldValue());
            ReturnValue = frame.HasFrameReturn ? frame.FrameReturnValue : PyNone.Instance;
            return PyIterationResult.End;
        }

        private void Complete()
        {
            try { _frame?.ReleaseGeneratorTemporaries(); }
            finally
            {
                _completed = true;
                var frame = _frame;
                _frame = null;
                _suspendedException = null;
                frame?.DetachGeneratorExecution();
            }
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "send" or "throw" or "close" or "__next__" or "__iter__")
            {
                value = new GeneratorMethod(this, name);
                return true;
            }
            value = PyNone.Instance;
            return false;
        }

        private sealed class GeneratorMethod(PyGenerator generator, string name) : ICallable
        {
            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
                => InvokeCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();
            public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
                => InvokeCoreAsync(arguments, span, context, true);
            private async ValueTask<object> InvokeCoreAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
            {
                if (arguments.Any(static argument => argument.IsKeyword)) throw new LythonRuntimeException("TypeError", "generator methods accept positional arguments only", span);
                if (name == "__iter__" && arguments.Length == 0) return generator;
                if (name == "close")
                {
                    if (arguments.Length != 0) throw new LythonRuntimeException("TypeError", "close() takes no arguments", span);
                    if (!generator._started || generator._completed)
                    { generator.Complete(); return PyNone.Instance; }
                    try
                    {
                        var exit = new LythonRuntimeException("GeneratorExit", "", span);
                        var closed = asynchronous
                            ? await generator.AdvanceAsync(PyNone.Instance, exit).ConfigureAwait(false)
                            : generator.Advance(PyNone.Instance, exit);
                        if (closed.HasValue) throw new LythonRuntimeException("RuntimeError", "generator ignored GeneratorExit", span);
                        return generator.ReturnValue;
                    }
                    catch (LythonRuntimeException exception) when (exception.ExceptionType == "GeneratorExit") { return PyNone.Instance; }
                }
                LythonRuntimeException? injected = null;
                var sent = PyNone.Instance as object;
                if (name == "throw")
                {
                    if (arguments.Length is < 1 or > 3) throw new LythonRuntimeException("TypeError", "throw() takes one to three arguments", span);
                    object exception = arguments[0].Value;
                    if (exception is PyException && arguments.Length > 1 && arguments[1].Value is not PyNone)
                        throw new LythonRuntimeException("TypeError", "instance exception may not have a separate value", span);
                    if (exception is not PyException && exception is IPythonExceptionType && exception is ICallable callable)
                    {
                        var values = arguments.Length > 1 && arguments[1].Value is not PyNone
                            ? arguments[1].Value is PyTuple tuple ? tuple.Select(CallArgumentValue.Positional).ToArray()
                                : [CallArgumentValue.Positional(arguments[1].Value)] : Array.Empty<CallArgumentValue>();
                        exception = asynchronous ? await callable.InvokeAsync(values, span, context).ConfigureAwait(false) : callable.Invoke(values, span, context);
                    }
                    if (exception is not PyException raised || (arguments.Length == 3 && arguments[2].Value is not PyNone))
                        throw new LythonRuntimeException("TypeError", "exceptions must derive from BaseException; traceback must be None", span);
                    injected = new LythonRuntimeException(raised.Identity, raised.Message, span, null, raised.Value)
                    { OriginalPythonException = raised, Attribution = raised.Attribution };
                }
                else if (name == "send" && arguments.Length == 1) sent = arguments[0].Value;
                else if (name != "__next__" || arguments.Length != 0) throw new LythonRuntimeException("TypeError", "invalid generator method arguments", span);
                var result = asynchronous
                    ? await generator.AdvanceAsync(sent, injected).ConfigureAwait(false)
                    : generator.Advance(sent, injected);
                if (result.HasValue) return result.Value;
                throw new LythonRuntimeException("StopIteration", "", span, null, generator.ReturnValue);
            }
        }
    }

    private sealed class GeneratorDelegation(object iterator, ExecutionContext context, LythonSourceSpan span)
    {

        public object ReturnValue { get; private set; } = PyNone.Instance;
        public PyIterationResult Advance(object sent, LythonRuntimeException? injected)
            => AdvanceAsync(sent, injected, false).GetAwaiter().GetResult();
        public async ValueTask<PyIterationResult> AdvanceAsync(object sent, LythonRuntimeException? injected, bool asynchronous)
        {
            async ValueTask<object> Call(ICallable callable, CallArgumentValue[] arguments)
                => asynchronous ? await callable.InvokeAsync(arguments, span, context).ConfigureAwait(false) : callable.Invoke(arguments, span, context);
            try
            {
                object value;
                if (injected is not null)
                {
                    if (injected.ExceptionType == "GeneratorExit")
                    {
                        if (PyMemberAccess.TryResolve(iterator, "close", context, span, out var close) && close is ICallable closing)
                            _ = await Call(closing, []).ConfigureAwait(false);
                        throw injected;
                    }
                    if (!PyMemberAccess.TryResolve(iterator, "throw", context, span, out var handler) || handler is not ICallable throwing) throw injected;
                    var forwarded = CreatePythonExceptionInstance(injected);
                    forwarded.Adopt(context, span);
                    value = await Call(throwing, [CallArgumentValue.Positional(forwarded)]).ConfigureAwait(false);
                }
                else if (sent is PyNone)
                    value = asynchronous ? await NextAsync([iterator], span, context).ConfigureAwait(false) : Next([iterator], span, context);
                else
                {
                    if (!PyMemberAccess.TryResolve(iterator, "send", context, span, out var send) || send is not ICallable sending)
                        throw new LythonRuntimeException("AttributeError", "iterator has no attribute 'send'", span);
                    value = await Call(sending, [CallArgumentValue.Positional(sent)]).ConfigureAwait(false);
                }
                return PyIterationResult.Yield(value);
            }
            catch (LythonRuntimeException exception) when (exception.ExceptionType == "StopIteration" && injected?.ExceptionType != "GeneratorExit")
            {
                ReturnValue = exception.Payload ?? PyNone.Instance;
                return PyIterationResult.End;
            }
        }
    }
}
