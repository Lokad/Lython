# Next frontend binding consolidation — source decision only

Current frontend source at3e863f40; independent B196 runtime candidate is frozen.
Do not mix this work into its before/after or infer compiler CPU from runtime data.

ScopeDirectiveFactsCollector.ForFunction currently walks direct scopes once for
global/nonlocal directives and again for local bindings, then filters local names.
CollectLocalBindings already recursively visits the same lexical child bodies and
stops at function/class definitions. It can collect directives in that same walk
and retain the end-of-scope filtering, including directives after earlier uses.
Its ExpressionStatement branch also traverses the expression before the common
EnumerateDirectExpressions loop visits it again. Remove that duplicate branch.

ExecutableScript.Builder.CollectLocals then independently walks a subset of lowered
assignments/if/while/match/with bodies, with separate assignment/pattern visitors.
Function construction subsequently appends ALL prepared scope LocalNames anyway.
Use complete prepared binding names once after the existing parameter prefix,
removing the three incomplete duplicate collectors. No new fact/cache/layer is
needed. Internal nonparameter slot order may change; Python namespace insertion
order must still follow runtime stores. Module locals require validation too:
LoadLocal already falls back to live module variables/builtins when uninitialized,
ChildClosureCandidates explicitly excludes module slots, imports/class/function
stores and globals() updates must continue updating actual frames. If those
invariants fail, correct the representation rather than hide a module exception
or retain a dormant second collector.

Expected deletion: one directive traversal plus ~110 lines of duplicate builder
collectors and a repeated expression binding traversal; all actual annotation,
class-cell/global/nonlocal facts, diagnostics and runtime lowered consumers stay.
This targets repeated compilation work; B195 compile-only~1.58x remains meaningful,
but compilation is a small share of the current large scripts' fresh total.
Do not label a compiler gain as the explanation of text/report warm gaps.

Acceptance before implementation: ordinary module fallback/builtin shadowing,
global dictionary mutation/import/def/class slots and insertion order; positional,
keyword-only and variadic parameter prefixes/default binding; local-before-binding
errors in dead for/try/with/match/unpacking/walrus/delete; nonlocal/closure/generator
and class-cell/super behavior; annotation/LA2010 diagnostics; compile cancellation,
first refusal, scope cleanup and original worker goldens on actual11/10.
Existing focused semantic families plus one public module-binding regression.

One bounded compile-only CSV/report and tiny/fresh-config source pair with two
unchanged baseline roles, ordinary warm controls separate; existing protocol,
all goldens/joins, ordinary GC/tiering. Decide source simplification and allocation
separately from elapsed/CPU; preserve adverse observations. This is the next
coherent candidate after B196 disposition, not another speculative audit campaign.
