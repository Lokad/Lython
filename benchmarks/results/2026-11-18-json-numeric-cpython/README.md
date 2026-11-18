# Numeric JSON against CPython

R535/B966, measured 2026-11-18 UTC on the quiet Azure F4as_v7 VM, Ubuntu
24.04.4. Lython used the frozen 40db9318 core with .NET 11 RC3 and the
optional 0.4.0 adapter. It contains R534 on dd4c5165c. The JSON change is
retained in 55fe3da03. This image predates the merged R533 regex change.
The reference was stock CPython 3.13.16 with PGO/LTO and the GIL enabled.

| Complete script | Phase | Lython | CPython | Lython / CPython | Lython / CPython control spread |
| --- | --- | ---: | ---: | ---: | ---: |
| Floating JSON report, 4,096 pairs / 56,495 B | Compile | 0.0557–0.0560 ms | 0.0604–0.0606 ms | 0.92–0.93× | 0.38 / 0.17% |
| Floating JSON report | Warm | 1.036–1.063 ms | 0.833–0.835 ms | 1.24–1.28× | 2.56 / 0.33% |
| Floating JSON report | Compile-to-result | 1.138–1.139 ms | 0.964–0.972 ms | 1.17–1.18× | 0.14 / 0.86% |
| Integer JSON-lines aggregation, 4,096 records / 110,592 B | Warm | 3.444–3.606 ms | 4.227–4.238 ms | 0.81–0.85× | 4.69 / 0.27% |

The ratio divides Lython elapsed by CPython elapsed across all pairs of process
medians. Two independent processes per engine used eight seconds of rotated
warmup and five measured rounds. The integer fixture includes 1,024 blank lines.
The 4.69% Lython control spread limits its precision. These are diagnostic
comparisons of the exact frozen image. The earlier 3f/a503 measurements remain
available separately. [Prior checkpoint](../2026-11-18-bindings-cpython-checkpoint/README.md)

Warm execution includes standard script work, mediated RAM-backed file I/O,
golden output checks and prior-result cleanup. Compile measures compilation
and compiled-owner disposal. Compile-to-result also includes one execution.
Startup, protocol, fixture reset and final cache teardown are outside guest
clocks. Process CPU includes wider costs and is archived. CPython uses its native
file APIs. Source-change speedups come from the separate two-Lython comparison.
[R534 source pair](../2026-11-18-json-number-lexing/README.md)

All 32 image hashes and the unchanged catalog were reverified. The four jobs
completed in 187.31 seconds. Actual output checks, quiet endpoints, worker
shutdown, fixture cleanup and the archived evidence hash all passed.
[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Identity](identity.json) · [Procedures](procedures.tar.xz)
