# Final-source CPython checkpoint after native wildcards — B143

2026-11-03, exact **f7dd0d3ace2d146d554a05a4df8bf8218f296b47**.
Fresh nonincremental Release core **2f78a866a272cb6ee6bb0a21f45333fcdec998fc25cf8b13414223198a7655fc**.
This measures the final retained B141/B142 source, including translation fixes.
B141's earlier source-pair timing measures its frozen pre-B142 patch separately.

[Assessment](assessment.json), [identities](identity.json), [collector](collect.py),
[frozen source/fixtures/goldens](catalog.json.gz), [raw responses](raw-results.tar.gz)
and [previous guide](previous-guide.md) preserve observations and their context.
The collector was fixed before collection; existing worker/protocol code is reused.

| Case | Fresh Lython / CPython ms (ratio) | Warm Lython / CPython ms (ratio) |
| --- | ---: | ---: |
| CSV/helpers |7.362 /3.700 (1.99×)|6.932 /3.481 (1.99×)|
| JSON-lines/helpers |7.316 /4.464 (1.64×)|7.118 /4.194 (1.70×)|
| Text/helpers |5.069 /1.467 (3.46×)|4.975 /1.298 (3.83×)|
| Search/early exit |1.912 /0.380 (5.04×)|1.890 /0.267 (7.07×)|
| Report rendering |7.581 /2.717 (2.79×)|7.761 /2.579 (3.01×)|
| Unicode filename filter |2.753 /0.583 (4.72×)|2.841 /0.492 (5.78×)|
| ASCII filename filter |2.308 /0.431 (5.36×)|2.271 /0.334 (6.81×)|

CSV/JSON/text/search/report fresh Lython IQR4.43/1.56/0.75/0.92/1.75%; identical
control drift−1.76/−0.91/−0.42/+1.39/+0.38%. Warm IQR1.20/0.77/2.24/1.23/3.09%;
controls+2.03/−1.08/+4.52/+0.91/−1.80%. Warm text precision remains limited by
control drift. Unicode/ASCII fresh IQR1.69/4.61%, controls+2.06/+0.15%; warm
IQR3.76/3.33%, controls−0.76/+0.39%. These short elapsed observations are diagnostic,
not portable release multipliers or CPU measurements. Identical Lython images
are labelled baseline/candidate by the reused collector: neither is older source.
Independent checkpoint medians cannot establish a causal source speedup.

Compile-only: CSV87.61µs/54.93µs (1.59×), report59.93µs/37.74µs (1.59×).
Lython IQR1.07/0.37%, CPython0.14/0.92%, controls−1.57/−2.21%. No guest execution;
startup is unmeasured. Historical config/copy still describe082179c4.
Earlier90785737 search270%/218% fresh and138%/77% warm IQR/control uncertainty
is preserved; this steadier checkpoint does not explain the earlier waiting.

Dedicated Standard_F4as_v7 Ubuntu24.04.4, four EPYC9V45 vCPUs,16GiB/no swap,
.NET11RC3, ordinary workstation GC/tiering; CPython3.13.16 GCC13.3 PGO/LTO,
ordinary GIL/GC, no JIT/debug, isolated -I -S -X utf8. Independent identical-current
Lython/control/CPython processes;4s warmup each, nine interleaved~200ms batches.
All16 jobs296.842s; source/fixtures/full stdout/files/loaded identities and quiet
gates PASS. Owned service exit0/inactive, workers joined, fixture directories
removed, lease free. [Joined collection](confirmation.json) records all jobs.

Fresh includes per-invocation compilation/disposal and complete public Run.
Warm excludes compilation/startup. Both include fixture reset/staging, actual
mediated file I/O with efficient host overrides, complete output/file checks and
prior-result disposal. Final result/cached-program/process/fixture cleanup is
outside batches and joined. Compile-only retains its final compilation outside
its timer. Script globals are fresh; ordinary CPython process-level module/library
caches persist, while Lython execution module/regex caches are isolated. These
are observable hosting boundaries, not an attribution of any particular gap.

Full delivery41566551688 is tracked with the native-wildcard evidence. Core remains
decoupled from the optional adapter. The whole-script performance objective remains
open; publication/tagging are postponed.
