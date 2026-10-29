using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        private InlinedComprehensionScope? _inlinedComprehension;

        internal InlinedComprehensionScope EnterComprehension(ScopeDirectiveFacts facts)
            => new(this, facts);

        internal bool TryGetInlinedBinding(string name, [MaybeNullWhen(false)] out ExecutionContext scope)
        {
            for (var current = _inlinedComprehension; current is not null; current = current.Previous)
                if (current.Bindings.ScopeFacts.LocalNames.Contains(name))
                {
                    scope = current.Bindings;
                    return true;
                }
            scope = null;
            return false;
        }

        internal ExecutionContext? InlinedComprehensionLookup
            => IsClassBody || IsAnnotationScope ? _inlinedComprehension?.Bindings : null;
        internal bool IncludeCallerLocals => _inlinedComprehension is null || FunctionName is not null;

        internal IEnumerable<ExecutionContext> EnumerateInlinedBindings()
        {
            var pending = new Stack<ExecutionContext>();
            for (var current = _inlinedComprehension; current is not null; current = current.Previous) pending.Push(current.Bindings);
            while (pending.Count != 0) yield return pending.Pop();
        }

        // This is a lexical binding lifetime, not an invocation frame. Eager
        // expressions execute with the original context; closures retain the
        // real iteration namespace while enclosing cells stay untouched.
        internal sealed class InlinedComprehensionScope : IExecutableTemporaryValue
        {

            private readonly ExecutionContext _context;
            private readonly ExecutionContext _previousClosure;
            internal InlinedComprehensionScope? Previous { get; }
            internal ExecutionContext Bindings { get; }

            internal InlinedComprehensionScope(ExecutionContext context, ScopeDirectiveFacts facts)
            {
                _context = context;
                Previous = context._inlinedComprehension;
                _previousClosure = context.FunctionClosureContext;
                Bindings = CreateComprehension(context, facts);
                context._inlinedComprehension = this;
                context.FunctionClosureContext = Bindings.FunctionClosureContext;
            }

            public void Dispose()
            {
                _context._inlinedComprehension = Previous;
                _context.FunctionClosureContext = _previousClosure;
            }
        }
    }
}
