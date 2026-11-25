using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        private InlinedComprehensionScope? _inlinedComprehension;

        internal InlinedComprehensionScope EnterComprehension(ScopeDirectiveFacts facts, bool forwardsBinding = false)
            => new(this, facts, forwardsBinding);

        internal bool TryGetInlinedBinding(string name, [MaybeNullWhen(false)] out ExecutionContext scope)
        {
            for (var current = _inlinedComprehension; current is not null; current = current.Previous)
                if (current.Facts.LocalNames.Contains(name))
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

        internal IEnumerable<InlinedComprehensionScope> EnumerateInlinedBindings()
        {
            var pending = new Stack<InlinedComprehensionScope>();
            for (var current = _inlinedComprehension; current is not null; current = current.Previous) pending.Push(current);
            while (pending.Count != 0) yield return pending.Pop();
        }

        // This is a lexical binding lifetime, not an invocation frame. Eager
        // expressions execute with the original context; closures retain the
        // real iteration namespace while enclosing cells stay untouched.
        internal sealed class InlinedComprehensionScope : ExecutableTemporaryValue
        {

            private readonly ExecutionContext _context;
            private readonly ExecutionContext _previousClosure;
            private ExecutionContext? _bindings;
            private object? _forwardedItem;
            internal InlinedComprehensionScope? Previous { get; }
            internal ScopeDirectiveFacts Facts { get; }
            internal ExecutionContext Bindings
            {
                get
                {
                    if (_bindings is not null) return _bindings;
                    var bindings = CreateComprehension(_context, Facts);
                    if (_forwardedItem is not null) bindings.Variables[Facts.LocalBindingOrder[0]] = _forwardedItem;
                    _forwardedItem = null;
                    return _bindings = bindings;
                }
            }

            internal InlinedComprehensionScope(ExecutionContext context, ScopeDirectiveFacts facts, bool forwardsBinding)
            {
                _context = context;
                Facts = facts;
                Previous = context._inlinedComprehension;
                _previousClosure = context.FunctionClosureContext;
                // Closed projections create no lexical children. Their live
                // item stays in the scope while the cursor advances again.
                if (!forwardsBinding) context.FunctionClosureContext = Bindings.FunctionClosureContext;
                context._inlinedComprehension = this;
            }

            internal void BindForwardedItem(object item)
            {
                if (_bindings is null) _forwardedItem = item;
                else _bindings.Variables[Facts.LocalBindingOrder[0]] = item;
            }

            internal bool TryRead(string name, [MaybeNullWhen(false)] out object value)
            {
                if (_bindings is not null) return _bindings.Frame.TryGetVariable(name, out value);
                value = _forwardedItem;
                return value is not null;
            }

            public override void Dispose()
            {
                _forwardedItem = null;
                _context._inlinedComprehension = Previous;
                _context.FunctionClosureContext = _previousClosure;
            }
        }
    }
}
