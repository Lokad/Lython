# Final-slot function binding

2026-11-15. Retained candidate frozen **8fbfa3f0** versus qualified
**a7ab41f5 /716176a8**. [Full changed-runtime platform/stress/package qualification
passes](delivery/README.md). Publication remains held.

Function binding now uses the existing unassigned-slot sentinel rather than a
parallel ArgumentPresence. Python None remains an ordinary non-null guest value.
Remove function presence creation, marking loops, >64-parameter overflow arrays,
the allocation-failure shell used solely for that overflow, and presence transport
through completed binding, native entry and continuation. Completed arguments carry
only final storage, logical parameter count and their existing owner. Builtins
retain their necessary object-array omission metadata. No new cache, dispatcher,
allowance, quota or ownership policy; preflights, aliases and awaited cleanup remain.

The new public narrow/65/130-parameter controls also expose an existing Python
bug: a starred positional value could overwrite a previously supplied keyword.
Qualified7161 and the first migrated source fail the same three cases; CPython
3.13.2 rejects them. The shared dynamic fill now checks the destination before
writing. Keep None/omission, replaced defaults, failed binding, clean reuse and
budget-baseline assertions for both hosting APIs. No public API change beyond
correctly rejecting those duplicate arguments.

Focused Release: **452 internal /2,396 public on each actual .NET11/10** pass,
covering binding/default protocols, methods, closures/generators, real suspension,
cancellation/refusal, stack protection, aliases, transfer and cleanup. One initial
native-call-continuation fixture exit7 is preserved. Direct reference/candidate
reproduction and subsequent focused checks pass; cause remains unestablished.
Failure messages now include existing fixture stdout; no assertions, stack sizes
or deadlines changed. Initial test-string/preparation errors occurred before their
affected checks/collection and were corrected without changing runtime policy.

The first eight-job local Windows comparison completes122.508s, two independent
processes per source, 2.5sfull-request warmups and five rotating~.15srequest batches.
All goldens, ownership, close, join and fixture-cleanup gates pass. File search is
lower across pairings, but report reaches+23.72%with a moving15.2%candidate control;
fulfillment/text/JSON/observed controls are mixed. Short helpers+0.49–2.48%is a
recorded local debt accepted for the state removal and correctness correction.

That uncertainty justifies one changed-measurement confirmation on the same
verified images: three affected whole scripts,12sfull-request warmups and~.35s
batches. File search−9.66..−2.36%with a moving reference; report−3.96..−1.72%with
closer controls; fulfillment−2.70..+1.44%mixed. The report adverse condition is not
reproduced here; do not erase it or infer warmup as its proven cause. Retain the
architecture without claiming a portable CPU or whole-script allocation gain.

Both runs preserve mediated I/O, full output/file verification and prior-result
cleanup; fresh adds compilation/compiled-owner disposal. Startup/reset/protocol/
final-cache teardown are excluded from elapsed clocks. No quiet-VM, refreshed
CPython, heap/RSS or embedding-tail claim. No new benchmark framework or VM users.
The broader whole-script objective remains open after this construction milestone.

[Decision, every median and adverse range](summary.json),
[exact589source/build inputs, core, focused logs, reference failure, CPython controls,
raw measurements and procedures](sources-checks-and-observations.tar.xz).
Image manifests reconstruct unchanged auxiliaries from the prior
[shared-prefix owned archive](../2026-11-15-shared-prefix-prototype/owned-metadata-refinement.tar.xz)
and reference core from its
[initial archive](../2026-11-15-shared-prefix-prototype/initial-and-routing-refinement.tar.xz).
Every component was independently hash-verified before retaining this evidence.
