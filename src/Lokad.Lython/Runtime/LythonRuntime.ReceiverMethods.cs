using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // A shared implementation is not a Python callable until paired with a
    // receiver. Immediate calls keep that pair in the existing operand storage;
    // an attribute exposed as a value uses the ordinary bound-method wrapper.
    internal sealed class ReceiverMethod(
        LythonCallableSignature signature,
        Func<object, ReadOnlyMemory<object?>, LythonSourceSpan, ExecutionContext, object> implementation,
        Func<object, ReadOnlyMemory<object?>, LythonSourceSpan, ExecutionContext, ValueTask<object>>? asyncImplementation = null)
    {
        private readonly Func<object, LythonSourceSpan, ExecutionContext, object>? _noArgumentsImplementation;

        public ReceiverMethod(LythonCallableSignature signature,
            Func<object, LythonSourceSpan, ExecutionContext, object> implementation)
            : this(signature, (receiver, _, span, context) => implementation(receiver, span, context))
        {
            _noArgumentsImplementation = implementation;
        }

        public LythonCallableSignature Signature { get; } = signature;

        public bool AcceptsPositional(int count)
            => count >= Signature.Parameters.MinimumArgumentCount &&
               Signature.Parameters.MaximumArgumentCount.Accepts(count) &&
               (Signature.Parameters is not NamedCallableParameterLayout named ||
                named.MaximumPositionalArgumentCount.Accepts(count));

        public ICallable Bind(object receiver) => BoundCallable.Create(receiver, this);

        public object Invoke(object receiver, ReadOnlyMemory<object?> arguments, LythonSourceSpan span, ExecutionContext context)
            => implementation(receiver, arguments, span, context);

        public object Invoke(object receiver, LythonSourceSpan span, ExecutionContext context)
            => _noArgumentsImplementation is { } operation ? operation(receiver, span, context)
                : implementation(receiver, ReadOnlyMemory<object?>.Empty, span, context);

        public ValueTask<object> InvokeAsync(object receiver, ReadOnlyMemory<object?> arguments, LythonSourceSpan span, ExecutionContext context)
            => asyncImplementation is null ? new(Invoke(receiver, arguments, span, context))
                : asyncImplementation(receiver, arguments, span, context);

        public bool CanSuspend => asyncImplementation is not null;
    }

    private static object InvokeReceiverMethod(ReceiverMethod method, object receiver,
        LythonSourceSpan span, ExecutionContext context)
    {
        context.EnterInterpreterFrame(span);
        try
        {
            context.CheckExecution(span);
            return method.Invoke(receiver, span, context);
        }
        catch (Exception error) when (TryTranslateCallableException(error, span, out var translated)) { throw translated; }
        finally { context.LeaveInterpreterFrame(); }
    }

    private static object InvokeReceiverMethod(ReceiverMethod method, object receiver, ReadOnlyMemory<object?> arguments,
        LythonSourceSpan span, ExecutionContext context)
    {
        context.EnterInterpreterFrame(span);
        try
        {
            context.CheckExecution(span);
            return method.Invoke(receiver, arguments, span, context);
        }
        catch (Exception error) when (TryTranslateCallableException(error, span, out var translated)) { throw translated; }
        finally { context.LeaveInterpreterFrame(); }
    }

    private static ValueTask<object> InvokeReceiverMethodAsync(ReceiverMethod method, object receiver,
        ReadOnlyMemory<object?> arguments, LythonSourceSpan span, ExecutionContext context)
        => method.CanSuspend ? AwaitReceiverMethodAsync(method, receiver, arguments, span, context)
            : new(InvokeReceiverMethod(method, receiver, arguments, span, context));

    private static async ValueTask<object> AwaitReceiverMethodAsync(ReceiverMethod method, object receiver,
        ReadOnlyMemory<object?> arguments, LythonSourceSpan span, ExecutionContext context)
    {
        context.EnterInterpreterFrame(span);
        try
        {
            context.CheckExecution(span);
            return await method.InvokeAsync(receiver, arguments, span, context).ConfigureAwait(false);
        }
        catch (Exception error) when (TryTranslateCallableException(error, span, out var translated)) { throw translated; }
        finally { context.LeaveInterpreterFrame(); }
    }

    private static class ReceiverMethods
    {
        internal static readonly ReceiverMethod ListAppend = new(
            LythonCallableSignature.Create("list.append", ["value"], 1, 1, LythonVariadicParameters.None, 1),
            static (receiver, arguments, span, context) => ListMembers.Append((PyList)receiver, arguments.Span[0]!, span, context));

        internal static readonly ReceiverMethod DictionaryGet = new(
            LythonCallableSignature.Create("dict.get", ["key", "default"], 1, 2, LythonVariadicParameters.None, 2),
            static (receiver, arguments, span, context) => DictMembers.Get((PyDict)receiver, arguments.Span[0]!,
                arguments.Length == 2 ? arguments.Span[1]! : PyNone.Instance, span, context),
            static (receiver, arguments, span, context) => ((PyDict)receiver).CanReadKeySynchronously(arguments.Span[0]!)
                ? new(DictMembers.Get((PyDict)receiver, arguments.Span[0]!, arguments.Length == 2 ? arguments.Span[1]! : PyNone.Instance, span, context))
                : DictMembers.GetAsync((PyDict)receiver, arguments.Span[0]!, arguments.Length == 2 ? arguments.Span[1]! : PyNone.Instance, span, context));

        internal static readonly ReceiverMethod DictionarySetDefault = new(
            LythonCallableSignature.Create("dict.setdefault", ["key", "default"], 1, 2, LythonVariadicParameters.None, 2),
            static (receiver, arguments, span, context) => DictMembers.SetDefault((PyDict)receiver, arguments.Span[0]!,
                arguments.Length == 2 ? arguments.Span[1]! : PyNone.Instance, span, context));

        internal static readonly ReceiverMethod TextWrite = new(
            LythonCallableSignature.Create("file.write", ["text"]),
            static (receiver, arguments, span, _) => TextFileHandleMembers.Write((ExecutionContext.TextFileHandle)receiver, arguments.Span[0]!, span));

        internal static readonly ReceiverMethod BinaryWrite = new(
            LythonCallableSignature.Create("file.write", ["value"], 1, 1, LythonVariadicParameters.None, 1),
            static (receiver, arguments, span, _) => ((ExecutionContext.BinaryFileHandle)receiver).Write(arguments.Span[0]!, span));

        internal static readonly ReceiverMethod CsvWriterow = new(
            LythonCallableSignature.Create("csv.writerow", ["row"]),
            static (receiver, arguments, span, context) => RuntimeValue(CsvWriterMembers.WriteRowFromValue((CsvWriterObject)receiver, arguments.Span[0]!, span, context)),
            static async (receiver, arguments, span, context) => RuntimeValue(await CsvWriterMembers.WriteRowFromValueAsync((CsvWriterObject)receiver, arguments.Span[0]!, span, context).ConfigureAwait(false)));

        internal static readonly ReceiverMethod StringJoin = new(
            LythonCallableSignature.Create("str.join", ["iterable"]),
            static (receiver, arguments, span, context) => StringMembers.Join((PyString)receiver, arguments.Span[0]!, span, context),
            static (receiver, arguments, span, context) => StringMembers.JoinAsync((PyString)receiver, arguments.Span[0]!, span, context));

        internal static readonly ReceiverMethod StringSplit = new(
            LythonCallableSignature.Create("str.split", ["sep", "maxsplit"], 0),
            static (receiver, arguments, span, context) => StringMembers.Split((PyString)receiver,
                arguments.Length >= 1 ? arguments.Span[0] : null, arguments.Length == 2 ? arguments.Span[1] : null, span, context),
            static (receiver, arguments, span, context) => arguments.Length == 2 && arguments.Span[1] is PyInstance
                ? StringMembers.SplitAsync((PyString)receiver, arguments.Span[0], arguments.Span[1]!, span, context)
                : new(StringMembers.Split((PyString)receiver, arguments.Length >= 1 ? arguments.Span[0] : null,
                    arguments.Length == 2 ? arguments.Span[1] : null, span, context)));

        internal static readonly ReceiverMethod StringReplace = new(
            LythonCallableSignature.Create("str.replace", ["old", "new", "count"], 2),
            static (receiver, arguments, span, context) => StringMembers.Replace((PyString)receiver,
                arguments.Span[0]!, arguments.Span[1]!, arguments.Length == 3 ? arguments.Span[2] : null, span, context));

        internal static readonly ReceiverMethod StringStartsWith = Affix("str.startswith", true);
        internal static readonly ReceiverMethod StringEndsWith = Affix("str.endswith", false);

        private static ReceiverMethod Affix(string name, bool isStart)
            => new(LythonCallableSignature.Create(name, 1),
                (receiver, arguments, span, context) => StringMembers.InvokeStringAffix((PyString)receiver, arguments, span, context, isStart, false).GetAwaiter().GetResult(),
                (receiver, arguments, span, context) => StringMembers.InvokeStringAffix((PyString)receiver, arguments, span, context, isStart,
                    (arguments.Length >= 2 && arguments.Span[1] is PyInstance) || (arguments.Length == 3 && arguments.Span[2] is PyInstance)));
    }
}
