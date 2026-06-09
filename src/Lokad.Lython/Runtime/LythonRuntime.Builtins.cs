using System.Globalization;
using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static object ReadText(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var path))
        {
            throw new LythonRuntimeException("TypeError", "read_text(path) expects one string argument.", span);
        }

        return ReadGovernedHostText(path.AsString(), context, span);
    }

    private static async ValueTask<object> ReadTextAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var path))
        {
            throw new LythonRuntimeException("TypeError", "read_text(path) expects one string argument.", span);
        }

        return await ReadGovernedHostTextAsync(path.AsString(), context, span).ConfigureAwait(false);
    }

    private static object WriteText(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2 || !PyStringOps.TryAsString(arguments[0], out var path) || !PyStringOps.TryAsString(arguments[1], out var text))
        {
            throw new LythonRuntimeException("TypeError", "write_text(path, text) expects two string arguments.", span);
        }

        text = PyStringOps.NormalizeNewlines(text);
        context.ObserveString(text, span);
        context.RegisterHostCall(span);
        context.WriteTextUtf8(path.AsString(), PyStringOps.EncodeUtf8(text), span);
        return PyNone.Instance;
    }

    private static async ValueTask<object> WriteTextAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2 || !PyStringOps.TryAsString(arguments[0], out var path) || !PyStringOps.TryAsString(arguments[1], out var text))
        {
            throw new LythonRuntimeException("TypeError", "write_text(path, text) expects two string arguments.", span);
        }

        text = PyStringOps.NormalizeNewlines(text);
        context.ObserveString(text, span);
        context.RegisterHostCall(span);
        await context.WriteTextUtf8Async(path.AsString(), PyStringOps.EncodeUtf8(text), span).ConfigureAwait(false);
        return PyNone.Instance;
    }

    private static object AppendText(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2 || !PyStringOps.TryAsString(arguments[0], out var path) || !PyStringOps.TryAsString(arguments[1], out var text))
        {
            throw new LythonRuntimeException("TypeError", "append_text(path, text) expects two string arguments.", span);
        }

        text = PyStringOps.NormalizeNewlines(text);
        context.ObserveString(text, span);
        context.RegisterHostCall(span);
        context.AppendTextUtf8(path.AsString(), PyStringOps.EncodeUtf8(text), span);
        return PyNone.Instance;
    }

    private static async ValueTask<object> AppendTextAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2 || !PyStringOps.TryAsString(arguments[0], out var path) || !PyStringOps.TryAsString(arguments[1], out var text))
        {
            throw new LythonRuntimeException("TypeError", "append_text(path, text) expects two string arguments.", span);
        }

        text = PyStringOps.NormalizeNewlines(text);
        context.ObserveString(text, span);
        context.RegisterHostCall(span);
        await context.AppendTextUtf8Async(path.AsString(), PyStringOps.EncodeUtf8(text), span).ConfigureAwait(false);
        return PyNone.Instance;
    }

    internal enum TextEncodingMode
    {
        Utf8,
        Utf8Bom,
    }

    private static object Open(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var (path, mode, encodingMode) = ParseOpenArguments(arguments, span);
        var modeText = mode.AsString();
        return modeText switch
        {
            "r" => ExecutionContext.TextFileHandle.ForRead(path.AsString(), context, encodingMode),
            "w" => ExecutionContext.TextFileHandle.ForWrite(path.AsString(), context, encodingMode),
            "a" => ExecutionContext.TextFileHandle.ForAppend(path.AsString(), context, encodingMode),
            _ => throw new LythonRuntimeException("ValueError", "open() only supports modes 'r', 'w', and 'a'.", span)
        };
    }

    private static async ValueTask<object> OpenAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var (path, mode, encodingMode) = ParseOpenArguments(arguments, span);
        var modeText = mode.AsString();
        return modeText switch
        {
            "r" => await ExecutionContext.TextFileHandle.ForReadAsync(path.AsString(), context, encodingMode).ConfigureAwait(false),
            "w" => ExecutionContext.TextFileHandle.ForWrite(path.AsString(), context, encodingMode),
            "a" => ExecutionContext.TextFileHandle.ForAppend(path.AsString(), context, encodingMode),
            _ => throw new LythonRuntimeException("ValueError", "open() only supports modes 'r', 'w', and 'a'.", span)
        };
    }

    private static (PyString Path, PyString Mode, TextEncodingMode EncodingMode) ParseOpenArguments(object[] arguments, LythonSourceSpan span)
    {
        if (arguments.Length is < 1 or > 5 || !PyStringOps.TryAsString(arguments[0], out var path))
        {
            throw new LythonRuntimeException("TypeError", "open(path[, mode][, encoding][, newline][, errors]) expects a string path plus supported text-mode options.", span);
        }

        var mode = arguments.Length >= 2
            ? arguments[1] switch
            {
                null => PyString.FromString("r"),
                PyNone => PyString.FromString("r"),
                PyString text => text,
                _ => throw new LythonRuntimeException("TypeError", "open(path, mode) expects mode to be a string.", span)
            }
            : PyString.FromString("r");

        var encodingMode = TextEncodingMode.Utf8;
        if (arguments.Length >= 3)
        {
            encodingMode = ParseTextEncoding(arguments[2], "open()", span);
        }

        if (arguments.Length >= 4)
        {
            if (arguments[3] is not null &&
                arguments[3] is not PyNone &&
                (!PyStringOps.TryAsString(arguments[3], out var newline) || newline.Length != 0))
            {
                throw new LythonRuntimeException("ValueError", "open() only supports newline=''.", span);
            }
        }

        if (arguments.Length == 5)
        {
            if (arguments[4] is not null &&
                arguments[4] is not PyNone &&
                (!PyStringOps.TryAsString(arguments[4], out var errors) ||
                 !errors.AsString().Equals("strict", StringComparison.OrdinalIgnoreCase)))
            {
                throw new LythonRuntimeException("ValueError", "open() only supports errors='strict'.", span);
            }
        }

        var modeText = mode.AsString();
        if (modeText.Contains('b'))
        {
            throw new LythonRuntimeException("ValueError", "open() only supports UTF-8 text modes; binary modes like 'rb' and 'wb' are unsupported.", span);
        }

        return (path, mode, encodingMode);
    }

    private static object Input(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length > 1)
        {
            throw new LythonRuntimeException("TypeError", "input([prompt]) expects zero or one string argument.", span);
        }

        if (arguments.Length == 1)
        {
            if (!PyStringOps.TryAsString(arguments[0], out var prompt))
            {
                throw new LythonRuntimeException("TypeError", "input(prompt) expects prompt to be a string.", span);
            }

            _ = context.State.Stdout.Write(prompt, span);
        }

        var line = context.State.Stdin.ReadLine(span);
        return PyStringOps.TrimTrailingNewline(line);
    }

    private static async ValueTask<object> InputAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length > 1)
        {
            throw new LythonRuntimeException("TypeError", "input([prompt]) expects zero or one string argument.", span);
        }

        if (arguments.Length == 1)
        {
            if (!PyStringOps.TryAsString(arguments[0], out var prompt))
            {
                throw new LythonRuntimeException("TypeError", "input(prompt) expects prompt to be a string.", span);
            }

            _ = await context.State.Stdout.WriteAsync(prompt, span).ConfigureAwait(false);
        }

        var line = await context.State.Stdin.ReadLineAsync(span).ConfigureAwait(false);
        return PyStringOps.TrimTrailingNewline(line);
    }

    private static TextEncodingMode ParseTextEncoding(object value, string owner, LythonSourceSpan span)
    {
        if (value is null or PyNone)
        {
            return TextEncodingMode.Utf8;
        }

        if (!PyStringOps.TryAsString(value, out var encoding))
        {
            throw new LythonRuntimeException("ValueError", $"{owner} only supports encoding='utf-8' or 'utf-8-sig'.", span);
        }

        return encoding.AsString().ToLowerInvariant() switch
        {
            "utf-8" => TextEncodingMode.Utf8,
            "utf-8-sig" => TextEncodingMode.Utf8Bom,
            _ => throw new LythonRuntimeException("ValueError", $"{owner} only supports encoding='utf-8' or 'utf-8-sig'.", span)
        };
    }

    private static object Str(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "str(value) expects one argument.", span);
        }

        return ToPythonPyString(arguments[0], context);
    }

    private static object Repr(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "repr(value) expects one argument.", span);
        }

        return ToReprPyString(arguments[0], context);
    }

    private static object Bool(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "bool(value) expects one argument.", span);
        }

        return IsTruthy(arguments[0]);
    }

    private static object Int(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "int(value) expects one argument.", span);
        }

        try
        {
            return arguments[0] switch
            {
                BigInteger integer => integer,
                double floating => new BigInteger(floating),
                PyDecimal decimalValue => new BigInteger(decimal.Truncate(decimalValue.Value)),
                PyString text => BigInteger.Parse(text.AsString(), CultureInfo.InvariantCulture),
                string text => BigInteger.Parse(text, CultureInfo.InvariantCulture),
                bool boolean => boolean ? BigInteger.One : BigInteger.Zero,
                _ => throw new LythonRuntimeException("TypeError", "int() does not support this value.", span)
            };
        }
        catch (FormatException ex)
        {
            throw new LythonRuntimeException("ValueError", ex.Message, span);
        }
    }

    private static object Float(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "float(value) expects one argument.", span);
        }

        try
        {
            return arguments[0] switch
            {
                double floating => floating,
                BigInteger integer => (double)integer,
                PyDecimal decimalValue => (double)decimalValue.Value,
                PyString text => double.Parse(text.AsString(), CultureInfo.InvariantCulture),
                string text => double.Parse(text, CultureInfo.InvariantCulture),
                bool boolean => boolean ? 1.0 : 0.0,
                _ => throw new LythonRuntimeException("TypeError", "float() does not support this value.", span)
            };
        }
        catch (FormatException ex)
        {
            throw new LythonRuntimeException("ValueError", ex.Message, span);
        }
    }

    private static object Bytes(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 0)
        {
            return new PyBytes(Array.Empty<byte>());
        }

        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "bytes([iterable]) expects zero or one argument.", span);
        }

        return arguments[0] switch
        {
            PyBytes bytes => CreateBytes(bytes.ToArray(), context, span),
            PyString text => CreateBytes(PyStringOps.EncodeUtf8(text), context, span),
            string text => CreateBytes(PyStringOps.EncodeUtf8(PyString.FromString(text)), context, span),
            _ => CreateBytes(ToByteArray(arguments[0], span), context, span)
        };
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

    private static object ObjectNew(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length == 0 || arguments[0] is not PyType type)
        {
            throw new LythonRuntimeException("TypeError", "object.__new__(cls, ...) expects the first argument to be a class.", span);
        }

        return new PyInstance(type);
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
            if (arguments.Length != 1 || arguments[0].Name is not null || arguments[0].Value is not PyInstance)
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
            if (arguments.Length == 0 || arguments[0].Name is not null || arguments[0].Value is not PyType)
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
                arguments[0].Name is not null ||
                arguments[1].Name is not null ||
                arguments[2].Name is not null ||
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
                arguments[0].Name is not null ||
                arguments[1].Name is not null ||
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
                arguments[0].Name is not null ||
                arguments[1].Name is not null ||
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

    private static object Super(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 0)
        {
            if (context.ImplicitSuperAnchorType is null || context.ImplicitSuperReceiver is null)
            {
                throw new LythonRuntimeException("TypeError", "zero-argument super() is only supported inside instance methods, classmethods, and property accessors in Lython.", span);
            }

            return context.ImplicitSuperReceiver switch
            {
                PyInstance instance => new PySuper(context.ImplicitSuperAnchorType, instance, instance.Type),
                PyType type => new PySuper(context.ImplicitSuperAnchorType, type, type),
                _ => throw new LythonRuntimeException("TypeError", "zero-argument super() could not resolve the current receiver.", span)
            };
        }

        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "super(type, object) expects exactly two arguments in Lython.", span);
        }

        if (arguments[0] is not PyType anchorType)
        {
            throw new LythonRuntimeException("TypeError", "super(type, object) expects the first argument to be a class.", span);
        }

        return arguments[1] switch
        {
            PyInstance instance when instance.Type.IsSubtypeOf(anchorType) => new PySuper(anchorType, instance, instance.Type),
            PyType type when type.IsSubtypeOf(anchorType) => new PySuper(anchorType, type, type),
            PyInstance => throw new LythonRuntimeException("TypeError", "super(type, object) expects the instance to be an instance of the given class or its subclass.", span),
            PyType => throw new LythonRuntimeException("TypeError", "super(type, object) expects the class argument to be a subclass of the given class.", span),
            _ => throw new LythonRuntimeException("TypeError", "super(type, object) expects the second argument to be an instance or class.", span)
        };
    }

    private static object List(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 0)
        {
            return new PyList([], context.MemoryGovernor, span);
        }

        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "list(iterable) expects one argument.", span);
        }

        var result = new PyList(ToSequence(arguments[0], span), context.MemoryGovernor, span);
        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static object Tuple(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length == 0)
        {
            return PyTuple.Empty;
        }

        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "tuple(iterable) expects one argument.", span);
        }

        var result = new PyTuple(ToSequence(arguments[0], span), context.MemoryGovernor, span);
        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static object Dict(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length == 0)
        {
            return new PyDict(context.MemoryGovernor, span);
        }

        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "dict(iterable_of_pairs) expects one argument.", span);
        }

        if (arguments[0] is PyDict sourceDict)
        {
            var copied = new PyDict(sourceDict, context.MemoryGovernor, span);
            context.ObserveCollectionCount(copied.Count, span);
            return copied;
        }

        var result = new PyDict(context.MemoryGovernor, span);
        foreach (var pair in ToSequence(arguments[0], span))
        {
            using var enumerator = ToSequence(pair, span).GetEnumerator();
            if (!enumerator.MoveNext())
            {
                throw new LythonRuntimeException("TypeError", "dict(iterable_of_pairs) expects key-value pairs.", span);
            }

            var key = enumerator.Current;
            if (!enumerator.MoveNext())
            {
                throw new LythonRuntimeException("TypeError", "dict(iterable_of_pairs) expects key-value pairs.", span);
            }

            var value = enumerator.Current;
            if (enumerator.MoveNext())
            {
                throw new LythonRuntimeException("TypeError", "dict(iterable_of_pairs) expects key-value pairs.", span);
            }

            result.SetItem(ValidateDictionaryKey(key, span, context.MemoryGovernor), value);
        }

        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static object Set(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 0)
        {
            return new PySet(context.MemoryGovernor, span);
        }

        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "set(iterable) expects one argument.", span);
        }

        var result = new PySet(context.MemoryGovernor, span);
        foreach (var item in ToSequence(arguments[0], span))
        {
            result.Add(ValidateSetItem(item, span, context.MemoryGovernor));
            context.ObserveCollectionCount(result.Count, span);
        }

        return result;
    }

    private static object IsInstance(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "isinstance(value, type) expects two arguments.", span);
        }

        return IsInstanceOf(arguments[0], arguments[1], span);
    }

    private static object IsSubclass(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "issubclass(type, base) expects two arguments.", span);
        }

        if (arguments[0] is not PyType type)
        {
            throw new LythonRuntimeException("TypeError", "issubclass(type, base) expects the first argument to be a class.", span);
        }

        return IsSubclassOf(type, arguments[1], span);
    }

    private static bool IsInstanceOf(object value, object typeSpec, LythonSourceSpan span)
    {
        if (TryMatchTypeTuple(typeSpec, candidate => IsInstanceAgainstSingleType(value, candidate), out var matched))
        {
            return matched;
        }

        throw new LythonRuntimeException("TypeError", "isinstance(value, type) expects a class or tuple of classes.", span);
    }

    private static bool IsSubclassOf(PyType type, object baseSpec, LythonSourceSpan span)
    {
        if (TryMatchTypeTuple(baseSpec, candidate => IsSubclassAgainstSingleType(type, candidate), out var matched))
        {
            return matched;
        }

        throw new LythonRuntimeException("TypeError", "issubclass(type, base) expects a class or tuple of classes.", span);
    }

    private static bool TryMatchTypeTuple(object typeSpec, Func<object, bool> predicate, out bool matched)
    {
        if (typeSpec is PyTuple tuple)
        {
            foreach (var candidate in tuple)
            {
                if (!IsSupportedTypeSpecifier(candidate))
                {
                    matched = false;
                    return false;
                }

                if (predicate(candidate))
                {
                    matched = true;
                    return true;
                }
            }

            matched = false;
            return true;
        }

        matched = predicate(typeSpec);
        return matched || IsSupportedTypeSpecifier(typeSpec);
    }

    private static bool IsSupportedTypeSpecifier(object typeSpec)
    {
        return typeSpec switch
        {
            PyType => true,
            PyNamedTupleType => true,
            BuiltinCallable builtin when IsBuiltinTypeName(builtin.Name) => true,
            PyBuiltinRuntimeType builtinType when IsBuiltinTypeName(builtinType.Name) => true,
            INamedRuntimeCallable namedCallable when IsBuiltinTypeName(namedCallable.Name) => true,
            _ => false
        };
    }

    private static bool IsInstanceAgainstSingleType(object value, object typeSpec)
    {
        return typeSpec switch
        {
            PyType runtimeType => value switch
            {
                PyInstance instance => instance.Type.IsSubtypeOf(runtimeType),
                PyType typeValue => typeValue.MetaType is not null && typeValue.MetaType.IsSubtypeOf(runtimeType),
                _ => false
            },
            PyNamedTupleType namedTupleType => value is PyNamedTupleObject namedTuple && ReferenceEquals(namedTuple.Type, namedTupleType),
            BuiltinCallable builtin => DoesObjectMatchBuiltinType(builtin.Name, value),
            PyBuiltinRuntimeType builtinType => DoesObjectMatchBuiltinType(builtinType.Name, value),
            INamedRuntimeCallable namedCallable => DoesObjectMatchBuiltinType(namedCallable.Name, value),
            _ => false
        };
    }

    private static bool IsSubclassAgainstSingleType(PyType type, object baseSpec)
    {
        return baseSpec switch
        {
            PyType runtimeType => type.IsSubtypeOf(runtimeType),
            _ => false
        };
    }

    private static bool IsBuiltinTypeName(string name)
    {
        return name is
            "bool" or
            "int" or
            "float" or
            "list" or
            "tuple" or
            "dict" or
            "set" or
            "str" or
            "bytes" or
            "pathlib.Path" or
            "pathlib.PurePath" or
            "pathlib.PurePosixPath" or
            "pathlib.PosixPath" or
            "datetime.timedelta" or
            "datetime.date" or
            "datetime.time" or
            "datetime.datetime" or
            "datetime.timezone";
    }

    private static bool DoesObjectMatchBuiltinType(string typeName, object value)
    {
        return typeName switch
        {
            "bool" => value is bool,
            "int" => value is BigInteger or int or bool,
            "float" => value is double,
            "list" => value is PyList,
            "tuple" => value is PyTuple or PyNamedTupleObject or PyTypingNamedTupleObject,
            "dict" => value is PyDict,
            "set" => value is PySet,
            "str" => value is PyString or string,
            "bytes" => value is PyBytes,
            "pathlib.Path" or "pathlib.PurePath" or "pathlib.PurePosixPath" or "pathlib.PosixPath" => value is PyPath,
            "datetime.timedelta" => value is PyTimedelta,
            "datetime.date" => value is PyDate,
            "datetime.time" => value is PyTime,
            "datetime.datetime" => value is PyDateTime,
            "datetime.timezone" => value is PyTimezone,
            _ => false
        };
    }

    private static object Len(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "len(value) expects one argument.", span);
        }

        return arguments[0] switch
        {
            PyString text => new BigInteger(text.Length),
            PyBytes bytes => new BigInteger(bytes.Length),
            string text => new BigInteger(PyString.FromString(text).Length),
            ReFindAllResult matches => new BigInteger(matches.Items.Count),
            PySet set => new BigInteger(set.Count),
            IReadOnlyCollection<object> collection => new BigInteger(collection.Count),
            PyDict dict => new BigInteger(dict.Count),
            System.Collections.ICollection collection => new BigInteger(collection.Count),
            _ => throw new LythonRuntimeException("TypeError", "Object has no len().", span)
        };
    }

    private static object Sorted(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 1 or > 3)
        {
            throw new LythonRuntimeException("TypeError", "sorted(iterable[, key][, reverse]) expects one iterable and optional key/reverse arguments.", span);
        }

        var values = new List<object>();
        foreach (var item in ToSequence(arguments[0], span))
        {
            values.Add(item);
        }
        var keyCallable = arguments.Length >= 2 ? arguments[1] : null;
        if (keyCallable is not null &&
            !ReferenceEquals(keyCallable, PyNone.Instance) &&
            keyCallable is not ICallable)
        {
            throw new LythonRuntimeException("TypeError", "sorted(..., key=...) expects a callable or None.", span);
        }

        var reverse = false;
        if (arguments.Length >= 3)
        {
            if (arguments[2] is not bool reverseFlag)
            {
                throw new LythonRuntimeException("TypeError", "sorted(..., reverse=...) expects a bool.", span);
            }

            reverse = reverseFlag;
        }

        var keyed = new List<SortKeyValue>(values.Count);
        foreach (var item in values)
        {
            keyed.Add(new SortKeyValue(
                item,
                keyCallable is ICallable callable
                    ? callable.Invoke([new CallArgumentValue(null, item)], span, context)
                    : item));
        }

        for (var i = 1; i < keyed.Count; i++)
        {
            var current = keyed[i];
            var j = i - 1;
            while (j >= 0 && CompareSortKeys(keyed[j].Key, current.Key, span, context) > 0)
            {
                keyed[j + 1] = keyed[j];
                j--;
            }

            keyed[j + 1] = current;
        }
        if (reverse)
        {
            keyed.Reverse();
        }

        var items = new object[keyed.Count];
        for (var i = 0; i < keyed.Count; i++)
        {
            items[i] = keyed[i].Value;
        }

        var result = new PyList(items, context.MemoryGovernor, span);
        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static async ValueTask<object> SortedAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 1 or > 3)
        {
            throw new LythonRuntimeException("TypeError", "sorted(iterable[, key][, reverse]) expects one iterable and optional key/reverse arguments.", span);
        }

        var values = await MaterializeSequenceAsync(arguments[0], span).ConfigureAwait(false);
        var keyCallable = arguments.Length >= 2 ? arguments[1] : null;
        if (keyCallable is not null &&
            !ReferenceEquals(keyCallable, PyNone.Instance) &&
            keyCallable is not ICallable)
        {
            throw new LythonRuntimeException("TypeError", "sorted(..., key=...) expects a callable or None.", span);
        }

        var reverse = false;
        if (arguments.Length >= 3)
        {
            if (arguments[2] is not bool reverseFlag)
            {
                throw new LythonRuntimeException("TypeError", "sorted(..., reverse=...) expects a bool.", span);
            }

            reverse = reverseFlag;
        }

        var keyed = new List<SortKeyValue>(values.Count);
        foreach (var item in values)
        {
            keyed.Add(new SortKeyValue(
                item,
                keyCallable is ICallable callable
                    ? await callable.InvokeAsync([new CallArgumentValue(null, item)], span, context).ConfigureAwait(false)
                    : item));
        }

        for (var i = 1; i < keyed.Count; i++)
        {
            var current = keyed[i];
            var j = i - 1;
            while (j >= 0 && await CompareSortKeysAsync(keyed[j].Key, current.Key, span, context).ConfigureAwait(false) > 0)
            {
                keyed[j + 1] = keyed[j];
                j--;
            }

            keyed[j + 1] = current;
        }
        if (reverse)
        {
            keyed.Reverse();
        }

        var items = new object[keyed.Count];
        for (var i = 0; i < keyed.Count; i++)
        {
            items[i] = keyed[i].Value;
        }

        var result = new PyList(items, context.MemoryGovernor, span);
        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static object Any(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "any(iterable) expects one argument.", span);
        }

        foreach (var item in ToSequence(arguments[0], span))
        {
            if (IsTruthy(item))
            {
                return true;
            }
        }

        return false;
    }

    private static object All(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "all(iterable) expects one argument.", span);
        }

        foreach (var item in ToSequence(arguments[0], span))
        {
            if (!IsTruthy(item))
            {
                return false;
            }
        }

        return true;
    }

    private static object Min(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "min(iterable) expects one argument.", span);
        }

        using var enumerator = ToSequence(arguments[0], span).GetEnumerator();
        if (!enumerator.MoveNext())
        {
            throw new LythonRuntimeException("ValueError", "min() arg is an empty sequence", span);
        }

        var best = enumerator.Current;
        while (enumerator.MoveNext())
        {
            var candidate = enumerator.Current;
            if (Compare(candidate, best, span) < 0)
            {
                best = candidate;
            }
        }

        return best;
    }

    private static object Max(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "max(iterable) expects one argument.", span);
        }

        using var enumerator = ToSequence(arguments[0], span).GetEnumerator();
        if (!enumerator.MoveNext())
        {
            throw new LythonRuntimeException("ValueError", "max() arg is an empty sequence", span);
        }

        var best = enumerator.Current;
        while (enumerator.MoveNext())
        {
            var candidate = enumerator.Current;
            if (Compare(candidate, best, span) > 0)
            {
                best = candidate;
            }
        }

        return best;
    }

    private static object Sum(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 1 or > 2)
        {
            throw new LythonRuntimeException("TypeError", "sum(iterable[, start]) expects one iterable and optional start argument.", span);
        }

        var total = arguments.Length == 2 ? arguments[1] : BigInteger.Zero;
        EnsureSummableValue(total, span);
        foreach (var item in ToSequence(arguments[0], span))
        {
            EnsureSummableValue(item, span);
            total = EvaluateAdd(total, item, context, span);
        }

        return total;
    }

    private static void EnsureSummableValue(object value, LythonSourceSpan span)
    {
        if (PyStringOps.TryAsString(value, out _) || value is PyBytes)
        {
            throw new LythonRuntimeException("TypeError", "sum() does not support string or bytes operands.", span);
        }
    }

    private static int CompareSortKeys(object left, object right, LythonSourceSpan span, ExecutionContext context)
    {
        if (left is PyCmpKey leftKey &&
            right is PyCmpKey rightKey &&
            ReferenceEquals(leftKey.Comparer, rightKey.Comparer))
        {
            var result = leftKey.Comparer.Invoke(
                [new CallArgumentValue(null, leftKey.Value), new CallArgumentValue(null, rightKey.Value)],
                span,
                context);
            if (!Numbers.PyNumberOps.TryAsInteger(result, out var integer))
            {
                throw new LythonRuntimeException("TypeError", "cmp_to_key comparator must return an integer.", span);
            }

            return integer.Sign;
        }

        return Compare(left, right, span);
    }

    private static async ValueTask<int> CompareSortKeysAsync(object left, object right, LythonSourceSpan span, ExecutionContext context)
    {
        if (left is PyCmpKey leftKey &&
            right is PyCmpKey rightKey &&
            ReferenceEquals(leftKey.Comparer, rightKey.Comparer))
        {
            var result = await leftKey.Comparer.InvokeAsync(
                    [new CallArgumentValue(null, leftKey.Value), new CallArgumentValue(null, rightKey.Value)],
                    span,
                    context)
                .ConfigureAwait(false);
            if (!Numbers.PyNumberOps.TryAsInteger(result, out var integer))
            {
                throw new LythonRuntimeException("TypeError", "cmp_to_key comparator must return an integer.", span);
            }

            return integer.Sign;
        }

        return Compare(left, right, span);
    }

    private static async ValueTask<List<object>> MaterializeSequenceAsync(object value, LythonSourceSpan span)
    {
        if (value is PyGeneratorExpression generator)
        {
            return await generator.IterateAsync().ConfigureAwait(false);
        }

        return [.. ToSequence(value, span)];
    }

    private static object Range(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        BigInteger start;
        BigInteger stop;
        BigInteger step;

        if (arguments.Length == 1 && arguments[0] is BigInteger stopOnly)
        {
            start = BigInteger.Zero;
            stop = stopOnly;
            step = BigInteger.One;
        }
        else if (arguments.Length == 2 && arguments[0] is BigInteger startArg && arguments[1] is BigInteger stopArg)
        {
            start = startArg;
            stop = stopArg;
            step = BigInteger.One;
        }
        else if (arguments.Length == 3 && arguments[0] is BigInteger startValue && arguments[1] is BigInteger stopValue && arguments[2] is BigInteger stepValue)
        {
            start = startValue;
            stop = stopValue;
            step = stepValue;
        }
        else
        {
            throw new LythonRuntimeException("TypeError", "range(stop), range(start, stop), or range(start, stop, step) expects integer arguments.", span);
        }

        if (step == BigInteger.Zero)
        {
            throw new LythonRuntimeException("ValueError", "range() arg 3 must not be zero", span);
        }

        var result = new PyList([], context.MemoryGovernor, span);
        if (step > BigInteger.Zero)
        {
            for (var current = start; current < stop; current += step)
            {
                result.Add(current);
                context.ObserveCollectionCount(result.Count, span);
            }
        }
        else
        {
            for (var current = start; current > stop; current += step)
            {
                result.Add(current);
                context.ObserveCollectionCount(result.Count, span);
            }
        }

        return result;
    }

    private static object Enumerate(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length is not 1 and not 2)
        {
            throw new LythonRuntimeException("TypeError", "enumerate(iterable[, start]) expects one or two arguments.", span);
        }

        var result = new PyList([], context.MemoryGovernor, span);
        var index = arguments.Length == 2 && arguments[1] is BigInteger start
            ? start
            : arguments.Length == 1
                ? BigInteger.Zero
                : throw new LythonRuntimeException("TypeError", "enumerate(iterable, start) expects an integer start.", span);

        foreach (var item in ToSequence(arguments[0], span))
        {
            result.Add(CreateTuple(2, i => i == 0 ? index : item, context, span));
            context.ObserveCollectionCount(result.Count, span);
            index += BigInteger.One;
        }

        return result;
    }

    private static object Zip(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var result = new PyList([], context.MemoryGovernor, span);
        if (arguments.Length == 0)
        {
            return result;
        }

        var enumerators = new System.Collections.IEnumerator[arguments.Length];
        for (var i = 0; i < arguments.Length; i++)
        {
            enumerators[i] = ToSequence(arguments[i], span).GetEnumerator();
        }

        try
        {
            while (true)
            {
                var ready = true;
                foreach (var enumerator in enumerators)
                {
                    if (enumerator.MoveNext())
                    {
                        continue;
                    }

                    ready = false;
                    break;
                }

                if (!ready)
                {
                    return result;
                }

                result.Add(CreateTuple(enumerators.Length, i => RuntimeValue(enumerators[i].Current), context, span));
                context.ObserveCollectionCount(result.Count, span);
            }
        }
        finally
        {
            foreach (var enumerator in enumerators)
            {
                (enumerator as IDisposable)?.Dispose();
            }
        }
    }

    private static object Next(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length is not 1 and not 2)
        {
            throw new LythonRuntimeException("TypeError", "next(iterator[, default]) expects one or two arguments.", span);
        }

        using var enumerator = ToSequence(arguments[0], span).GetEnumerator();
        if (enumerator.MoveNext())
        {
            return RuntimeValue(enumerator.Current);
        }

        if (arguments.Length == 2)
        {
            return arguments[1];
        }

        throw new LythonRuntimeException("StopIteration", "iterator is exhausted", span);
    }

    private static object Exists(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var path))
        {
            throw new LythonRuntimeException("TypeError", "exists(path) expects one string argument.", span);
        }

        context.RegisterHostCall(span);
        return context.HostExists(path.AsString(), span);
    }

    private static async ValueTask<object> ExistsAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var path))
        {
            throw new LythonRuntimeException("TypeError", "exists(path) expects one string argument.", span);
        }

        context.RegisterHostCall(span);
        return await context.HostExistsAsync(path.AsString(), span).ConfigureAwait(false);
    }

    private static object ListDir(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var path))
        {
            throw new LythonRuntimeException("TypeError", "listdir(path) expects one string argument.", span);
        }

        context.RegisterHostCall(span);
        var result = new PyList(
            context.HostListDir(path.AsString(), span).Select<string, object>(item => PyString.FromString(item)),
            context.MemoryGovernor,
            span);
        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static async ValueTask<object> ListDirAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var path))
        {
            throw new LythonRuntimeException("TypeError", "listdir(path) expects one string argument.", span);
        }

        context.RegisterHostCall(span);
        var names = await context.HostListDirAsync(path.AsString(), span).ConfigureAwait(false);
        var result = new PyList(
            names.Select<string, object>(item => PyString.FromString(item)),
            context.MemoryGovernor,
            span);
        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static object MkDir(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var path))
        {
            throw new LythonRuntimeException("TypeError", "mkdir(path) expects one string argument.", span);
        }

        context.RegisterHostCall(span);
        context.HostMkDir(path.AsString(), span);
        return PyNone.Instance;
    }

    private static async ValueTask<object> MkDirAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var path))
        {
            throw new LythonRuntimeException("TypeError", "mkdir(path) expects one string argument.", span);
        }

        context.RegisterHostCall(span);
        await context.HostMkDirAsync(path.AsString(), span).ConfigureAwait(false);
        return PyNone.Instance;
    }

    private static object Remove(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var path))
        {
            throw new LythonRuntimeException("TypeError", "remove(path) expects one string argument.", span);
        }

        context.RegisterHostCall(span);
        context.HostRemove(path.AsString(), span);
        return PyNone.Instance;
    }

    private static async ValueTask<object> RemoveAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var path))
        {
            throw new LythonRuntimeException("TypeError", "remove(path) expects one string argument.", span);
        }

        context.RegisterHostCall(span);
        await context.HostRemoveAsync(path.AsString(), span).ConfigureAwait(false);
        return PyNone.Instance;
    }

    private static object Copy(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2 ||
            !PyStringOps.TryAsString(arguments[0], out var source) ||
            !PyStringOps.TryAsString(arguments[1], out var destination))
        {
            throw new LythonRuntimeException("TypeError", "copy(source, destination) expects two string arguments.", span);
        }

        context.RegisterHostCall(span);
        context.HostCopy(source.AsString(), destination.AsString(), span);
        return PyNone.Instance;
    }

    private static async ValueTask<object> CopyAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2 ||
            !PyStringOps.TryAsString(arguments[0], out var source) ||
            !PyStringOps.TryAsString(arguments[1], out var destination))
        {
            throw new LythonRuntimeException("TypeError", "copy(source, destination) expects two string arguments.", span);
        }

        context.RegisterHostCall(span);
        await context.HostCopyAsync(source.AsString(), destination.AsString(), span).ConfigureAwait(false);
        return PyNone.Instance;
    }

    private static object Move(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2 ||
            !PyStringOps.TryAsString(arguments[0], out var source) ||
            !PyStringOps.TryAsString(arguments[1], out var destination))
        {
            throw new LythonRuntimeException("TypeError", "move(source, destination) expects two string arguments.", span);
        }

        context.RegisterHostCall(span);
        context.HostMove(source.AsString(), destination.AsString(), span);
        return PyNone.Instance;
    }

    private static async ValueTask<object> MoveAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2 ||
            !PyStringOps.TryAsString(arguments[0], out var source) ||
            !PyStringOps.TryAsString(arguments[1], out var destination))
        {
            throw new LythonRuntimeException("TypeError", "move(source, destination) expects two string arguments.", span);
        }

        context.RegisterHostCall(span);
        await context.HostMoveAsync(source.AsString(), destination.AsString(), span).ConfigureAwait(false);
        return PyNone.Instance;
    }

    private static object Cwd(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 0)
        {
            throw new LythonRuntimeException("TypeError", "cwd() expects no arguments.", span);
        }

        context.RegisterHostCall(span);
        return PyString.FromString(context.Host.Cwd);
    }

    private static object JoinPath(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length == 0)
        {
            throw new LythonRuntimeException("TypeError", "join_path(...) expects one or more string arguments.", span);
        }

        if (!PyStringOps.TryAsString(arguments[0], out var result))
        {
            throw new LythonRuntimeException("TypeError", "join_path(...) expects one or more string arguments.", span);
        }

        for (var i = 1; i < arguments.Length; i++)
        {
            if (!PyStringOps.TryAsString(arguments[i], out var next))
            {
                throw new LythonRuntimeException("TypeError", "join_path(...) expects one or more string arguments.", span);
            }

            if (next.StartsWith(PyStringOps.SlashLiteral))
            {
                result = next;
                continue;
            }

            if (result.EndsWith(PyStringOps.SlashLiteral))
            {
                result = result.Concat(next);
            }
            else
            {
                result = result.Concat(PyStringOps.SlashLiteral).Concat(next);
            }
        }

        return result;
    }

    private static object DirName(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var path))
        {
            throw new LythonRuntimeException("TypeError", "dirname(path) expects one string argument.", span);
        }

        var text = TrimTrailingSlash(path);
        if (text.Utf8Bytes.Length == 0 || text.Equals(PyStringOps.SlashLiteral))
        {
            return PyStringOps.SlashLiteral;
        }

        var slash = LastIndexOfSlash(text);
        return slash <= 0 ? PyStringOps.DotLiteral : text.SliceByByteRange(0, slash);
    }

    private static object BaseName(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var path))
        {
            throw new LythonRuntimeException("TypeError", "basename(path) expects one string argument.", span);
        }

        var text = TrimTrailingSlash(path);
        if (text.Utf8Bytes.Length == 0 || text.Equals(PyStringOps.SlashLiteral))
        {
            return PyStringOps.SlashLiteral;
        }

        var slash = LastIndexOfSlash(text);
        return slash < 0 ? text : text.SliceByByteRange(slash + 1, text.Utf8Bytes.Length);
    }

    private static object Stat(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var path))
        {
            throw new LythonRuntimeException("TypeError", "stat(path) expects one string argument.", span);
        }

        context.RegisterHostCall(span);
        return context.HostStat(path.AsString(), span);
    }

    private static async ValueTask<object> StatAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var path))
        {
            throw new LythonRuntimeException("TypeError", "stat(path) expects one string argument.", span);
        }

        context.RegisterHostCall(span);
        return await context.HostStatAsync(path.AsString(), span).ConfigureAwait(false);
    }

    private static PyString TrimTrailingSlash(PyString path)
    {
        var bytes = path.Utf8Bytes.Span;
        var end = bytes.Length;
        while (end > 0 && bytes[end - 1] == (byte)'/')
        {
            end--;
        }

        return end == bytes.Length ? path : path.SliceByByteRange(0, end);
    }

    private static int LastIndexOfSlash(PyString path)
    {
        var bytes = path.Utf8Bytes.Span;
        for (var i = bytes.Length - 1; i >= 0; i--)
        {
            if (bytes[i] == (byte)'/')
            {
                return i;
            }
        }

        return -1;
    }

    internal sealed partial class ExecutionContext
    {
        internal sealed class TextFileHandle : IPyAsyncContextManager, IPyIterableValue
        {
            private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

            private TextFileHandle(string path, string mode, PyString text, ExecutionContext context, TextEncodingMode encoding)
            {
                Path = path;
                Mode = mode;
                _text = text;
                _context = context;
                _encoding = encoding;
            }

            private PyString _text;
            private readonly ExecutionContext _context;
            private readonly TextEncodingMode _encoding;

            public string Path { get; }

            public string Mode { get; }

            public bool IsClosed { get; private set; }

            public bool IsReadable()
            {
                EnsureOpen();
                return Mode == "r";
            }

            public bool IsWritable()
            {
                EnsureOpen();
                return Mode is "w" or "a";
            }

            public bool IsSeekable()
            {
                EnsureOpen();
                return false;
            }

            public BigInteger Tell()
            {
                EnsureOpen();
                return Mode == "r" ? BigInteger.Zero : new BigInteger(_text.Length);
            }

            public object Flush()
            {
                EnsureOpen();
                return PyNone.Instance;
            }

            public object Seek(LythonSourceSpan span)
                => throw new LythonRuntimeException("NotImplementedError", "file.seek(...) is not supported by Lython text handles.", span);

            public static TextFileHandle ForRead(string path, ExecutionContext context, TextEncodingMode encoding = TextEncodingMode.Utf8)
            {
                var text = ReadGovernedHostText(path, context, null);
                if (encoding == TextEncodingMode.Utf8Bom)
                {
                    var decoded = text.AsString();
                    if (decoded.Length > 0 && decoded[0] == '\uFEFF')
                    {
                        text = PyString.FromString(decoded[1..]);
                    }
                }

                context.ObserveString(text, null);
                return new TextFileHandle(path, "r", text, context, encoding);
            }

            public static async ValueTask<TextFileHandle> ForReadAsync(string path, ExecutionContext context, TextEncodingMode encoding = TextEncodingMode.Utf8)
            {
                var text = await ReadGovernedHostTextAsync(path, context, null).ConfigureAwait(false);
                if (encoding == TextEncodingMode.Utf8Bom)
                {
                    var decoded = text.AsString();
                    if (decoded.Length > 0 && decoded[0] == '\uFEFF')
                    {
                        text = PyString.FromString(decoded[1..]);
                    }
                }

                context.ObserveString(text, null);
                return new TextFileHandle(path, "r", text, context, encoding);
            }

            public static TextFileHandle ForWrite(string path, ExecutionContext context, TextEncodingMode encoding = TextEncodingMode.Utf8)
                => new(path, "w", PyString.Empty, context, encoding);

            public static TextFileHandle ForAppend(string path, ExecutionContext context, TextEncodingMode encoding = TextEncodingMode.Utf8)
                => new(path, "a", PyString.Empty, context, encoding);

            public object Enter() => this;

            object IPyContextManager.Enter() => Enter();

            ValueTask<object> IPyAsyncContextManager.EnterAsync() => ValueTask.FromResult<object>(Enter());

            bool IPyContextManager.Exit(object exceptionType, object exceptionValue, object traceback)
            {
                _ = Exit();
                return false;
            }

            public object Exit()
            {
                if (IsClosed)
                {
                    return false;
                }

                if (Mode == "w")
                {
                    _context.ObserveString(_text, null);
                    _context.RegisterHostCall(null);
                    var utf8 = PyStringOps.EncodeUtf8(_text);
                    if (_encoding == TextEncodingMode.Utf8Bom)
                    {
                        utf8 = [.. Utf8Bom, .. utf8];
                    }

                    _context.WriteTextUtf8(Path, utf8, null);
                }
                else if (Mode == "a")
                {
                    _context.ObserveString(_text, null);
                    _context.RegisterHostCall(null);
                    _context.AppendTextUtf8(Path, PyStringOps.EncodeUtf8(_text), null);
                }

                IsClosed = true;
                return false;
            }

            public async ValueTask<object> ExitAsync()
            {
                if (IsClosed)
                {
                    return false;
                }

                if (Mode == "w")
                {
                    _context.ObserveString(_text, null);
                    _context.RegisterHostCall(null);
                    var utf8 = PyStringOps.EncodeUtf8(_text);
                    if (_encoding == TextEncodingMode.Utf8Bom)
                    {
                        utf8 = [.. Utf8Bom, .. utf8];
                    }

                    await _context.WriteTextUtf8Async(Path, utf8, null).ConfigureAwait(false);
                }
                else if (Mode == "a")
                {
                    _context.ObserveString(_text, null);
                    _context.RegisterHostCall(null);
                    await _context.AppendTextUtf8Async(Path, PyStringOps.EncodeUtf8(_text), null).ConfigureAwait(false);
                }

                IsClosed = true;
                return false;
            }

            async ValueTask<bool> IPyAsyncContextManager.ExitAsync(object exceptionType, object exceptionValue, object traceback)
            {
                _ = await ExitAsync().ConfigureAwait(false);
                return false;
            }

            public PyString Read()
            {
                EnsureOpen();
                if (Mode != "r")
                {
                    throw new LythonRuntimeException("ValueError", "file is not open for reading", null);
                }

                return _text;
            }

            public PyString ReadLine()
            {
                EnsureOpen();
                if (Mode != "r")
                {
                    throw new LythonRuntimeException("ValueError", "file is not open for reading", null);
                }

                var lines = SplitLinesPreservingNewlines(_text);
                return lines.Count == 0 ? PyString.Empty : lines[0];
            }

            public PyList ReadLines()
            {
                EnsureOpen();
                if (Mode != "r")
                {
                    throw new LythonRuntimeException("ValueError", "file is not open for reading", null);
                }

                var lines = SplitLinesPreservingNewlines(_text);
                var items = new object[lines.Count];
                for (var i = 0; i < lines.Count; i++)
                {
                    items[i] = lines[i];
                }

                return new PyList(items, _context.MemoryGovernor, null);
            }

            public IEnumerable<object> Iterate() => ReadLines();

            public BigInteger Write(PyString text)
            {
                EnsureOpen();
                if (Mode == "r")
                {
                    throw new LythonRuntimeException("ValueError", "file is not open for writing", null);
                }

                text = PyStringOps.NormalizeNewlines(text);
                _text = _text.Concat(text);
                _context.ObserveString(_text, null);
                return new BigInteger(text.Length);
            }

            public object WriteLines(object value, LythonSourceSpan span)
            {
                EnsureOpen();
                if (Mode == "r")
                {
                    throw new LythonRuntimeException("ValueError", "file is not open for writing", null);
                }

                foreach (var item in ToSequence(value, span))
                {
                    if (!PyStringOps.TryAsString(item, out var text))
                    {
                        throw new LythonRuntimeException("TypeError", "file.writelines(lines) expects an iterable of strings.", span);
                    }

                    _ = Write(text);
                }

                return PyNone.Instance;
            }

            private void EnsureOpen()
            {
                if (IsClosed)
                {
                    throw new LythonRuntimeException("ValueError", "I/O operation on closed file", null);
                }
            }

            private static IReadOnlyList<PyString> SplitLinesPreservingNewlines(PyString text)
            {
                var source = text.Utf8Bytes.Span;
                if (source.Length == 0)
                {
                    return Array.Empty<PyString>();
                }

                var lines = new List<PyString>();
                var start = 0;
                for (var i = 0; i < source.Length; i++)
                {
                    if (source[i] == (byte)'\n')
                    {
                        lines.Add(text.SliceByByteRange(start, i + 1));
                        start = i + 1;
                    }
                }

                if (start < source.Length)
                {
                    lines.Add(text.SliceByByteRange(start, source.Length));
                }

                return lines;
            }
        }
    }
}
