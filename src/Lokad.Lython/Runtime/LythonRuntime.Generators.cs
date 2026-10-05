using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed class PyGenerator : IPyAsyncIteratorValue, IPyDynamicAttributes, IPyTruthyValue,
        IPyGovernedValue, IPyOwnershipSnapshot
    {
        private ExecutableFrameInterpreter? _interpreter;
        private ExecutionContext? _frame;
        private ExecutableFrameState? _slots;
        private readonly LythonSourceSpan _span;
        private readonly long _bytes;
        private bool _started, _completed, _running;
        private PyException? _suspendedException;
        public object ReturnValue { get; private set; } = PyNone.Instance;
        public MemoryGovernor? OwnerMemoryGovernor { get; }
        public LythonSourceSpan? AllocationSpan => _span;
        bool IPyOwnershipSnapshot.TrySnapshotOwnership(out long bytes)
            => OwnershipSnapshot.Owned(OwnerMemoryGovernor, _bytes, out bytes);

        internal static PyGenerator Create(ExecutableCodeObject code, ExecutionContext frame, LythonSourceSpan span, IReadOnlyList<ExecutableCell>? closureCells = null)
        {
            // Fund locals, cells, operand stack including resize overlap, caches
            // and handler/delegation state before constructing retained storage.
            var bytes = checked(512L + 96L * (code.Blocks.Sum(block => block.Instructions.Count) +
                code.LocalNames.Count + code.MemberCacheCount + code.CallCacheCount + code.Blocks.Count));
            frame.MemoryGovernor.Reserve(bytes, span);
            frame.MemoryGovernor.Commit(bytes);
            try
            {
                var generator = new PyGenerator(code, frame, span, bytes, closureCells);
                frame.Services.State.CallTemporaries.TrackFreshMutable(generator, bytes, span);
                return generator;
            }
            catch { frame.MemoryGovernor.Release(bytes); throw; }
        }

        private PyGenerator(ExecutableCodeObject code, ExecutionContext frame, LythonSourceSpan span, long bytes, IReadOnlyList<ExecutableCell>? closureCells)
        {
            _frame = frame;
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
                foreach (var slot in code.CapturedLocalSlots) cells[slot] = new ExecutableCell(locals[slot]);
            }
            _slots = new ExecutableFrameState(code, locals, cells, closureCells);
            _interpreter = new ExecutableFrameInterpreter(code, frame, locals, cells);
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
            var result = AdvanceAsync(PyNone.Instance, null, false).GetAwaiter().GetResult();
            value = result.HasValue ? result.Value : PyNone.Instance;
            return result.HasValue;
        }
        public ValueTask<PyIterationResult> TryMoveNextAsync() => AdvanceAsync(PyNone.Instance, null, true);

        private async ValueTask<PyIterationResult> AdvanceAsync(object sent, LythonRuntimeException? injected, bool asynchronous)
        {
            if (_running) throw new LythonRuntimeException("ValueError", "generator already executing", _span);
            if (!_started && sent is not PyNone)
                throw new LythonRuntimeException("TypeError", "can't send non-None value to a just-started generator", _span);
            if (_completed) { ReturnValue = PyNone.Instance; if (injected is not null) throw injected; return PyIterationResult.End; }
            if (!_started && injected is not null) { Complete(); throw injected; }
            var frame = _frame!;
            var interpreter = _interpreter!;
            var previousSlots = frame.CurrentExecutableFrame;
            var previousException = frame.Services.CurrentException;
            frame.EnterFunctionCall(_span);
            try
            {
                frame.EnterInterpreterFrame(_span);
                try
                {
                    _running = true;
                    frame.EnterExecutableSlots(_slots!);
                    interpreter.SetCallerException(previousException);
                    frame.Services.SetCurrentException(interpreter.HasActiveHandler ? _suspendedException : previousException);
                    _started = true;
                    interpreter.Resume(sent, injected);
                    if (asynchronous) await interpreter.ExecuteAsync().ConfigureAwait(false);
                    else interpreter.Execute();
                    _suspendedException = frame.Services.CurrentException;
                    if (interpreter.HasYield) return PyIterationResult.Yield(interpreter.YieldValue);
                    ReturnValue = interpreter.HasFrameReturn ? interpreter.FrameReturnValue : PyNone.Instance;
                    Complete();
                    return PyIterationResult.End;
                }
                catch (LythonRuntimeException exception)
                {
                    Complete();
                    PyFunctionBinding.AnnotateException(exception, frame, frame.FunctionName ?? "<generator>", _span);
                    if (exception.ExceptionType == "StopIteration")
                        throw new LythonRuntimeException("RuntimeError", "generator raised StopIteration", _span, exception)
                        { PythonCause = CreatePythonExceptionInstance(exception), SuppressPythonContext = true };
                    throw;
                }
                catch { Complete(); throw; }
                finally
                {
                    _running = false;
                    frame.Services.SetCurrentException(previousException);
                    frame.LeaveExecutableSlots(previousSlots);
                    frame.LeaveInterpreterFrame();
                }
            }
            finally { frame.LeaveFunctionCall(); }
        }

        private void Complete()
        {
            _completed = true;
            _frame = null;
            _slots = null;
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
                    if (!generator._started || generator._completed) { generator.Complete(); return PyNone.Instance; }
                    try
                    {
                        var closed = await generator.AdvanceAsync(PyNone.Instance, new LythonRuntimeException("GeneratorExit", "", span), asynchronous).ConfigureAwait(false);
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
                var result = await generator.AdvanceAsync(sent, injected, asynchronous).ConfigureAwait(false);
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
                    value = await Call(throwing, [CallArgumentValue.Positional(CreatePythonExceptionInstance(injected))]).ConfigureAwait(false);
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
