# R219 — operation-owned regex projection storage

Retained source pair, 2026-11-11: committed `1208a681`, based on `57df6785` /
runtime `e122bdf6`. The core still owns the byte-range/scalar-position contract and has
no regex package dependency. The normal non-packable adapter and producer remain
0.4.0. This does not qualify a newer producer or complete the performance roadmap.

The existing prepared subject now owns its projection cursor and one coordinate
array. Unicode iteration and callable replacement reuse that array through actual
cleanup. This removes two arrays and a scratch reservation per match, independent
cursor fields in the iterator/callback loop, ref forwarding and a duplicate
match-construction overload. ASCII allocates no coordinate array. A signed byte
offset and endpoint slot fit in each ordinary managed long; no unsafe APIs or
native layout assumptions are introduced. Original-window coordinates seed the
cursor, and lookbehind retains the nearer-boundary scan.

Working storage is admitted before allocation, stays charged across suspended
callbacks/iteration and is relinquished only after provider cleanup. Retained
matches still own independent immutable ranges and their funded original subject.
Holding scratch between results can conservatively deny unrelated storage sooner.
No allowance, retry, collector, cache owner or public option is added. A lazy
retained-position cache was not selected: concurrency, lifetime and nonmonotonic
iteration costs would add machinery without this simplification.

Release builds have zero warnings/errors. **64 internal / 397 public regex checks
pass on each actual .NET 11/.NET 10 target**, including real adapter semantics,
original/reversed provider windows, retained ranges after disposal/purge, genuinely
suspended replacement callbacks, terminal refusal, active backward-scan
cancellation, joined cleanup and shared-budget reuse. Three additional independent
CPython 3.13.2 goldens exercise reverse reads of retained matches, retained callback
matches and late/endpoint windows. [Checks](focused-checks.json),
[original goldens](cpython-goldens.json), [capability correction](golden-capability-correction.json).
Full platform/stress/installed-consumer qualification is pending the next coherent
delivery batch; predecessor CI42931921242 does not qualify this changed source.

## Bounded diagnostic

Existing complete-job driver and four existing regex diagnostics, plus unchanged
complete CSV/text controls. Windows desktop, actual .NET 11 RC3, ordinary GC/tiering;
two byte-identical baseline roles and one candidate, isolated ALCs sharing CoreLib,
JIT and process GC. Only core differs. Per case: 256 warmups/image, five rotated
rounds, batch 16. Compilation and both warm APIs are separate. All complete output,
file, isolation, stable allowance, final-zero and owned-fixture cleanup checks pass.

| Existing diagnostic | CLR allocation, sync / async | Diagnostic elapsed, sync | Diagnostic elapsed, async |
| --- | ---: | ---: | ---: |
| ASCII prefix / 128 searches | −1.42% / −1.42% | −6.91..+26.25% | −1.86..+49.45% |
| Unicode prefix / 128 searches | −0.11% / −0.11% | −2.85..+5.48% | +1.57..+2.33% |
| Unicode lookbehind / 1,022 matches | −17.33% / −17.31% | −13.58..−6.28% | −6.87..−5.22% |
| Unicode forward captures / 2,048 matches | −18.06..−18.05% / −18.05% | −12.15..−11.05% | −7.93..−7.36% |
| Complete text control | effectively flat / flat | −3.20..+2.97% | −1.60..+5.02% |
| Complete CSV control | effectively flat / flat | −3.85..−0.34% | −7.13..+2.50% |

Change = `(candidate / baseline − 1) × 100`, against both baseline medians; ranges
are not confidence intervals. Compilation allocation is flat. Compilation elapsed
includes adverse +3.14..+8.60% lookbehind, +3.66..+5.64% CSV and up to +8.85% text;
unchanged controls also move. Warm prefix controls differ by up to 52.28%, text
6.31% and CSV 10.38%. No quiet-VM CPU confirmation ran. Retain the ownership
consolidation and allocation reduction; elapsed, neutrality and CPU magnitude
remain unproved. No current CPython row is refreshed from these diagnostics.

Warm includes public execution, fixture reset, mediated I/O for the file-backed
controls, full stdout/file validation and result disposal. Compilation, startup
and final script/cache teardown are excluded. Execution-only and coarse wider CPU
clocks are retained separately; wider CPU includes surrounding work. The observed
32-byte lower Unicode allowance peak is an estimate, not a tiny-budget contract.
No RSS, pending-I/O latency or embedding tail-latency claim follows.

[Sync raw data](warm-sync/measurements.json), [sync medians](warm-sync/summary.json),
[async raw data](warm-async/measurements.json), [async medians](warm-async/summary.json),
[compilation raw data](compile/measurements.json), [compilation medians](compile/summary.json).
[Source/image identity](source-identity.json), [common-image proof](diagnostic-identity.json),
[exact measured patch](measured-source.patch),
[frozen cores, driver, source, logs and all failed conditions](preparation.tar.xz),
[archive identity](archive-identity.json). Original capability-test failure and
setup mistakes remain explicit in [setup corrections](setup-corrections.md).
There are no remaining workers or guest fixtures. Publication remains postponed.
