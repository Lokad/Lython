# Affected complete-script CPython checkpoint

R511/B936,2026-11-18 UTC, committed retained **2e585229 / frozen1bce52ad** versus
stock **CPython3.13.16**(PGO/LTO,GIL,no JIT). Full retained delivery44225923745
is independently pending; no inherited qualification. Quiet AzureF4as_v7/4vCPU
AMD EPYC9V45/16GiB/Ubuntu24.04.4/.NET11RC3, default GC/tiering. Two affected
complete scripts/three phases, six jobs226.36seconds; six-second warmups/five
rotated rounds/two processes per engine. Existing workers/catalog/protocol only.

| Complete script | Phase | Lython | CPython | Lython / CPython | Status |
| --- | --- | ---: | ---: | ---: | --- |
| Text normalization | Compilation only | 0.036–0.036 ms | 0.037–0.037 ms | 0.95–0.96× | Diagnostic2e5/1bce; controls L/C 0.12/0.78%; IQR≤0.66% |
| Text normalization | Warm execution | 0.803–0.804 ms | 0.501–0.503 ms | 1.60–1.60× | Diagnostic2e5/1bce; controls L/C 0.04/0.38%; IQR≤1.03% |
| Text normalization | Compile-to-result | 0.873–0.878 ms | 0.593–0.595 ms | 1.47–1.48× | Diagnostic2e5/1bce; controls L/C 0.56/0.39%; IQR≤0.35% |
| Floating JSON numeric report | Compilation only | 0.056–0.057 ms | 0.061–0.061 ms | 0.91–0.93× | Diagnostic2e5/1bce; controls L/C 1.97/0.43%; IQR≤0.86% |
| Floating JSON numeric report | Warm execution | 1.320–1.346 ms | 0.813–0.818 ms | 1.61–1.66× | Diagnostic2e5/1bce; controls L/C 1.99/0.60%; IQR≤2.50% |
| Floating JSON numeric report | Compile-to-result | 1.371–1.397 ms | 0.948–0.995 ms | 1.38–1.47× | Diagnostic2e5/1bce; controls L/C 1.93/4.91%; IQR≤0.98% |

Text normalization processes4,096lines/52,224B; floating JSON4,096pairs/56,495B.
Ratio is Lython elapsed / CPython elapsed; above one means slower. Ranges cover
all four combinations of process medians, not confidence intervals. Warm text
controls are small; floating fresh CPython control4.91%limits precision. Compilation
is slightly faster here but does not explain away the remaining warm execution
gaps. Do not infer a causal gain by subtracting older source/collection conditions.
The previous5.49/8.33%floating Lython controls and all earlier adversity remain in
their original [checkpoint](../2026-11-18-agent-cpython-checkpoint/README.md).

Warm includes Run, mediated RAM-backed I/O, stdout/file goldens and prior-result
cleanup. Fresh adds compilation/compiled-owner disposal; compilation includes
owner disposal. Startup/reset/request framing/final cache teardown excluded;
wider process CPU includes reset/protocol. Separately warmed phases are not
additive. CPython uses native files without equivalent containment/ownership.

All32image hashes were reverified; unchanged actual-image/six-case independent
goldens and allocation/reuse from R510 were reused, not repeated. Every VM worker
also verifies goldens/Closed. Both quiet gates, all joined users and persistent/local
SHA archives pass before RAM teardown. No images retired for this collection;
current/qualified images/catalogs/toolchains and254/252MiBfloors remain intact.
Publication postponed; current cost attribution and the wider redesign continue.

[Summary](summary.json),[raw](observations.tgz),[identity](identity.json),
[catalog](catalog.json),[actual-image verification](preflight.json),
[verification reuse](preflight-reuse.json),[procedures](procedures.tar.xz),
[previous guide](prior-BENCHMARKS.md).
