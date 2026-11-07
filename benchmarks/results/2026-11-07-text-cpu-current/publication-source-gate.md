# Publication source assessment — R64

2026-11-07, `70267889`. This follows the bounded R63 CPU capture. No prototype,
timing run or repeated qualification was needed to establish the source facts.

`ExecutableStackLayout.ForwardValues` already marks adjacent eligible producer /
local-store pairs. `CompleteLocalResult` retires inputs, checks for pending disposal,
stores directly to the local binding and advances the instruction index over the
following store. Thus the common path already eliminates that store's dispatch
and temporary result write. The retained instruction supplies its destination and
original source span and is the existing continuation after delayed disposal.

Removing it completely needs another representation of the target/span, funded
temporary result and cleanup resumption. Reusing the existing operand-span table
adds a hot lookup; parallel metadata/continuation fields introduce storage and
exception-routing work. Those alternatives do not establish a complete remaining
dispatch phase to delete. The input carrier can also inline/scalar-replace; source
presence alone does not prove transport traffic. The source gate therefore does
not select this rewrite. It is a bounded design decision, not a permanent ban on
representation changes or a result inferred from an unstable timing.

Preserve the current retirement order: cleanup finishes before assignment becomes
visible; the result and inputs remain funded through actual users. A changed
destination encoding cannot erase the separate operation/store error spans,
cancellation/refusal precedence, protocol reentry or genuinely async cleanup.
The observed prefix is 9,335 bytes of Tier1 code, but that size and sampled self
time alone do not establish cache pressure or a predicted benefit from repartitioning.

The next frontend source assessment concerns a real repeated analysis:
`LexicalScopePreparation` requests binding facts for each function and class-cell
requirements; `ClassCellSyntaxFacts.ReferencesFromFunction` descends through nested
functions and can request their uncached `ForFunctionBindings` facts before normal
preparation. The module also asks for outward class-cell reads after the lexical
walk. Assess preparing binding/class-cell demands bottom-up and resolving enclosing
scope/preload requirements using those same facts. Preserve postponed annotations,
lambda defaults versus bodies, comprehensions, class suites versus methods,
global/nonlocal shadowing, private-name rewriting and later module `super` bindings.

Before code, specify the facts needed, exact repeated walks/helpers removed and
semantic acceptance. Reuse the existing prepared-fact representation if suitable;
avoid a general visitor/cache, partially prepared facts escaping to callers or
moving Python scoping machinery into the parser without resolving rewriting order.
CSV/text/report/config compilation is affected; no warm runtime benefit is predicted.
This remains part of the broader roadmap, with runtime dispatch/scalar/record work
open. Publication and final producer qualification remain independently deferred.
