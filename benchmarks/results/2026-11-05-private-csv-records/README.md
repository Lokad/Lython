# Private CSV record storage

2026-11-05. Retain parent7efe581b plus exact final patch/snapshot, core
**402ae9f1**, versus **7efe581b/b6046395**. Actual .NET11/10 focused checks pass;
retained-source platform/Stress/installed qualification pending. Publication held;
the broader performance objective remains open.

Queue final field arrays, without an intermediate Python list, separate row
reservation or per-field Add/EnsureCapacity. DictReader consumes the array and
returns its private capacity after header/key protocols and awaited cleanup.
csv.reader and exposed fieldnames adopt that same funded array without copying.
Delete the draft's extra list-returning source/async forwarding methods: one
record path, explicit adoption or relinquishment. No parse-time dictionary fusion,
per-row owner objects, new backend, tracing or hidden allowance.

Reuse the list-capacity estimate, deliberately including possible wrapper metadata
and scalar headroom. Payloads have independent owners; escaping dictionaries and
rest lists admit their capacities while the original array remains funded. Queued
completed records remain funded through EOF/errors. Mutable headers, shared-reader
reentrancy, lazy pulls, errors, line numbers, aliases, cancellation and terminal
refusal remain. Combined admission can reject earlier; identical tiny thresholds
are not promised. Unpublished payloads may remain conservatively batch-owned.

## Complete-script observations

Quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3, ordinary GC/tiering. Reuse the existing
three-image/two unchanged-baseline driver, same auxiliaries/CoreLib/key hashes.
Each source variant:6s guestwarmup/image,2ssettle,9rotated~200ms batches,
**134s owned**. Full invocation, mediated RAM-file I/O, full output checks and
result cleanup included. Fresh additionally includes public compilation and
compiled-script disposal. Reset/staging/startup/hash/final cache teardown excluded;
all users joined. Process allocation includes reset/background work.

Independent CPython3.13.16 full stdout/files, exact identities, normal runtime,
quiet endpoints, budget baseline/finalzero, closes/joins and fixture removal pass.
No compilation-only, async-performance, CPU/RSS, embedding-latency or new CPython-
ratio claim. Reader control uses the maintained CSV fixture/model and an explicit
csv.reader/indexed-row variant, kept in the raw catalog.

| Final complete job | Elapsed vs baseline / control | Baseline spread | Allocation vs both |
| --- | ---: | ---: | ---: |
| Native-helper DictReader warm |+1.22% /−1.99%|3.28%|−7.54% /−7.56%|
| Ordinary-helper DictReader warm |+0.39% /−3.32%|3.84%|−4.81% /−4.83%|
| Ordinary-helper DictReader fresh |−0.77% /−1.45%|0.69%|−4.67% /−4.69%|
| Public csv.reader warm |−5.28% /−4.21%|1.12%|−4.51% /−4.53%|
| Text control warm |**+5.13% /+1.90%**|3.16%|unchanged|

DictReader warm CPU improvement is **not established**; fresh's small favorable
elapsed point does not erase the initial variant's uncertainty. Allocation and
reader elapsed are separate outcomes. Initial untimed accounted execution peaks
fall6,776,621→5,531,741B for native DictReader and6,776,770→5,531,890B for ordinary
DictReader (about18.4%); reader/text peaks are unchanged. These are conservative
allowance estimates, **not heap/RSS measurements**. [Final plan](final/plan.md),
[metrics](final/assessment.json), [exact source/raw/gates](final/observations.tgz).

Initial core77b20fbd retained an unnecessary async forwarding layer. Its134s/all-
gates result has similar allocation reductions, reader elapsed−4.0–5.4%, native/
ordinary warm control variation, fresh+1.6% /−2.2%, text−0.6–0.8%. The source-only
refinement was chosen before inspecting those timings; no causal forwarding-layer
CPU claim. [Initial source/raw](initial/observations.tgz),
[metrics](initial/assessment.json), [refinement contract](refinement-plan.md).

One unchanged-bit text confirmation reverses initial image construction/warmup,
uses10s guestwarmup/image and9rotated250ms batches: **41s owned**, all original
gates pass. Elapsed **+0.77% /+0.97%**, baseline spread0.20%, candidateIQR0.46%;
allocation unchanged. Original larger adverse point remains unexplained and
preserved. **Accept roughly1% warm-text debt** for the removed per-row containers,
private-storage lifetime, allocation reductions and reader improvement. Reassess
debt at the next material representative checkpoint; no automatic unchanged
retiming, permanent veto or claim of general CPU improvement.
[Confirmation plan](text-confirmation/plan.md), [metrics](text-confirmation/assessment.json),
[raw](text-confirmation/observations.tgz).

## Correctness and failed conditions

Final nonincremental Release35.90s, zero warnings/errors. **116 internal/431 public
cases per actual11/10** pass. Coverage includes CSV dialects/headers/laziness/error
recovery, retained/public aliases, actual suspension/cancellation, refused
admission and budget reuse. Added full-allowance exact array adoption, three gated
header-cleanup outcomes (success/refusal/disposal error), and an independent
CPython3.13.2 shared-reader/reentrant-header golden through both APIs. The array
stays funded until actual async cleanup ends; first refusal is preserved.
[Validation](validation.json), [all build/focused logs](validation-logs.tgz).

First edit missed the empty-feed constructor; compiler rejected its return type
before tests, corrected before the clean build. A CPython probe originally used
the reader from fieldnames iteration during construction before it existed;
the corrected golden assigns the custom header after construction. No production
semantics were changed to accommodate those fixture/edit mistakes.

B257's first quiet pre-gate rejected3.96% idle CPU, before any timed job. Original
inventory/status/journal/markers remain in initial raw. One5s-settled unchanged-bit
retry passed original quiet thresholds; later collections use that settled pre-
gate. No adverse sample or failed precondition was dropped, and no timeout or
containment assertion was relaxed. Frozen workers remain available; archives
contain source snapshots/patches and full catalogs without duplicating dependencies.
