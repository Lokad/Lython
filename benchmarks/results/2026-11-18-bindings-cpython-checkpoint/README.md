# Prepared-binding CPython checkpoint

R522/B952, measured2026-11-18UTC: committed **3f873227 / frozen a5038a28** versus
stock **CPython3.13.16** (PGO/LTO,GIL,noJIT). Quiet AzureF4as_v7,4vCPU AMD EPYC9V45,
16GiB/Ubuntu24.04.4/.NET11RC3/defaultGC/tiering. Two complete scripts, three phases,
sixjobs226.44s; six-second full-request warmups/five rotated rounds/two processes
per engine. Full retained delivery44256409242 independently pending; no inherited
qualification. Existing workers/verified four-case catalog; no new framework.

| Complete script | Phase | Lython | CPython | Lython / CPython | Status |
| --- | --- | ---: | ---: | ---: | --- |
| Helper text normalization | Compilation only | 0.047–0.047 ms | 0.044–0.044 ms | 1.05–1.06× | Diagnostic3f/a503; controls L/C 0.47/0.39%; IQR≤0.97% |
| Helper text normalization | Warm execution | 1.130–1.137 ms | 0.856–0.907 ms | 1.25–1.33× | Diagnostic3f/a503; controls L/C 0.57/6.02%; IQR≤1.52% |
| Helper text normalization | Compile-to-result | 1.229–1.231 ms | 0.802–0.830 ms | 1.48–1.53× | Diagnostic3f/a503; controls L/C 0.13/3.48%; IQR≤3.43% |
| Floating JSON numeric report | Compilation only | 0.056–0.056 ms | 0.061–0.061 ms | 0.91–0.92× | Diagnostic3f/a503; controls L/C 0.03/0.87%; IQR≤0.75% |
| Floating JSON numeric report | Warm execution | 1.137–1.165 ms | 0.813–0.820 ms | 1.39–1.43× | Diagnostic3f/a503; controls L/C 2.52/0.78%; IQR≤0.79% |
| Floating JSON numeric report | Compile-to-result | 1.251–1.268 ms | 0.954–0.956 ms | 1.31–1.33× | Diagnostic3f/a503; controls L/C 1.37/0.19%; IQR≤1.00% |

Ratio above one means slower. Ranges combine all four process-median pairs, not
confidence intervals. Helper warm CPython control6.02% limits precision; helper
fresh CPython control3.48% remains visible. Floating warm Lython control2.52%,
fresh controls1.37/0.19%. No cross-collection causal source improvement is inferred.
The separate source-pair comparison supports a named floating warm/fresh gain;
the remaining CPython gap is still material. Earlier adverse/unqualified helper
measurements stay in [R517](../2026-11-18-helper-validation-cpython/README.md).

Helper input4,096lines/52,224B; floatingJSON4,096pairs/56,495B. Warm includes Run,
mediated RAM I/O, stdout/file goldens and previous-result cleanup. Fresh adds
compilation/compiled-owner disposal; compilation includes owner disposal. Startup,
fixture reset, request framing and final caches are outside guest clocks; process
CPU is wider. Separately warmed phases are not additive. CPython native files do
not provide equivalent containment/ownership. All32image files reverified; exact
four-case independent actual-image goldens reused, not repeated. Every worker
checks outputs/Closed; all users joined, quiet gates/SHAarchive passed before RAM
teardown. No replica retirement, space floors/current images remain unchanged.

[Summary](summary.json),[raw](observations.tgz),[identity](identity.json),
[catalog](catalog.json),[verification](preflight.json),[reuse](preflight-reuse.json),
[procedures](procedures.tar.xz),[previous guide](prior-BENCHMARKS.md).
Publication postponed; broader performance work continues.
