using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static partial class PyDataclass
{
    private sealed class DataclassReprMethod(string typeName, IReadOnlyList<DataclassFieldSpec> fields) : IPyBindableCallable
    {
        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            if (arguments.Length != 1 || arguments[0].Name is not null || arguments[0].Value is not PyInstance instance)
            {
                throw new LythonRuntimeException("TypeError", $"{typeName}.__repr__() expected a bound instance.", span);
            }

            var builder = new GovernedByteBuilder();
            builder.AppendString(typeName);
            builder.AppendAscii("(");
            for (var i = 0; i < fields.Count; i++)
            {
                if (i > 0)
                {
                    builder.AppendAscii(", ");
                }

                var field = fields[i];
                builder.AppendString(field.Name);
                builder.AppendAscii("=");
                _ = instance.TryGetOwnAttribute(field.Name, out var value);
                builder.Append(PyRendering.ToReprPyString(value ?? PyNone.Instance, new PyRenderingContext(context)));
            }

            builder.AppendAscii(")");
            return builder.ToPyStringAndRelease();
        }
    }

    private sealed class DataclassEqMethod(string typeName, IReadOnlyList<DataclassFieldSpec> fields) : IPyBindableCallable
    {
        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            var parameters = new[]
            {
                new LoweredFunctionParameter("self", FunctionParameterKind.Positional, null, null),
                new LoweredFunctionParameter("other", FunctionParameterKind.Positional, null, null)
            };
            var bound = LythonRuntime.BindFunctionArguments(arguments, span, $"{typeName}.__eq__", "Function", parameters, new Dictionary<string, object>(StringComparer.Ordinal), context);
            if (bound["self"] is not PyInstance self)
            {
                throw new LythonRuntimeException("TypeError", $"{typeName}.__eq__ expected a bound instance.", span);
            }

            if (bound["other"] is not PyInstance other || !ReferenceEquals(self.Type, other.Type))
            {
                return false;
            }

            foreach (var field in fields)
            {
                _ = self.TryGetOwnAttribute(field.Name, out var left);
                _ = other.TryGetOwnAttribute(field.Name, out var right);
                if (!PyEquality.AreEqual(left ?? PyNone.Instance, right ?? PyNone.Instance))
                {
                    return false;
                }
            }

            return true;
        }
    }

    private enum DataclassOrderOperation
    {
        Less,
        LessEqual,
        Greater,
        GreaterEqual
    }

    private sealed class DataclassOrderMethod(string typeName, DataclassOrderOperation operation) : IPyBindableCallable
    {
        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            var parameters = new[]
            {
                new LoweredFunctionParameter("self", FunctionParameterKind.Positional, null, null),
                new LoweredFunctionParameter("other", FunctionParameterKind.Positional, null, null)
            };
            var bound = LythonRuntime.BindFunctionArguments(arguments, span, $"{typeName}.__{OperationName(operation)}__", "Function", parameters, new Dictionary<string, object>(StringComparer.Ordinal), context);
            if (bound["self"] is not PyInstance self || bound["other"] is not PyInstance other || !ReferenceEquals(self.Type, other.Type))
            {
                throw new LythonRuntimeException("TypeError", $"{typeName} ordering expects two instances of the same dataclass type.", span);
            }

            var comparison = CompareOrderedInstances(self, other, span);
            return operation switch
            {
                DataclassOrderOperation.Less => comparison < 0,
                DataclassOrderOperation.LessEqual => comparison <= 0,
                DataclassOrderOperation.Greater => comparison > 0,
                DataclassOrderOperation.GreaterEqual => comparison >= 0,
                _ => throw new InvalidOperationException("Unsupported dataclass order operation.")
            };
        }

        private static string OperationName(DataclassOrderOperation operation)
            => operation switch
            {
                DataclassOrderOperation.Less => "lt",
                DataclassOrderOperation.LessEqual => "le",
                DataclassOrderOperation.Greater => "gt",
                DataclassOrderOperation.GreaterEqual => "ge",
                _ => throw new InvalidOperationException("Unsupported dataclass order operation.")
            };
    }

    private sealed class DataclassHashMethod(string typeName) : IPyBindableCallable
    {
        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 1 || arguments[0].Name is not null || arguments[0].Value is not PyInstance instance)
            {
                throw new LythonRuntimeException("TypeError", $"{typeName}.__hash__() expected a bound instance.", span);
            }

            return new BigInteger(instance.GetPyHashCode());
        }
    }

    private sealed class DataclassFrozenSetAttrMethod(string typeName) : IPyBindableCallable
    {
        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            _ = context;
            _ = arguments;
            throw new LythonRuntimeException("FrozenInstanceError", $"cannot assign to field of frozen dataclass '{typeName}'.", span);
        }
    }

    private sealed class DataclassFrozenDelAttrMethod(string typeName) : IPyBindableCallable
    {
        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            _ = context;
            _ = arguments;
            throw new LythonRuntimeException("FrozenInstanceError", $"cannot delete field of frozen dataclass '{typeName}'.", span);
        }
    }
}

