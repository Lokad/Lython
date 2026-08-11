using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class UnsupportedSubprocessCallable : ICallable, INamedRuntimeCallable, IPyRenderableValue
    {
        private readonly string _message;

        public UnsupportedSubprocessCallable(string name, string message)
        {
            Name = name;
            _message = message;
        }

        public string Name { get; }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = arguments;
            context.CheckExecutionBudget(span);
            throw new LythonRuntimeException("NotImplementedError", _message, span);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString(Name);
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class SubprocessCalledProcessErrorType : ICallable, IPyDynamicAttributes, IPyRenderableValue
    {
        public static readonly SubprocessCalledProcessErrorType Instance = new();

        private static readonly LythonCallableSignature CallSignature = LythonCallableSignature.Create(
            "subprocess.CalledProcessError",
            ["returncode", "cmd", "output", "stderr"],
            requiredCount: 2);

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "__name__" => PyString.FromString("CalledProcessError"),
                "type" => PyString.FromString("CalledProcessError"),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var bound = CallBinder.BindNamedArguments(
                arguments,
                span,
                CallSignature,
                PythonCallableKind.Builtin);
            var returnCode = bound[0] switch
            {
                BigInteger integer => integer,
                int integer => new BigInteger(integer),
                _ => throw new LythonRuntimeException("TypeError", "subprocess.CalledProcessError(returncode, cmd, ...) expects integer returncode.", span)
            };
            var output = GetArgument(bound, 2);
            var stderr = GetArgument(bound, 3);
            var payload = CreateCalledProcessErrorPayload(returnCode, bound[1], output, stderr, context, span);
            return new PyException("CalledProcessError", $"Command failed with return code {returnCode}.", payload);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<class 'subprocess.CalledProcessError'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class SubprocessTimeoutExpiredType : ICallable, IPyDynamicAttributes, IPyRenderableValue
    {
        public static readonly SubprocessTimeoutExpiredType Instance = new();

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "__name__" => PyString.FromString("TimeoutExpired"),
                "type" => PyString.FromString("TimeoutExpired"),
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var bound = CallBinder.BindNamedArguments(
                arguments,
                span,
                LythonKnownCallableSignatures.SubprocessTimeoutExpired,
                PythonCallableKind.Builtin);
            return new PyException(
                "TimeoutExpired",
                $"Command timed out after {bound[1]}.",
                CreateTimeoutExpiredPayload(bound[0], bound[1], GetArgument(bound, 2), GetArgument(bound, 3), context, span));
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<class 'subprocess.TimeoutExpired'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private static PyDict CreateTimeoutExpiredPayload(
        object command,
        object timeout,
        object output,
        object stderr,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        var payload = new PyDict(context.MemoryGovernor, span);
        payload.SetItem(PyString.FromString("cmd"), command);
        payload.SetItem(PyString.FromString("timeout"), timeout);
        payload.SetItem(PyString.FromString("output"), output);
        payload.SetItem(PyString.FromString("stdout"), output);
        payload.SetItem(PyString.FromString("stderr"), stderr);
        return payload;
    }
}
