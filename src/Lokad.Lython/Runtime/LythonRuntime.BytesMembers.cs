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
                "decode" => BoundCallable.Create((arguments, span, context) =>
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
        private BoundCallable(
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            LythonCallableSignature signature,
            Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>>? asyncImplementation)
            : base(signature, implementation, asyncImplementation, PythonCallableKind.Method)
        {
        }

        public static BoundCallable Create(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, LythonCallableSignature signature)
            => new(implementation, signature, null);

        public static BoundCallable Create(
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            LythonCallableSignature signature,
            Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation)
            => new(implementation, signature, asyncImplementation);

        public static BoundCallable Create(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation)
            => Create(implementation, LythonCallableSignature.Create("bound method"));

        public static BoundCallable Create(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, string? name)
            => Create(implementation, name, null, null);

        public static BoundCallable Create(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, string? name, string[]? parameterNames)
            => Create(implementation, name, parameterNames, null);

        public static BoundCallable Create(
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            string? name,
            string[]? parameterNames,
            int? requiredCount)
            => Create(implementation, LythonCallableSignature.Create(name ?? "bound method", parameterNames, requiredCount));

        public static BoundCallable Create(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation)
            => Create(implementation, asyncImplementation, null, null, null);

        public static BoundCallable Create(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation, string? name)
            => Create(implementation, asyncImplementation, name, null, null);

        public static BoundCallable Create(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation, string? name, string[]? parameterNames)
            => Create(implementation, asyncImplementation, name, parameterNames, null);

        public static BoundCallable Create(
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation,
            string? name,
            string[]? parameterNames,
            int? requiredCount)
            => Create(implementation, LythonCallableSignature.Create(name ?? "bound method", parameterNames, requiredCount), asyncImplementation);

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
