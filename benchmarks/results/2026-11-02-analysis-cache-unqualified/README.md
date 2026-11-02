# Abstract-value cache consolidation — unqualified, 2 November 2026

**The entire production prototype is removed.** The sole actual comparison
completes in 33.620 seconds, but compilation misses the frozen 10% requirement
and the fresh-config control fails variance/drift gates. No retained speedup.

B86 passive native profiling identifies 37 distinct syntax/source-span hashing
paths, all through AbstractState's existing cache. The candidate keys that cache
by node identity, stores AbstractValueResolution directly, and removes redundant
version counters/entry wrappers while retaining clear-on-mutation. It changes
one production file, not analysis passes, syntax, public APIs, owner systems,
cancellation or execution engines. Its hypothesis is reduced repeated structural
hashing; sample counts do not predict a gain or exact removable CPU fraction.

[pre-code-plan.md](pre-code-plan.md) freezes deletion, semantics and timing before
code. The baseline runtime is delivered 4cae38f4, later 019f417f is test/evidence
only. Exact before/candidate core hashes and identical ordinary 0.4.0 auxiliary
bits are recorded. Three isolated images share CoreLib and actual hash witnesses.
Normal GC/tiering, no forced collections or altered runtime policies.

Four unchanged complete workloads: CSV/text helper compilation, fresh config
editing and warm text execution. There are 1,024 interleaved compilation warmups
per image, 128 execution warmups, two seconds settling per case, seven rotating
batches. Compilation uses 2,048 jobs; fresh/warm controls use 32. Sizes use prior
complete-job costs, not candidate results. Startup is excluded. Compilation
includes validity/compiled-owner disposal; fresh includes compilation/full public
Run/reset/mediated I/O/output+file goldens/result+compiled disposal; warm excludes
compilation. No CPython timing or external performance ratio.

| Boundary | Before batch ms | Candidate batch ms | Identical-before batch ms |
| --- | ---: | ---: | ---: |
| Compile CSV helper | 173.308 | 164.524 | 171.091 |
| Compile text helper | 134.178 | 124.447 | 134.313 |
| Fresh full config editing | 96.470 | 100.275 | 111.161 |
| Warm full text helper | 215.588 | 219.629 | 215.221 |

Observed compilation gains are 3.84–5.07% CSV and 7.25–7.35% text, below 10% versus
both originals. Fresh control original drift is 15.23%, IQR reaches 13.32%, beyond
3%/10% controls. Warm text control passes. Every batch exceeds 50ms. Source/image/
hash isolation, full stdout/file/reference goldens, per-job/final zero allowance,
normal owned-fixture removal and both actual-run quiet gates pass. Processes are
joined, MainPID=0/control group empty and the shared lease is available.

Before this comparison, one **readiness-only** service exits after 0.601 seconds:
idle CPU is 3.48%, above the unchanged 3% gate. Reference/workload/measurement
never start; no samples exist. Its exact record is preserved in readiness-00.
Owned MSBuild/compiler-server processes from driver construction are then shut
down with the SDK's supported build-server command. The sole actual comparison
runs after machine preparation. This does not discard timings, relax a gate or
restart an observed workload. The 45-second measurement-service cap stays fixed.

Candidate focused Release checks pass 139 internal/1,090 public per actual .NET
11 and .NET 10, including diagnostics, annotations, regex binding, cancellation,
admission and ownership. Three independent resolver regressions cover unresolved
facts becoming bound, mutation/withdrawal, clone/replacement isolation and branch
joins. They assert inferred semantics, not comparer/version implementation.

The whole production change, including version/wrapper deletion, is withdrawn.
A fresh nonincremental solution rebuild and the same 139/1,090 focused checks per
actual runtime pass against restored source. Those three independent tests remain.
[restoration.json](restoration.json) and logs preserve proof. No runtime package,
full original stress/platform qualification or publication is repeated for an
unchanged runtime. Existing released/package identities remain separate.

[assessment.json](assessment.json) contains inclusive-linear IQR and every gate;
[assess.py](assess.py) preserves the exact calculation. The original source patch,
private driver, catalog, reference goldens, raw batches, preparation failure,
normal cleanup/machine/terminal proof are in [evidence.tar.gz](evidence.tar.gz).
This outcome calls for a larger demonstrated mechanism, not retiming or keeping
a fragment of the rejected cache design.
