# Complete file-search call/construction assessment

R377/B769, 2026-11-15, ec6c0416 / frozen6164a933. One5s cpu-clock/fp capture
at199Hz after8guest-seconds warmup, unchanged complete protected file-search
script. Diagnostic JIT/perf-map overrides are explicit. This is CPU attribution,
not an elapsed comparison, CPython refresh or removable-share estimate.

746 guest leaves /3.79guest CPU seconds;149unknown (121kernel), zero ambiguous.
Ordinary dispatch71, StartNativeCall61, NativeRunCore49, TryExecuteNativeCall46,
input dispatch42, ForNext32 and NativeCall30 are substantial leaves; StartInline20
and managed stack guard19 remain visible. Nearest owners include89admission,
52file/iteration and49comparison-host path-resolution leaves. Wider thread CPU,
including1.20s thread-pool work, remains in raw evidence. Buckets overlap and
contain useful/inlined work; no speedup follows by summing them.

## Actual construction and displacement assessment

The helper already consumes evaluated executable operands into final typed slots.
Small native calls use one fixed managed16-slot buffer; bound calls reuse their
funded local array. Direct receivers, definition-owned binding layout, live defaults,
deferred contexts and exact suspension transfer already exist. StartNativeCall
still initializes working storage and binds the keyword-only default in this script.
These are real costs; the profile does not isolate a removable whole activation.

An exception-shell-only edit is declined: NativeRun itself has6leaf samples and
its snapshot/context attachment also serves protected-body transfer and nested
handler state. Removing it alone would be another narrow change, with no evidence
of a substantial whole-script benefit. Do not remove body-local managed stack
admission or equate logical depth admission with sufficient stack at a deeper body.
Current guards and actual callback/suspension/cleanup boundaries remain intact.

No call arena, new controller, duplicate binder, tagged-value rewrite, cached mutable
defaults or arity-specific frame families are selected. Previous slower arena/control
designs retain their evidence. A coherent replacement must eliminate the full
activation/transport work, preserve isolation, aliases and live default changes,
and displace its original entry/cleanup implementation. The present capture is
useful evidence for that roadmap, not proof that existing costs are irreducible.

## Record and text source gate

The historical6–8%bulk CSV opportunity is already delivered: final capacity/ambient
construction, duplicate/custom/short/wide behavior, funded array adoption and shared
record keys remain. [Earlier source assessment](../2026-11-10-record-construction-assessment/README.md)
explains why replaying B74 would duplicate production code. Header identity walks
before/after admission remain necessary under reentry; no global list-version
infrastructure is introduced to avoid them.

The parser already compacts native input in its owned working buffer and admits
row/field construction together. FinishRecord still copies independent field
payloads. Most ordinary short fields fit inline8byte storage; a shared row payload
would retain aggregate capacity and add window shells for those fields. The
[withdrawn raw-range design](../2026-11-14-owned-csv-field-ranges/README.md) removed
a copy but added position state/raw syntax retention, with flat allocation and
weak whole-script benefit. It remains a contextual tradeoff, not a permanent ban.
Do not prototype another range inventory without a changed ownership/storage
argument and a representative workload exposing that remaining copy cost.

The source gate closes by declining these unsupported patches. The broader
performance objective remains active: whole activation/transport and long-field
construction are roadmap questions with explicit semantic and measurement gates,
not claimed completed optimizations. No runtime, API or dependency change here.

All identity/golden/quiet/join/archive gates pass; traces were persistently archived
and verified under lease before RAM unmount. Initial staging refused the unchanged
254MiB floor without mutation. Two exact committed archived raw JSON duplicates
were then deduplicated with fresh local/Git/remote/member/path/UID/link proofs;
all compressed originals/images/toolchains remain,254/252MiB floors unchanged.
Original refusal and corrected custody receipts are preserved. No VM users remain.
Full runtime43669461343qualification proceeds independently; publication held.
