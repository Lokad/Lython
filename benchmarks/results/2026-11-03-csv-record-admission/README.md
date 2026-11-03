# CSV record storage and admission — B150 retained

2026-11-03. Baseline3662fc56/core3b5b9c67 versus the recorded source patch/core87f3ce5b.
Both use identical helper/adapter bits and public scripts. Source-pair evidence,
not a new CPython ratio. Quiet F4as_v7 Ubuntu24.04.4/.NET11RC3, ordinary GC/tiering.
Exact [identity](identity.json), [source patch](source.patch.gz) and frozen catalogues
identify the measured implementation, not an unspecified future HEAD.

The existing parser now stages record bytes and native field descriptors, then
admits text payloads together before constructing the output row. It removes
per-text-field reservation/finalization and the object-valued staging-list copy.
Numeric conversion remains at its field boundary, preserving error/source effects.
Output strings remain independent copies. No global string representation, second
parser, cache, pool, ownership subsystem or public API is added.

| Complete workload | Initial elapsed Δ / unchanged control | Confirmation Δ / control | Allocation Δ / control |
| --- | ---: | ---: | ---: |
| Canonical CSV, warm |+2.05% /+0.74%|−2.45% /−0.58%|−4.54% / about0%|
| Canonical CSV, compile-run |+0.43% /−0.57%|−3.49% /+3.91%|Not repeated|
| Variable-width CSV, warm |−10.59% /+0.27%|−10.12% /+2.10%|−13.55% / about0%|
| Quoted multiline CSV, warm |−2.93% /+0.57%|Not repeated|−7.12% / about0%|
| Long-field CSV, warm |+0.26% /−0.26%|Not repeated|−0.06% / about0%|
| Unchanged JSON, warm |−0.78% /−2.41%|−2.70% /−2.05%|Not measured|
| Unchanged report, warm |+1.04% /+1.82%|Not repeated|Not measured|
| Unchanged report, compile-run |−3.71% /−3.83%|Not repeated|Not measured|

Variable-width process CPU is also lower:−10.05%/control+0.53%, then−11.27%/
control+0.77%. Canonical observations vary; they establish neither a precise CPU
gain nor a persistent regression. Initial compile-run CPU controls drift about22%,
and confirmation elapsed control drifts3.9%. All adverse observations remain.
Do not attribute unchanged JSON/report movements to this CSV-only change.

Retained for the consistent wider-record benefit, lower allocation and fewer
admission transactions. No fixed%acceptance veto. Canonical timing uncertainty
remains explicit; no additional confirmation is needed for this milestone.

Working storage is the tradeoff. Multi-row fixture accounted execution/aggregate
peaks rise16–112B. A separate **single32-column/61,716B input record** shows the
larger envelope: execution185,296→248,944B and aggregate root283,888→347,536B,
an additional63,648B. This local Windows.NET11 diagnostic uses the same frozen
images/goldens and existing driver; its elapsed numbers are not performance evidence.
These are conservative admission counters, not CLR residency or RSS. Allow headroom
for a record buffer alongside output payloads; tight allowances can refuse earlier.
Capacity/replacement overlap stays admitted, and scratch release cannot refund
retained rows. No hidden allowance or policy ceiling is introduced.

Initial eight timing jobs181.063s; bounded same-source confirmation101.576s. The
initial owned timing/allocation service207.810s and confirmation102.176s are terminal;
workers/fixtures joined, observations retrieved and shared VM lease free. Before/
after quiet gates and complete stdout/file goldens PASS. The new multiline/long
fixtures also verify under isolated VM CPython3.13.16, separately from performance.

Command-v2 timing excludes fixture reset/staging, includes real guest I/O, full
output checks and prior-result cleanup. Compile-run also compiles/disposes; warm
reuses compilation. Startup is excluded. External process CPU includes reset,
transport, GC and JIT; it is not a guest-thread-only measurement. Allocation/peak
diagnostics use isolated images sharing CoreLib/process GC, include fixture reset,
and separately verify zero balances, image/hash isolation and complete goldens.
Peaks are sampled on one untimed initial invocation before measurement.

Focused Release checks on actual.NET11/.NET10:101 internal/413 public per target
PASS; nine numeric/default-budget cases rechecked after preserving inherited-budget
coverage. Six initial white-box failures reflected removed field storage and changed
allocation timing; original logs remain. Updated assertions preserve funded prefixes,
whole-output refusal before allocation, prior aliases and terminal attribution.
The new whole-record denial has enough allowance for one large field but not the
whole output, detecting partial allocation. The added numeric golden confirms an
invalid field fails before another source callback. Full platform/stress/package
qualification is tracked separately at the retained delivery checkpoint.

[Initial assessment](assessment.json), [confirmation](confirmation-assessment.json),
[allocation](allocation-assessment.json), [accounted peaks](accounted-peaks.json),
[wide-record observation](single-wide-row-observation.json), raw archives and
[diagnostic source/focused logs](diagnostic-source-and-focused-logs.tar.gz) preserve
the complete evidence. Publication remains postponed. The broader whole-script
performance objective continues.
