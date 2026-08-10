using System.Globalization;
using System.Numerics;
using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static class BytesMembers
    {
        public static bool TryGetMember(PyBytes bytes, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "decode" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "bytes.decode([encoding][, errors]) expects zero to two arguments.", span);
                    }

                    var encoding = arguments.Length >= 1
                        ? ParseTextEncoding(arguments[0], "bytes.decode()", span)
                        : TextEncodingMode.Utf8;
                    var errors = arguments.Length == 2
                        ? ParseTextErrors(arguments[1], "bytes.decode()", span)
                        : TextErrorMode.Strict;
                    return DecodeText(bytes.ToArray(), encoding, context, span, errors, TextNewlineMode.PreserveUniversal);
                }, "bytes.decode", ["encoding", "errors"], 0),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    private sealed class BoundCallable : BoundArgumentsCallable
    {
        public BoundCallable(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, LythonCallableSignature signature) : this(implementation, signature, null) { }

        public BoundCallable(
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            LythonCallableSignature signature,
            Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>>? asyncImplementation)
            : base(signature, implementation, asyncImplementation, PythonCallableKind.Method, parameterIndices: null)
        {
        }

        public BoundCallable(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation) : this(implementation, new LythonCallableSignature("bound method")) { }

        public BoundCallable(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, string? name) : this(implementation, name, null, null) { }

        public BoundCallable(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, string? name, string[]? parameterNames) : this(implementation, name, parameterNames, null) { }

        public BoundCallable(
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            string? name,
            string[]? parameterNames,
            int? requiredCount)
            : this(implementation, new LythonCallableSignature(name ?? "bound method", parameterNames, requiredCount))
        {
        }

        public BoundCallable(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation) : this(implementation, asyncImplementation, null, null, null) { }

        public BoundCallable(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation, string? name) : this(implementation, asyncImplementation, name, null, null) { }

        public BoundCallable(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation, string? name, string[]? parameterNames) : this(implementation, asyncImplementation, name, parameterNames, null) { }

        public BoundCallable(
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation,
            string? name,
            string[]? parameterNames,
            int? requiredCount)
            : this(implementation, new LythonCallableSignature(name ?? "bound method", parameterNames, requiredCount), asyncImplementation)
        {
        }

    }

    private sealed class RawBoundCallable(
        Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, object> implementation) : ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return implementation(arguments, span, context);
        }
    }
}
