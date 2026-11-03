# Corrected command clock / CPython checkpoint — B145

2026-11-03. Runtime exact **f7dd0d3ace2d146d554a05a4df8bf8218f296b47**, qualified
by cached41566551688. Exact frozen core **2f78a866a272cb6ee6bb0a21f45333fcdec998fc25cf8b13414223198a7655fc**.
Worker protocol2 committed **02efd797**; no runtime change or optimization claim.

The [component diagnosis](../2026-11-03-comparison-boundary-diagnostic/README.md)
found .NET recursive fixture deletion dominates a1.4–1.6ms reset, versus0.11–0.18ms
CPython reset. Both workers now subtract reset/staging from elapsed execution,
while still resetting every invocation. Guest I/O, host mediation, full stdout/file
verification, prior-result disposal and fresh compilation/disposal remain timed.
Wall deadlines cover all work. Final result/program/process/fixtures are joined
outside batches. Natural process-level CPython caches remain, versus isolated
Lython execution caches; script globals are fresh. Startup is unmeasured.

[Previous included-staging results](../2026-11-03-wildcard-cpython-checkpoint/README.md)
remain intact. Independent medians and different clock boundaries cannot establish
a runtime speedup. Config/copy remain historical082179c4/v1, not refreshed here.

| Complete script | Fresh Lython / CPython ms (ratio) | Warm Lython / CPython ms (ratio) |
| --- | ---: | ---: |
| CSV summary/helpers |5.681 /3.546 (1.60×)|5.492 /3.391 (1.62×)|
| JSON-lines/helpers |5.897 /4.251 (1.39×)|5.523 /4.147 (1.33×)|
| Text summary/helpers |3.724 /1.297 (2.87×)|3.592 /1.186 (3.03×)|
| Search with early exit |0.491 /0.245 (2.01×)|0.358 /0.151 (2.38×)|
| JSON-to-Markdown report |5.815 /2.542 (2.29×)|5.735 /2.392 (2.40×)|
| Unicode filename filter |0.874 /0.422 (2.07×)|0.789 /0.332 (2.37×)|
| ASCII filename filter |0.774 /0.292 (2.65×)|0.638 /0.211 (3.03×)|

Fresh controls drift−1.48/−1.69/−2.44/−3.02/+4.45/+2.37/+0.36%; warm
−1.20/+3.57/+0.18/+4.85/+0.12/−1.61/+2.88%, in table order. Fresh report remains
variable (4.54% Lython IQR). Warm JSON/search precision is limited by control drift.
All within-worker IQR and original controls are in [assessment](assessment.json),
not discarded. These are diagnostic elapsed points, not precise CPU attribution.
Identical current Lython processes retain baseline/candidate labels from the reused
collector; neither is older source. Each job has4s warmup/image, nine interleaved
~100ms measured batches. Dedicated Standard_F4as_v7/Ubuntu24.04.4/.NET11RC3 versus
isolated CPython3.13.16 PGO/LTO, ordinary GC/tiering/GIL, no JIT/debug/overrides.

CSV/report compilation alone87.17/61.40µs versus54.70/37.74µs (1.59×/1.63×),
controls−1.28/−2.98%. No guest execution. Sixteen jobs **283.769s**; full identities,
goldens and before/after quiet gates PASS; owned service exit0/inactive, workers
joined, fixture directories removed, lease free. [Raw responses](raw-results.tar.gz),
[collector](collect.py), [source/fixtures](catalog.json.gz) and [identities](identity.json).

Windows focused Release **576 tests per actual.NET11/10 PASS**, including protocol,
file replay/wrong output, sampling eligibility, worker cancellation/unwind and
ownership. A real CPython3.13.2 Windows [smoke](local-verification.json) verifies all
four selected complete scripts. Initial tests exposed17 synthetic qualification
fixtures still hardcoding protocol1; those fixture values were corrected without
relaxing semantic/admission/cleanup assertions; initial and passing logs remain.
The controlled wrong-version subprocess now sends protocol1 and is rejected.

Only benchmark clocks/protocol/test fixtures/docs changed. Runtime source/package/
adapter remain unchanged; the qualified runtime receipt is not replayed. Full
platform/stress/package checks batch with the next retained runtime delivery.
Publication stays postponed. Next: actual text-CPU attribution, then a coherent
storage/call/dispatch redesign supported by whole-script evidence.
