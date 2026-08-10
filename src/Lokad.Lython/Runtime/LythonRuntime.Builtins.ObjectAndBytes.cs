using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static object Float(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
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
                string text => ParsePythonFloatText(text),
                bool boolean => boolean ? 1.0 : 0.0,
                _ => throw new LythonRuntimeException("TypeError", "float() does not support this value.", span)
            };
        }
        catch (FormatException ex)
        {
            throw new LythonRuntimeException("ValueError", ex.Message, span);
        }
    }

    private static double ParsePythonFloatText(string text)
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
            _ => CreateBytes(ToByteArray(arguments[0], span), context, span)
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

    private static byte[] ToByteArray(object value, LythonSourceSpan span)
    {
        var bytes = new List<byte>();
        foreach (var item in ToSequence(value, span))
        {
            bytes.Add(ToByte(item, span));
        }

        return [.. bytes];
    }

    private static object Property(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length > 3)
        {
            throw new LythonRuntimeException("TypeError", "property([fget][, fset][, fdel]) expects zero to three callable arguments.", span);
        }

        var getter = arguments.Length >= 1 ? ParsePropertyCallable(arguments[0], "fget", span) : null;
        var setter = arguments.Length >= 2 ? ParsePropertyCallable(arguments[1], "fset", span) : null;
        var deleter = arguments.Length >= 3 ? ParsePropertyCallable(arguments[2], "fdel", span) : null;
        return new PyProperty(getter, setter, deleter);
    }

    private static object StaticMethod(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1 || arguments[0] is not ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", "staticmethod(func) expects one callable argument.", span);
        }

        return new PyStaticMethod(callable);
    }

    private static object ClassMethod(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1 || arguments[0] is not ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", "classmethod(func) expects one callable argument.", span);
        }

        return new PyClassMethod(callable);
    }

    private sealed class ObjectNewMethod : ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);

            if (arguments.Length == 0 || arguments[0].Value is not PyType type)
            {
                throw new LythonRuntimeException("TypeError", "object.__new__(cls, ...) expects the first argument to be a class.", span);
            }

            return new PyInstance(type);
        }
    }

    private sealed class ObjectInitMethod : IPyBindableCallable
    {
        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 1 || arguments[0].IsKeyword || arguments[0].Value is not PyInstance)
            {
                throw new LythonRuntimeException("TypeError", "object.__init__(self) does not accept additional arguments.", span);
            }

            return PyNone.Instance;
        }
    }

    private sealed class ObjectInitSubclassMethod : IPyBindableCallable
    {
        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length == 0 || arguments[0].IsKeyword || arguments[0].Value is not PyType)
            {
                throw new LythonRuntimeException("TypeError", "object.__init_subclass__(cls) expects a class receiver.", span);
            }

            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "object.__init_subclass__ does not accept keyword arguments in Lython.", span);
            }

            return PyNone.Instance;
        }
    }

    private sealed class ObjectSetAttrMethod : IPyBindableCallable
    {
        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 3 ||
                arguments[0].IsKeyword ||
                arguments[1].IsKeyword ||
                arguments[2].IsKeyword ||
                arguments[0].Value is not PyInstance instance ||
                !PyStringOps.TryAsString(arguments[1].Value, out var name))
            {
                throw new LythonRuntimeException("TypeError", "object.__setattr__(self, name, value) expects an instance, a string name, and a value.", span);
            }

            if (instance.Type.TryLookupInMro(name.AsString(), 0, out var rawValue, out _) &&
                PyAttributeLookup.TrySetDescriptorValue(rawValue, instance, arguments[2].Value, context, span))
            {
                return PyNone.Instance;
            }

            instance.SetAttribute(name.AsString(), arguments[2].Value);
            return PyNone.Instance;
        }
    }

    private sealed class ObjectDelAttrMethod : IPyBindableCallable
    {
        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 2 ||
                arguments[0].IsKeyword ||
                arguments[1].IsKeyword ||
                arguments[0].Value is not PyInstance instance ||
                !PyStringOps.TryAsString(arguments[1].Value, out var name))
            {
                throw new LythonRuntimeException("TypeError", "object.__delattr__(self, name) expects an instance and a string name.", span);
            }

            var memberName = name.AsString();
            if (instance.Type.TryLookupInMro(memberName, 0, out var rawValue, out _) &&
                PyAttributeLookup.TryDeleteDescriptorValue(rawValue, instance, context, span))
            {
                return PyNone.Instance;
            }

            if (!instance.RemoveAttribute(memberName))
            {
                throw new LythonRuntimeException("AttributeError", $"Object has no attribute '{memberName}'.", span);
            }

            return PyNone.Instance;
        }
    }

    private sealed class ObjectGetAttrMethod : IPyBindableCallable
    {
        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 2 ||
                arguments[0].IsKeyword ||
                arguments[1].IsKeyword ||
                arguments[0].Value is not PyInstance instance ||
                !PyStringOps.TryAsString(arguments[1].Value, out var name))
            {
                throw new LythonRuntimeException("TypeError", "object.__getattribute__(self, name) expects an instance and a string name.", span);
            }

            var memberName = name.AsString();
            if (PyAttributeLookup.TryResolveInstanceMemberWithoutGetAttrFallback(instance, memberName, context, span, out var value))
            {
                return value;
            }

            throw new LythonRuntimeException("AttributeError", $"Object has no attribute '{memberName}'.", span);
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

    private static byte ToByte(object value, LythonSourceSpan span)
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
