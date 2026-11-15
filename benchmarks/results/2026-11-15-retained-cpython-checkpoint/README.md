# Retained 0.11 CPython checkpoint

2026-11-15 UTC. Committed runtime **a7ab41f5 / frozen 716176a8**, qualified by
[43675539106](../2026-11-15-csv-final-payload/delivery/README.md), versus stock
CPython 3.13.16. Quiet Azure F4as_v7, Ubuntu 24.04.4, four EPYC 9V45 vCPUs,
16 GiB/no swap, .NET 11 RC3, ordinary workstation GC/tiering and CPython GIL/GC.
No production or provider change is introduced by this checkpoint.

| Complete workload / phase | Lython | CPython | Lython / CPython | Status |
| --- | ---: | ---: | ---: | --- |
| CSV fulfillment / pricing and validation / compile | 129.617–130.770 µs | 114.740–115.506 µs | 1.122–1.140× | Diagnostic; spreads 0.88/0.67%; max IQR 1.07% |
| CSV fulfillment / pricing and validation / warm | 8.067–8.184 ms | 6.736–6.793 ms | 1.188–1.215× | Diagnostic; spreads 1.43/0.84%; max IQR 0.94% |
| CSV fulfillment / pricing and validation / compile-run | 8.223–8.254 ms | 6.990–7.120 ms | 1.155–1.181× | Diagnostic; spreads 0.38/1.82%; max IQR 1.12% |
| Text / captured helper and counter / warm | 1.220–1.267 ms | 0.903–0.921 ms | 1.324–1.403× | Variable; spreads 3.90/2.02%; max IQR 3.22% |
| JSON-lines / aggregation helpers / warm | 3.454–3.481 ms | 4.071–4.100 ms | 0.842–0.855× | Diagnostic; spreads 0.79/0.70%; max IQR 1.15% |
| File search / defensive keyword-default helper / warm | 0.267–0.284 ms | 0.151–0.152 ms | 1.756–1.881× | Variable; spreads 5.86/0.84%; max IQR 2.31% |
| JSON-to-Markdown report / warm | 2.772–2.840 ms | 2.308–2.319 ms | 1.196–1.231× | Variable; spreads 2.44/0.45%; max IQR 6.24% |

Above one means Lython slower. Two independent processes per engine, five rotating
rounds; ranges span all four process-median pairings, not confidence intervals.
Full-request warmups remain 5s, 12s for captured text/report, with 0.5s target
request batches. Every warmup/sample and wider process CPU is preserved.

Warm includes mediated RAM I/O, full stdout/file verification and prior-result
cleanup. It excludes compilation, startup, fixture staging/reset, protocol and
final cache teardown. Compile-run adds compilation and compiled-owner disposal;
compilation is separately measured, and independently warmed medians are not
additive. Wider CPU includes fixture reset/protocol. Startup, contention,
genuinely pending I/O, heap/RSS peaks and embedding tails remain unmeasured.

File search has 5.86% Lython process spread, captured text 3.90%; report maximum
IQR is 6.24%. Keep these uncertainties visible. No unchanged automatic retiming
or inference that a design failed follows. The current file/call gap warrants
the next bounded design attribution; JSON is already below this CPython baseline.
The source-pair tradeoffs of R373/R376/R379 are not erased or causally explained
by this external comparison. All original older and adverse observations remain.

All seven jobs finish in **292.683s**, with quiet gates before/after, exact
worker/core/provider/catalog/source/fixture/output identities, every golden,
joined workers, removed RAM fixtures and verified persistent archive under lease.
No new image/catalog upload, retirement or deduplication; existing immutable
image links exclude mutable build outputs. The 254/252 MiB floors stay unchanged;
free storage after archive is 267,587,584 B. No VM users remain.

[Exact measurements and CPU](summary.json), [identity](identity.json),
[terminal service/cleanup state](terminal-status.json), [archive receipt](archive-receipt.json),
[full raw observations, machine gates, catalog, workers and collection procedures](observations.tgz).
All 585 production inputs match the committed qualified source after explicit
newline normalization, as proved by the linked delivery. Binary identity is the
existing frozen image, not inferred from a later rebuild. Publication remains held.
