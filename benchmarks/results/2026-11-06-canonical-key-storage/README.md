# Canonical dictionary/set storage — retained with explicit performance debt

K01 ondb3d1908, measured2026-11-06 on the existing quiet Azure Standard_F4as_v7
Ubuntu24.04.4/four AMD EPYC9V45 vCPUs; .NET11RC3 and CPython3.13.16.
[Full local validation](validation.json):4207internal/16072public per actual11/10,
zero-warning/error Release builds, nine independent CPython3.13.2 goldens. Original
stress, Windows/Ubuntu and installed-consumer K01 qualification is pending.
P01 delivery42135195574 is closed GREEN/all8 and excludes these changes.

The existing native entry array now owns keys, stored Python hashes and insertion
identities. Both containers use it; dictionaries retain insertion order, sets move
payloads on removal. Protocol shadows, dual actual/structural hashes, candidate side
indexes, BCL HashSet storage and per-numeric funding/refund bookkeeping disappear.
Class-slot mutation stays contextual; callbacks distinguish a current entry from a
removed/reinserted object. Positive set-insertion comparisons retain their separate
Python behavior. Copies transfer stored metadata without callbacks. Plain and
contextual queries share the same authority and classify/hash once, without another
key wrapper, query packet, allowance or backend.

Capacity includes metadata/scalar headroom: one column for sets, two for dicts.
Removals keep capacity funded; clear drops set backing/refunds capacity but retains
a shell. Replacement overlap and captures remain admitted. Live namespace/env
bridges retain values and use their actual owners' stored hash metadata. Exact set
operands are borrowed; private materialized working sets clean up after use.
`set(existing_set)` still copies. No tracing, weak reclamation, forcedGC, unsafe
code, execution quotas or retry after refused admission is introduced.

## Controlled comparisons and uncertainty

B307138.91s/five jobs/all gates, core075aaab9 versus P01/core0873:
set bulk−44.83/−44.68%, dataclass−29.16/−26.69%; text+3.65/+2.92%, CSVwarm
+6.11/+4.42% adverse; CSVcompile-run+3.52/+8.11% with4.44% current-role spread.
Preserved under [initial](initial/summary.json). Source found repeated classification
and a contextual dispatcher on plain-key paths. The [bounded refinement contract](refinement-contract.md)
removed that repeated work; no retiming solely for favorable signs.

B308139.06s/five jobs/all gates, coread74e9c3 against the same reference. Two
independent identical-current processes; normalGC/tiering, unchanged protocol2,
five-second warmup/role/job and seven rotated150ms rounds:

| Workload / lane | Reference ms | Current ms | Identical current ms | CPython ms | Two current/reference deltas |
| --- | ---: | ---: | ---: | ---: | --- |
| scripts.text-summary-helpers.large / warm | 2.266291 | 2.312342 | 2.315363 | 1.183696 | +2.03% / +2.17% |
| scripts.csv-summary-functions.large / warm | 4.796584 | 4.778823 | 4.700080 | 3.241489 | -0.37% / -2.01% |
| pure.set-bulk-control.128 / warm | 0.438760 | 0.230653 | 0.230529 | 0.033250 | -47.43% / -47.46% |
| pure.dataclass-key-control.64 / warm | 0.292738 | 0.201266 | 0.204258 | 0.231831 | -31.25% / -30.22% |
| scripts.csv-summary-functions.large / compile-run | 4.955802 | 4.885659 | 4.927392 | 3.411968 | -1.42% / -0.57% |

Retain for corrected semantics, removal of roughly1,500net production lines and
supported set/dataclass gains; do not claim a broad script speedup or allocation/CPU
causation from elapsed measurements. The full suite then exposed repeated fallback
source-span allocation. One immutable diagnostic sentinel corrects it without
relaxing the decimal allocation threshold. Final primary checkpoint below measures
that corrected source; B308 is not a timing claim about later HEAD.

## Final representative checkpoint

B309328.85s/twelve jobs/all gates, final coreadbd1b6b, four complete CSV/text/JSON/
report scripts across compile/warm/compile-to-result. [Raw evidence](checkpoint/observations.tgz)
and [summary](checkpoint/summary.json) retain all results. All workers joined,
fixtures removed and endpoints quiet. Final full suites pass on this runtime
source; subsequent ownership-block indentation is whitespace only.

Final fresh CSV is about1.43–1.45× CPython, text1.80–1.85×, JSON1.12–1.13×,
report1.34–1.40×. Warm CSV+4–6% and text+3–5% versus P01 remain explicit adverse
observations; these differ from B308's flatter CSV. Fresh CSV is flat/mixed, text
improves5–8%, JSON+1–2% is small and report0.7–4.5% has~5% IQR. Process-varying
native hashes/buckets are a plausible contributor, not demonstrated causation in
these runs. Do not compound earlier deltas or erase adverse cases. Retain this
architectural milestone with warm CSV/text debt; the whole-script objective remains
active. Next work must address demonstrated complete-script costs, not just native
controls. No additional micro retiming is authorized merely to change these signs.

[The current guide](../../../docs/BENCHMARKS.md) leads with CPython. Warm clocks
include invocation, mediatedRAM-file I/O, complete validation and prior-result
cleanup; compile excludes execution; compile-run adds compilation/program disposal.
Fixture staging/reset, process startup/transport and final process/program/result
teardown remain excluded. CPython keeps imports across invocations; Lython isolates
execution imports. Startup, genuinely pending-I/O latency, RSS/heap and embedding
latency remain unmeasured.
