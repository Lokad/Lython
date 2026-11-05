# Current runtime-cost source gate

2026-11-05, source6c776d0d, observed core66263582 (built before commit metadata),
workerb94. Full local4125internal/16004public per actual.NET11/10 pass;
Windows/Ubuntu/stress/installed-consumer CI42044419339 is GREEN/all8,
[terminal status](../2026-11-05-executable-temporary-contract/delivery-ci-status.json) cached.

One39-second owned capture, within45s, after six guest warmup seconds per case.
Same complete text/ordinaryCSV scripts and independent output/file goldens,
actual mediated /dev/shm I/O and cleanup. Normal GC/tiering; diagnostic perf-map
instrumentation only. Quiet endpoints, exclusive lease, actual guest TIDs,
Closed/final-zero funding, profiler/worker joins and fixture removal pass.
Raw evidence uses persistent working storage; no sole evidence in RAM fixtures.

| Sampled CPU leaf | Text,723 samples | Ordinary CSV,1017 samples |
| --- | ---: | ---: |
| Main instruction prefix |157 /21.72%|125 /12.29%|
| Ordinary call selection |41 /5.67%|—|
| SetSubscriptValue |34 /4.70%|—|
| Whitespace forward range collection |27 /3.73%|—|
| AppendWhitespaceRange |14 /1.94%|—|
| Native CSV NextAsync |—|49 /4.82%|
| Native CSV Feed / FinishRecord |—|33 /27 samples|
| CreateExecutableInterpreter |—|15 /1.47%|

These include real Python work. Inclusive split construction142/723 (19.64%),
ordinary call92/723 (12.72%)/76/1017 (7.47%), native evaluation175/723 (24.20%)
overlap and are **not removable or additive overhead**. Unknown57/723 (7.88%) and
89/1017 (8.75%), one truncated map line each, remain in the archive. Ordinary perf
reports118390wakeups/1032total samples; text3/744. This is diagnostic source
attribution, not an elapsed/allocation/RSS/embedding-latency or causal before/after
claim. The old interface-test leaf is no longer among the leading sampled leaves;
that alone does not quantify the preceding nominal-contract improvement.

## Source decision

Instructions already have one compact opcode/span/five-int representation.
Arguments and local/operand slots already share admitted backing. Simply merging
context/interpreter objects moves their fields and changes closure retention;
neither construction allocation share nor this CPU capture justifies calling it
the dominant cost. The slower B61/B69/D46 call-control/arena/transport results
remain actual adverse results. Do not add another budget/argument arena or
replace the existing managed stack policy on these observations.

One demonstrated redundant traversal remains in bulk text construction: the fused
lower/upper whitespace split decodes scalars to find word ranges, rescans each word
to estimate conservative cased capacity, then writes final words. Select one
coherent **range-and-capacity traversal**: derive the same non-ASCII allowance
from widths already decoded, remove the per-word capacity scan and unused
source/casing parameters in range append/reverse collection. Keep standalone case
sizing and the actual Unicode writers. No shared-fragment representation, ASCII
scanner, per-string fields, cache, new owner or strategy is needed.

The [pre-implementation contract](selected-text-contract.md) sets Unicode,
cancellation/admission, ownership, complete-script control and stopping criteria.
Its magnitude needs a separate comparison. This gate is closed; the broader
ordinary call/frame/operand redesign and performance objective remain open.

[Raw source/maps/perf/gates](observations.tgz), [identities](identity.json),
[inclusive attribution](inclusive-assessment.json), [validation](validation.json).
Archive SHA256 `2dcea8ccd6c5b076a8aab01d5af09bc8650bc4e61c715780b4deb235d3c502ec`.
