# Authoritative captured slots — R281

2026-11-12, qualifiedb3369e3e/frozen4f57ca33 versus candidatee4cc65b2 over
documentation98585baa. **Retain the representation simplification; full delivery
qualification follows.** Captured executable slots hold their existing shared
cell directly. Remove sparse LocalCells arrays and parameters, owner-slot
backpointers, active-frame cache propagation, generator cell-to-slot resume loops
and duplicate namespace seeding. No new backend, opcode, policy, pool, value
representation or public option. Guest reads unwrap internal cells; uncaptured
dictionary segments retain their compiler-proven raw slots.

Closures retain their existing cells/owners after workspace return. Namespace
retention seeds current values before switching cells to namespace reads. The
first migration exposed three generic-function failures in that handover; original
actual11 logs and reproducible migration script remain. Corrected source removes
the cache dependency. Python aliases, unbound/deletion errors, generators, class/
annotation fallback, match/import writes, real suspension and cleanup remain.
No public API or cancellation/memory contract changes; smaller frame preflights
can admit storage previously rejected. Cell estimates remain conservative.

Corrected nonincremental Release build passes.1055internal/2674public checks pass
per actual.NET11/10 (7458executions), including cancellation, terminal refusal,
retained aliases and pending-user cleanup. An overlapping annotation/match/import/
locals/context-manager public filter passes3683per target; do not count these as
additional unique tests. Two white-box fixtures adopt the new storage convention
while retaining their independent ownership/refusal assertions.

Seven unchanged complete search/guarded-search/CSV/captured-text/JSON/UTF8-report/
UTF16-report scripts pass both APIs, separate compilation and compile-to-result.
All stdout/files, actual images, allowance balances and joined fixtures pass.
Whole-job allocation is effectively flat (warm−0.00..−0.04%); the deleted array is
per captured frame, not per native helper call. No substantial allocation claim.

## Controlled observations and retention debt

Same AzureF4as_v7/Ubuntu24.04.4, four EPYCvCPUs/16GiB/no swap, .NET11RC3,
normalGC/tiering, fixedfec79 worker and optional adapter0.4. Two processes/image,
seven rotated500ms rounds. Positive means slower. Ranges span four process-median
pairings, not confidence intervals; widerCPU includes reset/protocol/GC/JIT.

| Complete warm script / API | Elapsed change | Wider processCPU | Reference / candidate spread |
| --- | --- | --- | --- |
| B638 captured text / Run | −6.03..−4.21% | −4.53..−2.56% | 1.62/0.31% |
| Captured text / RunAsync | −0.35..+3.42% | −1.22..+3.88% | 0.38/3.38% |
| CSV / Run | −4.45..+0.92% | −2.87..+1.96% | 4.35/1.01% |
| CSV / RunAsync | −1.92..+6.40% | −1.85..+6.25% | 3.50/4.69% |
| Report control / Run | +0.14..+3.24% | +1.22..+4.24% | 2.32/0.70% |
| B639 CSV / RunAsync confirmation | +0.38..+2.28% | −0.92..+2.00% | 0.50/1.37% |
| Report / Run confirmation | −2.12..+1.98% | −2.96..+0.00% | 0.49/3.55% |

B638uses12scaptured/8sreport/5sCSV warmups; five jobs264.498s under360sowner.
B639uses the exact same source/cores and12swarmups to examine report/CSVAsync
adversity; two jobs139.022s under180sowner. Both include30sstop/30smargin. Initial
four-job preparation survives; CSVAsync was added before launch to cover actual
desktop adversity. Local retrieval/summary job-count checks were corrected to five
before first observation; the actual collector/ownership gates already requested
and verified five. No runtime refinement or failed condition was erased.

Retain actual storage/synchronization removal and supporting capturedRun benefit.
Accept CSVAsync elapsed debt of roughly0.4..2.3%; its CPU remains inconclusive.
CapturedAsync and report CPU/elapsed stay uncertain. The initial report CPU
adversity was not reproduced; neither absence of regression nor its cause is
established. No automatic further unchanged timing, fixed-percentage veto,
universal CPU claim or completion of the broader performance objective.

Desktop clocks are diagnostic and preserved: capturedRun−5..−9%/Async−13..−14%,
CSVRun+1.8..2.5%/Async+3.9..4.1% with relatively stable controls; fresh captured
+4..26%/17%control and freshCSV123%control remain uncertain. Compile allocation is
flat; adverse compileCSV+13..16% does not establish an unchanged-compiler cause.

Warm includes invocation, mediated RAM I/O, full validation and prior-result
cleanup; excludes compile/startup/reset/protocol/final caches. Both collections
pass source/core/auxiliary identities, goldens, zero balances, quiet, Closed/exit0/
joined users, unmount, persistent archive and independent localSHA gates. Terminal
states cached; no VM users or sole RAM evidence. No old evidence/images deleted;
OS240MiB/evidence12MiB floors unchanged, final free277,164,032B.

Frozen source/core, all original diagnostics/failures and raw observations survive
in the archives. No extrapolated CPython ratio, RSS, pending-I/O or embedding-tail
claim. Full platform/stress/installed-consumer delivery remains separate.
Publication stays held.
