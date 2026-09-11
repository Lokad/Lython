using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static object Float(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length > 1)
        {
            throw new LythonRuntimeException("TypeError", "float([value]) expects at most one argument.", span);
        }

        if (arguments.Length == 0)
        {
            return 0.0;
        }

        if (arguments[0] is PyInstance floatInstance)
        {
            if (!floatInstance.TryGetAttribute("__float__", context, span, out var member) || member is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "float() argument must be a real number", span);
            }

            var converted = callable.Invoke([], span, context);
            if (converted is not double floating)
            {
                throw new LythonRuntimeException("TypeError", "__float__ returned non-float", span);
            }

            return floating;
        }

        try
        {
            return arguments[0] switch
            {
                double floating => floating,
                BigInteger integer => (double)integer,
                PyDecimal decimalValue => (double)decimalValue.Value,
                PyString text => ParsePythonFloatText(text.AsString()),
                bool boolean => boolean ? 1.0 : 0.0,
                _ => throw new LythonRuntimeException("TypeError", "float() does not support this value.", span)
            };
        }
        catch (FormatException ex)
        {
            throw new LythonRuntimeException("ValueError", ex.Message, span);
        }

        static double ParsePythonFloatText(string text)
        {
            var normalized = text.Trim().Replace("_", string.Empty, StringComparison.Ordinal);
            return normalized.ToLowerInvariant() switch
            {
                "inf" or "+inf" or "infinity" or "+infinity" => double.PositiveInfinity,
                "-inf" or "-infinity" => double.NegativeInfinity,
                "nan" or "+nan" => double.NaN,
                "-nan" => -double.NaN,
                _ => double.Parse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture),
            };
        }
    }

    private static object Bytes(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 0)
        {
            return new PyBytes(Array.Empty<byte>());
        }

        if (arguments.Length > 3)
        {
            throw new LythonRuntimeException("TypeError", "bytes([source[, encoding[, errors]]]) expects zero to three arguments.", span);
        }

        if (arguments.Length >= 2)
        {
            if (!PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "bytes(source, encoding[, errors]) expects source to be a string.", span);
            }

            var encoding = ParseTextEncoding(arguments[1], "bytes()", span);
            var errors = arguments.Length == 3 ? ParseTextErrors(arguments[2], "bytes()", span) : TextErrorMode.Strict;
            return CreateBytes(
                EncodeText(text, encoding, errors, TextNewlineMode.PreserveUniversal, context, span),
                context,
                span);
        }

        return arguments[0] switch
        {
            PyBytes bytes => CreateBytes(bytes.ToArray(), context, span),
            PyString => throw new LythonRuntimeException("TypeError", "string argument without an encoding", span),
            string => throw new LythonRuntimeException("TypeError", "string argument without an encoding", span),
            BigInteger size => CreateZeroBytes(size, context, span),
            int size => CreateZeroBytes(new BigInteger(size), context, span),
            bool size => CreateZeroBytes(size ? BigInteger.One : BigInteger.Zero, context, span),
            _ => CreateBytes(ToByteArray(arguments[0], span, context), context, span)
        };
    }

    private static PyBytes CreateZeroBytes(BigInteger size, ExecutionContext context, LythonSourceSpan span)
    {
        if (size < 0)
        {
            throw new LythonRuntimeException("ValueError", "negative count", span);
        }

        if (size > int.MaxValue)
        {
            throw new LythonRuntimeException("OverflowError", "bytes object is too large", span);
        }

        return CreateBytes(new byte[(int)size], context, span);
    }

    private static byte[] ToByteArray(object value, LythonSourceSpan span, ExecutionContext context)
    {
        var bytes = new List<byte>();
        foreach (var item in ToSequence(value, span, context))
        {
            bytes.Add(ToByte(item, span));
        }

        return [.. bytes];

        static byte ToByte(object value, LythonSourceSpan span)
        {
            if (!PyNumberOps.TryAsInteger(value, out var integer))
            {
                throw new LythonRuntimeException("TypeError", "bytes(iterable) expects integers between 0 and 255.", span);
            }

            if (integer < byte.MinValue || integer > byte.MaxValue)
            {
                throw new LythonRuntimeException("ValueError", "bytes(iterable) expects integers between 0 and 255.", span);
            }

            return (byte)integer;
        }
    }

    private static object Property(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length > 3)
        {
            throw new LythonRuntimeException("TypeError", "property([fget][, fset][, fdel]) expects zero to three callable arguments.", span);
        }

        var getter = arguments.Length >= 1 ? ParsePropertyCallable(arguments[0], "fget", span) : null;
        var setter = arguments.Length >= 2 ? ParsePropertyCallable(arguments[1], "fset", span) : null;
        var deleter = arguments.Length >= 3 ? ParsePropertyCallable(arguments[2], "fdel", span) : null;
        // The retained callables stay aliased; own the descriptor shell.
        context.MemoryGovernor.Reserve(64L, span);
        context.MemoryGovernor.Commit(64L);
        return new PyProperty(getter, setter, deleter);
    }

    private static object StaticMethod(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1 || arguments[0] is not ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", "staticmethod(func) expects one callable argument.", span);
        }

        context.MemoryGovernor.Reserve(64L, span);
        context.MemoryGovernor.Commit(64L);
        return new PyStaticMethod(callable);
    }

    private static object ClassMethod(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1 || arguments[0] is not ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", "classmethod(func) expects one callable argument.", span);
        }

        context.MemoryGovernor.Reserve(64L, span);
        context.MemoryGovernor.Commit(64L);
        return new PyClassMethod(callable);
    }

    private sealed class ObjectNewMethod : ICallable, IPyDynamicAttributes, IClassOwnedMember, IPyRenderableValue
    {
        // object.__new__ is a builtin method: CPython reports the short
        // __name__, the qualified __qualname__ and a None __module__.
        // The owning type is threaded at class construction so __self__
        // reports the defining type like CPython.
        private PyType? _owner;

        public void BindOwner(PyType owner) => _owner = owner;

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString("__new__");
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object.__new__");
                return true;
            }

            if (name is "__module__")
            {
                value = PyNone.Instance;
                return true;
            }

            if (name is "__self__" && _owner is not null)
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        // object.__new__ renders like CPython builtin methods bound to the
        // type, minus the address suffix (like the other new slots).
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<built-in method __new__ of type object>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);

            if (arguments.Length == 0 || arguments[0].Value is not PyType type)
            {
                throw new LythonRuntimeException("TypeError", "object.__new__(cls, ...) expects the first argument to be a class.", span);
            }

            return new PyInstance(type, context.MemoryGovernor, span);
        }
    }

    private sealed class ObjectInitMethod : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes, IPySlotWrapper, IClassOwnedMember, IPyRenderableValue
    {
        // Unbound object slots render like CPython slot wrappers (quoted
        // owner, plural objects).
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<slot wrapper '__init__' of 'object' objects>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public string Name => "__init__";

        // The owning type is threaded at class construction so
        // __objclass__ reports the defining type like CPython.
        private PyType? _owner;

        public void BindOwner(PyType owner) => _owner = owner;

        // Slot wrappers expose the short __name__ and the qualified
        // __qualname__ like CPython; __module__ stays missing (CPython
        // raises AttributeError there), unlike builtin methods.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object." + Name);
                return true;
            }

            if (name is "__objclass__" && _owner is not null)
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            // Any single receiver initializes to None like CPython; only the
            // arity is enforced here (attribute support is resolved by the
            // receiver type, not by this slot).
            if (arguments.Length != 1 || arguments[0].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "object.__init__(self) does not accept additional arguments.", span);
            }

            return PyNone.Instance;
        }
    }

    private sealed class ObjectInitSubclassMethod : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes, IPyBoundEngineMethod
    {
        public string Name => "__init_subclass__";

        // __init_subclass__ surfaces as a bound builtin method: CPython
        // reports the qualified __qualname__ and a None __module__.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object." + Name);
                return true;
            }

            if (name is "__module__")
            {
                value = PyNone.Instance;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            // The bound receiver is always class-like at this point (user types
            // arrive as PyType, builtin values as their type-denoting object);
            // only the arity is enforced, like CPython.
            if (arguments.Length != 1 || arguments[0].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "object.__init_subclass__() takes no arguments.", span);
            }

            return PyNone.Instance;
        }
    }

    private sealed class ObjectSetAttrMethod : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes, IPySlotWrapper, IClassOwnedMember, IPyRenderableValue
    {
        // Unbound object slots render like CPython slot wrappers (quoted
        // owner, plural objects).
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<slot wrapper '__setattr__' of 'object' objects>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public string Name => "__setattr__";

        // The owning type is threaded at class construction so
        // __objclass__ reports the defining type like CPython.
        private PyType? _owner;

        public void BindOwner(PyType owner) => _owner = owner;

        // Slot wrappers expose the short __name__ and the qualified
        // __qualname__ like CPython; __module__ stays missing (CPython
        // raises AttributeError there), unlike builtin methods.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object." + Name);
                return true;
            }

            if (name is "__objclass__" && _owner is not null)
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 3 ||
                arguments[0].IsKeyword ||
                arguments[1].IsKeyword ||
                arguments[2].IsKeyword ||
                !PyStringOps.TryAsString(arguments[1].Value, out var name))
            {
                throw new LythonRuntimeException("TypeError", "object.__setattr__(self, name, value) expects an instance, a string name, and a value.", span);
            }

            if (arguments[0].Value is PyInstance instance)
            {
                if (instance.Type.TryLookupInMro(name.AsString(), 0, out var rawValue, out _) &&
                    PyAttributeLookup.TrySetDescriptorValue(rawValue, instance, arguments[2].Value, context, span))
                {
                    return PyNone.Instance;
                }

                instance.AttachMemoryGovernor(context.MemoryGovernor, span);
                instance.SetAttribute(name.AsString(), arguments[2].Value);
                return PyNone.Instance;
            }

            // Other receivers follow statement assignment: writable modules,
            // types and mutable members accept; anything else has no
            // attribute table to write.
            if (PyMemberAccess.TryAssign(arguments[0].Value, name.AsString(), arguments[2].Value, context, span))
            {
                return PyNone.Instance;
            }

            throw PyMemberAccess.CreateMissingMemberError(arguments[0].Value, name.AsString(), span, context, operation: MissingMemberOperation.Write);
        }
    }

    private sealed class ObjectDelAttrMethod : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes, IPySlotWrapper, IClassOwnedMember, IPyRenderableValue
    {
        // Unbound object slots render like CPython slot wrappers (quoted
        // owner, plural objects).
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<slot wrapper '__delattr__' of 'object' objects>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public string Name => "__delattr__";

        // The owning type is threaded at class construction so
        // __objclass__ reports the defining type like CPython.
        private PyType? _owner;

        public void BindOwner(PyType owner) => _owner = owner;

        // Slot wrappers expose the short __name__ and the qualified
        // __qualname__ like CPython; __module__ stays missing (CPython
        // raises AttributeError there), unlike builtin methods.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object." + Name);
                return true;
            }

            if (name is "__objclass__" && _owner is not null)
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 2 ||
                arguments[0].IsKeyword ||
                arguments[1].IsKeyword ||
                !PyStringOps.TryAsString(arguments[1].Value, out var name))
            {
                throw new LythonRuntimeException("TypeError", "object.__delattr__(self, name) expects an instance and a string name.", span);
            }

            var memberName = name.AsString();
            if (arguments[0].Value is PyInstance instance)
            {
                if (instance.Type.TryLookupInMro(memberName, 0, out var rawValue, out _) &&
                    PyAttributeLookup.TryDeleteDescriptorValue(rawValue, instance, context, span))
                {
                    return PyNone.Instance;
                }

                if (!instance.RemoveAttribute(memberName))
                {
                    throw PyMemberAccess.CreateMissingMemberError(instance, memberName, span, context, operation: MissingMemberOperation.Delete);
                }

                return PyNone.Instance;
            }

            // Other receivers follow statement deletion; anything else has no
            // attribute table to delete from.
            if (PyMemberAccess.TryDelete(arguments[0].Value, memberName, context, span))
            {
                return PyNone.Instance;
            }

            throw PyMemberAccess.CreateMissingMemberError(arguments[0].Value, memberName, span, context, operation: MissingMemberOperation.Delete);
        }
    }

    private sealed class ObjectGetAttrMethod : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes, IPySlotWrapper, IClassOwnedMember, IPyRenderableValue
    {
        // Unbound object slots render like CPython slot wrappers (quoted
        // owner, plural objects).
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<slot wrapper '__getattribute__' of 'object' objects>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public string Name => "__getattribute__";

        // The owning type is threaded at class construction so
        // __objclass__ reports the defining type like CPython.
        private PyType? _owner;

        public void BindOwner(PyType owner) => _owner = owner;

        // Slot wrappers expose the short __name__ and the qualified
        // __qualname__ like CPython; __module__ stays missing (CPython
        // raises AttributeError there), unlike builtin methods.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object." + Name);
                return true;
            }

            if (name is "__objclass__" && _owner is not null)
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 2 ||
                arguments[0].IsKeyword ||
                arguments[1].IsKeyword ||
                !PyStringOps.TryAsString(arguments[1].Value, out var name))
            {
                throw new LythonRuntimeException("TypeError", "object.__getattribute__(self, name) expects an instance and a string name.", span);
            }

            var memberName = name.AsString();
            if (arguments[0].Value is PyInstance instance)
            {
                if (PyAttributeLookup.TryResolveInstanceMemberWithoutGetAttrFallback(instance, memberName, context, span, out var value))
                {
                    return value;
                }

                throw PyMemberAccess.CreateMissingMemberError(instance, memberName, span, context);
            }

            // Other receivers resolve through the same choke as ordinary
            // member reads, including bound-method receivers, since reads
            // never route back through this slot.
            if (TryResolveRuntimeMember(arguments[0].Value, memberName, context, span, out var resolved))
            {
                return resolved;
            }

            throw PyMemberAccess.CreateMissingMemberError(arguments[0].Value, memberName, span, context);
        }
    }

    private sealed class ObjectEqMethod : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes, IPySlotWrapper, IClassOwnedMember, IPyRenderableValue
    {
        // Unbound object slots render like CPython slot wrappers (quoted
        // owner, plural objects).
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<slot wrapper '__eq__' of 'object' objects>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public string Name => "__eq__";

        // The owning type is threaded at class construction so
        // __objclass__ reports the defining type like CPython.
        private PyType? _owner;

        public void BindOwner(PyType owner) => _owner = owner;

        // Slot wrappers expose the short __name__ and the qualified
        // __qualname__ like CPython; __module__ stays missing (CPython
        // raises AttributeError there), unlike builtin methods.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object." + Name);
                return true;
            }

            if (name is "__objclass__" && _owner is not null)
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            // Default equality is pure object identity like CPython (value and dataclass comparisons keep their own paths);
            // only the arity is enforced here.
            if (arguments.Length != 2 || arguments[0].IsKeyword || arguments[1].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "object.__eq__(self, other) expects exactly two arguments.", span);
            }

            return ReferenceEquals(arguments[0].Value, arguments[1].Value);
        }
    }

    private sealed class ObjectNeMethod : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes, IPySlotWrapper, IClassOwnedMember, IPyRenderableValue
    {
        // Unbound object slots render like CPython slot wrappers (quoted
        // owner, plural objects).
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<slot wrapper '__ne__' of 'object' objects>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public string Name => "__ne__";

        // The owning type is threaded at class construction so
        // __objclass__ reports the defining type like CPython.
        private PyType? _owner;

        public void BindOwner(PyType owner) => _owner = owner;

        // Slot wrappers expose the short __name__ and the qualified
        // __qualname__ like CPython; __module__ stays missing (CPython
        // raises AttributeError there), unlike builtin methods.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object." + Name);
                return true;
            }

            if (name is "__objclass__" && _owner is not null)
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            // Default inequality negates identity like CPython (value and dataclass comparisons keep their own paths);
            // only the arity is enforced here.
            if (arguments.Length != 2 || arguments[0].IsKeyword || arguments[1].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "object.__ne__(self, other) expects exactly two arguments.", span);
            }

            return !ReferenceEquals(arguments[0].Value, arguments[1].Value);
        }
    }
    private sealed class ObjectLtMethod : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes, IPySlotWrapper, IClassOwnedMember, IPyRenderableValue
    {
        // Unbound object slots render like CPython slot wrappers (quoted
        // owner, plural objects).
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<slot wrapper '__lt__' of 'object' objects>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public string Name => "__lt__";

        // The owning type is threaded at class construction so
        // __objclass__ reports the defining type like CPython.
        private PyType? _owner;

        public void BindOwner(PyType owner) => _owner = owner;

        // Slot wrappers expose the short __name__ and the qualified
        // __qualname__ like CPython; __module__ stays missing (CPython
        // raises AttributeError there), unlike builtin methods.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object." + Name);
                return true;
            }

            if (name is "__objclass__" && _owner is not null)
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            // Default ordering is undefined like CPython (value and dataclass
            // comparisons keep their own paths); the slot always answers
            // NotImplemented, even for identical operands. Only the arity is
            // enforced here.
            if (arguments.Length != 2 || arguments[0].IsKeyword || arguments[1].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "object.__lt__(self, other) expects exactly two arguments.", span);
            }

            return PyNotImplemented.Instance;
        }
    }    private sealed class ObjectLeMethod : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes, IPySlotWrapper, IClassOwnedMember, IPyRenderableValue
    {
        // Unbound object slots render like CPython slot wrappers (quoted
        // owner, plural objects).
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<slot wrapper '__le__' of 'object' objects>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public string Name => "__le__";

        // The owning type is threaded at class construction so
        // __objclass__ reports the defining type like CPython.
        private PyType? _owner;

        public void BindOwner(PyType owner) => _owner = owner;

        // Slot wrappers expose the short __name__ and the qualified
        // __qualname__ like CPython; __module__ stays missing (CPython
        // raises AttributeError there), unlike builtin methods.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object." + Name);
                return true;
            }

            if (name is "__objclass__" && _owner is not null)
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            // Default ordering is undefined like CPython (value and dataclass
            // comparisons keep their own paths); the slot always answers
            // NotImplemented, even for identical operands. Only the arity is
            // enforced here.
            if (arguments.Length != 2 || arguments[0].IsKeyword || arguments[1].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "object.__le__(self, other) expects exactly two arguments.", span);
            }

            return PyNotImplemented.Instance;
        }
    }    private sealed class ObjectGtMethod : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes, IPySlotWrapper, IClassOwnedMember, IPyRenderableValue
    {
        // Unbound object slots render like CPython slot wrappers (quoted
        // owner, plural objects).
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<slot wrapper '__gt__' of 'object' objects>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public string Name => "__gt__";

        // The owning type is threaded at class construction so
        // __objclass__ reports the defining type like CPython.
        private PyType? _owner;

        public void BindOwner(PyType owner) => _owner = owner;

        // Slot wrappers expose the short __name__ and the qualified
        // __qualname__ like CPython; __module__ stays missing (CPython
        // raises AttributeError there), unlike builtin methods.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object." + Name);
                return true;
            }

            if (name is "__objclass__" && _owner is not null)
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            // Default ordering is undefined like CPython (value and dataclass
            // comparisons keep their own paths); the slot always answers
            // NotImplemented, even for identical operands. Only the arity is
            // enforced here.
            if (arguments.Length != 2 || arguments[0].IsKeyword || arguments[1].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "object.__gt__(self, other) expects exactly two arguments.", span);
            }

            return PyNotImplemented.Instance;
        }
    }    private sealed class ObjectGeMethod : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes, IPySlotWrapper, IClassOwnedMember, IPyRenderableValue
    {
        // Unbound object slots render like CPython slot wrappers (quoted
        // owner, plural objects).
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<slot wrapper '__ge__' of 'object' objects>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public string Name => "__ge__";

        // The owning type is threaded at class construction so
        // __objclass__ reports the defining type like CPython.
        private PyType? _owner;

        public void BindOwner(PyType owner) => _owner = owner;

        // Slot wrappers expose the short __name__ and the qualified
        // __qualname__ like CPython; __module__ stays missing (CPython
        // raises AttributeError there), unlike builtin methods.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object." + Name);
                return true;
            }

            if (name is "__objclass__" && _owner is not null)
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            // Default ordering is undefined like CPython (value and dataclass
            // comparisons keep their own paths); the slot always answers
            // NotImplemented, even for identical operands. Only the arity is
            // enforced here.
            if (arguments.Length != 2 || arguments[0].IsKeyword || arguments[1].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "object.__ge__(self, other) expects exactly two arguments.", span);
            }

            return PyNotImplemented.Instance;
        }
    }    private sealed class ObjectStrMethod : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes, IPySlotWrapper, IClassOwnedMember, IPyRenderableValue
    {
        // Unbound object slots render like CPython slot wrappers (quoted
        // owner, plural objects).
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<slot wrapper '__str__' of 'object' objects>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public string Name => "__str__";

        private PyType? _owner;

        public void BindOwner(PyType owner) => _owner = owner;

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object." + Name);
                return true;
            }

            if (name is "__objclass__" && _owner is not null)
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length != 1 || arguments[0].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "object.__str__(self) expects exactly one argument.", span);
            }

            // Like CPython, the default string conversion defers to __repr__
            // so custom representations win; otherwise the default applies.
            if (arguments[0].Value is PyInstance instance &&
                instance.TryGetAttribute("__repr__", context, span, out var member) &&
                member is ICallable reprCallable)
            {
                var text = reprCallable.Invoke([], span, context);
                if (PyStringOps.TryAsString(text, out var rendered))
                {
                    return rendered;
                }

                throw new LythonRuntimeException("TypeError", "__repr__ returned non-string", null);
            }

            if (arguments[0].Value is PyInstance plain)
            {
                return PyString.FromString("<" + plain.Type.Name + " object>");
            }

            return PyRendering.ToInterpolatedPyString(arguments[0].Value, new PyRenderingContext(context));
        }
    }
    private sealed class ObjectReprMethod : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes, IPySlotWrapper, IClassOwnedMember, IPyRenderableValue
    {
        // Unbound object slots render like CPython slot wrappers (quoted
        // owner, plural objects).
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<slot wrapper '__repr__' of 'object' objects>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public string Name => "__repr__";

        private PyType? _owner;

        public void BindOwner(PyType owner) => _owner = owner;

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object." + Name);
                return true;
            }

            if (name is "__objclass__" && _owner is not null)
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 1 || arguments[0].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "object.__repr__(self) expects exactly one argument.", span);
            }

            // The default representation never consults overrides (explicit
            // root-slot calls bypass them like CPython); user and generated
            // representations shadow this slot through member lookup.
            if (arguments[0].Value is PyInstance instance)
            {
                return PyString.FromString("<" + instance.Type.Name + " object>");
            }

            return PyRendering.ToReprPyString(arguments[0].Value, new PyRenderingContext(context));
        }
    }
    private sealed class ObjectHashMethod : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes, IPySlotWrapper, IClassOwnedMember, IPyRenderableValue
    {
        // Unbound object slots render like CPython slot wrappers (quoted
        // owner, plural objects).
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<slot wrapper '__hash__' of 'object' objects>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public string Name => "__hash__";

        private PyType? _owner;

        public void BindOwner(PyType owner) => _owner = owner;

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object." + Name);
                return true;
            }

            if (name is "__objclass__" && _owner is not null)
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length != 1 || arguments[0].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "object.__hash__(self) expects exactly one argument.", span);
            }

            // Unhashable dataclass instances keep their shaped failure like
            // the hash builtin (generated hashes shadow this slot instead).
            if (arguments[0].Value is PyInstance instance &&
                instance.Type.DataclassFields is { } &&
                instance.Type.DataclassHashMode == DataclassHashMode.Unhashable)
            {
                throw RuntimeErrors.UnhashableType(arguments[0].Value, span);
            }

            // Identity hashes surface as BigInteger like every other guest
            // integer (raw CLR integers stay second-class in numeric paths).
            return new BigInteger(RuntimeHelpers.GetHashCode(arguments[0].Value));
        }
    }
    // object.__format__ behaves like CPython method descriptors: the unbound
    // shape renders like one, calls forward through a bound engine method,
    // and binding reports the receiver type through the shared helper.
    private sealed class ObjectFormatMethod : ICallable, IPyBindableCallable, IPyDynamicAttributes, IPyHashableValue, IPyRenderableValue, IClassOwnedMember
    {
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<method '__format__' of 'object' objects>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public int GetPyHashCode() => HashCode.Combine(StringComparer.Ordinal.GetHashCode("object"), StringComparer.Ordinal.GetHashCode("__format__"));

        private PyType? _owner;

        public void BindOwner(PyType owner) => _owner = owner;

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString("__format__");
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object.__format__");
                return true;
            }

            if (name is "__objclass__" && _owner is not null)
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self)
        {
            var bound = BoundCallable.Create(
                (arguments, span, context) => FormatImpl(self, arguments, span, context),
                "object.__format__",
                ["spec"],
                1);
            bound.AttachReceiver(self);
            return bound;
        }

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var receiverIndex = -1;
            for (var i = 0; i < arguments.Length; i++)
            {
                if (arguments[i].IsPositional)
                {
                    receiverIndex = i;
                    break;
                }
            }

            if (receiverIndex < 0)
            {
                throw new LythonRuntimeException("TypeError", "unbound method object.__format__() needs an argument", span);
            }

            var receiver = arguments[receiverIndex].Value;
            var bound = (ICallable)Bind(receiver);
            var rest = new CallArgumentValue[arguments.Length - 1];
            Array.Copy(arguments, 0, rest, 0, receiverIndex);
            Array.Copy(arguments, receiverIndex + 1, rest, receiverIndex, rest.Length - receiverIndex);
            return bound.Invoke(rest, span, context);
        }

        private static object FormatImpl(object receiver, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (!PyStringOps.TryAsString(arguments[0], out var spec))
            {
                throw new LythonRuntimeException("TypeError", "object.__format__(self, spec) expects the format specification to be a string.", span);
            }

            // Only the empty spec formats through str() like CPython; any
            // other spec on the default implementation reports TypeError
            // naming the receiver type, mirroring the format builtin.
            if (spec.AsString().Length != 0)
            {
                throw new LythonRuntimeException("TypeError", $"unsupported format string passed to {RuntimeErrors.OperandTypeName(receiver)}.__format__", span);
            }

            return ToInterpolatedPyString(receiver, context);
        }
    }
    // object.__dir__ behaves like CPython method descriptors: the unbound
    // shape renders like one, calls forward through a bound engine method,
    // and binding reports the receiver type through the shared helper.
    private sealed class ObjectDirMethod : ICallable, IPyBindableCallable, IPyDynamicAttributes, IPyHashableValue, IPyRenderableValue, IClassOwnedMember
    {
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<method '__dir__' of 'object' objects>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public int GetPyHashCode() => HashCode.Combine(StringComparer.Ordinal.GetHashCode("object"), StringComparer.Ordinal.GetHashCode("__dir__"));

        private PyType? _owner;

        public void BindOwner(PyType owner) => _owner = owner;

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString("__dir__");
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString("object.__dir__");
                return true;
            }

            if (name is "__objclass__" && _owner is not null)
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Bind(object self)
        {
            var bound = BoundCallable.Create(
                (arguments, span, context) => DirImpl(self, span, context),
                "object.__dir__");
            bound.AttachReceiver(self);
            return bound;
        }

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var receiverIndex = -1;
            for (var i = 0; i < arguments.Length; i++)
            {
                if (arguments[i].IsPositional)
                {
                    receiverIndex = i;
                    break;
                }
            }

            if (receiverIndex < 0)
            {
                throw new LythonRuntimeException("TypeError", "unbound method object.__dir__() needs an argument", span);
            }

            var receiver = arguments[receiverIndex].Value;
            var bound = (ICallable)Bind(receiver);
            var rest = new CallArgumentValue[arguments.Length - 1];
            Array.Copy(arguments, 0, rest, 0, receiverIndex);
            Array.Copy(arguments, receiverIndex + 1, rest, receiverIndex, rest.Length - receiverIndex);
            return bound.Invoke(rest, span, context);
        }

        // Listing delegates to the same helpers as the dir builtin so
        // explicit calls always agree with dir() by construction.
        private static object DirImpl(object receiver, LythonSourceSpan span, ExecutionContext context)
        {
            var names = EnumerateDirNames(receiver);
            if (names is null)
            {
                throw new LythonRuntimeException("TypeError", "dir(object) is not supported for this object.", span);
            }

            return CreateUnsortedNameList(names, context, span);
        }
    }
    private static ICallable? ParsePropertyCallable(object value, string parameterName, LythonSourceSpan span)
    {
        return value switch
        {
            null or PyNone => null,
            ICallable callable => callable,
            _ => throw new LythonRuntimeException("TypeError", $"property({parameterName}=...) expects a callable or None.", span)
        };
    }


}
