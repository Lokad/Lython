# Current representative CPython checkpoint

2026-11-13UTC, R332/B704. Committedd90dd5fe, frozen798edc1d; all578production
inputs match the R331 snapshot. Fixed B685workeracda3ef3 and optional nonpackable
adapter0.4, stock CPython3.13.16, quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3,
four EPYC9V45vCPUs/16GiB/no swap, ordinary GC/tiering and CPython GIL/GC.
Focused actual .NET11/10 checks pass; full Windows/Ubuntu, original stress and installed-package qualification43520131516 is still pending.

| Representative workload, warm execution | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV invoice / pricing helpers |4,096 records /127,031 B|7.646–7.662 ms|6.082–6.108 ms|1.25–1.26×|Diagnostic; spreads0.21/0.43%; maxIQR2.78%|
| Text / captured helper and counter |3,072 lines /35,840 B|1.377–1.388 ms|900.3–912.7 µs|1.51–1.54×|Diagnostic; spreads0.84/1.38%; maxIQR1.83%|
| JSON-lines / aggregation helpers |4,096 records /110,592 B|3.390–3.489 ms|4.089–4.134 ms|0.82–0.85×|Diagnostic; spreads2.92/1.10%; maxIQR1.05%|
| File search / keyword-default helper |2,049 lines /42,010 B; stop after1,025|260.5–265.7 µs|157.7–158.2 µs|1.65–1.69×|Diagnostic; spreads1.98/0.34%; maxIQR5.01%|
| JSON-to-Markdown report |3,072 records /189,441 B|2.825–2.848 ms|2.330–2.334 ms|1.21–1.22×|Diagnostic; spreads0.82/0.15%; maxIQR4.62%|

Invoice compilation 101.5–102.4 µs versus93.2–93.6 µs,
1.08–1.10×; compile-to-result 7.691–7.876 ms versus
6.201–6.268 ms,1.23–1.27×. All seven jobs complete in
294.095s under one360s owner, with two independent processes per
engine, five rotating rounds and unchanged command-v2 clocks.

Warm includes mediated RAM-file I/O, full stdout/files validation and prior-result
cleanup; excludes compilation, startup, fixture reset/protocol and final cache
disposal. Compile-to-result adds compilation/compiled-owner disposal. Compile-only
includes compilation/owner disposal. Wider CPU includes reset/protocol. Warmups
use full-request wall budgets (5s,12s for captured text/report); batch sizing
targets0.5s full-request wall time. Original warm responses remain available for
guest-time totals. Ranges pair process medians, not confidence intervals.

This is a current external diagnostic, not a source-pair CPU/allocation experiment.
No causal speedup is inferred against B683 or earlier sources. Keep moving controls,
all original adverse observations and earlier qualification labels. Identity,
golden-output, allowance/close, quiet, joined-user and archive checks pass. Guest
fixtures are removed/unmounted after all users end; observations remain persistent
and the resulting archive is independently SHA-verified. No working artifacts are
retired, no storage floor changed, no VM users remain. No embedding-tail, pending-I/O,
startup or live-heap claim follows. Publication remains postponed.

The original B703setup fails its case-presence assertion before guest work: a
two-case catalog was mistakenly supplied. Its journal/setup are preserved and
empty fixture mount joined/unmounted. Corrected B704 initially refuses staging
at the unchanged disk floor, both for the complete historical catalog and for
only its five measured cases. Cases retain exact source/fixture/golden fields;
omitting unused cases changes no measured workload. Under an idle lease,
apt-get clean removes only rebuildable downloaded packages, recovering131,891,200B;
benchmark images/evidence remain intact. Successful staging retains the floor.
Original failed commands, payloads and conditions remain in the tooling archive.

[Summary/wider CPU](summary.json), [raw](observations.tgz),
[exact tooling/catalog](measurement-tools.tar.xz), [first failed setup](first-setup-failed.tgz),
[previous guide](prior-BENCHMARKS.md).
The exact frozen sources/bits and focused checks are reused from
[R331](../2026-11-13-dictionary-boundaries/source-checks.tar.xz); qualification is
tracked [there](../2026-11-13-dictionary-boundaries/README.md), without repeating it.
