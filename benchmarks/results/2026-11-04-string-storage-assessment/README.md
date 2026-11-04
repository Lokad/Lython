# String storage assessment and retained cache simplification

2026-11-04, source f74b5193 (ae9b82b7 runtime) plus the archived patch.
Retained frozen core454cd98c3a16d303290f08052089130574d5b914bffa8c07d8251b10f3e9b719;
qualified baseline67373996 is ae9b82b7. Exact source snapshots/hashes are in each
raw archive. General string windows and compact split payloads are withdrawn.

## Retained implementation

Delete PyString's cache-charge accumulator and its mutating helper. Its one
optional rune-offset table describes its own retained capacity. Allocation
reserves before construction, commits after success, and releases reservation
on construction failure. Actual object metadata falls72->64B on both.NET11/10;
this is an actual-object allocation probe, not just a layout model. Standalone
strings, independent split payloads, lifetime rules and conservative estimates
remain unchanged. No shared backing owner, window factory/flags/offset fields,
new policy, public API or dormant alternative representation remains.

Whole-job allocation versus B203, separate existing three-image driver, both APIs:

| Complete job/control | Run | RunAsync |
| --- | ---: | ---: |
| csv-summary-helpers | -2.74% | -2.54% |
| text-summary-helpers | -4.07% | -4.06% |
| json-summary-helpers | -2.99% | -2.99% |
| report-render | -1.47% | -1.48% |
| config-edit | -3.58% | -3.57% |
| scalar-above-range | -0.01% | -0.01% |
| fixed-text-padding | -2.19% | -2.18% |
| fixed-text-slices | -3.78% | -3.78% |

All eight admitted execution/root peaks match baseline on both APIs. Smaller
physical objects do not imply smaller admission estimates, RSS, heap or embedding
latency. Allocation benefits are not additive with earlier source-pair results.

## Final elapsed observations and explicit debt

Quiet Azure F4as_v7, Ubuntu24.04.4, four EPYC9V45 vCPUs,16GiB/no swap, pinned.NET11
RC3, normal GC/tiering. Same existing checked workers/protocol2, persistent evidence
and RAM guest fixtures. Warm clocks include invocation, mediated RAM-file I/O,
full checks and prior-result cleanup. Compilation/startup, fixture staging/reset,
response hashing and final cached teardown are excluded; all users are joined.
No unaffected frontend or external CPython campaign is repeated. External ratios
in docs/BENCHMARKS.md still describe ffc01134, not this candidate.

Six-job old/candidate/old collection:6s warmup, nine rotating~200ms samples;
202.521s capture/203.135s owned service,420s bound. All goldens, balances, identities,
quiet gates, normal closes/zero exits and joins pass. Negative is faster:

| Whole job | Candidate vs old first / last | Old-role spread |
| --- | ---: | ---: |
| text warm | +4.02% / +8.25% | -3.90% |
| text warm-async | -5.67% / +5.47% | -10.56% |
| report warm | -1.81% / -1.70% | -0.12% |
| report warm-async | -1.50% / -0.83% | -0.68% |
| JSON warm-async | -0.25% / +0.60% | -0.84% |
| CSV warm | +0.56% / -1.79% | +2.39% |

Large identical-source text drift justifies one bounded unchanged-source
measurement correction:20s warmup/process,11 rotating~300ms batches, two text jobs,
reversed startup order on the second API.151.142s capture/151.743s service below240s;
all original gates/goldens/balances/normal exits/joins pass. No allocation replay.

| Text confirmation | Candidate vs old first / last | Old-role spread | Candidate IQR |
| --- | ---: | ---: | ---: |
| warm | +3.49% / +5.84% | -2.22% | 0.65% |
| warm-async | -5.90% / -2.91% | -3.09% | 1.14% |

Retain the smaller, simpler accounting representation with synchronous-text
3.5-5.8% elapsed regression accepted as explicit debt. Async text improves under
confirmation conditions; JSON/CSV are control-limited and report small. There is
no broad CPU-gain claim: process CPU ticks are a separate coarse diagnostic.
The original +4..8% sync and inconsistent async points remain valid observations.
B203's separate prior synchronous-text debt is not closed; do not add independent
deltas. Next completion milestone is the coherent operand/call transport redesign,
which must compare complete text against this exact source and decide remaining
debt. No unchanged retiming is scheduled without new evidence of a measurement issue.

## Withdrawn window representations and preserved failure evidence

Initialbe5dd48f embedded managed ArraySegment windows and one compact split
payload.322.363s capture/322.990s service, eight four-image jobs,6s warmup,nine
rotating samples. It reduces text allocation4.73%, but text/JSON/report costs of
about2-4% remain. It does not prove CPU improvement or close B203 debt.

Refined801ab677 uses managed raw buffer/offset/count, same metadata72B and same
4.73% allocation reduction.303.304s capture/303.926s service, six five-image jobs
including both old controls, the initial candidate and earlier B201 reference.
Sync text nearly flat (+0.18/+0.31%), but JSONasync+2.44/+2.08% (old spread0.35%)
and reportsync+5.14/+6.03% (old spread0.84%) do not justify its general access and
ownership costs. Both collections pass original goldens/balances/gates/joins.
Initial/refined source and every adverse observation are retained. Reconsidering
requires a materially different cost/work-elimination design, not a permanent ban.

The raw-buffer refinement first missed two replacement copies' logical offsets;
two public cases failed. Corrected through UTF8 span slicing and an independent
empty-pattern replacement assertion. The failure/source and corrected checks
remain. Final restored-copy ownership test initially double-counted backing;
its original1failure/443pass log remains, then a layout-independent funding
assertion restores the intended invariant. No production guarantee was relaxed.

## Focused correctness and delivery status

Final nonincremental Release build:zero warnings/errors.444 internal/2,667 public
per actual.NET11/10 PASS. Useful semantic tests remain independent of withdrawn
layout: Unicode/NUL/index/slicing/hash/collections/format/JSON, genuine delayed
file write/awaited close, retained-result funding, private-word refunds/cache
funding, regex pattern/subject/replacement/flags/captures/scalar windows and original
subject identity against the normal optional0.4 adapter. Trusted CPython3.13.2
confirms the script goldens; this is semantic validation, not backend timing.
Existing cancellation/refusal/rollback/shared-budget checks remain.

Final retained commitbf0d38f3 is fully qualified in
[cached run41807227386](delivery-ci-status.json): Windows/Ubuntu ordinary actual
.NET11/10, original stress on both runtimes and installed Release consumers,
all eight gates GREEN. Baselineae9b82b7's independent41793317194 receipt remains
historical. No unchanged qualification replay. Publication postponed.

Raw archives preserve plans, exact source, every sample, machine gates and worker
identities; focused-qualification.tgz preserves all original/corrected logs/TRX,
actual-shape probes, independent CPython goldens and useful retained test source.
