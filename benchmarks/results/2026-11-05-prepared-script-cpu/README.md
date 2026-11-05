# Current prepared-runtime text/report CPU attribution

2026-11-05, source **8475a8dd**, frozen **81deb3be**. Full delivery41958212610 is
GREEN on exact8475a8dd: Windows/Ubuntu actual .NET11/.NET10, original Stress both
targets and installed Release consumers. Its terminal receipt is cached; no
unchanged qualification was replayed for this diagnostic.

Quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3, normal GC/tiering. Reuse the existing
checked worker/protocol2 and perf199Hz/frame-pointer collector for the unchanged
complete text summary and JSON-to-Markdown scripts.6s guestwarmup,2048invocations
each; named guest CPU5.57s/8.11s,1,108/1,613samples. Owned service36s. Exact loaded
core/libraries, before/after stdout/file goldens, quiet endpoints, normal worker/
collector exits, joined users, removed RAM fixtures and free lease all pass.
Persistent maps/raw/logs survive service teardown; archive reads root-owned perf
data under sudo. Only the CLR symbolization-map setting differs from ordinary runs.

This is current attribution, not a source-pair elapsed/CPU improvement or a new
CPython comparison. Earlier5c profiles do not attribute this source. Compilation/
startup/real pending IO/RSS and new public budget-balance proof are outside scope.
Frame-pointer ancestors can be incomplete and methods inlined. Unknown4.33%/7.32%
and one incomplete trailing map line per capture remain in the raw evidence.

| Current stage | Text leaf / inclusive | Report leaf / inclusive |
| --- | ---: | ---: |
| Native dynamic proof |1.35% /1.62%|7.32% /9.86%|
| Native evaluation |1.62% /24.55%|6.76% /36.76%|
| JSON |—|15.81% /29.39%|
| Shared primitive formatting |—|4.84% /16.00%|
| Numeric formatting |—|3.41% /5.83%|
| Whitespace construction |8.39% /21.21%|—|
| Unicode casing |4.15% /5.69%|—|
| Ordinary call |4.24% /9.84%|0.37% /36.08%|
| Subscript store |4.87% /6.14%|—|

Inclusive groups count unique samples per group and overlap useful work; do not
sum them or predict removable CPU. Instruction-prefix leaf is188/1108(16.97%)
text and101/1613(6.26%)report. Scalar classification has only15report leaf samples
(0.93%); boxing/type totals alone do not justify a universal representation change.
Classifiers inspect actual declaring owners/methods, excluding parameter-type
matches. [Counts/callers](current-attribution.json), [offline reader](analyze-current.py).

## Source decision and next bounded milestone

Monitor enter/exit has37text/163report leaf samples(3.34%/10.11%). Report callers
include MemoryGovernor.Reserve52, dictionary initial storage39, root reservation
resize36 and string allocation24. The standalone allowance ALREADY uses one shared
gate for scopes/reservations and iterative disposal. There is no demonstrated
duplicate-lock policy to remove. Keep mandatory shared funding/preflights; do not
reopen headroom-credit/quantization or the failed B198 owner-capacity design.

Current primitive formatting already writes directly into native output; no new
per-character CLR-string or truncated-PyString removal is available here. Dynamic
field proof/read snapshots cannot be cached across later admission/effects. Avoid
another getter/argument-packet/cache/JIT proposal from these overlapping totals.

Select **whole formatted-string construction capacity planning**. Multi-part
formats currently start an empty builder, despite compiled literal bytes and field
widths; ordinary and native paths share that builder. A modest initial capacity
hint from existing static layout can remove intermediate arrays/copies and their
reserve/refund transitions while preserving every actual field read and formatter.
The builder still admits its retained capacity/replacement overlap and grows under
the borrowed allowance. No new owner, governor credit, token cache or public cap.
This is an unimplemented hypothesis, not an allocation or elapsed claim.

Before code, record conservative earlier-admission tradeoffs, a bounded initial
hint (large/invalid widths must not cause speculative huge allocations), immediate
terminal refusal, post-admission cancellation before allocation and constructor
rollback. Reuse parsed specifiers once, patch only builder-construction metadata
and preserve original value/callback/generator/exception order. Focused semantics/
ownership/cancellation then one bounded complete report/numeric/control comparison;
explicit retain/debt/withdraw. No local threshold-tuning series or performance forecast.

[Premeasurement plan](plan.md), [identity](identity.json), [raw](observations.tgz).
SHA256 `571898ab1783c83f497984db3f2fb4b639b11c6284f103b016a1d04555803a59`.
Before launch, increase only the old diagnostic warmup safety bound64->128 batches:
the current2.72ms×32×64≈5.57s could not reliably meet6s. No failed capture or samples
were hidden; guest execution policy and runtime controls remain unchanged.
