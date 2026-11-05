# CPython checkpoint: shared construction and prepared helpers

2026-11-05. Exact runtime **0997baa8**, frozen core **68a9f3f8**; previous external
**cf3dc9cd/43fdf658** and an identical-current control run alongside CPython.
Full delivery41963810520 is terminal GREEN on exact0997baa8, all eight platform/
framework/originalStress/installed-consumer gates; [cached receipt](../2026-11-05-formatted-construction/delivery-ci-status.json).
Publication postponed.
The separate D31 working prototype is not included in these measurements.

Quiet dedicated Azure Standard_F4as_v7, four AMD EPYC9V45 vCPUs/16 GiB/no swap,
Ubuntu24.04.4, .NET11RC3 SDK/runtime11.0.100-rc.3.26522.148/11.0.0-rc.3.26522.148.
CPython3.13.16/GCC13.3 PGO/LTO, ordinary GIL/GC, `-I -S -X utf8`, no JIT/debug.
Existing protocol2 workers, ordinary GC/tiering; six representative jobs in warm
and compile-to-result lanes plus CSV/report compilation. Five-second warmup per
engine/job, seven rotated ~150ms batches, reversed roles for later jobs.
**385.81 s**, within480s owned bound. No ten-minute iteration lane or new framework.

Timed work includes invocation setup, mediated RAM-file I/O, full stdout/output
checks and prior-result cleanup. Fresh adds Compile/Dispose; compilation-only
excludes execution and I/O. Reset/staging/startup/hash/final cache/process teardown
excluded; every actual user joins. All exact library/runtime/catalog identities,
independent complete goldens, normal Closed/exit0, quiet endpoints, removed fixtures
and free exclusive lease pass. No actual suspended I/O, RSS/startup/embedding-latency
or individual CPU-cause claim. Ratios are Lython elapsed / CPython elapsed.

| Fresh complete script | Lython ms | CPython ms | Ratio | Current control / candidate IQR |
| --- | ---: | ---: | ---: | ---: |
| CSV/helpers,4096records |5.157|3.458|1.49×|1.43% /0.66%|
| JSON-lines/helpers,4096records |5.022|4.225|1.19×|1.09% /1.31%|
| Text/helpers,3072lines |2.983–3.082|1.354|2.20–2.28×|3.32% /0.82%|
| JSON-to-Markdown,3072records |3.935|2.498|1.57×|0.87% /4.24%|
| Config edit,1024paths |0.265|0.253|1.05×|1.00% /2.64%|
| Long CSV,16records |0.400|2.432|0.16×|0.13% /0.61%|

Warm ratios: CSV1.45×,JSON1.18×,text2.32–2.47×,report1.70×,config1.02×,longCSV0.11×.
Compile-only CSV70.69µs/54.47µs=1.30×,report50.79µs/37.60µs=1.35×.
Text ranges are medians of identical-source roles, not confidence intervals.
Warm text control differs5.85%; report dispersion also limits small claims.
Keep earlier text variation visible rather than infer improvement from ratios
across separate collections. No automatic retiming needed to establish that a
substantial text gap remains. Further bounded correction remains permissible.

Coupled current versus previous cf3 report is ~6% favorable warm/fresh, with
dispersion limiting magnitude; text changes are control-limited. Other scripts
and compilation are near unchanged at this resolution. This combined comparison
does not attribute individual changes or add earlier source-pair percentages.
Possible ~1% typical CSV cost remains accepted uncertainty, not erased by controls.

## Preserved staging failure

B250 symlinked the worker executable to an earlier frozen directory. Dotnet resolved
that worker's **81deb3be** core instead of intended **68a9f3f8**. The mandatory Ready
identity assertion failed before any timed job; original record has zero jobs.
A bounded owned Ready/Closed probe confirmed the loaded hash. All processes joined,
fixtures removed, lease free; journal/exit1/setup/identity retained in
[original failed observations](failed-observations.tgz).

B251 copies the identical benchmark executable/deps/runtimeconfig beside the intended
core, matching established staging. No runtime/guest/parameters changed. The complete
corrected observations remain distinct. This was a measurement-fixture correction,
not an adverse runtime result or a speedup. Do not use the failed condition as data.

[Plan](plan.md), [all metrics](summary.json), [raw/source/identities/gates](observations.tgz).
Raw SHA256 `3b3d2f91a12cf12f38d2fe4a37e70d7f604ed3f1f93bbdc8f676b44da4c8307f`.
[Earlier source/confirmation](../2026-11-05-runtime-cpython-pipeline-checkpoint/README.md)
retains its original/adverse variation; docs/BENCHMARKS.md is the current guide.
