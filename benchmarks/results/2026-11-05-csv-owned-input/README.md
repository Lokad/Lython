# Owned native CSV input transport

2026-11-05, source dd92a2fa plus the D44 snapshot in observations.tgz, measured
core c10ddd55. Reference is fully qualified 7dcc3b48/final a4ce2b48. This is an
architectural replacement and a bounded elapsed comparison, not an allocation
measurement or proof of attributed CPU savings. Publication remains postponed.

Native text files copy one complete decoded physical line directly into the
existing admitted CSV buffer. The same parser compacts that input in place;
its writes cannot overtake unread input or require replacement capacity.
Independent field payloads are admitted before publication. Public readline
and generic input strings retain their existing independent ownership.

Independent CPython3.13.2 probes corrected two old assumptions: each source item
completes at most one record (trailing CR/LF allowed), and a failed next call
discards its unfinished record, including source/type errors. Non-string items
raise csv.Error. This removes the ready-row list, compaction/capacity/EOF queue
handling and parser/source backlink. One funded pending array remains. Constructor
non-iterability stays TypeError. No alternate parser or ownership policy is added.

Raw syntax plus an existing decoded prefix may increase conservative working
capacity/denial points. All construction and replacement peaks remain admitted;
returned rows and delayed host users remain funded through actual cleanup.
The original source contract and baseline failures are retained beside the
CPython correction, rather than silently relabeled as correct.

## Five complete-script observations

143.12s, all identities/goldens/quiet gates, Closed responses, successful joins,
fixture removal and final zero reservation assertions passed. Same frozen worker
b94fa98c beside each core, independent identical-current process, CPython3.13.16.
Five-second per-engine warmup/job, seven rotated approximately150ms samples;
normal GC/tiering on the dedicated Azure F4as_v7 VM described in the main guide.

| Script/lane | Current ms | Reference ms | Current changes against reference | Current-role spread |
| --- | ---: | ---: | ---: | ---: |
| Native CSV/warm |4.334 /4.505|4.495|−3.58% /+0.20%|3.93%|
| Ordinary helper CSV/compile plus run |5.026 /5.136|5.229|−3.90% /−1.79%|2.20%|
| Ordinary helper CSV/warm async |5.936 /5.790|6.094|−2.60% /−5.00%|2.46%|
| Long-field CSV/warm |0.2056 /0.2049|0.2517|−18.32% /−18.59%|0.33%|
| Unchanged text/warm |2.626 /2.509|2.539|+3.46% /−1.18%|4.48%|

Current values are two independent identical-build medians, not confidence
intervals. Native CSV has an unchanged-current role slightly slower than the
reference; small ordinary-CSV magnitudes remain control-limited. Long-field CSV
shows a consistent substantial elapsed improvement. Text retains an adverse
observation, but its controls disagree; neither a regression cause nor debt
closure is established. Retain the simpler pipeline and the measured long-field
benefit; no automatic retiming or claim that overall Python performance is solved.

Clocks include invocation, actual mediated RAM-filesystem I/O on /dev/shm,
complete stdout/file validation and prior-result cleanup. Fresh also compiles
and disposes per invocation. Startup, fixture staging/reset, transport/hashing
and final process/cache/fixture teardown are excluded. Async uses completed host
operations; genuinely suspended I/O is correctness-tested separately. Full raw
samples and identities are in observations.tgz; summary.json is derived.

The current broad external baseline remains the separately labeled e958c9b1
seven-script checkpoint. These targeted observations do not replace it or add
arithmetically to earlier improvements.

## Validation and remaining work

Focused actual .NET11 Release:358 public CSV,103 internal CSV and371 public
text/codec/window/shared-memory checks passed. Ten new public cases cover both
APIs, retained rows/shared cursors, embedded delimiter rejection, parse recovery,
generic type-error lifecycle and an interrupted native read; delayed I/O is real.
Existing CSV cases cover in-flight cancellation, refusal, aliases and budget reuse.
Release nonincremental solution build23.33s, zero warnings/errors; frozen measured
core matches the rebuild. Full local actual.NET11/10 each pass4109internal and
15993public. Exact source723b9638 delivery42025698023 is running Windows/Ubuntu,
actual11/10, original stress and installed consumers; terminal status pending.

Separate proven preexisting mismatch: non-strict quoted EOF should emit a final
row; current Finish rejects it. A bounded follow-up will preserve the final row's
funding through EOF cleanup. This timed source does not include that correction.
