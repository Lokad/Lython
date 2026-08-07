using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static partial class PyDataclass
{
    private static readonly LoweredFunctionParameter[] BinaryProtocolParameters =
    [
        new("self", FunctionParameterKind.Positional, null, null),
        new("other", FunctionParameterKind.Positional, null, null)
    ];

    private static readonly IReadOnlyDictionary<string, object> EmptyDefaultValues =
        new Dictionary<string, object>(StringComparer.Ordinal);

    private sealed class DataclassInitMethod : IPyBindableCallable
    {
        private readonly FunctionBindingPlan _bindingPlan;
        private readonly IReadOnlyList<DataclassFieldSpec> _fields;
        private readonly string _typeName;

        public DataclassInitMethod(string typeName, IReadOnlyList<DataclassFieldSpec> fields)
        {
            _typeName = typeName;
            _fields = fields;

            var parameters = new List<LoweredFunctionParameter>(fields.Count + 1)
            {
                new("self", FunctionParameterKind.Positional, null, null)
            };
            var defaults = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var field in fields)
            {
                if (field.Kind == DataclassFieldKind.ClassVar || !field.Init)
                {
                    continue;
                }

                parameters.Add(new LoweredFunctionParameter(
                    field.Name,
                    field.KwOnly ? FunctionParameterKind.KeywordOnly : FunctionParameterKind.Positional,
                    null,
                    null));
                if (field.HasDefaultFactory)
                {
                    defaults[field.Name] = DefaultFactorySentinel;
                }
                else if (field.HasDefault)
                {
                    defaults[field.Name] = field.DefaultValue;
                }
            }

            _bindingPlan = new FunctionBindingPlan($"{typeName}.__init__", "Function", parameters, defaults);
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            var bound = LythonRuntime.BindFunctionArguments(arguments, span, _bindingPlan, context);
            if (bound["self"] is not PyInstance instance)
            {
                throw new LythonRuntimeException("TypeError", $"{_typeName}.__init__ expected a bound instance.", span);
            }

            var initVarValues = new List<object>();
            foreach (var field in _fields)
            {
                if (!field.Store)
                {
                    if (field.Kind == DataclassFieldKind.InitVar && field.Init)
                    {
                        var initVarValue = bound[field.Name];
                        if (ReferenceEquals(initVarValue, DefaultFactorySentinel))
                        {
                            initVarValue = InvokeDefaultFactory(field, span, context);
                        }

                        initVarValues.Add(initVarValue);
                    }
                    continue;
                }

                object value;
                if (field.Init)
                {
                    value = bound[field.Name];
                    if (ReferenceEquals(value, DefaultFactorySentinel))
                    {
                        value = InvokeDefaultFactory(field, span, context);
                    }
                }
                else if (field.HasDefaultFactory)
                {
                    value = InvokeDefaultFactory(field, span, context);
                }
                else if (field.HasDefault)
                {
                    value = field.DefaultValue;
                }
                else
                {
                    continue;
                }

                SetAttributeDuringDataclassInit(instance, field.Name, value, context, span);
            }

            if (instance.Type.TryGetMember("__post_init__", out var postInitRaw))
            {
                var callable = postInitRaw switch
                {
                    IPyBindableCallable bindable => bindable.Bind(instance),
                    IPyDescriptor descriptor => descriptor.Get(instance, instance.Type, context, span),
                    _ => postInitRaw
                };
                if (callable is not LythonRuntime.ICallable postInitCallable)
                {
                    throw new LythonRuntimeException("TypeError", $"{_typeName}.__post_init__ must be callable.", span);
                }

                var postInitArguments = initVarValues.Select(value => CallArgumentValue.Positional(value)).ToArray();
                _ = postInitCallable.Invoke(postInitArguments, span, context);
            }

            return PyNone.Instance;
        }

        internal static object InvokeDefaultFactory(DataclassFieldSpec field, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            if (field.DefaultFactory is not LythonRuntime.ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", $"Dataclass field '{field.Name}' has a non-callable default_factory.", span);
            }

            return callable.Invoke([], span, context);
        }
    }

    private sealed class DataclassReprMethod(string typeName, IReadOnlyList<DataclassFieldSpec> fields) : IPyBindableCallable
    {
        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            if (arguments.Length != 1 || arguments[0].IsKeyword || arguments[0].Value is not PyInstance instance)
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
        private readonly FunctionBindingPlan _bindingPlan =
            new($"{typeName}.__eq__", "Function", BinaryProtocolParameters, EmptyDefaultValues);

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            var bound = LythonRuntime.BindFunctionArguments(arguments, span, _bindingPlan, context);
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

    private sealed class DataclassOrderMethod : IPyBindableCallable
    {
        private readonly FunctionBindingPlan _bindingPlan;
        private readonly DataclassOrderOperation _operation;
        private readonly string _typeName;

        public DataclassOrderMethod(string typeName, DataclassOrderOperation operation)
        {
            _typeName = typeName;
            _operation = operation;
            var operationName = operation switch
            {
                DataclassOrderOperation.Less => "lt",
                DataclassOrderOperation.LessEqual => "le",
                DataclassOrderOperation.Greater => "gt",
                DataclassOrderOperation.GreaterEqual => "ge",
                _ => throw new InvalidOperationException("Unsupported dataclass order operation.")
            };
            _bindingPlan = new FunctionBindingPlan(
                $"{typeName}.__{operationName}__",
                "Function",
                BinaryProtocolParameters,
                EmptyDefaultValues);
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            var bound = LythonRuntime.BindFunctionArguments(arguments, span, _bindingPlan, context);
            if (bound["self"] is not PyInstance self || bound["other"] is not PyInstance other || !ReferenceEquals(self.Type, other.Type))
            {
                throw new LythonRuntimeException("TypeError", $"{_typeName} ordering expects two instances of the same dataclass type.", span);
            }

            var comparison = CompareOrderedInstances(self, other, span);
            return _operation switch
            {
                DataclassOrderOperation.Less => comparison < 0,
                DataclassOrderOperation.LessEqual => comparison <= 0,
                DataclassOrderOperation.Greater => comparison > 0,
                DataclassOrderOperation.GreaterEqual => comparison >= 0,
                _ => throw new InvalidOperationException("Unsupported dataclass order operation.")
            };
        }

    }

    private sealed class DataclassHashMethod(string typeName) : IPyBindableCallable
    {
        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 1 || arguments[0].IsKeyword || arguments[0].Value is not PyInstance instance)
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
