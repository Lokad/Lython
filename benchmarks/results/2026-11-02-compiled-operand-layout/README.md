# Compiled operand layout and linear execution — B132

2026-11-02. Retained normal-engine redesign, baseline3bbeab0f/core16e38f84;
measured candidate84302e8d7ba628758703c713d1243851c106ea9735eac774a27887fa4a6fe735.
No public API, syntax, alternate backend or new runtime mode.

The compiler checks stack shapes, prepares a whole-frame maximum and protected-entry
depths, then retains one instruction array with absolute branch/exception ranges.
Native expression links reference that same stream; current fast functions and
actual asynchronous operations remain. Temporary compiler blocks are discarded.
An explicit Complete instruction preserves implicit module/import completion.

Removed runtime operand resizing/replacement admission, per-frame block-entry depth
arrays/updates, current-block position and nested block/instruction drivers. No old
path remains dormant. Shape analysis is new compiler work; this is not a net source
line reduction. Unlike a register renaming or shared buffer, this removes runtime
mechanisms without moving operand views into another dispatch layer.

Contract tradeoff: admit the conservative whole workspace before construction,
including generators before first advancement. An unused branch can cause earlier
denial. Same shared allowance and deterministic owners; no cap/hidden allowance or
retry. Values/cursors/aliases and pending operations remain funded through actual
completion/awaited cleanup. Large native argument packets retain separate preflight.
Current instruction/operation cancellation and managed nesting remain; compilation
retains documented phase boundaries, with no new in-pass interruption guarantee.

## Why this reconsideration is justified

[B66](../2026-11-01-static-operands-unqualified/README.md) timed out at its original
90-second deadline, with no usable timings or normal managed-cleanup proof. Those
conditions/results remain unchanged. This is a current-source reimplementation,
including shared iteration, native helper forwarding and structured fallthrough;
no stale patch is restored wholesale. Current formatted-output shapes replace the
obsolete join operation. Missing measurements do not establish a failed design.
[B132's plan](pre-code-plan.md) names the removed implementation and decision scope.

## Focused acceptance

4,031 internal and6,855 selected public Release cases PASS on each actual.NET11RC3
and.NET10.0.8. Functions/keywords/cells/introspection, class/type protocols, annotations,
imports, comprehensions/generators, protected control flow, genuine suspension,
exception/traceback order, cancellation, terminal refusal and governed ownership
retain their assertions. [Census](census.json) confirms no retained block graph;
representative module maxima3–9, helper maxima2–5; report helper uses the full engine.

Initial failures are preserved. Old growth fixtures supplied an undersized buffer
or expected lazy admission: supply their declared capacity, keep all reference and
failed-disposal assertions, and assert full-workspace refusal before the first
observable marker/operator. Generator workspace is now already charged at creation;
retained result and close-refund assertions remain. Ten broader failures identified
unreachable syntactic handler tails after unconditional raise. The compiler drops
only regions without a reachable operand entry; existing69 cancellation/refusal
controls then pass. No semantic assertions/timeouts were relaxed.

## One controlled comparison

Quiet Azure F4as_v7 Ubuntu24.04.4/.NET11RC3; ordinary workstation GC/tiering.
Existing worker/protocol, four-second warmup/image, nine interleaved~200ms batches,
independent original/candidate/identical-original processes, full source/fixture/
stdout/file identities. [All observations](raw-timing.tar.gz), including warmups
and drifting/adverse controls, remain intact. Nine jobs171.339s, owned service
exit0/inactive, workers joined, fixtures removed, lease free; both quiet gates PASS.

| Complete case / phase | Elapsed change | Process CPU | Unchanged elapsed / CPU |
| --- | ---: | ---: | ---: |
| CSV / warm |+0.00%|−0.95%|+3.54% /+5.44%|
| Text / warm |−1.02%|+0.52%|+0.98% /+1.25%|
| JSON-lines / warm |+0.05%|+0.00%|+2.66% /+6.08%|
| Report / warm |−1.07%|−1.66%|−0.68% /−2.11%|
| Range / warm |−7.67%|−9.99%|−3.75% /−4.74%|
| CSV / fresh compile-to-result |+0.34%|−6.28%|+1.73% /−3.58%|
| Text / fresh compile-to-result |−0.57%|−6.66%|+3.39% /−2.63%|
| CSV / compile only |+1.49%|+2.08%|−0.56% /+1.55%|
| Report / compile only |+2.09%|+2.07%|−0.18% /+0.94%|

Range is about4% lower elapsed and5.5% lower process CPU relative to its control.
Complete-script points are mostly flat/mildly better against drifting controls;
report tracks control. No substantial general whole-script CPU gain is established.
Compilation costs about2% more elapsed against stable controls. Accept that explicit
debt for the completed runtime simplification; fresh whole jobs do not show a
corresponding regression. Further work must target remaining whole-script costs.

Warm excludes compilation/startup; fresh includes each compilation/disposal. Both
include fixture reset/staging, actual efficient mediated I/O, full output/file checks
and prior-result cleanup. Final result/program/process/fixture teardown is outside
batches and joined. Compile-only executes no guest work; final compilation disposal
is outside its clock. External TGID CPU includes runtime/JIT/response, not guest-only.

Separate four-job allocation diagnostic uses192 warmups/image,9x32 complete public
runs, isolated images/shared CoreLib/GC, full goldens and zero final balances.
Report9,754,396.5→9,606,704.5 bytes/run (−1.5141%, unchanged−0.0003%); CSV/text/JSON
changes below0.004% are immaterial. These diagnostic clocks do not override the
timing run. Service53.835s exit0/inactive, gates/hash witnesses/cleanup PASS.
[Raw allocation](raw-allocation.tar.gz), readable drivers and validation logs preserve
all evidence; no external CPython ratio follows from this source pair.

Full Windows/Ubuntu ordinary.NET11/10, original Stress and installed-package
consumers are batched at this delivery. Exact060a8359 already qualifies preceding
iteration/forwarding; this source needs its own checkpoint. Publication postponed.
