using System.Text.RegularExpressions;
using Lokad.Lython.Frontend;
using System.Numerics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        public ExecutionContext(ILythonHost host, LythonRunOptions? options)
        {
            var builtinVariables = CreateBuiltinVariables();
            Services = new ExecutionServices(new ExecutionState(host, options, builtinVariables));
            var sourcePath = options?.SourcePath is null ? null : PathOps.Normalize(options.SourcePath, host.Cwd);
            SourcePath = sourcePath;
            Frame = new ExecutionFrame(parent: null, CreateModuleVariables(builtinVariables, sourcePath, "__main__"));
            ParentContext = null;
            FunctionClosureContext = this;
            ScopeFacts = ScopeDirectiveFacts.Empty;
            NonlocalTargets = new Dictionary<string, ExecutionContext>(StringComparer.Ordinal);

            var globals = options?.Globals;
            if (globals is null)
            {
                return;
            }

            foreach (var pair in globals)
            {
                Frame.Variables[pair.Key] = NormalizeRuntimeValue(pair.Value, this);
            }
        }

        public ExecutionContext(ExecutionContext parent)
        {
            Services = parent.Services;
            SourcePath = parent.SourcePath;
            Frame = new ExecutionFrame(parent.Frame, new Dictionary<string, object>(StringComparer.Ordinal));
            ParentContext = parent;
            FunctionClosureContext = this;
            ScopeFacts = ScopeDirectiveFacts.Empty;
            NonlocalTargets = new Dictionary<string, ExecutionContext>(StringComparer.Ordinal);
        }

        public ExecutionContext(ExecutionContext parent, ScopeDirectiveFacts scopeFacts)
        {
            Services = parent.Services;
            SourcePath = parent.SourcePath;
            Frame = new ExecutionFrame(parent.Frame, new Dictionary<string, object>(StringComparer.Ordinal));
            ParentContext = parent;
            FunctionClosureContext = this;
            ScopeFacts = scopeFacts;
            NonlocalTargets = ResolveNonlocalTargets(parent, scopeFacts);

            static Dictionary<string, ExecutionContext> ResolveNonlocalTargets(ExecutionContext parent, ScopeDirectiveFacts scopeFacts)
            {
                var targets = new Dictionary<string, ExecutionContext>(StringComparer.Ordinal);
                foreach (var name in scopeFacts.NonlocalNames)
                {
                    for (var current = parent; current is not null && current.Parent is not null; current = current.Parent)
                    {
                        if (current.ScopeFacts.LocalNames.Contains(name))
                        {
                            targets[name] = current;
                            break;
                        }
                    }

                    if (!targets.ContainsKey(name))
                    {
                        throw RuntimeErrors.NameNotDefined(name, null);
                    }
                }

                return targets;
            }
        }

        public static ExecutionContext CreateModule(
            ExecutionContext template,
            string? sourcePath,
            string moduleName)
            => new(template, new ModuleScope(sourcePath, moduleName));

        public static ExecutionContext CreateClassBody(ExecutionContext parent)
            => new(parent, ClassBodyScope.Instance);

        private ExecutionContext(ExecutionContext template, ModuleScope scope)
        {
            Services = template.Services;
            SourcePath = scope.SourcePath;
            Frame = new ExecutionFrame(
                parent: null,
                CreateModuleVariables(State.BuiltinVariables, scope.SourcePath, scope.Name));
            ParentContext = null;
            FunctionClosureContext = this;
            ScopeFacts = ScopeDirectiveFacts.Empty;
            NonlocalTargets = new Dictionary<string, ExecutionContext>(StringComparer.Ordinal);
        }

        private ExecutionContext(ExecutionContext parent, ClassBodyScope _)
        {
            Services = parent.Services;
            SourcePath = parent.SourcePath;
            Frame = new ExecutionFrame(parent.Frame, new Dictionary<string, object>(StringComparer.Ordinal));
            ParentContext = parent;
            FunctionClosureContext = parent.FunctionClosureContext;
            ScopeFacts = ScopeDirectiveFacts.Empty;
            NonlocalTargets = new Dictionary<string, ExecutionContext>(StringComparer.Ordinal);
        }

        private readonly record struct ModuleScope(string? SourcePath, string Name);

        private sealed class ClassBodyScope
        {
            public static readonly ClassBodyScope Instance = new();

            private ClassBodyScope() { }
        }

        public ExecutionServices Services { get; }

        public ExecutionFrame Frame { get; }

        public string? SourcePath { get; }

        public ExecutionState State => Services.State;

        public ILythonHost Host => Services.Host;

        public ExecutionContext? Parent => ParentContext;

        public ExecutionContext? ParentContext { get; }

        public ExecutionContext FunctionClosureContext { get; }

        internal ScopeDirectiveFacts ScopeFacts { get; }

        private Dictionary<string, ExecutionContext> NonlocalTargets { get; }

        internal ExecutableFrameState? CurrentExecutableFrame { get; private set; }

        public PyType? ImplicitSuperAnchorType { get; private set; }

        public object? ImplicitSuperReceiver { get; private set; }

        public ExecutionLimits Limits => Services.Limits;

        public MemoryGovernor MemoryGovernor => Services.MemoryGovernor;

        public Dictionary<string, object> Variables => Frame.Variables;

        public PyDecimalContext DecimalContext => State.DecimalContext;

        public void SetDecimalContext(PyDecimalContext context) => State.DecimalContext = context;

        internal bool TryGetNonlocalTarget(string name, [MaybeNullWhen(false)] out ExecutionContext context)
            => NonlocalTargets.TryGetValue(name, out context);

        internal void EnterExecutableSlots(ExecutableFrameState frame) => CurrentExecutableFrame = frame;

        internal void LeaveExecutableSlots(ExecutableFrameState? previous) => CurrentExecutableFrame = previous;

        public void BindImplicitSuper(PyType anchorType, object receiver)
        {
            ImplicitSuperAnchorType = anchorType;
            ImplicitSuperReceiver = receiver;
        }

        public ReadOnlyMemory<byte> ReadTextUtf8(string path, LythonSourceSpan? span)
            => AwaitHost(Host, () => Host.ReadTextUtf8Async(ContainedHostPath(path, span), Limits.CancellationToken), "read_text", span);

        public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, LythonSourceSpan? span)
            => AwaitHostAsync(() => Host.ReadTextUtf8Async(ContainedHostPath(path, span), Limits.CancellationToken), "read_text", span);

        public void WriteTextUtf8(string path, ReadOnlyMemory<byte> utf8, LythonSourceSpan? span)
            => AwaitHost(Host, () => Host.WriteTextUtf8Async(ContainedHostPath(path, span), utf8, Limits.CancellationToken), "write_text", span);

        public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, LythonSourceSpan? span)
            => AwaitHostAsync(() => Host.WriteTextUtf8Async(ContainedHostPath(path, span), utf8, Limits.CancellationToken), "write_text", span);

        public void AppendTextUtf8(string path, ReadOnlyMemory<byte> utf8, LythonSourceSpan? span)
            => AwaitHost(Host, () => Host.AppendTextUtf8Async(ContainedHostPath(path, span), utf8, Limits.CancellationToken), "append_text", span);

        public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, LythonSourceSpan? span)
            => AwaitHostAsync(() => Host.AppendTextUtf8Async(ContainedHostPath(path, span), utf8, Limits.CancellationToken), "append_text", span);

        public ReadOnlyMemory<byte> ReadHostBytes(string path, LythonSourceSpan? span)
            => AwaitHost(Host, () => Host.ReadBytesAsync(ContainedHostPath(path, span), Limits.CancellationToken), "read_bytes", span);

        public ValueTask<ReadOnlyMemory<byte>> ReadHostBytesAsync(string path, LythonSourceSpan? span)
            => AwaitHostAsync(() => Host.ReadBytesAsync(ContainedHostPath(path, span), Limits.CancellationToken), "read_bytes", span);

        public void WriteHostBytes(string path, ReadOnlyMemory<byte> payload, LythonSourceSpan? span)
            => AwaitHost(Host, () => Host.WriteBytesAsync(ContainedHostPath(path, span), payload, Limits.CancellationToken), "write_bytes", span);

        public ValueTask WriteHostBytesAsync(string path, ReadOnlyMemory<byte> payload, LythonSourceSpan? span)
            => AwaitHostAsync(() => Host.WriteBytesAsync(ContainedHostPath(path, span), payload, Limits.CancellationToken), "write_bytes", span);

        public void AppendHostBytes(string path, ReadOnlyMemory<byte> payload, LythonSourceSpan? span)
            => AwaitHost(Host, () => Host.AppendBytesAsync(ContainedHostPath(path, span), payload, Limits.CancellationToken), "append_bytes", span);

        public ValueTask AppendHostBytesAsync(string path, ReadOnlyMemory<byte> payload, LythonSourceSpan? span)
            => AwaitHostAsync(() => Host.AppendBytesAsync(ContainedHostPath(path, span), payload, Limits.CancellationToken), "append_bytes", span);

        public bool HostExists(string path, LythonSourceSpan? span)
            => AwaitHost(Host, () => Host.ExistsAsync(ContainedHostPath(path, span), Limits.CancellationToken), "exists", span);

        public ValueTask<bool> HostExistsAsync(string path, LythonSourceSpan? span)
            => AwaitHostAsync(() => Host.ExistsAsync(ContainedHostPath(path, span), Limits.CancellationToken), "exists", span);

        public IReadOnlyList<string> HostListDir(string path, LythonSourceSpan? span)
            => AwaitHost(Host, () => Host.ListDirAsync(ContainedHostPath(path, span), Limits.CancellationToken), "listdir", span);

        public ValueTask<IReadOnlyList<string>> HostListDirAsync(string path, LythonSourceSpan? span)
            => AwaitHostAsync(() => Host.ListDirAsync(ContainedHostPath(path, span), Limits.CancellationToken), "listdir", span);

        public void HostMkDir(string path, LythonSourceSpan? span)
            => AwaitHost(Host, () => Host.MkDirAsync(ContainedHostPath(path, span), Limits.CancellationToken), "mkdir", span);

        public ValueTask HostMkDirAsync(string path, LythonSourceSpan? span)
            => AwaitHostAsync(() => Host.MkDirAsync(ContainedHostPath(path, span), Limits.CancellationToken), "mkdir", span);

        public void HostRemove(string path, LythonSourceSpan? span)
            => AwaitHost(Host, () => Host.RemoveAsync(ContainedHostPath(path, span), Limits.CancellationToken), "remove", span);

        public ValueTask HostRemoveAsync(string path, LythonSourceSpan? span)
            => AwaitHostAsync(() => Host.RemoveAsync(ContainedHostPath(path, span), Limits.CancellationToken), "remove", span);

        public void HostCopy(string source, string destination, LythonSourceSpan? span)
            => AwaitHost(Host, () => Host.CopyAsync(ContainedHostPath(source, span), ContainedHostPath(destination, span), Limits.CancellationToken), "copy", span);

        public ValueTask HostCopyAsync(string source, string destination, LythonSourceSpan? span)
            => AwaitHostAsync(() => Host.CopyAsync(ContainedHostPath(source, span), ContainedHostPath(destination, span), Limits.CancellationToken), "copy", span);

        public void HostMove(string source, string destination, LythonSourceSpan? span)
            => AwaitHost(Host, () => Host.MoveAsync(ContainedHostPath(source, span), ContainedHostPath(destination, span), Limits.CancellationToken), "move", span);

        public ValueTask HostMoveAsync(string source, string destination, LythonSourceSpan? span)
            => AwaitHostAsync(() => Host.MoveAsync(ContainedHostPath(source, span), ContainedHostPath(destination, span), Limits.CancellationToken), "move", span);

        public LythonPathStat HostStat(string path, LythonSourceSpan? span)
            => AwaitHost(Host, () => Host.StatAsync(ContainedHostPath(path, span), Limits.CancellationToken), "stat", span);

        public ValueTask<LythonPathStat> HostStatAsync(string path, LythonSourceSpan? span)
            => AwaitHostAsync(() => Host.StatAsync(ContainedHostPath(path, span), Limits.CancellationToken), "stat", span);

        private string ContainedHostPath(string path, LythonSourceSpan? span)
            => PathOps.RequireContainedPath(PathOps.Normalize(path, Host.Cwd), span);

        public LythonSubprocessResult RunSubprocess(LythonSubprocessRequest request, LythonSourceSpan? span)
        {
            var runner = Host.SubprocessRunner;
            if (runner is null)
            {
                throw new LythonRuntimeException("RuntimeError", "subprocess is not available in this host.", span);
            }

            var result = AwaitHost(runner, () => runner.RunAsync(request, Limits.CancellationToken), "subprocess.run", span);
            ValidateSubprocessOutput(request, result, span);
            return result;
        }

        public async ValueTask<LythonSubprocessResult> RunSubprocessAsync(LythonSubprocessRequest request, LythonSourceSpan? span)
        {
            var runner = Host.SubprocessRunner;
            if (runner is null)
            {
                throw new LythonRuntimeException("RuntimeError", "subprocess is not available in this host.", span);
            }

            var result = await AwaitHostAsync(() => runner.RunAsync(request, Limits.CancellationToken), "subprocess.run", span).ConfigureAwait(false);
            ValidateSubprocessOutput(request, result, span);
            return result;
        }

        private static void ValidateSubprocessOutput(
            LythonSubprocessRequest request,
            LythonSubprocessResult result,
            LythonSourceSpan? span)
        {
            if (request.OutputLimit is not { } outputLimit)
            {
                return;
            }

            if (request.StandardOutput == LythonSubprocessStreamMode.Pipe && result.StandardOutputUtf8.Length > outputLimit.Bytes)
            {
                throw RuntimeErrors.Runtime(
                    $"subprocess standard output exceeded maximum captured output bytes ({outputLimit.Bytes}); received {result.StandardOutputUtf8.Length} bytes.",
                    span);
            }

            if (request.StandardError == LythonSubprocessStreamMode.Pipe && result.StandardErrorUtf8.Length > outputLimit.Bytes)
            {
                throw RuntimeErrors.Runtime(
                    $"subprocess standard error exceeded maximum captured output bytes ({outputLimit.Bytes}); received {result.StandardErrorUtf8.Length} bytes.",
                    span);
            }

            var combinedCapturedBytes =
                (request.StandardOutput == LythonSubprocessStreamMode.Pipe ? (long)result.StandardOutputUtf8.Length : 0L) +
                (request.StandardError == LythonSubprocessStreamMode.Pipe ? result.StandardErrorUtf8.Length : 0L);
            if (combinedCapturedBytes > outputLimit.Bytes)
            {
                throw RuntimeErrors.Runtime(
                    $"subprocess combined captured output exceeded maximum bytes ({outputLimit.Bytes}); received {combinedCapturedBytes} bytes.",
                    span);
            }
        }

        private T AwaitHost<T>(object capability, Func<ValueTask<T>> operation, string name, LythonSourceSpan? span)
            => HostOperation.Await(capability, operation, name, span);

        private void AwaitHost(object capability, Func<ValueTask> operation, string name, LythonSourceSpan? span)
            => HostOperation.Await(capability, operation, name, span);

        private static ValueTask<T> AwaitHostAsync<T>(Func<ValueTask<T>> operation, string name, LythonSourceSpan? span)
            => HostOperation.AwaitAsync(operation, name, span);

        private static ValueTask AwaitHostAsync(Func<ValueTask> operation, string name, LythonSourceSpan? span)
            => HostOperation.AwaitAsync(operation, name, span);

        public bool TryGetBuiltinType(string name, [MaybeNullWhen(false)] out PyType type)
        {
            if (TryGetBuiltin(name, out var value) && value is PyType resolved)
            {
                type = resolved;
                return true;
            }

            type = null;
            return false;
        }

        public bool TryGetBuiltin(string name, [MaybeNullWhen(false)] out object value)
        {
            for (var current = this; current is not null; current = current.ParentContext)
            {
                if (current.Frame.Variables.TryGetValue(name, out value))
                {
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static Dictionary<string, object> CreateModuleVariables(
            Dictionary<string, object> builtinVariables,
            string? sourcePath,
            string moduleName)
        {
            var variables = new Dictionary<string, object>(builtinVariables, StringComparer.Ordinal)
            {
                ["__name__"] = PyString.FromString(moduleName)
            };

            if (!string.IsNullOrWhiteSpace(sourcePath))
            {
                variables["__file__"] = PyString.FromString(PathOps.Normalize(sourcePath));
            }

            return variables;
        }

        private const int MaxGlobalNormalizationDepth = 1_000;

        private static object NormalizeRuntimeValue(object? value, ExecutionContext context)
            => NormalizeRuntimeValue(
                value,
                context,
                new HashSet<object>(ReferenceEqualityComparer.Instance),
                depth: 0);

        private static object NormalizeRuntimeValue(
            object? value,
            ExecutionContext context,
            HashSet<object> activeContainers,
            int depth)
        {
            if (depth > MaxGlobalNormalizationDepth)
            {
                throw RuntimeErrors.Type($"initial global values cannot be nested more than {MaxGlobalNormalizationDepth} levels", null);
            }

            switch (value)
            {
                case null:
                case PyNone:
                    return PyNone.Instance;
                case bool boolean:
                    return boolean;
                case BigInteger integer:
                    context.ObserveValue(integer, null);
                    return integer;
                case sbyte integer:
                    return integer;
                case byte integer:
                    return integer;
                case short integer:
                    return integer;
                case ushort integer:
                    return integer;
                case int integer:
                    return integer;
                case uint integer:
                    return integer;
                case long integer:
                    return integer;
                case ulong integer:
                    return integer;
                case float floating:
                    return floating;
                case double floating:
                    return floating;
                case decimal number:
                    return number;
                case string text:
                    return CreateString(text, context, null);
                case PyString text:
                    return CreateString(text.AsString(), context, null);
                case byte[] bytes:
                    return CreateBytes(bytes.ToArray(), context, null);
                case PyBytes bytes:
                    return CreateBytes(bytes.ToArray(), context, null);
            }

            var valueType = value.GetType();
            return valueType == typeof(PyList)
                ? NormalizeContainer(value, activeContainers, () => NormalizePyList((PyList)value, context, activeContainers, depth + 1))
                : valueType == typeof(PyTuple)
                    ? NormalizeContainer(value, activeContainers, () => NormalizePyTuple((PyTuple)value, context, activeContainers, depth + 1))
                    : valueType == typeof(List<object?>)
                        ? NormalizeContainer(value, activeContainers, () => NormalizeObjectList((List<object?>)value, context, activeContainers, depth + 1))
                        : valueType == typeof(object?[])
                            ? NormalizeContainer(value, activeContainers, () => NormalizeObjectArray((object?[])value, context, activeContainers, depth + 1))
                            : valueType == typeof(PySet)
                                ? NormalizeContainer(value, activeContainers, () => NormalizePySet((PySet)value, context, activeContainers, depth + 1))
                                : valueType == typeof(HashSet<object?>)
                                    ? NormalizeContainer(value, activeContainers, () => NormalizeObjectSet((HashSet<object?>)value, context, activeContainers, depth + 1))
                                    : valueType == typeof(Dictionary<string, object?>)
                                        ? NormalizeContainer(value, activeContainers, () => NormalizeStringKeyDictionary((Dictionary<string, object?>)value, context, activeContainers, depth + 1))
                                        : valueType == typeof(Dictionary<object, object?>)
                                            ? NormalizeContainer(value, activeContainers, () => NormalizeObjectKeyDictionary((Dictionary<object, object?>)value, context, activeContainers, depth + 1))
                                            : valueType == typeof(PyDict)
                                                ? NormalizeContainer(value, activeContainers, () => NormalizePyDict((PyDict)value, context, activeContainers, depth + 1))
                                                : throw RuntimeErrors.Type("initial global values may contain only supported scalar and collection values", null);

            static T NormalizeContainer<T>(object container, HashSet<object> activeContainers, Func<T> normalize)
            {
                if (!activeContainers.Add(container))
                {
                    throw RuntimeErrors.Type("initial global values cannot contain reference cycles", null);
                }

                try
                {
                    return normalize();
                }
                finally
                {
                    activeContainers.Remove(container);
                }
            }

            static PyList NormalizePyList(
                PyList list,
                ExecutionContext context,
                HashSet<object> activeContainers,
                int depth)
            {
                context.ObserveCollectionCount(list.Count, null);
                var items = new object[list.Count];
                for (var i = 0; i < list.Count; i++)
                {
                    items[i] = NormalizeRuntimeValue(list[i], context, activeContainers, depth);
                }

                return new PyList(items, context.MemoryGovernor, null);
            }

            static PyTuple NormalizePyTuple(
                PyTuple tuple,
                ExecutionContext context,
                HashSet<object> activeContainers,
                int depth)
            {
                context.ObserveCollectionCount(tuple.Count, null);
                var items = new object[tuple.Count];
                for (var i = 0; i < tuple.Count; i++)
                {
                    items[i] = NormalizeRuntimeValue(tuple[i], context, activeContainers, depth);
                }

                return new PyTuple(items, context.MemoryGovernor, null);
            }

            static PyList NormalizeObjectList(
                List<object?> list,
                ExecutionContext context,
                HashSet<object> activeContainers,
                int depth)
            {
                context.ObserveCollectionCount(list.Count, null);
                var items = new object[list.Count];
                for (var i = 0; i < list.Count; i++)
                {
                    items[i] = NormalizeRuntimeValue(list[i], context, activeContainers, depth);
                }

                return new PyList(items, context.MemoryGovernor, null);
            }

            static PyTuple NormalizeObjectArray(
                object?[] tuple,
                ExecutionContext context,
                HashSet<object> activeContainers,
                int depth)
            {
                context.ObserveCollectionCount(tuple.Length, null);
                var items = new object[tuple.Length];
                for (var i = 0; i < tuple.Length; i++)
                {
                    items[i] = NormalizeRuntimeValue(tuple[i], context, activeContainers, depth);
                }

                return new PyTuple(items, context.MemoryGovernor, null);
            }

            static PySet NormalizePySet(
                PySet set,
                ExecutionContext context,
                HashSet<object> activeContainers,
                int depth)
            {
                context.ObserveCollectionCount(set.Count, null);
                var normalized = new PySet(context.MemoryGovernor, null);
                foreach (var item in set)
                {
                    normalized.Add(RuntimeValue(NormalizeRuntimeValue(item, context, activeContainers, depth)));
                }

                return normalized;
            }

            static PySet NormalizeObjectSet(
                HashSet<object?> set,
                ExecutionContext context,
                HashSet<object> activeContainers,
                int depth)
            {
                context.ObserveCollectionCount(set.Count, null);
                var normalized = new PySet(context.MemoryGovernor, null);
                foreach (var item in set)
                {
                    normalized.Add(RuntimeValue(NormalizeRuntimeValue(item, context, activeContainers, depth)));
                }

                return normalized;
            }

            static PyDict NormalizeStringKeyDictionary(
                Dictionary<string, object?> dict,
                ExecutionContext context,
                HashSet<object> activeContainers,
                int depth)
            {
                context.ObserveCollectionCount(dict.Count, null);
                var normalized = new PyDict(context.MemoryGovernor, null);
                foreach (var pair in dict)
                {
                    normalized.SetItem(CreateString(pair.Key, context, null), NormalizeRuntimeValue(pair.Value, context, activeContainers, depth));
                }

                return normalized;
            }

            static PyDict NormalizeObjectKeyDictionary(
                Dictionary<object, object?> dict,
                ExecutionContext context,
                HashSet<object> activeContainers,
                int depth)
            {
                context.ObserveCollectionCount(dict.Count, null);
                var normalized = new PyDict(context.MemoryGovernor, null);
                foreach (var pair in dict)
                {
                    normalized.SetItem(
                        ValidateDictionaryKey(NormalizeRuntimeValue(pair.Key, context, activeContainers, depth), null, context.MemoryGovernor),
                        NormalizeRuntimeValue(pair.Value, context, activeContainers, depth));
                }

                return normalized;
            }

            static PyDict NormalizePyDict(
                PyDict dict,
                ExecutionContext context,
                HashSet<object> activeContainers,
                int depth)
            {
                context.ObserveCollectionCount(dict.Count, null);
                var normalized = new PyDict(context.MemoryGovernor, null);
                foreach (var pair in dict)
                {
                    normalized.SetItem(
                        ValidateDictionaryKey(NormalizeRuntimeValue(pair.Key, context, activeContainers, depth), null, context.MemoryGovernor),
                        NormalizeRuntimeValue(pair.Value, context, activeContainers, depth));
                }

                return normalized;
            }
        }

        public void CheckExecutionBudget(LythonSourceSpan? span) => Services.CheckExecutionBudget(span);

        public void ObserveValue(object value, LythonSourceSpan? span) => Services.ObserveValue(value, span);

        public void RegisterHostCall(LythonSourceSpan? span) => Services.RegisterHostCall(span);

        public void EnterFunctionCall(LythonSourceSpan? span) => Services.EnterFunctionCall(span);

        public void LeaveFunctionCall() => Services.LeaveFunctionCall();

        public void EnterInterpreterFrame(LythonSourceSpan? span) => Services.EnterInterpreterFrame(span);

        public void LeaveInterpreterFrame() => Services.LeaveInterpreterFrame();

        public void ObserveString(PyString text, LythonSourceSpan? span) => Services.ObserveString(text, span);

        public void ObserveCollectionCount(int count, LythonSourceSpan? span) => Services.ObserveCollectionCount(count, span);
    }

}
