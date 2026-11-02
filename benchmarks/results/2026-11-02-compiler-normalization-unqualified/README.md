# Compiler graph normalization — unqualified, 2 November 2026

**The entire production prototype is removed. No retained speedup is claimed.**
One 31.766-second VM service finishes all semantic/ownership checks, but misses
the frozen improvement and variance controls. It is not retimed.

Hypothesis: replace repeated per-target HashSets, reachability hashing/sorting and
dictionary remapping with one iterative canonical jump table and dense block IDs.
The candidate changes two compiler files; it does not change emitted instruction
semantics, runtime dispatch, value representation or public APIs. Cyclic jump
entry identity, original protected AbruptJump targets, handlers and block order
are preserved. Compile-local arrays use the existing coarse compilation funding;
current stage cancellation and terminal refusal remain effective.

The [pre-code-plan.md](pre-code-plan.md) freezes the change and acceptance before
code. The archive contains the exact diff.patch;
[prototype-tests.cs](prototype-tests.cs) preserves
the discarded implementation and independent per-entry reference-walk tests:
all small functional graphs through five nodes, distinct cycle entries and tails,
a 20,000-node chain and invalid internal successors. Candidate focused Release
checks pass 331 native/356 public per .NET 11 and .NET 10.

One process loads before/candidate/identical-before in isolated assembly contexts,
sharing CoreLib, actual hash witnesses, normal GC/tiering and identical auxiliary
DLLs. Baseline runtime is delivered 4cae38f4; candidate is its compiler change on
f9fc488d. Informational revision metadata differs; exact core and auxiliary hashes
are recorded. Adapter remains 0.4.0. No producer or target-framework substitution.
128 interleaved warmups per image/case, two seconds settling, seven rotating
batches: 1,024 compile jobs, 64 fresh file-search jobs, 32 warm text jobs.

| Boundary | Before batch ms | Candidate batch ms | Identical-before batch ms | Observed candidate change |
| --- | ---: | ---: | ---: | --- |
| Compile CSV helper | 88.911 | 86.396 | 89.778 | 2.83–3.77% faster |
| Compile text helper | 69.745 | 68.273 | 68.829 | 0.81–2.11% faster |
| Fresh full file search | 121.166 | 129.126 | 134.477 | 6.57% slower / 3.98% faster |
| Warm full text helper | 213.948 | 207.750 | 217.183 | 2.90–4.34% faster |

These are diagnostic medians, not qualified gains. Both compile workloads miss
7.5% versus both originals. Original compile IQR reaches 69–133%; fresh original
drift is 10.99%, exceeding the frozen 10% IQR/3% drift controls. All batches exceed
50ms. Startup is excluded. Compile includes validation/compiled-owner disposal;
fresh includes compilation/full Run, reset, mediated I/O, output/file checks and
result/compiled disposal. Warm excludes compilation. No CPython timing/ratio.

Complete stdout/file goldens, per-job and final allowance, fixture removal,
actual image/hash isolation and both quiet-machine gates pass. The service is
joined, its control group ends and the comparison lease is available. No abandoned
work. The raw [evidence.tar.gz](evidence.tar.gz) retains sources, catalog, controls,
identities, samples, machine checks and process proof. [assessment.json](assessment.json)
records inclusive-linear IQR and every failed gate.

Both production files are byte-for-byte restored and the candidate-only tests
removed. A fresh nonincremental solution build and 326 native/356 public focused
Release checks per actual .NET 11/10 pass. [restoration.json](restoration.json) and
logs retain proof; rebuilt binary metadata may differ from the older baseline.
Full platform/stress/package checks are not repeated for unchanged runtime code.
