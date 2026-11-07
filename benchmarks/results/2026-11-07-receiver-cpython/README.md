# Representative CPython checkpoint after R41 — B395

2026-11-07. Committed productionfba865ef; frozen5d2ca8e1 has identical production
source with precommit SourceLink1971487a. Full source/image/aux identities preserved.
Worker7cf37ca5, adapter27211dbe, normal PythonRe0.4; no development producer bridge.
This refresh is independent of final adapter qualification and producer publication.

Same five maintained scripts, source/fixture/output/file hashes and checked protocol
as B388. Quiet AzureStandard_F4as_v7, Ubuntu24.04.4, four AMD EPYC9V45 vCPUs,
16GiB/no swap, .NET11RC3 normal GC/tiering; CPython3.13.16 GCC13.3 PGO/LTO,
normal GIL/GC/no JIT/debug, -I -S -X utf8. One CPython and two independent identical
Lython workers per job,5s warmup/role/job,7rotated150ms rounds. Fifteen jobs complete
311.66s/all gates.420s owner+30s stop+30s margin, observed after480s. Archive verified
under lease and independently after retrieval; all workers joined/fixtures removed.

Warm/fresh elapsed includes mediated RAM I/O, complete guest work and output/file
checks, prior-result cleanup. Fresh adds compilation/program disposal; compile-only
excludes execution/I/O. Startup/staging/reset/transport/final cached program/result/
process teardown excluded. Imported modules persist in CPython; Lython isolates
execution imports. No source-cache workaround or identical-internals claim. Ratios
above1 mean slower. Ranges span two Lython-role medians, not confidence intervals.
Control is their absolute difference/candidate; IQR is the larger Lython-role value.
Diagnostic/Variable labels summarize short observations, not design veto thresholds.

Full CI42357660230 on exactfba865ef is terminal GREEN:Windows/Ubuntu ordinary
actual.NET11/10, original Stress both targets and installed core-package consumers.
Setup correctly recorded pending qualification; the terminal receipt independently
qualifies the delivered runtime. No repeated query/qualification for unchanged code.
No publication, startup/RSS/pending-I/O/embedding-tail or per-change causal CPU claim.

## Compile plus one execution

| Workload | Lython ms | CPython ms | Lython / CPython | Status; control / IQR |
| --- | ---: | ---: | ---: | --- |
| CSV/ordinary helper |4.108508–4.110142|3.400024|1.21–1.21×|Diagnostic;0.04%/1.06%|
| Text/helpers |2.153352–2.196110|1.262642|1.71–1.74×|Diagnostic;1.99%/1.29%|
| JSON-lines/helpers |4.365098–4.629884|4.241570|1.03–1.09×|Variable;6.07%/0.79%|
| JSON-to-Markdown report |3.593463–3.761933|2.788563|1.29–1.35×|Variable;4.48%/5.30%|
| Configuration edit |0.144745–0.145047|0.124272|1.16–1.17×|Variable;0.21%/3.67%|

## Warm execution

| Workload | Lython ms | CPython ms | Lython / CPython | Status; control / IQR |
| --- | ---: | ---: | ---: | --- |
| CSV/ordinary helper |3.868139–3.996881|3.237963|1.19–1.23×|Variable;3.22%/0.47%|
| Text/helpers |2.039782–2.086169|1.163419|1.75–1.79×|Variable;2.22%/0.61%|
| JSON-lines/helpers |4.220693–4.226584|4.071568|1.04–1.04×|Diagnostic;0.14%/0.75%|
| JSON-to-Markdown report |3.549197–3.593700|2.602897|1.36–1.38×|Variable;1.24%/8.69%|
| Configuration edit |0.075062–0.075250|0.060212|1.25–1.25×|Diagnostic;0.25%/0.93%|

## Compilation/program disposal only

| Workload | Lython ms | CPython ms | Lython / CPython | Status; control / IQR |
| --- | ---: | ---: | ---: | --- |
| CSV/ordinary helper |0.067955–0.068465|0.059471|1.14–1.15×|Diagnostic;0.75%/2.73%|
| Text/helpers |0.049876–0.050284|0.041640|1.20–1.21×|Diagnostic;0.82%/2.09%|
| JSON-lines/helpers |0.058505–0.058685|0.050710|1.15–1.16×|Diagnostic;0.31%/2.30%|
| JSON-to-Markdown report |0.046770–0.046959|0.038039|1.23–1.23×|Variable;0.40%/5.82%|
| Configuration edit |0.041959–0.042187|0.034605|1.21–1.22×|Diagnostic;0.54%/1.65%|

Text remains the largest primary warm gap,1.75–1.79×, motivating one bounded
current-source text cost gate. Other current warm ratios:CSV1.19–1.23×,JSON1.04×,
report1.36–1.38×,config1.25×. Preserve variable fresh JSON/report/config and report
warm IQR8.69%, text role spread2.22%, CSV warm3.22%. This combined checkpoint does
not repay R41's original adverse CSV result or explain other historical controls.

Before this collection, an inactive first-prototype core and duplicate evidence
files inside one old delivery source checkout were retired only after exact hash/
link/path checks against already committed archives/Git blobs. All originals remain
recoverable byte-for-byte; source, current images, input catalogs and sole evidence
were retained. Setup receipts/restore paths are in collection-commands.

[Observations](observations.tgz), [summary](summary.json), [identities](identity.json),
[collection/setup commands](collection-commands.tgz), [owned archive receipt](archive-receipt.json)
and [previous primary B388](../2026-11-07-formatted-cpython/README.md) preserve all evidence.

[Full delivery receipt](delivery-ci.json) covers exactfba865ef production.
