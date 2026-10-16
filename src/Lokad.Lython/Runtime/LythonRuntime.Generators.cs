using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static LythonRuntimeException GeneratorRaisedStopIteration(
        LythonRuntimeException exception, ExecutionContext context, LythonSourceSpan span)
    {
        var cause = CreatePythonExceptionInstance(exception, context);
        return new LythonRuntimeException("RuntimeError", "generator raised StopIteration", span, exception)
        { PythonCause = cause, PythonContext = cause, SuppressPythonContext = true };
    }

    internal sealed class PyGenerator : IPyAsyncIteratorValue, IPyDynamicAttributes, IPyTruthyValue,
        IPyGovernedValue, IPyOwnershipSnapshot
    {
        private ExecutableFrameInterpreter? _interpreter;
        private ExecutionContext? _frame;
        private readonly LythonSourceSpan _span;
        private readonly long _bytes;
        private bool _started, _completed, _running;
        private PyException? _suspendedException;
        public object ReturnValue { get; private set; } = PyNone.Instance;
        public MemoryGovernor? OwnerMemoryGovernor { get; }
        public LythonSourceSpan? AllocationSpan => _span;
        bool IPyOwnershipSnapshot.TrySnapshotOwnership(out long bytes)
            => OwnershipSnapshot.Owned(OwnerMemoryGovernor, checked(_bytes + (_interpreter?.RetainedStackBytes ?? 0)), out bytes);

        private static long EstimateStorage(ExecutableCodeObject code)
            => checked(512L + 96L * (code.LocalNames.Count + code.MemberCacheCount + code.Blocks.Count));

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
                var cells = CaptureExecutableClosures(code, context, span);
                var frame = new ExecutionContext(context.FunctionClosureContext, code.ScopeFacts)
                { FunctionName = "<genexpr>", FirstPositionalParameterName = ".0" };
                generator = new PyGenerator(code, frame, span, bytes, cells);
                var outer = asynchronous
                    ? await PyIteration.AcquireGeneratorOuterAsync(outerValue, outerSpan, context).ConfigureAwait(false)
                    : PyIteration.AcquireGeneratorOuter(outerValue, outerSpan, context);
                cursor = PyIteration.Cursor.FromGeneratorOuter(outer, outerValue, asynchronous, outerSpan, context);
                // Invalid outer acquisition creates no escaping closure. Admit
                // retention before publication, after the cursor can be cleaned up.
                ChargeClosureRetention(context.FunctionClosureContext, governor, span);
                if (code.RequiresLocalVariableMirroring) RetainLocalsForLambda(context.FunctionClosureContext);
                frame.Variables[".0"] = outer.Iterator;
                generator._interpreter!.SeedGeneratorOuter(outer.Iterator, cursor);
                cursor = null; // ownership transferred to the retained frame
                return generator;
            }
            catch
            {
                try
                {
                    try { if (cursor is not null) await cursor.DisposeAsync().ConfigureAwait(false); }
                    finally { if (generator is not null) await generator.CompleteAsync(asynchronous).ConfigureAwait(false); }
                }
                finally { governor.Release(bytes); }
                throw;
            }
        }

        internal static PyGenerator Create(ExecutableCodeObject code, ExecutionContext frame, LythonSourceSpan span, IReadOnlyList<ExecutableCell>? closureCells = null)
        {
            // Fund locals, cells, caches and handler/delegation state. Operand
            // buffers admit actual capacity and replacement overlap separately.
            var bytes = EstimateStorage(code);
            frame.MemoryGovernor.Reserve(bytes, span);
            frame.MemoryGovernor.Commit(bytes);
            PyGenerator generator;
            try { generator = new PyGenerator(code, frame, span, bytes, closureCells); }
            catch { frame.MemoryGovernor.Release(bytes); throw; }
            return generator;
        }

        private PyGenerator(ExecutableCodeObject code, ExecutionContext frame, LythonSourceSpan span, long bytes,
            IReadOnlyList<ExecutableCell>? closureCells)
        {
            _frame = frame;
            // Mirrored namespaces remain authoritative for retained cells while
            // the executable frame is suspended, without retaining an active frame.
            frame.MirrorLocalStores |= code.RequiresLocalVariableMirroring;
            _span = span;
            _bytes = bytes;
            OwnerMemoryGovernor = frame.MemoryGovernor;
            var locals = new object[code.LocalNames.Count];
            Array.Fill(locals, UninitializedLocal);
            for (var i = 0; i < locals.Length; i++)
                if (frame.Variables.TryGetValue(code.LocalNames[i], out var value)) locals[i] = value;
            ExecutableCell?[]? cells = null;
            if (code.CapturedLocalSlots.Count != 0)
            {
                cells = new ExecutableCell[locals.Length];
                foreach (var slot in code.CapturedLocalSlots) cells[slot] = new ExecutableCell(locals[slot], frame, code.LocalNames[slot]);
            }
            _interpreter = new ExecutableFrameInterpreter(code, frame, locals, cells, closureCells);
        }

        public bool IsTruthy() => true;
        public IEnumerable<object> Iterate() => PyIteration.EnumerateIterator(this);
        public async IAsyncEnumerable<object> IterateAsync()
        {
            while (true)
            {
                var result = await TryMoveNextAsync().ConfigureAwait(false);
                if (!result.HasValue) yield break;
                yield return result.Value;
            }
        }
        public bool TryMoveNext([MaybeNullWhen(false)] out object value)
        {
            var result = Advance(PyNone.Instance, null);
            value = result.HasValue ? result.Value : PyNone.Instance;
            return result.HasValue;
        }
        public ValueTask<PyIterationResult> TryMoveNextAsync() => AdvanceAsync(PyNone.Instance, null);

        private PyIterationResult Advance(object sent, LythonRuntimeException? injected)
        {
            if (!CanAdvance(sent, injected)) return PyIterationResult.End;
            if (!_started && injected is not null) { CompleteSynchronously(); throw injected; }
            var frame = _frame!;
            var interpreter = _interpreter!;
            var previousSlots = frame.CurrentExecutableFrame;
            var previousException = frame.Services.CurrentException;
            var entered = false;
            try
            {
                EnterFrame(frame, interpreter, previousException);
                entered = true;
                interpreter.Resume(sent, injected);
                interpreter.Execute();
                var result = ReadResult(frame, interpreter);
                if (!result.HasValue) CompleteSynchronously();
                return result;
            }
            catch (LythonRuntimeException exception)
            {
                CompleteSynchronously();
                PyFunctionBinding.AnnotateException(exception, frame, frame.FunctionName ?? "<generator>", _span);
                if (exception.Identity == PythonExceptionIdentity.Builtin("StopIteration"))
                    throw GeneratorRaisedStopIteration(exception, frame, _span);
                throw;
            }
            catch { CompleteSynchronously(); throw; }
            finally { if (entered) LeaveFrame(frame, previousSlots, previousException); }
        }

        private async ValueTask<PyIterationResult> AdvanceAsync(object sent, LythonRuntimeException? injected)
        {
            if (!CanAdvance(sent, injected)) return PyIterationResult.End;
            if (!_started && injected is not null) { await CompleteAsync(true).ConfigureAwait(false); throw injected; }
            var frame = _frame!;
            var interpreter = _interpreter!;
            var previousSlots = frame.CurrentExecutableFrame;
            var previousException = frame.Services.CurrentException;
            var entered = false;
            try
            {
                EnterFrame(frame, interpreter, previousException);
                entered = true;
                interpreter.Resume(sent, injected);
                await interpreter.ExecuteAsync().ConfigureAwait(false);
                var result = ReadResult(frame, interpreter);
                if (!result.HasValue) await CompleteAsync(true).ConfigureAwait(false);
                return result;
            }
            catch (LythonRuntimeException exception)
            {
                await CompleteAsync(true).ConfigureAwait(false);
                PyFunctionBinding.AnnotateException(exception, frame, frame.FunctionName ?? "<generator>", _span);
                if (exception.Identity == PythonExceptionIdentity.Builtin("StopIteration"))
                    throw GeneratorRaisedStopIteration(exception, frame, _span);
                throw;
            }
            catch { await CompleteAsync(true).ConfigureAwait(false); throw; }
            finally { if (entered) LeaveFrame(frame, previousSlots, previousException); }
        }

        private bool CanAdvance(object sent, LythonRuntimeException? injected)
        {
            if (_running) throw new LythonRuntimeException("ValueError", "generator already executing", _span);
            if (_completed) { ReturnValue = PyNone.Instance; if (injected is not null) throw injected; return false; }
            if (!_started && sent is not PyNone)
                throw new LythonRuntimeException("TypeError", "can't send non-None value to a just-started generator", _span);
            return true;
        }

        private void EnterFrame(ExecutionContext frame, ExecutableFrameInterpreter interpreter, PyException? previousException)
        {
            frame.EnterFunctionCall(_span);
            try { frame.EnterInterpreterFrame(_span); }
            catch { frame.LeaveFunctionCall(); throw; }
            _running = true;
            frame.EnterExecutableSlots(interpreter);
            interpreter.SetCallerException(previousException);
            frame.Services.SetCurrentException(interpreter.HasActiveHandler ? _suspendedException : previousException);
            _started = true;
        }

        private void LeaveFrame(ExecutionContext frame, ExecutableFrameState? previousSlots, PyException? previousException)
        {
            _running = false;
            frame.Services.SetCurrentException(previousException);
            frame.LeaveExecutableSlots(previousSlots);
            frame.LeaveInterpreterFrame();
            frame.LeaveFunctionCall();
        }

        private PyIterationResult ReadResult(ExecutionContext frame, ExecutableFrameInterpreter interpreter)
        {
            _suspendedException = frame.Services.CurrentException;
            if (interpreter.HasYield) return PyIterationResult.Yield(interpreter.YieldValue);
            ReturnValue = interpreter.HasFrameReturn ? interpreter.FrameReturnValue : PyNone.Instance;
            return PyIterationResult.End;
        }

        // Only terminal cleanup uses this bridge; ordinary synchronous pulls
        // enter and execute the frame without an async advancement state machine.
        private void CompleteSynchronously() => CompleteAsync(false).GetAwaiter().GetResult();

        private async ValueTask CompleteAsync(bool asynchronous)
        {
            try
            {
                if (_interpreter is not null)
                    await _interpreter.ReleaseGeneratorTemporariesAsync(asynchronous).ConfigureAwait(false);
            }
            finally { Complete(); }
        }

        private void Complete()
        {
            _completed = true;
            _frame = null;
            _interpreter = null;
            _suspendedException = null;
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
                    { await generator.CompleteAsync(asynchronous).ConfigureAwait(false); return PyNone.Instance; }
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
                    { PythonExplicitArgs = raised.ArgsOverride ?? raised.ExplicitArgs, OriginalPythonException = raised,
                        PythonCause = raised.Cause, PythonContext = raised.Context, SuppressPythonContext = raised.SuppressContext };
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
                    var forwarded = CreatePythonExceptionInstance(injected, context);
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
