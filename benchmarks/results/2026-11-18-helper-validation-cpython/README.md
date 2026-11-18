# Helper text and CSV validation against CPython

R517/B946, measured 2026-11-18 UTC: committed **7db1de02 / frozen c0367d98**
versus stock **CPython 3.13.16** (PGO/LTO, GIL, no JIT). Azure F4as_v7,
4 vCPU AMD EPYC 9V45 / 16 GiB, Ubuntu 24.04.4, .NET 11 RC3, default GC/tiering.
Six jobs / 227.62 seconds; six-second warmups, five rotated rounds, two processes
per engine. Existing worker protocol/catalog, no new measurement framework.

**Qualification limitation:** full run 44247104176 failed existing entry-error
context regressions on both platforms. Cancellation/refusal after a forwarded
call result can acquire operation context. A subsequent correction must be
qualified independently. These observations remain useful for these exact
successful scripts, but describe neither corrected HEAD nor a qualified release.

| Complete script | Phase | Lython | CPython | Lython / CPython | Status |
| --- | --- | ---: | ---: | ---: | --- |
| Helper text normalization | Compilation only | 0.046–0.047 ms | 0.044–0.044 ms | 1.03–1.05× | Diagnostic7db/c036; controls L/C 1.16/0.53%; IQR≤0.38% |
| Helper text normalization | Warm execution | 1.117–1.124 ms | 0.854–0.876 ms | 1.28–1.32× | Diagnostic7db/c036; controls L/C 0.63/2.58%; IQR≤1.56% |
| Helper text normalization | Compile-to-result | 1.223–1.244 ms | 0.809–0.810 ms | 1.51–1.54× | Diagnostic7db/c036; controls L/C 1.74/0.13%; IQR≤4.89% |
| CSV field validation | Compilation only | 0.026–0.026 ms | 0.027–0.027 ms | 0.96–0.98× | Diagnostic7db/c036; controls L/C 1.70/0.30%; IQR≤2.30% |
| CSV field validation | Warm execution | 2.975–3.003 ms | 4.311–4.327 ms | 0.69–0.70× | Diagnostic7db/c036; controls L/C 0.92/0.37%; IQR≤2.99% |
| CSV field validation | Compile-to-result | 2.976–3.039 ms | 4.384–4.402 ms | 0.68–0.69× | Diagnostic7db/c036; controls L/C 2.12/0.42%; IQR≤0.74% |

Ratios above one mean slower. Ranges combine the four process-median pairs, not
confidence intervals. Helper text warm is 1.28–1.32× and fresh 1.51–1.54×;
CSV validation warm is 0.69–0.70× and fresh 0.68–0.69×. Helper fresh IQR reaches
4.89%; all adverse samples and controls remain in the raw archive. This is an
external baseline, not a causal improvement over another Lython revision.

Helper input: 4,096 lines / 52,224 B. CSV: 4,128 rows / 128,183 B, including
32 malformed fields, quoted newlines and Unicode. The complete script parses
quantity and price, counts valid/invalid rows and emits fixed independent goldens.
This new validation script complements the existing numeric CSV report.

Warm includes Run, mediated RAM I/O, goldens and previous-result cleanup. Fresh
adds compilation/compiled-owner disposal; compilation includes owner disposal.
Startup/reset/request framing/final cache teardown are outside guest clocks;
process CPU is wider. Separately warmed phases are not additive. CPython native
files do not provide equivalent containment/ownership. All 32 image files and
the actual catalog passed verification. Every worker verifies outputs and Closed;
quiet gates, joined users and persistent/local SHA archives passed before RAM
teardown. Existing space floors and current images remain intact.

[Summary](summary.json), [raw](observations.tgz), [identity](identity.json),
[catalog](catalog.json), [goldens](preflight.json), [procedures](procedures.tar.xz),
[previous guide](prior-BENCHMARKS.md). Publication remains postponed.
