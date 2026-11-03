# Current execution CPU source gate — B175

2026-11-03, retained runtime **7c183083**, core
**bf2ddeb1d6adb8b4dccda00e0980225502b3a5a92e8aec019ab753ac3a6e69ed**.
One current diagnostic gate, not a timing comparison or runtime prototype.
Reuse the checked worker/perf protocol: complete synchronous RAM-file text and
JSON-to-Markdown report, six seconds bounded warmup and 2,048 complete invocations
each, ordinary GC/tiering plus diagnostic `DOTNET_PerfMapEnabled=1`. Actual named
active guest TIDs, not a max-CPU heuristic. Both full goldens before/after, exit zero,
quiet gates, fixture cleanup and independently free shared lease pass. Owned service
39.288 s; guest CPU 6.43/10.33 s. Captured times are instrumented, not speedups.

| Named leaf samples | Text /1,277 | Report /2,052 |
| --- | ---: | ---: |
| Shared instruction prefix |195 (15.27%)|168 (8.19%)|
| Function entry selector |13 (1.02%)|22 (1.07%)|
| Monitor enter/exit |72 (5.64%)|218 (10.62%)|
| JSON-module names |0|239 (11.65%)|
| JSON container state machines |0|73 (3.56%)|
| JSON value dispatch |0|29 (1.41%)|
| Async builder start, all paths |0|22 (1.07%)|
| Unknown |81 (6.34%)|296 (14.42%)|

JSON rows overlap; do not add them. Inlining and unknown/native leaves prevent
interpreting these as exact causal costs or upper bounds. Function selector names
do not include all activation costs, but allocation shares alone still do not
justify repeating saved-control/slot-arena designs that measured slower. Full
unfiltered leaf counts and raw mapped stacks preserve all uncertainty.

Monitor stacks associate this work with existing reservation/capacity admission:
nearest report managed ancestors include Reserve74, TryResize58, string copy19,
temporary growth19, operand construction14 and builder finalization10. Text's
nearest ancestors include lower19, read-line19 and list construction16. These are
stack associations, not independent inclusive CPU shares. Do not invent root
headroom tiers, new pools, deferred refunds or weakened admission from this profile.

## Selected milestone B176: one JSON reader between actual suspensions

Current source routes both APIs through recursive async object/array readers and
per-child value dispatch, even when their children complete synchronously. Scalar
strings/literals and default number conversion already have native paths; preserve
that completed work. Do not add another specialized default parser alongside them.

Select a single token-driven reader, with current-container state and admitted
parent storage only when nesting requires it. Run native parsing until an actual
number/constant/object/pairs callback suspends; then resume the same state. Reuse
existing UTF-8 string/number grammars, error helpers, immutable record-key snapshots
and final dict/list construction. Delete recursive ReadJsonObjectAsync and
ReadJsonArrayAsync and their recursive value-dispatch plumbing when replaced.
Do not stage a second member representation: B169 already measured that worse.

This is a coherent parsing/continuation simplification, not a new general VM or
memory subsystem. State must retain input, current keys/containers and callback
borrows through completion; existing shared reservations preflight parent capacity
and replacement overlap, then refund scratch after all actual users. No new quotas,
background tasks, trace roots, cache owners, token capture or speculative retry.
A shallow document must not eagerly allocate a parent stack. Cursor metadata may
add fixed storage; judge complete JSON-lines, not only a deep-container microcase.

Acceptance: whole-document and raw-prefix positions/errors, original Unicode
coordinates, duplicate keys and first-key/last-value/order, pairs-hook duplicates,
mutable/reentrant options, nested key snapshots, numeric/custom hooks and real
suspension, retained aliases, cancellation in native scans and callbacks, terminal
refusal before publication, pending-borrow cleanup and bounded shared-budget reuse.
Keep supported depth diagnostics and managed-stack admission; any depth-contract
change needs an explicit decision rather than silently expanding or weakening it.

Before coding, record the complete replacement and lifetime invariant in PLAN.md.
Focused semantic/cancellation/ownership Release first, then one controlled complete
CSV/text/JSON/report/config comparison under BOTH APIs with unchanged controls,
allocation and accounted peaks. Retain/refine/withdraw from those observations;
explicitly decide carried B174 CSV and B171 report debt. No predicted speedup or
fixed improvement veto. General activation/scalar/frontend work stays open; this
parser alone cannot explain or close the CPython gap. No additional unchanged CPU
capture selected. This gate ends in that concrete milestone, not a profiling campaign.

[Full leaf assessment and nearest monitor ancestors](assessment.json),
[raw samples/maps/stacks/worker records](observations.tgz), [identity](identity.json).
Archive retrieved and SHA-256 verified. At cleanup, ordinary archived staging
was already absent for an unmeasured reason; remaining raw profile staging was
removed after verification, with lease free. Runtime
internal native signatures in profiler maps do not add unsafe code to Lython.
