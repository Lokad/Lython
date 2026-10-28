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
            context.CheckExecution(span);
            throw new LythonRuntimeException("NotImplementedError", _message, span);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString(Name);
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class SubprocessCalledProcessErrorType : ICallable, IPyDynamicAttributes, IPyRenderableValue, IPythonExceptionType
    {
        public static readonly SubprocessCalledProcessErrorType Instance = new();

        public PythonExceptionIdentity ExceptionIdentity => ModuleException("subprocess", "CalledProcessError");

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
            context.CheckExecution(span);
            var bound = CallBinder.BindNamedArguments(
                arguments,
                span,
                CallSignature,
                PythonCallableKind.Builtin);
            return CreateSubprocessErrorValue("CalledProcessError", bound[0], bound[1],
                GetArgument(bound, 2), GetArgument(bound, 3),
                new PyTuple(arguments.Where(a => a.IsPositional).Select(a => a.Value), context.MemoryGovernor, span), context, span);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<class 'subprocess.CalledProcessError'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class SubprocessTimeoutExpiredType : ICallable, IPyDynamicAttributes, IPyRenderableValue, IPythonExceptionType
    {
        public static readonly SubprocessTimeoutExpiredType Instance = new();

        public PythonExceptionIdentity ExceptionIdentity => ModuleException("subprocess", "TimeoutExpired");

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
            context.CheckExecution(span);
            var bound = CallBinder.BindNamedArguments(
                arguments,
                span,
                LythonKnownCallableSignatures.SubprocessTimeoutExpired,
                PythonCallableKind.Builtin);
            return CreateSubprocessErrorValue("TimeoutExpired", bound[1], bound[0],
                GetArgument(bound, 2), GetArgument(bound, 3),
                new PyTuple(arguments.Where(a => a.IsPositional).Select(a => a.Value), context.MemoryGovernor, span), context, span);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<class 'subprocess.TimeoutExpired'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private static PyException CreateSubprocessErrorValue(string type, object codeOrTimeout, object command,
        object output, object stderr, PyTuple arguments, ExecutionContext context, LythonSourceSpan span)
    {
        var fields = new PyDict(context.MemoryGovernor, span);
        fields.SetItem(PyString.FromString(type == "CalledProcessError" ? "returncode" : "timeout"), codeOrTimeout);
        fields.SetItem(PyString.FromString("cmd"), command);
        fields.SetItem(PyString.FromString("output"), output);
        fields.SetItem(PyString.FromString("stderr"), stderr);
        var value = PyException.CreateOwned(ModuleException("subprocess", type), string.Empty, null, context, span, arguments);
        value.CustomDict = fields;
        return value;
    }
}
