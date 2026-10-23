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

    internal sealed class DataclassInitMethod : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes
    {
        public string Name => "__init__";

        // Generated dunder methods expose __name__ like their CPython
        // counterparts; __module__ aliases the defining module captured
        // at decoration time.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__" or "__qualname__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name == "__module__" && ModuleName is not null)
            {
                value = ModuleName;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        private readonly FunctionBindingPlan _bindingPlan;
        private readonly IReadOnlyList<DataclassFieldSpec> _fields;
        private readonly string _typeName;
        private readonly bool _hasPostInit;
        private readonly PyString? _moduleName;

        public PyString? ModuleName => _moduleName;

        public DataclassInitMethod(PyType declaringType, IReadOnlyList<DataclassFieldSpec> fields, PyString? moduleName, LythonRuntime.ExecutionContext context)
        {
            var typeName = declaringType.Name;
            _typeName = typeName;
            _hasPostInit = declaringType.TryGetMember("__post_init__", out _);
            _fields = fields;
            _moduleName = moduleName;

            var parameters = new List<LoweredFunctionParameter>(fields.Count + 1)
            {
                new("self", FunctionParameterKind.Positional, null, null)
            };
            using var defaultStorage = LythonRuntime.ReserveDefaultArgumentsWorkingStorage(fields.Count(static field => field.HasDefault || field.HasDefaultFactory), context.MemoryGovernor, null);
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

            _bindingPlan = new FunctionBindingPlan($"{typeName}.__init__", PythonCallableKind.Function, parameters, defaults, context);
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => InvokeCore(arguments, span, context, false).GetAwaiter().GetResult();

        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => InvokeCore(arguments, span, context, true);

        private async ValueTask<object> InvokeCore(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context, bool asynchronous)
        {
            var bound = LythonRuntime.BindFunctionArguments(arguments, span, _bindingPlan, context);
            try
            {
                if (bound.Values[_bindingPlan.LayoutParameterIndex["self"]] is not PyInstance instance)
                {
                    throw new LythonRuntimeException("TypeError", $"{_typeName}.__init__ expected a bound instance.", span);
                }

                using var working = context.MemoryGovernor.ReserveTemporary(checked(64L + _fields.Count * 40L), span);
                var initVarValues = new List<object>(_fields.Count);
                foreach (var field in _fields)
                {
                    if (!field.Store)
                    {
                        if (field.Kind == DataclassFieldKind.InitVar && field.Init)
                        {
                            var initVarValue = bound.Values[_bindingPlan.LayoutParameterIndex[field.Name]];
                            if (ReferenceEquals(initVarValue, DefaultFactorySentinel))
                            {
                                initVarValue = await InvokeDefaultFactoryCore(field, span, context, asynchronous).ConfigureAwait(false);
                            }

                            initVarValues.Add(initVarValue);
                        }
                        continue;
                    }

                    object value;
                    if (field.Init)
                    {
                        value = bound.Values[_bindingPlan.LayoutParameterIndex[field.Name]];
                        if (ReferenceEquals(value, DefaultFactorySentinel))
                        {
                            value = await InvokeDefaultFactoryCore(field, span, context, asynchronous).ConfigureAwait(false);
                        }
                    }
                    else if (field.HasDefaultFactory)
                    {
                        value = await InvokeDefaultFactoryCore(field, span, context, asynchronous).ConfigureAwait(false);
                    }
                    else if (field.HasDefault)
                    {
                        value = field.DefaultValue;
                    }
                    else
                    {
                        continue;
                    }

                    if (asynchronous) await LythonRuntime.SetObjectInstanceAttributeAsync(instance, field.Name, value, context, span).ConfigureAwait(false);
                    else LythonRuntime.SetObjectInstanceAttribute(instance, field.Name, value, context, span);
                }

                if (_hasPostInit)
                {
                    var callable = asynchronous ? await ReadFieldAsync(instance, "__post_init__", context, span).ConfigureAwait(false) : ReadField(instance, "__post_init__", context, span);
                    if (callable is not LythonRuntime.ICallable postInitCallable) throw RuntimeErrors.Type("__post_init__ must be callable", span);
                    var postInitArguments = initVarValues.Select(CallArgumentValue.Positional).ToArray();
                    if (asynchronous) await postInitCallable.InvokeAsync(postInitArguments, span, context).ConfigureAwait(false);
                    else postInitCallable.Invoke(postInitArguments, span, context);
                }

                return PyNone.Instance;
            }
            finally
            {
                // Generated dunders enter no frame, so the rented
                // bound-values box is returned on every exit path.
                LythonRuntime.ReturnBoundValues(bound.Values);
            }
        }

        private static async ValueTask<object> InvokeDefaultFactoryCore(DataclassFieldSpec field, LythonSourceSpan span, LythonRuntime.ExecutionContext context, bool asynchronous)
        {
            if (field.DefaultFactory is not LythonRuntime.ICallable callable) throw RuntimeErrors.Type("default_factory must be callable", span);
            return asynchronous ? await callable.InvokeAsync([], span, context).ConfigureAwait(false) : callable.Invoke([], span, context);
        }
    }

    internal sealed class DataclassReprMethod(string typeName, IReadOnlyList<DataclassFieldSpec> fields, PyString? moduleName) : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes
    {
        private readonly PyString? _moduleName = moduleName;

        public PyString? ModuleName => _moduleName;

        public string Name => "__repr__";

        // Generated dunder methods expose __name__ like their CPython
        // counterparts; __module__ aliases the defining module captured
        // at decoration time.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__" or "__qualname__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name == "__module__" && ModuleName is not null)
            {
                value = ModuleName;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => InvokeCore(arguments, span, context, false).GetAwaiter().GetResult();
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => InvokeCore(arguments, span, context, true);
        private async ValueTask<object> InvokeCore(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context, bool asynchronous)
        {
            if (arguments.Length != 1 || arguments[0].IsKeyword || arguments[0].Value is not PyInstance instance)
            {
                throw new LythonRuntimeException("TypeError", $"{typeName}.__repr__() expected a bound instance.", span);
            }

            var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
            try
            {
                builder.AppendString(instance.Type.Name);
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
                    var value = asynchronous ? await ReadFieldAsync(instance, field.Name, context, span).ConfigureAwait(false) : ReadField(instance, field.Name, context, span);
                    builder.Append(asynchronous && value is PyInstance leaf ? (PyString)await LythonRuntime.RenderInstanceStringAsync(leaf, 'r', context, span).ConfigureAwait(false) : PyRendering.ToReprPyString(value, new PyRenderingContext(context)));
                }

                builder.AppendAscii(")");
                return builder.ToPyStringAndRelease();
            }
            finally { builder.Release(); }
        }
    }

    internal sealed class DataclassEqMethod(string typeName, IReadOnlyList<DataclassFieldSpec> fields, PyString? moduleName) : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes
    {
        private readonly PyString? _moduleName = moduleName;

        public PyString? ModuleName => _moduleName;

        public string Name => "__eq__";

        // Generated dunder methods expose __name__ like their CPython
        // counterparts; __module__ aliases the defining module captured
        // at decoration time.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__" or "__qualname__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name == "__module__" && ModuleName is not null)
            {
                value = ModuleName;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        private readonly FunctionBindingPlan _bindingPlan =
            new($"{typeName}.__eq__", PythonCallableKind.Function, BinaryProtocolParameters, EmptyDefaultValues);

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => InvokeCore(arguments, span, context, false).GetAwaiter().GetResult();
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => InvokeCore(arguments, span, context, true);
        private async ValueTask<object> InvokeCore(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context, bool asynchronous)
        {
            var bound = LythonRuntime.BindFunctionArguments(arguments, span, _bindingPlan, context);
            try
            {
                if (bound.Values[_bindingPlan.LayoutParameterIndex["self"]] is not PyInstance self)
                {
                    throw new LythonRuntimeException("TypeError", $"{typeName}.__eq__ expected a bound instance.", span);
                }

                if (bound.Values[_bindingPlan.LayoutParameterIndex["other"]] is not PyInstance other || !ReferenceEquals(self.Type, other.Type))
                {
                    return PyNotImplemented.Instance;
                }

                using (PyStructuralGuard.EnterPair(self, other, span, context))
                {
                    foreach (var field in fields)
                    {
                        PyStructuralGuard.NoteWork();
                        var left = asynchronous ? await ReadFieldAsync(self, field.Name, context, span).ConfigureAwait(false) : ReadField(self, field.Name, context, span);
                        var right = asynchronous ? await ReadFieldAsync(other, field.Name, context, span).ConfigureAwait(false) : ReadField(other, field.Name, context, span);
                        if (!(asynchronous ? await LythonRuntime.ElementEqualsAsync(left, right, context, span).ConfigureAwait(false) : LythonRuntime.ElementEquals(left, right, context, span)))
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }
            finally
            {
                // Generated dunders enter no frame, so the rented
                // bound-values box is returned on every exit path.
                LythonRuntime.ReturnBoundValues(bound.Values);
            }
        }
    }

    internal enum DataclassOrderOperation
    {
        Less,
        LessEqual,
        Greater,
        GreaterEqual
    }

    internal sealed class DataclassOrderMethod : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes
    {
        private readonly PyString? _moduleName;

        public PyString? ModuleName => _moduleName;

        public string Name => _operation switch
        {
            DataclassOrderOperation.Less => "__lt__",
            DataclassOrderOperation.LessEqual => "__le__",
            DataclassOrderOperation.Greater => "__gt__",
            DataclassOrderOperation.GreaterEqual => "__ge__",
            _ => throw new InvalidOperationException("Unsupported dataclass order operation."),
        };

        // Generated dunder methods expose __name__ like their CPython
        // counterparts; __module__ aliases the defining module captured
        // at decoration time.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__" or "__qualname__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name == "__module__" && ModuleName is not null)
            {
                value = ModuleName;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        private readonly FunctionBindingPlan _bindingPlan;
        private readonly DataclassOrderOperation _operation;
        private readonly IReadOnlyList<DataclassFieldSpec> _fields;
        private readonly string _typeName;

        public DataclassOrderMethod(string typeName, IReadOnlyList<DataclassFieldSpec> fields, DataclassOrderOperation operation, PyString? moduleName)
        {
            _typeName = typeName;
            _operation = operation;
            _fields = fields;
            _moduleName = moduleName;
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
                PythonCallableKind.Function,
                BinaryProtocolParameters,
                EmptyDefaultValues);
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => InvokeCore(arguments, span, context, false).GetAwaiter().GetResult();
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => InvokeCore(arguments, span, context, true);
        private async ValueTask<object> InvokeCore(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context, bool asynchronous)
        {
            var bound = LythonRuntime.BindFunctionArguments(arguments, span, _bindingPlan, context);
            try
            {
                if (bound.Values[_bindingPlan.LayoutParameterIndex["self"]] is not PyInstance self || bound.Values[_bindingPlan.LayoutParameterIndex["other"]] is not PyInstance other || !ReferenceEquals(self.Type, other.Type))
                {
                    return PyNotImplemented.Instance;
                }

                using var guard = PyStructuralGuard.EnterPair(self, other, span, context);
                foreach (var field in _fields)
                {
                    context.CheckExecution(span);
                    var left = asynchronous ? await ReadFieldAsync(self, field.Name, context, span).ConfigureAwait(false) : ReadField(self, field.Name, context, span);
                    var right = asynchronous ? await ReadFieldAsync(other, field.Name, context, span).ConfigureAwait(false) : ReadField(other, field.Name, context, span);
                    if (asynchronous ? await LythonRuntime.ElementEqualsAsync(left, right, context, span).ConfigureAwait(false) : LythonRuntime.ElementEquals(left, right, context, span)) continue;
                    var op = _operation switch
                    {
                        DataclassOrderOperation.Less => BinaryOperatorSyntax.Less,
                        DataclassOrderOperation.LessEqual => BinaryOperatorSyntax.LessEqual,
                        DataclassOrderOperation.Greater => BinaryOperatorSyntax.Greater,
                        _ => BinaryOperatorSyntax.GreaterEqual
                    };
                    return asynchronous ? await LythonRuntime.EvaluateBinaryOperatorAsync(op, left, right, context, span).ConfigureAwait(false) : LythonRuntime.EvaluateBinaryOperator(op, left, right, context, span);
                }
                return _operation is DataclassOrderOperation.LessEqual or DataclassOrderOperation.GreaterEqual;
            }
            finally
            {
                // Generated dunders enter no frame, so the rented
                // bound-values box is returned on every exit path.
                LythonRuntime.ReturnBoundValues(bound.Values);
            }
        }

    }

    internal sealed class DataclassHashMethod(string typeName, PyString? moduleName) : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes
    {
        private readonly PyString? _moduleName = moduleName;

        public PyString? ModuleName => _moduleName;

        public string Name => "__hash__";

        // Generated dunder methods expose __name__ like their CPython
        // counterparts; __module__ aliases the defining module captured
        // at decoration time.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__" or "__qualname__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name == "__module__" && ModuleName is not null)
            {
                value = ModuleName;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            if (arguments.Length != 1 || arguments[0].IsKeyword || arguments[0].Value is not PyInstance instance)
            {
                throw new LythonRuntimeException("TypeError", $"{typeName}.__hash__() expected a bound instance.", span);
            }

            using var ambient = PyStructuralGuard.PushAmbient(context, span);
            return new BigInteger(instance.GetPyHashCode());
        }
    }

    internal sealed class DataclassFrozenSetAttrMethod(PyType declaringType, IReadOnlyList<DataclassFieldSpec> fields, PyString? moduleName) : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes
    {
        private readonly PyString? _moduleName = moduleName;

        public PyString? ModuleName => _moduleName;

        public string Name => "__setattr__";

        // Generated dunder methods expose __name__ like their CPython
        // counterparts; __module__ aliases the defining module captured
        // at decoration time.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__" or "__qualname__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name == "__module__" && ModuleName is not null)
            {
                value = ModuleName;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => InvokeCore(arguments, span, context, false).GetAwaiter().GetResult();
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => InvokeCore(arguments, span, context, true);
        private async ValueTask<object> InvokeCore(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context, bool asynchronous)
        {
            if (arguments.Length != 3 || arguments.Any(static a => a.IsKeyword) || arguments[0].Value is not PyInstance instance) throw RuntimeErrors.Type("__setattr__ expects self, name and value", span);
            var name = RuntimeArgumentValidation.ExpectString(arguments[1].Value, "__setattr__", span);
            if (ReferenceEquals(instance.Type, declaringType) || fields.Any(f => f.Kind == DataclassFieldKind.Normal && f.Name == name))
                throw new LythonRuntimeException(LythonRuntime.ModuleException("dataclasses", "FrozenInstanceError"), $"cannot assign to field '{name}'", span);
            if (asynchronous) await LythonRuntime.SetObjectInstanceAttributeAsync(instance, name, arguments[2].Value, context, span).ConfigureAwait(false);
            else LythonRuntime.SetObjectInstanceAttribute(instance, name, arguments[2].Value, context, span);
            return PyNone.Instance;
        }
    }

    internal sealed class DataclassFrozenDelAttrMethod(PyType declaringType, IReadOnlyList<DataclassFieldSpec> fields, PyString? moduleName) : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes
    {
        private readonly PyString? _moduleName = moduleName;

        public PyString? ModuleName => _moduleName;

        public string Name => "__delattr__";

        // Generated dunder methods expose __name__ like their CPython
        // counterparts; __module__ aliases the defining module captured
        // at decoration time.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__" or "__qualname__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name == "__module__" && ModuleName is not null)
            {
                value = ModuleName;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => InvokeCore(arguments, span, context, false).GetAwaiter().GetResult();
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => InvokeCore(arguments, span, context, true);
        private async ValueTask<object> InvokeCore(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context, bool asynchronous)
        {
            if (arguments.Length != 2 || arguments.Any(static a => a.IsKeyword) || arguments[0].Value is not PyInstance instance) throw RuntimeErrors.Type("__delattr__ expects self and name", span);
            var name = RuntimeArgumentValidation.ExpectString(arguments[1].Value, "__delattr__", span);
            if (ReferenceEquals(instance.Type, declaringType) || fields.Any(f => f.Kind == DataclassFieldKind.Normal && f.Name == name))
                throw new LythonRuntimeException(LythonRuntime.ModuleException("dataclasses", "FrozenInstanceError"), $"cannot delete field '{name}'", span);
            if (instance.Type.TryLookupInMro(name, 0, out var raw, out _) && (asynchronous ? await PyAttributeLookup.TryDeleteDescriptorValueAsync(raw, instance, context, span).ConfigureAwait(false) : PyAttributeLookup.TryDeleteDescriptorValue(raw, instance, context, span))) return PyNone.Instance;
            if (!instance.RemoveAttribute(name)) throw new LythonRuntimeException("AttributeError", $"missing attribute '{name}'", span);
            return PyNone.Instance;
        }
    }
}
