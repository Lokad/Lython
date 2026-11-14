# Literal-storage CPython checkpoint

2026-11-14UTC, R352/B724: committed `7e2f7cac` / frozen `72c9a1e5`,
581production inputs, stockCPython3.13.16. Focused actual11/10 checks pass;
full43579225278qualification is pending independently at collection.
[Source and subsequent delivery](../2026-11-14-compiled-literal-tuples/README.md).
Publication/tagging and optional-adapter promotion remain held.

| Complete script / phase | Lython ms | CPython ms | Lython / CPython | Lython/CPython process spread | Max IQR |
| --- | ---: | ---: | ---: | ---: | ---: |
|csv-invoice.large /warm|7.276859–7.458060|6.028798–6.091137|1.19–1.24×|2.43/1.03%|1.13%|
|csv-invoice.large /compile|0.102060–0.102399|0.093261–0.093912|1.09–1.10×|0.33/0.69%|1.48%|
|csv-invoice.large /compile-run|7.629655–7.714891|6.226331–6.311671|1.21–1.24×|1.10/1.35%|1.04%|
|text-summary-helpers.nonlocal-counter.large /warm|1.284181–1.322978|0.886526–0.902273|1.42–1.49×|2.93/1.78%|0.92%|
|json-summary-helpers.large /warm|3.371563–3.429722|4.067791–4.074689|0.83–0.84×|1.72/0.17%|1.69%|
|file-search.large /warm|0.263819–0.266368|0.157580–0.161473|1.63–1.69×|0.96/2.41%|2.22%|
|report-render.large /warm|2.784965–2.905760|2.321379–2.337721|1.19–1.25×|4.16/0.70%|6.20%|
|csv-fulfillment.large /warm|8.546944–8.624011|6.707523–6.741066|1.27–1.29×|0.89/0.50%|1.05%|
|csv-check-digits.for-text.large /warm|5.372545–5.390606|4.176471–4.213872|1.27–1.29×|0.34/0.90%|1.29%|

Ratio = Lython elapsed / CPython elapsed, above one means slower. Ranges pair
two fresh process medians per engine in all four combinations, not confidence
intervals. Report process spread4.16% and maximumIQR6.20% remain uncertainty.
Earlier unchanged/adverse controls and original failed conditions survive; no
causal gain is inferred by subtracting earlier collections.

Quiet AzureF4as_v7, Ubuntu24.04.4, four EPYC9V45vCPUs/16GiB/no swap,
.NET11RC3 ordinary workstation GC/tiering and CPython GIL/GC. Existing B685workers,
optional nonpackable0.4adapter and unchanged B717nine-job catalog/goldens. Each job:
four fresh processes, five-second full-request warmup (12s captured text/report),
five rotating0.5s batches sized by full-request wall time. Guest clocks unchanged.

Warm execution includes mediated RAM-file I/O, stdout/file validation and prior
result cleanup; excludes compile/startup, fixture staging/reset, protocol and final
cache shutdown. Compilation alone includes compiled-owner disposal. Compile-to-
result adds compilation/disposal to execution. Wider process CPU includes reset/
protocol. Independently warmed phases cannot be subtracted for attribution.
No startup, shared-contention, pending-I/O, RSS/heap or embedding-tail claim.

Nine jobs complete360.499s within one480sowner. All exact image/
auxiliary/golden/budget/quiet/join/archive gates pass. No remaining workers or RAM
fixtures. First staging stopped at the unchanged254MiBguard before mount/service;
failure is preserved. One canonical-proven duplicate old tool was retired, identical
staged hashes and floor verified, and the first measurement owner started. No
measurement restart. Current images/catalogs and254/252MiBfloors remain intact.

Persistent raw SHA256: `1230e5a22882985406fd9286031430416642768c9bd2cdf516d71884896b8991`.
[Summary/identities](summary.json), [raw](observations.tgz),
[collection tools and staging correction](collection-tools.tar.xz),
[previous guide](prior-BENCHMARKS.md). Exact source/core and focused checks are in
the [R351archive](../2026-11-14-compiled-literal-tuples/sources-checks-and-observations.tar.xz).
This checkpoint informs the continuing whole-script roadmap; it does not complete
the performance objective or demonstrate a uniform speedup.

The measured source subsequently [passes full43579225278qualification](../2026-11-14-compiled-literal-tuples/delivery/README.md).
Collection-time pending identity/prospective records remain unmodified.
