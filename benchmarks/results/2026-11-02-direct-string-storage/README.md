# Admitted final UTF-8 string storage

2026-11-02, source base `aa16828b` plus the exact patch in the raw archive.
**Measured allocation reduction; elapsed improvement remains unqualified.**
The original pre-code plan's complete retention condition was **not met**:
report timing failed its post-idle check. The explicit delivery-policy correction
below was made after that observation; the frozen plan and failure remain intact.

## Construction change

Specified string formats and `ljust`/`rjust`/`center` write one exact final UTF-8
array under the existing reservation/ownership transfer. This removes the padded
working builder, its capacity lifetime and final copy. String format subjects also
avoid CLR decoding, precision slicing, CLR padding and re-encoding. Numeric/date/
complex algorithms and custom formatting hooks remain.

The exact final estimate is reserved before allocation, while input storage stays
funded. Byte arithmetic and actual array bounds are checked. Scalar scans, fills
and copies observe cancellation; admission refusal remains terminal. Publication
checks refund only private new output, preserving aliases. Existing managed stack,
host capabilities and current-token behavior are unchanged. No owner framework,
pool, tracing, quota or public option was added. Smaller estimates reflect removed
intermediate storage, not a promise of identical tiny-budget thresholds.

## Controlled complete-job allocations

One **11.875 s** owned collection on the dedicated Ubuntu F4as_v7 / .NET 11 RC3 VM
reuses the existing report phase driver: 32 warmups and eight observations per
lane, normal GC/tiering, original/candidate/identical-original processes. The
benchmark/adapter/auxiliary files match; all three actual loaded core hashes are
checked. Complete stdout/file goldens, both quiet gates, per-invocation balances,
final allowance zero, joined users and normal fixture cleanup pass.

| Complete public warm report | Managed allocation / invocation | Direct elapsed median |
| --- | ---: | ---: |
| Original | 12,918,464 bytes | 9.553 ms |
| Candidate | 11,798,120 bytes | 15.125 ms |
| Identical original control | 12,917,736 bytes | 9.399 ms |

Allocation falls **1,120,344 bytes / 8.67%** against the original and 8.67% against
its control. Original allocation drift is 0.0056%; allocation IQR/median is
0.0022% / 0.0408% / 0.0024%. This meets the prospective allocation gate. The
adverse candidate elapsed observation stays visible; allocation benefit does
**not** establish throughput or embedding-latency improvement.

The process-wide counter covers public execution through result creation,
including mediated I/O and runtime cleanup. Fixture reset/golden verification and
result disposal are outside that allocation interval. Result disposal, reflected
execution/context/cleanup, nested host calls and fresh compilation have separate
clocks; reflected allocations are current-thread only. They are not additive.

## Separate short latency observations

Each maintained micro command runs once, with normal workers and CPython context:

| Complete warm job | Original / candidate / CPython | Lython IQR | Status |
| --- | --- | --- | --- |
| Report rendering | 10.514 / 9.036 / 2.682 ms | 29.3% / 20.0% | Inconclusive; post-idle 3.4826% also fails |
| JSON helpers control | 7.930 / 7.935 / 4.277 ms | 3.6% / 4.5% | Diagnostic; +0.07% point change, quiet gates pass |

Both commands complete all goldens and join worker/fixture cleanup. Report service
exits 3 on the post-idle check after its completed micro receipt; control service
finishes normally. There is no repeated report observation. Timing includes fixture
staging/checks and declared cleanup; compilation/startup are excluded. Process hash
seeds differ. These clocks differ from the phase driver above and do not resolve
its adverse elapsed result. No speedup or exact CPU attribution is claimed.

The original full retention condition remains failed. An explicitly recorded
**post-observation policy correction** separates development retention for a
verified allocation/construction improvement from elapsed qualification. This is
not a statement that every original gate passed. The complete change proceeds
through full correctness/platform/package qualification; stable material latency
regression remains a rejection condition. The report's failed qualification and
all raw observations stay available for review.

## Validation

Focused Release: **455 internal / 695 public checks per actual .NET 11/10**.
Full local ordinary Release: **4,004 internal / 15,677 public per runtime**.
Twelve new independent CPython padding goldens cover Unicode/invalid fills and
center parity; the previous 36 format goldens, terminal-refusal/cancellation,
retained-alias and real async-protocol controls remain. No assertions/timeouts
were relaxed. Platform, original-Stress and installed Release package consumer
qualification is recorded at delivery.

Delivery review caught a precision-only attribution bug in `6e1ca104`: a refused
copy used the input's creation span. Two sync/async regressions reproduce line 99
instead of the formatting call's line 3. Copying directly under the current
formatter span fixes it, preserving aliases and cleanup. Corrected focused checks
pass **457 internal / 695 public per runtime**. The report measurement uses the
unchanged padded-output branch; it is not repeated. Full corrected-head delivery
supersedes the initial workflow and earlier package-consumer proof.

Corrected runtime `df548ddf` passes [delivery CI](delivery-ci.json): **Windows and
Ubuntu, ordinary .NET 11/10, original stress scales on both runtimes, and installed
Release package consumers on both runtimes**. The superseded `6e1ca104` workflow
was cancelled; it is not the corrected-head proof. Local explicit Release
consumers also pass both actual runtimes. No package was published or tag created.

The [bounded offline latency assessment](offline-latency-assessment.json) is closed.
It preserves the adverse elapsed observations and explains why the saved records
cannot establish their cause; no additional execution or profiling was performed.

[Frozen original plan](pre-code-plan.md),
[explicit policy correction](delivery-policy-correction.md),
[identities, phases and checks](summary.json),
[allocation controls](allocation-assessment.json), [all elapsed observations](latency-assessment.json),
[new Python goldens](padding-goldens.json), [validation logs](validation-logs.tar.gz),
[raw source/collectors/observations/cleanup](evidence.tar.gz).
Raw SHA256: `fca5d2c9f29b585ee0ae0508c4e54fc3c77c468f20283beb202a17a264967c7a`.
