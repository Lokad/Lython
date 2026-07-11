using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;
using System.Text.RegularExpressions;
using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal interface INamedRuntimeCallable
{
    string Name { get; }
}

internal sealed partial class LythonRuntime
{
    private static readonly PyString DefaultPrintSeparator = PyString.FromOwnedUtf8([(byte)' ']);
    private static readonly PyString DefaultPrintEnding = PyString.FromOwnedUtf8([(byte)'\n']);

    private static object InvokeCall(CallExpressionSyntax call, ExecutionContext context)
    {
        var target = EvaluateExpression(call.Target, context);
        return InvokeCallableTarget(
            target,
            call.Target.Span,
            call.Span,
            context,
            () => ExpandCallArguments(call.Arguments, context));
    }

    private static CallArgumentValue[] ExpandCallArguments(
        IReadOnlyList<CallArgumentSyntax> arguments,
        ExecutionContext context)
        => CallExpansion.ExpandRawArguments(arguments, context, EvaluateExpression);

    private static object InvokeCallableTarget(
        object target,
        LythonSourceSpan targetSpan,
        LythonSourceSpan callSpan,
        ExecutionContext context,
        Func<CallArgumentValue[]> expandArguments)
    {
        if (target is not ICallable callable)
        {
            throw RuntimeErrors.NotCallable(targetSpan);
        }

        context.EnterInterpreterFrame(callSpan);
        try
        {
            context.CheckExecutionBudget(callSpan);
            var arguments = expandArguments();
            return RuntimeValue(callable.Invoke(arguments, callSpan, context));
        }
        catch (RegexParseException ex)
        {
            throw new LythonRuntimeException("ValueError", ex.Message, callSpan);
        }
        catch (LythonRuntimeException)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            throw RuntimeErrors.Runtime(ex.Message, callSpan);
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }
    }

    private static async ValueTask<object> InvokeCallableTargetAsync(
        object target,
        LythonSourceSpan targetSpan,
        LythonSourceSpan callSpan,
        ExecutionContext context,
        Func<ValueTask<CallArgumentValue[]>> expandArguments)
    {
        if (target is not ICallable callable)
        {
            throw RuntimeErrors.NotCallable(targetSpan);
        }

        context.EnterInterpreterFrame(callSpan);
        try
        {
            context.CheckExecutionBudget(callSpan);
            var arguments = await expandArguments().ConfigureAwait(false);
            return RuntimeValue(await callable.InvokeAsync(arguments, callSpan, context).ConfigureAwait(false));
        }
        catch (RegexParseException ex)
        {
            throw new LythonRuntimeException("ValueError", ex.Message, callSpan);
        }
        catch (LythonRuntimeException)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            throw RuntimeErrors.Runtime(ex.Message, callSpan);
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }
    }

    private static readonly IReadOnlyDictionary<string, string[]> ExceptionBaseNames =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Exception"] = ["BaseException"],
            ["SystemExit"] = ["BaseException"],
            ["ArithmeticError"] = ["Exception"],
            ["AssertionError"] = ["Exception"],
            ["AttributeError"] = ["Exception"],
            ["ImportError"] = ["Exception"],
            ["LookupError"] = ["Exception"],
            ["MemoryError"] = ["Exception"],
            ["NameError"] = ["Exception"],
            ["OSError"] = ["Exception", "IOError", "EnvironmentError"],
            ["RuntimeError"] = ["Exception"],
            ["StopIteration"] = ["Exception"],
            ["SyntaxError"] = ["Exception"],
            ["TypeError"] = ["Exception"],
            ["ValueError"] = ["Exception"],
            ["Warning"] = ["Exception"],
            ["ZeroDivisionError"] = ["ArithmeticError"],
            ["OverflowError"] = ["ArithmeticError"],
            ["KeyError"] = ["LookupError"],
            ["IndexError"] = ["LookupError"],
            ["ModuleNotFoundError"] = ["ImportError"],
            ["FileNotFoundError"] = ["OSError"],
            ["FileExistsError"] = ["OSError"],
            ["IsADirectoryError"] = ["OSError"],
            ["NotADirectoryError"] = ["OSError"],
            ["PermissionError"] = ["OSError"],
            ["TimeoutError"] = ["OSError"],
            ["IOError"] = ["OSError", "EnvironmentError"],
            ["EnvironmentError"] = ["OSError", "IOError"],
            ["NotImplementedError"] = ["RuntimeError"],
            ["RecursionError"] = ["RuntimeError"],
            ["UnicodeError"] = ["ValueError"],
            ["UnicodeEncodeError"] = ["UnicodeError"],
            ["UnicodeDecodeError"] = ["UnicodeError"],
            ["UnicodeTranslateError"] = ["UnicodeError"],
            ["JSONDecodeError"] = ["ValueError"],
            ["SubprocessError"] = ["Exception"],
            ["CalledProcessError"] = ["SubprocessError"],
        };

    private static bool MatchesExceptionTypeName(string caughtTypeName, string thrownTypeName)
        => string.Equals(caughtTypeName, thrownTypeName, StringComparison.Ordinal) ||
           IsExceptionSubtype(thrownTypeName, caughtTypeName) ||
           (IsRegexPatternErrorName(caughtTypeName) && IsRegexPatternErrorName(thrownTypeName));

    private static bool IsExceptionSubtype(string thrownTypeName, string caughtTypeName)
    {
        var pending = new Stack<string>();
        pending.Push(thrownTypeName);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!seen.Add(current))
            {
                continue;
            }

            if (!ExceptionBaseNames.TryGetValue(current, out var bases))
            {
                continue;
            }

            foreach (var baseName in bases)
            {
                if (string.Equals(baseName, caughtTypeName, StringComparison.Ordinal))
                {
                    return true;
                }

                pending.Push(baseName);
            }
        }

        return false;
    }

    private static bool IsRegexPatternErrorName(string typeName)
        => string.Equals(typeName, "error", StringComparison.Ordinal) ||
           string.Equals(typeName, "PatternError", StringComparison.Ordinal);

    private static object InvokeCallableTarget(
        object target,
        LythonSourceSpan targetSpan,
        LythonSourceSpan callSpan,
        ExecutionContext context,
        CallArgumentValue[] arguments)
        => InvokeCallableTarget(target, targetSpan, callSpan, context, () => arguments);

    internal interface ICallable
    {
        object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context);

        ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => ValueTask.FromResult(Invoke(arguments, span, context));
    }

    private sealed class BuiltinCallable : ICallable, INamedRuntimeCallable, IPyRenderableValue, IPyHashableValue, IPyDynamicAttributes
    {
        private readonly Func<object[], LythonSourceSpan, ExecutionContext, object> _implementation;
        private readonly Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>>? _asyncImplementation;
        private readonly LythonCallableSignature _signature;
        private readonly Dictionary<string, int>? _parameterIndices;

        public BuiltinCallable(
            LythonCallableSignature signature,
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>>? asyncImplementation = null)
        {
            _signature = signature;
            _implementation = implementation;
            _asyncImplementation = asyncImplementation;
            _parameterIndices = signature.ParameterNames is null ? null : CreateParameterIndices(signature.ParameterNames);
        }

        public BuiltinCallable(
            string name,
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            string[]? parameterNames = null,
            int? requiredCount = null)
            : this(new LythonCallableSignature(name, parameterNames, requiredCount), implementation)
        {
        }

        public BuiltinCallable(
            string name,
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation,
            string[]? parameterNames = null,
            int? requiredCount = null)
            : this(new LythonCallableSignature(name, parameterNames, requiredCount), implementation, asyncImplementation)
        {
        }

        public string Name => _signature.Name;

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString(Name);
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public int GetPyHashCode() => RuntimeHelpers.GetHashCode(this);

        public bool TryGetMember(string name, out object value)
        {
            if (IsBuiltinTypeName(Name) && name is "__name__" or "__qualname__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (IsBuiltinTypeName(Name) && name == "__module__")
            {
                value = PyString.FromString("builtins");
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public bool TrySetMember(string name, object value)
        {
            _ = name;
            _ = value;
            return false;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var positional = CallBinder.BindNamedArguments(arguments, span, _signature, "Builtin", _parameterIndices);
            return _implementation(positional, span, context);
        }

        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var positional = CallBinder.BindNamedArguments(arguments, span, _signature, "Builtin", _parameterIndices);
            return _asyncImplementation is null
                ? _implementation(positional, span, context)
                : await _asyncImplementation(positional, span, context).ConfigureAwait(false);
        }
    }

    private sealed class MinMaxCallable(bool isMin) : ICallable, INamedRuntimeCallable, IPyRenderableValue, IPyHashableValue
    {
        public string Name => isMin ? "min" : "max";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return MinMax(arguments, isMin, span, context);
        }

        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return MinMaxAsync(arguments, isMin, span, context);
        }

        public PyString RenderPython(PyRenderingContext context) => PyString.FromString(Name);

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public int GetPyHashCode() => RuntimeHelpers.GetHashCode(this);

    }

    private sealed class ZipCallable : ICallable, INamedRuntimeCallable, IPyRenderableValue, IPyHashableValue
    {
        public string Name => "zip";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return Zip(arguments, span);
        }

        public PyString RenderPython(PyRenderingContext context) => PyString.FromString(Name);
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
        public int GetPyHashCode() => RuntimeHelpers.GetHashCode(this);
    }

    private sealed class DictCallable : ICallable, INamedRuntimeCallable, IPyRenderableValue, IPyHashableValue
    {
        public string Name => "dict";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return Dict(arguments, span, context);
        }

        public PyString RenderPython(PyRenderingContext context) => PyString.FromString(Name);
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
        public int GetPyHashCode() => RuntimeHelpers.GetHashCode(this);
    }

    private sealed class OpenCallable : ICallable, INamedRuntimeCallable, IPyRenderableValue, IPyHashableValue
    {
        private const string Signature = "open(file/path[, mode][, buffering][, encoding][, errors][, newline][, closefd][, opener])";
        private static readonly string[] ParameterNames = ["file/path", "mode", "buffering", "encoding", "errors", "newline", "closefd", "opener"];

        public string Name => "open";

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString(Name);
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public int GetPyHashCode() => RuntimeHelpers.GetHashCode(this);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return Open(BindArguments(arguments, span), span, context);
        }

        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return await OpenAsync(BindArguments(arguments, span), span, context).ConfigureAwait(false);
        }

        private static BoundOpenArguments BindArguments(CallArgumentValue[] arguments, LythonSourceSpan span)
        {
            var bound = new object[ParameterNames.Length];
            Array.Fill(bound, PyNone.Instance);
            var assigned = new bool[ParameterNames.Length];
            var positionalIndex = 0;

            foreach (var argument in arguments)
            {
                if (argument.Name is null)
                {
                    if (positionalIndex >= bound.Length)
                    {
                        throw new LythonRuntimeException("TypeError", $"{Signature} received too many positional arguments.", span);
                    }

                    bound[positionalIndex] = argument.Value;
                    assigned[positionalIndex] = true;
                    positionalIndex++;
                    continue;
                }

                var index = argument.Name switch
                {
                    "file" or "path" => 0,
                    "mode" => 1,
                    "buffering" => 2,
                    "encoding" => 3,
                    "errors" => 4,
                    "newline" => 5,
                    "closefd" => 6,
                    "opener" => 7,
                    _ => -1
                };

                if (index < 0)
                {
                    throw new LythonRuntimeException("TypeError", $"{Signature} got an unexpected keyword argument '{argument.Name}'.", span);
                }

                if (assigned[index])
                {
                    throw new LythonRuntimeException("TypeError", $"{Signature} got multiple values for argument '{ParameterNames[index]}'.", span);
                }

                bound[index] = argument.Value;
                assigned[index] = true;
            }

            if (!assigned[0])
            {
                throw new LythonRuntimeException("TypeError", $"{Signature} expects a file/path argument.", span);
            }

            var count = bound.Length;
            while (count > 1 && !assigned[count - 1])
            {
                count--;
            }

            return new BoundOpenArguments(bound, assigned, count);
        }
    }

    private sealed class PrintCallable : ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);

            var positional = new List<object>(arguments.Length);
            PyString separator = DefaultPrintSeparator;
            PyString ending = DefaultPrintEnding;
            object? outputTarget = null;
            var flush = false;
            var seenSeparator = false;
            var seenEnding = false;
            var seenFile = false;
            var seenFlush = false;

            foreach (var argument in arguments)
            {
                if (argument.Name is null)
                {
                    positional.Add(argument.Value);
                    continue;
                }

                switch (argument.Name)
                {
                    case "sep":
                        if (seenSeparator)
                        {
                            throw CallErrors.MultipleValues("Builtin", "print", "sep", span);
                        }

                        separator = ReferenceEquals(argument.Value, PyNone.Instance)
                            ? DefaultPrintSeparator
                            : PyStringOps.TryAsString(argument.Value, out var separatorValue)
                                ? separatorValue
                                : throw new LythonRuntimeException("TypeError", "print(..., sep=...) expects a string or None.", span);
                        seenSeparator = true;
                        break;

                    case "end":
                        if (seenEnding)
                        {
                            throw CallErrors.MultipleValues("Builtin", "print", "end", span);
                        }

                        ending = ReferenceEquals(argument.Value, PyNone.Instance)
                            ? DefaultPrintEnding
                            : PyStringOps.TryAsString(argument.Value, out var endingValue)
                                ? endingValue
                                : throw new LythonRuntimeException("TypeError", "print(..., end=...) expects a string or None.", span);
                        seenEnding = true;
                        break;

                    case "file":
                        if (seenFile)
                        {
                            throw CallErrors.MultipleValues("Builtin", "print", "file", span);
                        }

                        outputTarget = argument.Value switch
                        {
                            PyNone => null,
                            ExecutionContext.TextFileHandle handle => handle,
                            HostTextOutputHandle handle => handle,
                            _ => throw new LythonRuntimeException(
                                "TypeError",
                                "print(..., file=...) expects a writable Lython text stream, writable file handle, or None.",
                                span)
                        };
                        seenFile = true;
                        break;

                    case "flush":
                        if (seenFlush)
                        {
                            throw CallErrors.MultipleValues("Builtin", "print", "flush", span);
                        }

                        flush = IsTruthy(argument.Value);
                        seenFlush = true;
                        break;

                    default:
                        throw CallErrors.UnexpectedKeyword("Builtin", "print", argument.Name, span);
                }
            }

            for (var i = 0; i < positional.Count; i++)
            {
                if (i > 0)
                {
                    AppendOutput(separator, context, outputTarget, span);
                }

                AppendOutput(ToInterpolatedPyString(positional[i], context), context, outputTarget, span);
            }

            AppendOutput(ending, context, outputTarget, span);
            if (flush)
            {
                FlushOutput(context, outputTarget, span);
            }

            return PyNone.Instance;
        }

        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);

            var positional = new List<object>(arguments.Length);
            PyString separator = DefaultPrintSeparator;
            PyString ending = DefaultPrintEnding;
            object? outputTarget = null;
            var flush = false;
            var seenSeparator = false;
            var seenEnding = false;
            var seenFile = false;
            var seenFlush = false;

            foreach (var argument in arguments)
            {
                if (argument.Name is null)
                {
                    positional.Add(argument.Value);
                    continue;
                }

                switch (argument.Name)
                {
                    case "sep":
                        if (seenSeparator)
                        {
                            throw CallErrors.MultipleValues("Builtin", "print", "sep", span);
                        }

                        separator = ReferenceEquals(argument.Value, PyNone.Instance)
                            ? DefaultPrintSeparator
                            : PyStringOps.TryAsString(argument.Value, out var separatorValue)
                                ? separatorValue
                                : throw new LythonRuntimeException("TypeError", "print(..., sep=...) expects a string or None.", span);
                        seenSeparator = true;
                        break;

                    case "end":
                        if (seenEnding)
                        {
                            throw CallErrors.MultipleValues("Builtin", "print", "end", span);
                        }

                        ending = ReferenceEquals(argument.Value, PyNone.Instance)
                            ? DefaultPrintEnding
                            : PyStringOps.TryAsString(argument.Value, out var endingValue)
                                ? endingValue
                                : throw new LythonRuntimeException("TypeError", "print(..., end=...) expects a string or None.", span);
                        seenEnding = true;
                        break;

                    case "file":
                        if (seenFile)
                        {
                            throw CallErrors.MultipleValues("Builtin", "print", "file", span);
                        }

                        outputTarget = argument.Value switch
                        {
                            PyNone => null,
                            ExecutionContext.TextFileHandle handle => handle,
                            HostTextOutputHandle handle => handle,
                            _ => throw new LythonRuntimeException(
                                "TypeError",
                                "print(..., file=...) expects a writable Lython text stream, writable file handle, or None.",
                                span)
                        };
                        seenFile = true;
                        break;

                    case "flush":
                        if (seenFlush)
                        {
                            throw CallErrors.MultipleValues("Builtin", "print", "flush", span);
                        }

                        flush = IsTruthy(argument.Value);
                        seenFlush = true;
                        break;

                    default:
                        throw CallErrors.UnexpectedKeyword("Builtin", "print", argument.Name, span);
                }
            }

            for (var i = 0; i < positional.Count; i++)
            {
                if (i > 0)
                {
                    await AppendOutputAsync(separator, context, outputTarget, span).ConfigureAwait(false);
                }

                await AppendOutputAsync(ToInterpolatedPyString(positional[i], context), context, outputTarget, span).ConfigureAwait(false);
            }

            await AppendOutputAsync(ending, context, outputTarget, span).ConfigureAwait(false);
            if (flush)
            {
                await FlushOutputAsync(context, outputTarget, span).ConfigureAwait(false);
            }

            return PyNone.Instance;
        }

        private static void AppendOutput(PyString value, ExecutionContext context, object? outputTarget, LythonSourceSpan span)
        {
            if (outputTarget is null)
            {
                _ = context.State.Stdout.Write(value, span);
            }

            else if (outputTarget is ExecutionContext.TextFileHandle fileHandle)
            {
                _ = fileHandle.Write(value);
            }

            else if (outputTarget is HostTextOutputHandle outputHandle)
            {
                _ = outputHandle.Write(value, span);
            }
        }

        private static void FlushOutput(ExecutionContext context, object? outputTarget, LythonSourceSpan span)
        {
            if (outputTarget is null)
            {
                _ = context.State.Stdout.Flush(span);
            }
            else if (outputTarget is ExecutionContext.TextFileHandle fileHandle)
            {
                _ = fileHandle.Flush();
            }
            else if (outputTarget is HostTextOutputHandle outputHandle)
            {
                _ = outputHandle.Flush(span);
            }
        }

        private static async ValueTask AppendOutputAsync(PyString value, ExecutionContext context, object? outputTarget, LythonSourceSpan span)
        {
            if (outputTarget is null)
            {
                _ = await context.State.Stdout.WriteAsync(value, span).ConfigureAwait(false);
            }

            else if (outputTarget is ExecutionContext.TextFileHandle fileHandle)
            {
                _ = fileHandle.Write(value);
            }

            else if (outputTarget is HostTextOutputHandle outputHandle)
            {
                _ = await outputHandle.WriteAsync(value, span).ConfigureAwait(false);
            }
        }

        private static async ValueTask FlushOutputAsync(ExecutionContext context, object? outputTarget, LythonSourceSpan span)
        {
            if (outputTarget is null)
            {
                _ = await context.State.Stdout.FlushAsync(span).ConfigureAwait(false);
            }
            else if (outputTarget is ExecutionContext.TextFileHandle fileHandle)
            {
                _ = await fileHandle.FlushAsync().ConfigureAwait(false);
            }
            else if (outputTarget is HostTextOutputHandle outputHandle)
            {
                _ = await outputHandle.FlushAsync(span).ConfigureAwait(false);
            }
        }
    }

    internal static Dictionary<string, object> BindFunctionArguments(
        CallArgumentValue[] arguments,
        LythonSourceSpan span,
        string callableName,
        string callableKind,
        IReadOnlyList<LoweredFunctionParameter> parameters,
        IReadOnlyDictionary<string, object> defaultValues,
        ExecutionContext context)
    {
        var positionalParameters = new List<LoweredFunctionParameter>(parameters.Count);
        var keywordOnlyParameters = new List<LoweredFunctionParameter>(parameters.Count);
        var namedParameters = new Dictionary<string, LoweredFunctionParameter>(StringComparer.Ordinal);
        LoweredFunctionParameter? variadicList = null;
        LoweredFunctionParameter? variadicDictionary = null;
        foreach (var parameter in parameters)
        {
            switch (parameter.Kind)
            {
                case FunctionParameterKind.Positional:
                    positionalParameters.Add(parameter);
                    namedParameters[parameter.Name] = parameter;
                    break;
                case FunctionParameterKind.KeywordOnly:
                    keywordOnlyParameters.Add(parameter);
                    namedParameters[parameter.Name] = parameter;
                    break;
                case FunctionParameterKind.VariadicList:
                    variadicList = parameter;
                    break;
                case FunctionParameterKind.VariadicDictionary:
                    variadicDictionary = parameter;
                    break;
            }
        }

        var bound = new Dictionary<string, object>(StringComparer.Ordinal);
        var extraPositional = new PyList([], context.MemoryGovernor, span);
        var extraKeywords = new Dictionary<string, object>(StringComparer.Ordinal);
        var positionalIndex = 0;

        foreach (var argument in arguments)
        {
            if (argument.Name is null)
            {
                if (positionalIndex >= positionalParameters.Count)
                {
                    if (variadicList is null)
                    {
                        throw CallErrors.TooManyPositional(callableKind, callableName, span);
                    }

                    extraPositional.Add(argument.Value);
                    continue;
                }

                var parameter = positionalParameters[positionalIndex++];
                bound[parameter.Name] = argument.Value;
                continue;
            }

            if (!namedParameters.TryGetValue(argument.Name, out var named))
            {
                if (variadicDictionary is null)
                {
                    throw CallErrors.UnexpectedKeyword(callableKind, callableName, argument.Name, span);
                }

                if (!extraKeywords.TryAdd(argument.Name, argument.Value))
                {
                    throw CallErrors.MultipleValues(callableKind, callableName, argument.Name, span);
                }

                continue;
            }

            if (!bound.TryAdd(named.Name, argument.Value))
            {
                throw CallErrors.MultipleValues(callableKind, callableName, argument.Name, span);
            }
        }

        foreach (var parameter in positionalParameters)
        {
            if (bound.ContainsKey(parameter.Name))
            {
                continue;
            }

            if (defaultValues.TryGetValue(parameter.Name, out var defaultValue))
            {
                bound[parameter.Name] = defaultValue;
                continue;
            }

            throw CallErrors.MissingArgument(callableKind, callableName, parameter.Name, span);
        }

        foreach (var parameter in keywordOnlyParameters)
        {
            if (bound.ContainsKey(parameter.Name))
            {
                continue;
            }

            if (defaultValues.TryGetValue(parameter.Name, out var defaultValue))
            {
                bound[parameter.Name] = defaultValue;
                continue;
            }

            throw CallErrors.MissingArgument(callableKind, callableName, parameter.Name, span);
        }

        if (variadicList is not null)
        {
            bound[variadicList.Name] = CreateTuple(extraPositional.Count, i => extraPositional[i], context, span);
        }

        if (variadicDictionary is not null)
        {
            var keywordDict = new PyDict(context.MemoryGovernor, span);
            foreach (var pair in extraKeywords)
            {
                keywordDict.SetItem(PyString.FromString(pair.Key), pair.Value);
            }

            bound[variadicDictionary.Name] = keywordDict;
        }

        return bound;
    }

    private static Dictionary<string, int> CreateParameterIndices(string[] parameterNames)
    {
        var indices = new Dictionary<string, int>(parameterNames.Length, StringComparer.Ordinal);
        for (var i = 0; i < parameterNames.Length; i++)
        {
            indices[parameterNames[i]] = i;
        }

        return indices;
    }

    internal static Dictionary<string, object> BuildDefaultArgumentMap(
        IReadOnlyList<LoweredFunctionParameter> parameters,
        Func<LoweredExpression, object> evaluate)
    {
        var defaults = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var parameter in parameters)
        {
            if (parameter.DefaultValue is not null)
            {
                defaults[parameter.Name] = RuntimeValue(evaluate(parameter.DefaultValue));
            }
        }

        return defaults;
    }

    internal static async ValueTask<Dictionary<string, object>> BuildDefaultArgumentMapAsync(
        IReadOnlyList<LoweredFunctionParameter> parameters,
        Func<LoweredExpression, ValueTask<object>> evaluate)
    {
        var defaults = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var parameter in parameters)
        {
            if (parameter.DefaultValue is not null)
            {
                defaults[parameter.Name] = RuntimeValue(await evaluate(parameter.DefaultValue).ConfigureAwait(false));
            }
        }

        return defaults;
    }

    private sealed class LambdaFunction : ICallable
    {
        private readonly IReadOnlyList<LoweredFunctionParameter> _parameters;
        private readonly LoweredExpression _body;
        private readonly ExecutionContext _closure;
        private readonly Dictionary<string, object> _defaultValues;

        public LambdaFunction(IReadOnlyList<LoweredFunctionParameter> parameters, LoweredExpression body, ExecutionContext closure, Dictionary<string, object> defaultValues)
        {
            _parameters = parameters;
            _body = body;
            _closure = closure;
            _defaultValues = defaultValues;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var boundArguments = BindFunctionArguments(arguments, span, "<lambda>", "lambda", _parameters, _defaultValues, context);

            var frame = new ExecutionContext(_closure);
            foreach (var pair in boundArguments)
            {
                frame.Variables[pair.Key] = pair.Value;
            }

            frame.EnterFunctionCall(span);
            try
            {
                return EvaluateLoweredExpression(_body, frame);
            }
            catch (LythonRuntimeException ex)
            {
                ex.SetSourcePathIfMissing(frame.SourcePath);
                ex.AddFrame("<lambda>", span, frame.SourcePath);
                throw;
            }
            finally
            {
                frame.LeaveFunctionCall();
            }
        }

        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var boundArguments = BindFunctionArguments(arguments, span, "<lambda>", "lambda", _parameters, _defaultValues, context);

            var frame = new ExecutionContext(_closure);
            foreach (var pair in boundArguments)
            {
                frame.Variables[pair.Key] = pair.Value;
            }

            frame.EnterFunctionCall(span);
            try
            {
                return await EvaluateLoweredExpressionAsync(_body, frame).ConfigureAwait(false);
            }
            catch (LythonRuntimeException ex)
            {
                ex.SetSourcePathIfMissing(frame.SourcePath);
                ex.AddFrame("<lambda>", span, frame.SourcePath);
                throw;
            }
            finally
            {
                frame.LeaveFunctionCall();
            }
        }
    }

    private sealed class ExceptionTypeValue : ICallable, IPyDynamicAttributes, IPyRenderableValue, IEquatable<ExceptionTypeValue>
    {
        public ExceptionTypeValue(string typeName)
        {
            TypeName = typeName;
        }

        public string TypeName { get; }

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "__name__" => PyString.FromString(TypeName),
                "type" => PyString.FromString(TypeName),
                _ => null!
            };

            return value is not null;
        }

        public bool TrySetMember(string name, object value)
        {
            _ = name;
            _ = value;
            return false;
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<class '" + TypeName + "'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public bool Equals(ExceptionTypeValue? other)
            => other is not null && string.Equals(TypeName, other.TypeName, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is ExceptionTypeValue other && Equals(other);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(TypeName);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            for (var i = 0; i < arguments.Length; i++)
            {
                if (arguments[i].Name is not null)
                {
                    throw new LythonRuntimeException("TypeError", $"{TypeName}(message) does not accept keyword arguments.", span);
                }
            }

            if (TypeName == "SystemExit")
            {
                if (arguments.Length > 1)
                {
                    throw new LythonRuntimeException("TypeError", "SystemExit([code]) expects zero or one argument.", span);
                }

                var value = arguments.Length == 0 ? PyNone.Instance : arguments[0].Value;
                return new PyException(TypeName, FormatSystemExitMessage(value), value);
            }

            var values = arguments.Select(argument => argument.Value).ToArray();
            var args = new PyTuple(values, context.MemoryGovernor, span);
            var message = values.Length switch
            {
                0 => string.Empty,
                1 => PyRendering.ToInterpolatedString(values[0], new PyRenderingContext(context)),
                _ => PyRendering.ToReprPyString(args, new PyRenderingContext(context)).AsString(),
            };
            var payload = values.Length == 0 ? PyNone.Instance : values.Length == 1 ? values[0] : args;
            return new PyException(TypeName, message, payload, args);
        }
    }

    private static string FormatSystemExitMessage(object value)
        => value switch
        {
            PyNone => string.Empty,
            BigInteger integer => integer.ToString(),
            bool boolean => boolean ? "True" : "False",
            _ when PyStringOps.TryAsString(value, out var text) => text.AsString(),
            _ => value.ToString() ?? string.Empty
        };
}
