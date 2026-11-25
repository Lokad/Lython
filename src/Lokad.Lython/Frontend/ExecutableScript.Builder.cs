using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Frontend;

internal sealed partial class ExecutableScript
{
    private ExecutableScript(
        LythonSourceSpan? annotationInitializationSpan,
        ExecutableCodeObject entryPoint)
    {
        AnnotationInitializationSpan = annotationInitializationSpan;
        EntryPoint = entryPoint;
    }

    public LythonSourceSpan? AnnotationInitializationSpan { get; }

    public ExecutableCodeObject EntryPoint { get; }

    public static ExecutableScript Compile(ScriptSyntax script, CancellationToken cancellationToken = default, MemoryGovernor? governor = null)
    {
        var compilation = new Compilation(cancellationToken, governor);
        List<ExecutableCodeObject> moduleCode = [];
        var entryPoint = new Builder(compilation, null, null, ScopeDirectiveFactsCollector.ForModule(script),
            completeLexicalParent: true, moduleCode: moduleCode)
            .CompileCodeObject("<module>", script.Statements);
        // Actual executable sites have supplied their lexical capture facts.
        // Remaining callables belong to retained lowered consumers or lazy
        // annotations. Prepare their namespace code without evaluating them.
        foreach (var syntax in script.PreparedCallables)
        {
            PythonTokenization.CheckCancellation(cancellationToken, 0);
            switch (syntax)
            {
                case LambdaExpressionSyntax lambdaSyntax:
                    if (lambdaSyntax is { PreparedCallable: not null, PreparedDefinition: null })
                        CompileNamespaceLambda(lambdaSyntax, compilation);
                    break;
                case GeneratorExpressionSyntax generatorSyntax:
                    var generator = generatorSyntax.PreparedCallable ?? (LoweredGeneratorExpression)LoweredScript.LowerExpression(generatorSyntax);
                    if (generator.PreparedCode is null)
                        generator.CompleteCompilation(new Builder(compilation, FunctionParameterLayout.Create([]), null,
                                GeneratorFacts(generator), generator: true)
                            .CompileCodeObject("<genexpr>", [], generator));
                    break;
                default: throw new InvalidOperationException("Unexpected prepared callable syntax.");
            }
        }
        foreach (var code in moduleCode) code.PrepareModuleGlobals(entryPoint, cancellationToken);
        return new ExecutableScript(entryPoint.ScopeFacts.ContainsVariableAnnotations
            ? script.Statements[0].Span : null, entryPoint);
    }

    private static ScopeDirectiveFacts GeneratorFacts(LoweredGeneratorExpression expression)
        => expression.ScopeFacts with { LocalNames = new HashSet<string>(expression.ScopeFacts.LocalNames, StringComparer.Ordinal) { ".0" } };

    private static ExecutableCreateFunction CompileNamespaceLambda(LambdaExpressionSyntax lambda, Compilation compilation)
    {
        // Lazy annotation expressions are outside the statement yield walk.
        var generator = GeneratorSyntaxFacts.ContainsYield(lambda.Body);
        var code = new Builder(compilation, lambda.ParameterLayout, null, ScopeDirectiveFactsCollector.ForLambda(lambda), generator: generator)
            .CompileCodeObject("<lambda>", [], qualifiedName: lambda.PreparedQualifiedName, lambda: lambda);
        return CompleteLambdaDefinition(lambda, code, generator, ExecutableDefinitionKind.NamespaceLambda);
    }

    private static ExecutableCreateFunction CompleteLambdaDefinition(LambdaExpressionSyntax lambda,
        ExecutableCodeObject code, bool generator, ExecutableDefinitionKind kind)
        => lambda.PreparedDefinition = new ExecutableCreateFunction(code, lambda.ParameterLayout, 0, generator, 0,
            [], null, null, null, Kind: kind);

    // Recursive emission has one active buffer set per builder. A compilation
    // keeps one spare for the next sibling. Only final arrays and per-scope
    // capture maps escape compilation.
    private sealed class Compilation(CancellationToken cancellationToken, MemoryGovernor? governor)
    {
        private Builder.Buffers? _spare;

        internal CancellationToken CancellationToken { get; } = cancellationToken;
        internal MemoryGovernor? Governor { get; } = governor;

        internal Builder.Buffers TakeBuffers()
        {
            var buffers = _spare;
            _spare = null;
            return buffers ?? new Builder.Buffers();
        }

        internal void ReturnBuffers(Builder.Buffers buffers)
        {
            if (_spare is not null) return;
            buffers.Clear();
            _spare = buffers;
        }
    }

    private sealed partial class Builder
    {
        private static readonly PyString LambdaName = PyString.FromString("<lambda>");
        private static readonly LythonSourceSpan EmptySpan = new(0, 0, 0, 0);

        internal sealed class Buffers
        {
            internal readonly List<object?> Constants = [];
            internal readonly Dictionary<object, int> ConstantIndexes = [];
            internal readonly List<string> Names = [];
            internal readonly Dictionary<string, int> NameIndexes = new(StringComparer.Ordinal);
            internal readonly List<string> Locals = [];
            internal readonly List<string> Closures = [];
            internal readonly List<ExecutableInstruction> EntryBlock = [];
            internal readonly List<List<ExecutableInstruction>> Blocks = [];
            internal readonly List<ExecutableExceptionRegion> Regions = [];
            internal readonly List<ImportStatementSyntax> Imports = [];
            internal readonly List<ExecutableCallSite> CallSites = [];
            internal readonly List<ExecutableMatchCaseBinding> MatchCases = [];
            internal readonly Stack<LoopContext> Loops = [];
            internal readonly Stack<IReadOnlyCollection<string>> ComprehensionNames = [];

            internal void Clear()
            {
                Constants.Clear();
                ConstantIndexes.Clear();
                Names.Clear();
                NameIndexes.Clear();
                Locals.Clear();
                Closures.Clear();
                EntryBlock.Clear();
                Blocks.Clear();
                Regions.Clear();
                Imports.Clear();
                CallSites.Clear();
                MatchCases.Clear();
                Loops.Clear();
                ComprehensionNames.Clear();
            }
        }

        private readonly Compilation _compilation;
        private readonly Buffers _buffers;
        // These maps become code metadata, including the identity used to
        // select a prepared capture layout at execution.
        private Dictionary<string, int> _localIndexes;
        private readonly string[] _declaredLocalNames;
        private readonly Dictionary<string, int> _closureIndexes = new(StringComparer.Ordinal);
        private int _lookupCacheCount;
        private int _protectedDepth;
        private readonly bool _generator;
        private int _syntheticLocalCounter;

        private readonly FunctionParameterLayout? _parameterLayout;
        private readonly Builder? _lexicalParent;
        private readonly IReadOnlyCollection<string>? _typeParameterNames;
        private readonly bool _completeLexicalParent;
        private readonly bool _classBody;
        private readonly ScopeDirectiveFacts _scopeFacts;
        private readonly List<ExecutableCodeObject>? _moduleCode;

        internal Builder(
            Compilation compilation,
            FunctionParameterLayout? parameterLayout,
            Builder? lexicalParent,
            ScopeDirectiveFacts scopeFacts,
            bool completeLexicalParent = false,
            List<ExecutableCodeObject>? moduleCode = null,
            IReadOnlyCollection<string>? typeParameterNames = null,
            bool generator = false,
            bool classBody = false)
        {
            _compilation = compilation;
            _buffers = compilation.TakeBuffers();
            _generator = generator;
            _classBody = classBody;
            _moduleCode = moduleCode;
            _scopeFacts = scopeFacts;
            _localIndexes = scopeFacts.PreparedLocalSlots ?? new(StringComparer.Ordinal);
            _declaredLocalNames = scopeFacts.PreparedLocalSlots is null ? [] : scopeFacts.LocalBindingOrder;
            _parameterLayout = parameterLayout;
            // Only enclosing executable compilation supplies complete lexical
            // bindings. Independently compiled class methods/generator lambdas use
            // their live retained namespaces for unresolved names.
            _completeLexicalParent = completeLexicalParent;
            _lexicalParent = lexicalParent;
            _typeParameterNames = typeParameterNames;
        }

        public ExecutableCodeObject CompileCodeObject(string name, IReadOnlyList<StatementSyntax> statements,
            LoweredGeneratorExpression? generatorExpression = null, PyString? qualifiedName = null,
            LambdaExpressionSyntax? lambda = null)
        {
            // A lexical super read also requests the hidden free __class__ cell.
            // Ordinary enclosing locals must be retained like explicit reads.
            // Separately built generator functions resolve this hidden name
            // through their retained lexical chain. Generator expressions have
            // the defining builder's candidates and capture the existing cell.
            // Module facts are prepared too, but a module is not a closure.
            if ((!_generator || generatorExpression is not null || _completeLexicalParent) && _scopeFacts.RequiresClassCell && _parameterLayout is not null)
                InternClosure("__class__");
            // Function declarations already supply their executable prefix.
            // Generator expressions still build the implicit loop's locals here.
            if (_parameterLayout is not null && _scopeFacts.PreparedLocalSlots is null)
            {
                foreach (var parameterName in _parameterLayout.LayoutParameterNames)
                    if (IsLocalBindingName(parameterName)) InternLocal(parameterName);
                foreach (var localName in _scopeFacts.LocalBindingOrder)
                    InternLocal(localName);
            }

            var resolvedNonlocals = _completeLexicalParent;
            if (resolvedNonlocals && generatorExpression is null && _scopeFacts.NonlocalNames.Count != 0)
            {
                // Bound names used by declarations, deletion and locals() also
                // need a capture when no ordinary expression loads them.
                foreach (var nonlocalName in _scopeFacts.NonlocalNames)
                {
                    if (HasEnclosingBinding(nonlocalName)) InternClosure(nonlocalName);
                    else resolvedNonlocals = false;
                }
            }

            var callable = _parameterLayout is not null && generatorExpression is null;
            var docstring = (callable || _classBody) && statements.Count > 0 &&
                statements[0] is ExpressionStatementSyntax { Expression: StringLiteralExpressionSyntax literal }
                ? PyString.FromString(literal.Value) : null;
            var entryBlock = CreateBlock();
            int? exitBlock;
            if (generatorExpression is not null)
                exitBlock = CompileComprehensionBody(generatorExpression.Generator.Clauses, generatorExpression.Generator.ItemExpression,
                    null, generatorExpression.Span, entryBlock, yielding: true);
            else if (lambda is not null)
            {
                CompileReturnValue(lambda.Body, entryBlock, lambda.Span);
                exitBlock = null;
            }
            else exitBlock = CompileStatements(statements, entryBlock, docstring is null ? 0 : 1);
            // Module completion is distinct from an executed top-level return:
            // imports reject the latter. The interpreter already ends naturally
            // at an unterminated final block. Functions still return Python None.
            if (_parameterLayout is not null && exitBlock is int finalBlock && !IsTerminated(finalBlock))
            {
                AddInstruction(finalBlock, ExecutableInstruction.ReturnNone(EmptySpan));
            }

            var (capturedLocalSlots, containsCallable, needsMirroring, completeChildren, forwardsCaptures, hasPlainModuleClass) =
                PrepareDeclarations();
            var (firstSnapshotCell, snapshotTail) = PrepareSnapshotOrder(capturedLocalSlots);
            // Annotation values can retain functions or deferred generators after
            // module slots leave. Keep their existing global namespace lifetime
            // conservatively; scalar-only declarations need no such mirror.
            var moduleAnnotationNamespace = _parameterLayout is null &&
                _scopeFacts.ContainsVariableAnnotations && containsCallable;
            var requiresLocalVariableMirroring =
                moduleAnnotationNamespace || (_generator && generatorExpression is null && !_completeLexicalParent) ||
                needsMirroring;
            // A descendant that can observe a lexical namespace keeps the
            // defining chain and the bridge to it.
            var usesOnlyLexicalSlots = callable && resolvedNonlocals &&
                !requiresLocalVariableMirroring &&
                !_scopeFacts.RequiresClassCell && _buffers.MatchCases.Count == 0 &&
                completeChildren;

            // The existing compilation owner funds the immutable function metadata.
            // A function and its body can share the same leading string literal.
            // This builder is single-use; its completed slot maps become code metadata.
            var code = new ExecutableCodeObject(
                name,
                _buffers.Constants.ToArray(),
                _buffers.Names.ToArray(),
                CompleteLocalNames(),
                _localIndexes,
                capturedLocalSlots,
                _buffers.Closures.ToArray(),
                _closureIndexes,
                _scopeFacts,
                _buffers.Blocks,
                0,
                _buffers.Regions,
                _buffers.Imports.ToArray(),
                _buffers.CallSites.ToArray(),
                _lookupCacheCount,
                _buffers.MatchCases.ToArray(),
                requiresLocalVariableMirroring,
                usesOnlyLexicalSlots,
                isFunctionScope: _parameterLayout is not null,
                initialOperandDepth: generatorExpression is null ? 0 : 1,
                functionName: callable ? name == "<lambda>" ? LambdaName : PyString.FromString(name) : null,
                docstring: docstring,
                cancellationToken: _compilation.CancellationToken)
            {
                QualifiedName = qualifiedName,
                HasPlainModuleClass = hasPlainModuleClass && !requiresLocalVariableMirroring,
                ForwardsCaptures = forwardsCaptures,
                FirstSnapshotCell = firstSnapshotCell,
                SnapshotTailOrder = snapshotTail
            };
            _moduleCode?.Add(code);
            _compilation.ReturnBuffers(_buffers);
            return code;
        }

        private (int[] CapturedLocals, bool ContainsCallable, bool NeedsMirroring, bool CompleteChildren, bool ForwardsCaptures,
            bool HasPlainModuleClass) PrepareDeclarations()
        {
            var containsCallable = false;
            var needsMirroring = false;
            var completeChildren = true;
            var forwardsCaptures = false;
            var hasPlainModuleClass = false;
            HashSet<int>? captured = null;
            for (var index = 0; index < _buffers.Constants.Count; index++)
            {
                if ((index & 1023) == 0) PythonTokenization.CheckCancellation(_compilation.CancellationToken, 0);
                switch (_buffers.Constants[index])
                {
                    case ExecutableCreateFunction function:
                        PrepareChild(function.CodeObject);
                        break;
                    case ExecutableCreateClass definition:
                        if (!needsMirroring && _parameterLayout is null && !_classBody && UsesOnlyPlainMethodDeclarations(definition))
                            hasPlainModuleClass = true;
                        else needsMirroring = true;
                        break;
                    case ExecutableBindTypeAlias:
                        needsMirroring = true;
                        break;
                    case ExecutableCreateGeneratorExpression generator:
                        completeChildren = false;
                        PrepareChild(generator.CodeObject);
                        break;
                }
            }

            var slots = captured?.ToArray() ?? [];
            if (slots.Length > 1) Array.Sort(slots);
            return (slots, containsCallable, needsMirroring, completeChildren, forwardsCaptures, hasPlainModuleClass);

            void PrepareChild(ExecutableCodeObject child)
            {
                containsCallable = true;
                needsMirroring |= child.RequiresLocalVariableMirroring;
                completeChildren &= child.UsesOnlyLexicalSlots;

                // Forward names through intermediate functions even when they
                // own no local cells. Namespace captures keep runtime lookup.
                var names = child.ClosureNames;
                if (names.Count == 0) return;
                var sourceStart = child.Instructions[child.EntryIndex].Span.Start;
                var resolved = true;
                for (var i = 0; i < names.Count; i++)
                {
                    if ((i & 255) == 0) PythonTokenization.CheckCancellation(_compilation.CancellationToken, sourceStart);
                    var name = names[i];
                    if (_localIndexes.TryGetValue(name, out var slot)) (captured ??= []).Add(slot);
                    else
                    {
                        forwardsCaptures = true;
                        if (!_closureIndexes.ContainsKey(name))
                        {
                            if (HasEnclosingBinding(name)) InternClosure(name);
                            else resolved = false;
                        }
                    }
                }
                if (resolved && child.CaptureSources is null && _moduleCode is not null)
                {
                    // A value capture must originate in this frame and never
                    // be forwarded from the child to another binding owner.
                    var byValue = names.Count == 1 && !child.ForwardsCaptures &&
                        _localIndexes.ContainsKey(names[0]) && IsUnchangingParameter(names[0]);
                    child.PrepareCaptureSources(_localIndexes, _closureIndexes, byValue,
                        _compilation.Governor, _compilation.CancellationToken);
                }
            }
        }

        private bool UsesOnlyPlainMethodDeclarations(ExecutableCreateClass definition)
        {
            // The module's slots already support global reads and writes.
            // A plain class suite creates only lexical functions and never
            // hands its enclosing namespace to a construction callback.
            if (definition.TypeParameters is not null || definition.Syntax.Decorators.Count != 0 ||
                definition.Syntax.HeaderArguments.Count != 0 || definition.Body.ScopeFacts.CreatesClassCell ||
                definition.Body.ScopeFacts.ContainsVariableAnnotations) return false;
            var code = definition.Body;
            for (var i = 0; i < code.Instructions.Length; i++)
            {
                PythonTokenization.CheckCancellation(_compilation.CancellationToken, i);
                ref readonly var instruction = ref code.Instructions[i];
                if (instruction.OpCode is ExecutableOpCode.Complete or ExecutableOpCode.StoreName) continue;
                if (instruction.OpCode != ExecutableOpCode.ApplyOperation ||
                    code.Constants[instruction.ConstantIndex] is not ExecutableCreateFunction
                    {
                        IsLambda: false, InputCount: 0, IsGenerator: false, TypeParameters: null,
                        DeferredAnnotations.Length: 0, EagerAnnotationReads: null, PostponedAnnotations: null,
                        CodeObject:
                        {
                            ClosureNames.Count: 0, RequiresLocalVariableMirroring: false,
                            ScopeFacts: { NonlocalNames.Count: 0, RequiresClassCell: false },
                        },
                    }) return false;
            }
            return true;
        }

        private (int FirstCell, int[]? Tail) PrepareSnapshotOrder(int[] captured)
        {
            var free = _buffers.Closures;
            if (captured.Length == 0 && free.Count < 2) return (0, null);

            var parameters = _parameterLayout?.ParameterCount ?? 0;
            var firstCell = Array.BinarySearch(captured, parameters);
            if (firstCell < 0) firstCell = ~firstCell;
            var cells = captured.Length - firstCell;
            var token = _compilation.CancellationToken;

            if (cells == 0)
            {
                var index = 1;
                for (; index < free.Count; index++)
                {
                    PythonTokenization.CheckCancellation(token, index);
                    if (string.CompareOrdinal(free[index - 1], free[index]) > 0) break;
                }
                if (index >= free.Count) return (firstCell, null);
            }
            return (firstCell, CreateSnapshotTail(captured.AsSpan(firstCell)));
        }

        private int[] CreateSnapshotTail(ReadOnlySpan<int> cells)
        {
            // Physical bindings keep their operand indices. The snapshot tail
            // contains nonparameter cells followed by free variables in name
            // order. Negative entries address the existing free-variable table.
            var free = _buffers.Closures;
            var token = _compilation.CancellationToken;
            var count = checked(cells.Length + free.Count);
            var bytes = checked(32L + 4L * count);
            using var storage = _compilation.Governor?.ReserveTemporary(bytes + 256L, EmptySpan);
            PythonTokenization.CheckCancellation(token, 0);
            var order = new int[count];
            cells.CopyTo(order);
            for (var index = 0; index < free.Count; index++)
            {
                PythonTokenization.CheckCancellation(token, index);
                order[cells.Length + index] = ~index;
            }
            var comparisons = 0;
            Comparison<int> compare = CompareSlots;
            if (cells.Length > 1) ExecutionSort.Sort(order.AsSpan(0, cells.Length), compare);
            if (free.Count > 1) ExecutionSort.Sort(order.AsSpan(cells.Length), compare);
            PythonTokenization.CheckCancellation(token, 0);
            storage?.Commit(bytes);
            return order;

            int CompareSlots(int left, int right)
            {
                if ((comparisons++ & 63) == 0) PythonTokenization.CheckCancellation(token, 0);
                return string.CompareOrdinal(Name(left), Name(right));
            }

            string Name(int slot) => slot < 0 ? free[~slot] : LocalName(slot);
        }

    }
}
