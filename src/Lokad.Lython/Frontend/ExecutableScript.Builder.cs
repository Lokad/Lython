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

    internal static ExecutableScript Compile(LoweredScript lowered)
        => Compile(lowered.Syntax);

    public static ExecutableScript Compile(ScriptSyntax script, CancellationToken cancellationToken = default, MemoryGovernor? governor = null)
    {
        List<ExecutableCodeObject> moduleCode = [];
        var entryPoint = new Builder(null, null, ScopeDirectiveFactsCollector.ForModule(script), completeLexicalParent: true,
            moduleCode: moduleCode, compilationCancellation: cancellationToken, compilationGovernor: governor)
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
                    var lambda = lambdaSyntax.PreparedCallable ?? (LoweredLambdaExpression)LoweredScript.LowerStandaloneExpression(lambdaSyntax);
                    if (lambda.PreparedCode is null) CompileNamespaceLambda(lambda, cancellationToken, governor);
                    break;
                case GeneratorExpressionSyntax generatorSyntax:
                    var generator = generatorSyntax.PreparedCallable ?? (LoweredGeneratorExpression)LoweredScript.LowerStandaloneExpression(generatorSyntax);
                    if (generator.PreparedCode is null)
                        generator.CompleteCompilation(new Builder(FunctionParameterLayout.Create([]), true, GeneratorFacts(generator), cancellationToken, governor)
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

    private static void CompileNamespaceLambda(LoweredLambdaExpression lambda, CancellationToken cancellationToken, MemoryGovernor? governor)
        => lambda.CompleteCompilation(new Builder(lambda.ParameterLayout, lambda.IsGenerator, lambda.ScopeFacts, cancellationToken, governor)
            .CompileCodeObject("<lambda>", [], qualifiedName: lambda.Lambda.PreparedQualifiedName, lambda: lambda), namespaceClosure: true);

    internal static ExecutableCodeObject CompileClassFunction(LoweredFunctionDefinitionStatement function)
        => CompileClassFunction(function.Syntax, default, null);

    internal static ExecutableCodeObject CompileClassFunction(FunctionDefinitionStatementSyntax function,
        CancellationToken cancellationToken, MemoryGovernor? governor)
        => new Builder(function.ParameterLayout, function.IsGenerator, ScopeDirectiveFactsCollector.ForFunction(function), cancellationToken, governor)
            .CompileCodeObject(function.DeclaredName, function.Body, qualifiedName: function.PreparedQualifiedName);

    private sealed partial class Builder
    {
        private static readonly PyString LambdaName = PyString.FromString("<lambda>");
        private static readonly LythonSourceSpan EmptySpan = new(0, 0, 0, 0);

        private readonly List<object?> _constants = [];
        private readonly Dictionary<object, int> _constantIndexes = new(ConstantValueComparer.Instance);
        private int? _nullConstantIndex;
        private readonly List<string> _names = [];
        private readonly List<string> _locals = [];
        private readonly List<string> _closures = [];
        private readonly Dictionary<string, int> _nameIndexes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _localIndexes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _closureIndexes = new(StringComparer.Ordinal);
        private readonly List<List<ExecutableInstruction>> _blocks = [];
        private readonly List<ExecutableExceptionRegion> _regions = [];
        private readonly List<ImportStatementSyntax> _imports = [];
        private readonly List<ExecutableCallSite> _callSites = [];
        private int _lookupCacheCount;
        private readonly List<ExecutableMatchCaseBinding> _matchCases = [];
        private readonly Stack<LoopContext> _loops = [];
        private readonly Stack<IReadOnlySet<string>> _comprehensionNames = [];
        private int _protectedDepth;
        private bool _generator;
        private int _syntheticLocalCounter;

        private readonly FunctionParameterLayout? _parameterLayout;
        private readonly Builder? _lexicalParent;
        private readonly IReadOnlySet<string>? _typeParameterNames;
        private readonly bool _completeLexicalParent;
        private readonly ScopeDirectiveFacts _scopeFacts;
        private readonly List<ExecutableCodeObject>? _moduleCode;
        private readonly CancellationToken _compilationCancellation;
        private readonly MemoryGovernor? _compilationGovernor;

        internal Builder(FunctionParameterLayout parameterLayout, bool generator, ScopeDirectiveFacts scopeFacts,
            CancellationToken cancellationToken, MemoryGovernor? governor)
            : this(parameterLayout, null, scopeFacts, compilationCancellation: cancellationToken, compilationGovernor: governor)
        {
            _generator = generator;
        }

        internal Builder(
            FunctionParameterLayout? parameterLayout,
            Builder? lexicalParent,
            ScopeDirectiveFacts scopeFacts,
            bool completeLexicalParent = false,
            List<ExecutableCodeObject>? moduleCode = null,
            IReadOnlySet<string>? typeParameterNames = null,
            CancellationToken compilationCancellation = default,
            MemoryGovernor? compilationGovernor = null)
        {
            _moduleCode = moduleCode;
            _compilationCancellation = lexicalParent?._compilationCancellation ?? compilationCancellation;
            _compilationGovernor = lexicalParent?._compilationGovernor ?? compilationGovernor;
            _scopeFacts = scopeFacts;
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
            LoweredLambdaExpression? lambda = null)
        {
            // A lexical super read also requests the hidden free __class__ cell.
            // Ordinary enclosing locals must be retained like explicit reads.
            // Separately built generator functions resolve this hidden name
            // through their retained lexical chain. Generator expressions have
            // the defining builder's candidates and capture the existing cell.
            // Module facts are prepared too, but a module is not a closure.
            if ((!_generator || generatorExpression is not null || _completeLexicalParent) && _scopeFacts.RequiresClassCell && _parameterLayout is not null)
                InternClosure("__class__");
            // Binding and executable locals use one prepared parameter order.
            // Body locals are appended below, without reclassifying the signature.
            if (_parameterLayout is not null)
                foreach (var parameterName in _parameterLayout.LayoutParameterNames)
                    if (IsLocalBindingName(parameterName)) InternLocal(parameterName);
            // Function locals include bindings in dead control-flow bodies.
            // Keep the argument prefix above; no second binding walk. Modules
            // retain their dynamic namespace; actual stores intern needed slots.
            if (_parameterLayout is not null)
                foreach (var localName in _scopeFacts.LocalBindingOrder)
                    InternLocal(localName);

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
            var docstring = callable && statements.Count > 0 &&
                statements[0] is ExpressionStatementSyntax { Expression: StringLiteralExpressionSyntax literal }
                ? PyString.FromString(literal.Value) : null;
            var entryBlock = CreateBlock();
            int? exitBlock;
            if (generatorExpression is not null)
                exitBlock = CompileComprehensionBody(generatorExpression.Clauses, generatorExpression.ItemExpression,
                    null, generatorExpression.Span, entryBlock, yielding: true);
            else if (lambda is not null)
            {
                var resultBlock = CompileExpression(lambda.Body, entryBlock);
                AddInstruction(resultBlock, ExecutableInstruction.Return(lambda.Span));
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

            var jumpTargets = ResolveJumpTargets();
            var blockTargets = new ExecutableBlockTargets(RetainReachableBlocks(entryBlock, jumpTargets), jumpTargets);
            var normalizedRegions = NormalizeRegions(blockTargets);
            var (capturedLocalSlots, containsCallable, needsMirroring, completeChildren) = PrepareDeclarations();
            // Annotation values can retain functions or deferred generators after
            // module slots leave. Keep their existing global namespace lifetime
            // conservatively; scalar-only declarations need no such mirror.
            var moduleAnnotationNamespace = _parameterLayout is null &&
                _scopeFacts.ContainsVariableAnnotations && containsCallable;
            var requiresLocalVariableMirroring =
                moduleAnnotationNamespace || (_generator && generatorExpression is null && !_completeLexicalParent) ||
                needsMirroring;

            // The existing compilation owner funds the immutable function metadata.
            // A function and its body can share the same leading string literal.
            // This builder is single-use; its completed slot maps become code metadata.
            var code = new ExecutableCodeObject(
                name,
                _constants.ToArray(),
                _names.ToArray(),
                _locals.ToArray(),
                _localIndexes,
                capturedLocalSlots,
                _closures.ToArray(),
                _closureIndexes,
                _scopeFacts,
                _blocks,
                0,
                blockTargets,
                normalizedRegions,
                _imports.ToArray(),
                _callSites.ToArray(),
                _lookupCacheCount,
                _matchCases.ToArray(),
                requiresLocalVariableMirroring,
                isFunctionScope: _parameterLayout is not null,
                initialOperandDepth: generatorExpression is null ? 0 : 1,
                functionName: callable ? name == "<lambda>" ? LambdaName : PyString.FromString(name) : null,
                docstring: docstring)
            {
                QualifiedName = qualifiedName,
                // Code using only slots can own cells independently of an
                // activation. A descendant that can still observe a lexical
                // namespace keeps its defining chain and the bridge to it.
                UsesOnlyLexicalSlots = callable && resolvedNonlocals &&
                    !requiresLocalVariableMirroring &&
                    !_scopeFacts.RequiresClassCell && _matchCases.Count == 0 &&
                    completeChildren
            };
            _moduleCode?.Add(code);
            return code;
        }

        private (int[] CapturedLocals, bool ContainsCallable, bool NeedsMirroring, bool CompleteChildren) PrepareDeclarations()
        {
            var containsCallable = false;
            var needsMirroring = false;
            var completeChildren = true;
            HashSet<int>? captured = null;
            var firstLambda = _constants.Count;
            var firstGenerator = _constants.Count;
            for (var index = 0; index < _constants.Count; index++)
            {
                if ((index & 1023) == 0) PythonTokenization.CheckCancellation(_compilationCancellation, 0);
                switch (_constants[index])
                {
                    case ExecutableCreateFunction function:
                        PrepareChild(function.CodeObject);
                        break;
                    case ExecutableCreateClass or ExecutableBindTypeAlias:
                        needsMirroring = true;
                        break;
                    case ExecutableCreateLambda:
                        firstLambda = Math.Min(firstLambda, index);
                        break;
                    case ExecutableCreateGeneratorExpression:
                        firstGenerator = Math.Min(firstGenerator, index);
                        break;
                }
            }

            // Forwarded cells contribute to locals() snapshots. Preserve the
            // declaration order of named functions, lambdas and generators.
            for (var index = firstLambda; index < _constants.Count; index++)
            {
                if ((index & 1023) == 0) PythonTokenization.CheckCancellation(_compilationCancellation, 0);
                if (_constants[index] is ExecutableCreateLambda lambda) PrepareChild(lambda.CodeObject);
            }
            for (var index = firstGenerator; index < _constants.Count; index++)
            {
                if ((index & 1023) == 0) PythonTokenization.CheckCancellation(_compilationCancellation, 0);
                if (_constants[index] is not ExecutableCreateGeneratorExpression generator) continue;
                completeChildren = false;
                PrepareChild(generator.CodeObject);
            }

            var slots = captured?.ToArray() ?? [];
            if (slots.Length > 1) Array.Sort(slots);
            return (slots, containsCallable, needsMirroring, completeChildren);

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
                    if ((i & 255) == 0) PythonTokenization.CheckCancellation(_compilationCancellation, sourceStart);
                    var name = names[i];
                    if (_localIndexes.TryGetValue(name, out var slot)) (captured ??= []).Add(slot);
                    else if (!_closureIndexes.ContainsKey(name))
                    {
                        if (HasEnclosingBinding(name)) InternClosure(name);
                        else resolved = false;
                    }
                }
                if (resolved && child.CaptureSources is null && _moduleCode is not null)
                    child.PrepareCaptureSources(_localIndexes, _closureIndexes, _compilationGovernor, _compilationCancellation);
            }
        }

    }
}
