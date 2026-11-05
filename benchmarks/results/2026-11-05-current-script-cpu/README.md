# Current complete-script CPU/source gate

2026-11-05, committed **7dcc3b48**, frozen final core **a4ce2b48**. Its full
Windows/Ubuntu, actual.NET11/10, original stress and installed-package checkpoint
42014687629 is GREEN ([cached receipt](../2026-11-05-executable-targets/delivery-ci-status.json)).
No production prototype or release. The performance objective remains active.

One **39-second** owned diagnostic, within45s: text summary and ordinary-helper
CSV,1536/1024 complete invocations after six guest warmup seconds each. Quiet
F4as_v7/Ubuntu24.04.4/.NET11RC3; ordinary GC/tiering, only PerfMapEnabled diagnostic
instrumentation. Existing checked workerb94 and independent full CPython goldens.
Both endpoints quiet, before/after full output/files correct, actual guest TIDs
identified, perf/workers joined, Closed/exit0/final reservations zero, fixtures
removed and exclusive lease released. Offline symbol export follows under the
lease after all workers close. This adds no production dependency/instrumentation.

## Selected guest CPU leaves

| Leaf | Text,783 samples | Ordinary CSV,1022 samples |
| --- | ---: | ---: |
| Main instruction prefix |162 /20.69%|110 /10.76%|
| Interface type test helper |53 /6.77%|42 /4.11%|
| Ordinary call selection |36 /4.60%|not among first24 leaves|
| Target instruction execution |26 /3.32%|not among first24 leaves|
| SetSubscriptValue |33 /4.21%|not among first24 leaves|
| Monitor enter/exit |26 /3.32%|63 /6.16%|
| CSV NextAsync / Feed / FinishRecord |—|48 /31 /29 samples|
| BeginPendingCall |not among first24 leaves|17 /1.66%|

These are sampled leaves, not removable or additive inclusive costs. Guest CPU
was3.94/5.15s. Unknown symbols are **40/87 (5.11/8.51%)**; each saved perf map has
one truncated final line, preserved in full. Ordinary collector reports125492
wakeups for1037 total process samples; keep this instrumental condition visible.
No elapsed speedup, allocation/live-heap, RSS, startup or embedding-latency claim.
Small percentages are not precise prioritization thresholds.

Lock first-managed callers include dictionary initialization, FinishRecord,
UTF8 allocation, bound argument storage and invocation cleanup. Shared allowance
synchronization is real; the sample does not establish it as the dominant cost
or justify another reservation cache/tier. The inclusive classifier initially
matched parameter types as method names; the corrected method-prefix analysis
and [prior derived result](inclusive-assessment-before-method-filter.json) remain.
No measurement rerun was needed for that offline correction.

## Source assessment and selected work

CSV fields/row arrays are already admitted together; parsing already bulk-scans
UTF8. Native dictionaries already have one backing. Do not propose those completed
changes again. Native helper returns and shared argument/local backing also remain.
The sampled context/interpreter allocation share from D36 is not CPU dominance,
and older slower call-arena/control records retain their lessons.

One concrete remaining boundary: native text-file iteration creates an owned
line PyString/byte array, then CSV Feed copies it into its own record buffer.
That intermediate line never reaches guest code. Select **owned CSV input
transport**: append a complete decoded physical line into the existing parser
builder, truncate to the decoded prefix and compact through the same Feed logic.
Truncate already retains backing; Feed cannot expand beyond consumed input.
No file-window borrow escapes the read call and no new ownership layer is needed.
Actual buffer/position/failure proofs and comparison still precede implementation.
[Selected deletion/compatibility/acceptance contract](selected-input-contract.md).

This source gate is closed. No further profile campaign, universal tagged values,
general guest tracing, budget-capacity cache, new dispatcher or arena follows from
these samples. Native input removes real objects/copies/admissions; its magnitude
requires a separate bounded comparison with semantic and ownership checks.

## Preserved failed and corrected conditions

B268 failed its immediate pre-gate at6% CPU, exit3, **no guest/perf samples**.
[Exact original condition](failed-condition.tgz), [receipt](failed-condition.json).
B269 keeps the thresholds, adds5s settling before each quiet gate and moves offline
symbol analysis after worker cleanup. Both captures retain normal GC/tiering.
The successful capture is not a retiming of the completed elapsed benchmark.

Offline archival initially encountered root-owned0600 perf.data. Only the two
completed owned capture files changed ownership; raw contents/capture conditions
were unchanged. Offline readers then run unprivileged. These corrections and one
failed local staging command are recorded in [validation](validation.json).

[Pre-capture contract](pre-measurement-plan.md), [identities](identity.json),
[text leaves](text-leaves.json), [CSV leaves](ordinary-leaves.json),
[inclusive analysis](inclusive-assessment.json), [analysis source](analyze-inclusive.py),
[source/maps/raw/gates](observations.tgz), [validation](validation.json).
Archive SHA256 `1b54118151ce5f0ded87a81a51d06829303d59a255aea82ba2e079596cd5ddd5`.
