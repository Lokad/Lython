# Maintained record-transform versus CPython

2026-11-17 UTC,B927; committedea8e9f91 production/frozencf847936 versus stock
CPython3.13.16(PGO/LTO,GIL,noJIT). Quiet AzureF4as_v7,4EPYC9V45vCPU/16GiB,
Ubuntu24.04.4/.NET11RC3. Three jobs116.40s, six-second rotated full-request warmups,
five rounds/two independent processes per engine, naturalGC/tiering. Existing
workers/protocol/catalog; no new framework or unchanged four-workload replay.

Complete maintained JSON record transform:4,096records/110,592B. It parses each
line, calls an ordinary helper returning a two-field dict, aggregates and writes
a validated report. Same independent JSON input/output/file goldens as the helper
aggregation; no predicted ratio from the earlier within-Lython improvements.

| Workload | Phase | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| JSON-lines record transform | Compilation only | 0.057–0.058 ms | 0.056–0.056 ms | 1.01–1.02× | Diagnostic; controls L/C 1.68/0.08%; IQR≤1.81% |
| JSON-lines record transform | Warm execution | 3.898–4.012 ms | 4.388–4.423 ms | 0.88–0.91× | Diagnostic; controls L/C 2.93/0.79%; IQR≤0.79% |
| JSON-lines record transform | Compile-to-result | 4.085–4.220 ms | 4.517–4.546 ms | 0.90–0.93× | Diagnostic; controls L/C 3.31/0.63%; IQR≤1.11% |

Ratios use all combinations of process medians, not confidence intervals. Lython
warm2.93%/fresh3.31%same-source movement limits precision; original controls/CPU/
IQRs remain. No causal source improvement or general speedup is inferred. This
fills a missing named external baseline; it does not replace or relabel the valid
four-workload0d2a6ab7/8d26963c checkpoint. Current text/floating gaps remain visible.

Warm includes Run, mediated RAM I/O, all stdout/file golden checks and prior-result
cleanup. Fresh adds compile/compiled-owner disposal; compilation-only includes
compiled-owner disposal. Reset/protocol/startup/final cache teardown excluded;
processCPU wider. CPython native filesystem is uncontained; this is useful script
latency, not equal containment. Independent warmed medians are not additive.

All32frozen files(copies, including worker/optional adapter) and the unchanged
six-case catalog compare to the actual R499 verification before staging. Reuse
that exact image/catalog preflight, not a core-only guess. The maintained script
text equals catalog source. Identity/source-archive hashes identify measured
inputs; all585production inputs subsequently qualified at ea8e9f91 by full
Windows/Ubuntu actual11/10,originalstress/installed Release consumers. No rebuilt
binary equality is inferred. [Delivery](../2026-11-17-constant-record-keys/delivery/README.md),
[sources](../2026-11-17-constant-record-keys/candidate-sources.tar.xz).

Quiet/goldens/allowance/Closed/zero-exit/join gates pass. Persistent archive verified
under lease and locally; fixtures removed. Only exact committed unpromotedbfef
core replica retires; currentcfimage/catalog/toolchains and original adverse
evidence stay.254/252MiB guards unchanged. Reused collection/service limits were
corrected locally to160/210s before launching; original prepared limits retained.
No collection failed or condition was retimed. Terminal cached; publication held.

[Summary](summary.json),[raw](observations.tgz),[identity](identity.json),
[catalog](catalog.json),[reused verification](reused-preflight.json),[procedures](procedures.tar.xz).
