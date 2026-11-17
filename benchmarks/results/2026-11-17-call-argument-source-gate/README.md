# Call-argument staging source gate

R470, 2026-11-17 UTC, c882e206 (runtime unchanged from qualified ccf20ba3).
This is final compiled-IR/source evidence, not a CPU measurement or prototype.
The existing white-box compiler harness emits the observations without executing
new guest code; its temporary diagnostic is removed from the test project.

| Complete script | Static call sites | Sites with adjacent pure argument tails | Tail leaf instructions |
| --- | ---: | ---: | ---: |
| Numeric squared-error report | 11 | 7 | 12 |
| Positional helper/range | 3 | 3 | 3 |
| Helper-list normalization | 17 | 10 | 16 |
| Helper-list report | 11 | 9 | 11 |

These are static sites, not dynamic invocation counts or removable sample shares.
Main loop call operands still contain pure LoadLocal/LoadConst instructions.
Existing ForwardValues forwards many ordinary operation inputs, while Call has
ValueInputCount zero. Thus call arguments still pay leaf dispatch/publication
before the existing call boundary consumes a contiguous owned window.

Selected hypothesis: extend existing operand preparation/call-site metadata to
batch eligible adjacent leaf reads at the call boundary, deleting their separate
instructions. Retain the existing funded caller window, final parameter binding
and real suspension/frame owners. No new calling backend, frame controller,
arena, pool, option or generic dispatch framework. This removes different work
from the withdrawn R439 shared-kernel and R456 native-block designs; their slower
measurements remain relevant warnings about added indirection/compile overhead.

Only uncaptured/unmirrored locals and constants in an adjacent pure tail qualify.
Never move a read across another argument's effect or protocol. Snapshot inputs
before callable checks, admission/reentry and actual suspension; preserve canonical
identity, missing-name versus non-callable/recursion error precedence and source
spans. Fund conservative physical capacity and compiler descriptors. Partial
failure, cancellation/refusal and pending async users must retain correct extents
and cleanup. Dynamic/default/keyword/expanded binding remains supported.

Stopping criterion: one bounded complete helper-text warm/fresh source pair plus
ordinary-call diagnostics and a numeric complete-script control, after focused
semantics/ownership/current-token checks. Separate allocation from CPU, preserve
all controls/adversity and decide retain/refine/withdraw; no speedup prediction
or repeat of unchanged profiling. Full platform/package delivery only if retained.

[Observed final IR](current-shapes.json), [source facts/acceptance](source-assessment.json),
[one-off diagnostic source](CallOperandSourceDiagnostic.cs).
