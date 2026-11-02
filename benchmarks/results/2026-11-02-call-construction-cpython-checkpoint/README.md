# B115: affected CPython checkpoint, source609e5de1

2026-11-02. Runtime source609e5de1; exact fresh nonincremental Release image,
worker dependencies and source/fixture hashes recorded. B113/b6a75be3 is fully
qualified by cached41472979014; the later routing delta is focused-qualified,
not covered by that full proof. Publication remains postponed.

| Complete script | Lane | Lython | CPython3.13.16 | Lython / CPython | Lython / Python IQR | Identical Lython control |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| CSV/helpers,4,096 records | Fresh compile-to-result |7.448ms|3.691ms|2.02×|1.19% /0.09%|–1.28%|
| Text/helpers,3,072 lines | Fresh compile-to-result |5.779ms|1.463ms|3.95×|5.57% /1.07%|–2.45%|
| CSV/helpers | Warm |7.676ms|3.600ms|2.13×|0.99% /3.76%|–1.85%|
| Text/helpers | Warm |5.551ms|1.359ms|4.08×|1.28% /0.55%|+6.23%|

Ratio is Lython elapsed divided by CPython elapsed; greater than one is slower.
IQR uses inclusive quartiles divided by median. Text is a variable diagnostic,
especially warm text: narrow within-worker IQR does not eliminate6.2% unchanged
control drift. No retiming or false precision; the gap remains substantial.

Reuse the established B112 worker protocol/catalog, two independent identical
Lython workers and one CPython worker,4s timed warmup/engine and9 interleaved
batches targeting200ms. Same quiet Azure Standard_F4as_v7 Ubuntu24.04.4,
four AMD EPYC9V45 vCPUs/16GiB, .NET11 RC3, ordinary GC/tiering; CPython3.13.16
GCC PGO/LTO, ordinary GC/GIL, no JIT/debug, isolated `-I -S -X utf8`.

Fresh includes compilation and one execution in an already-running host. Warm
reuses compiled code. Batch timing includes fixture staging/reset, actual
efficient mediated file I/O, full stdout/file reads/checks and prior-result
disposal. Fresh disposes per-invocation compilation inside the timer. The last
result remains funded through response delivery, then disposes outside the batch;
transport/hash framing and final cached-owner/process/fixture cleanup are outside.
Every worker is joined and owned fixtures removed. Startup remains unmeasured.

All four complete jobs, hashes/goldens, before/after quiet gates and cleanup pass;
75.573s owned service exits0 (collector74.969s). Unit is terminal/inactive and the
shared VM lease is free. No assertion failure, timeout or background abandoned
worker. Source optimization claims come from separate controlled source pairs,
not independently collected CPython medians. Earlier clocks, the earlier
supervisor failure and all adverse/inconclusive data remain in their original
artifacts.

Only materially affected CSV/text were refreshed. Other five representative
scripts remain explicitly historical082179c4; external compilation measurements
remain namedaf82bdfc. B113's conditioned compilation source pair is effectively
flat, routing changes no compiler source/metadata. No repeated unchanged full
qualification, new benchmark framework, package producer or publication.
