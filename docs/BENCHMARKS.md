# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Status |
| --- | --- | ---: | ---: | ---: | --- |
| [Text summary/helper](../benchmarks/results/2026-10-18-text-milestone/README.md) | 3,072 lines / 35,840 bytes | 7.354–7.451 ms | 1.527 ms | 4.818–4.881× | Historical diagnostic, `4acd532f`, 2026-10-18; unqualified |
| [CSV summary/helper](../benchmarks/results/2026-10-18-csv-milestone/README.md) | 4,096 records / 57,361 bytes | 8.549 ms | 3.962 ms | 2.158× | Historical diagnostic, `34d4da35`, 2026-10-18; unqualified |
| [JSON config read/edit/write](../benchmarks/results/2026-10-18-config-checkpoint/README.md) | 16 paths / 181 bytes | 2.584–2.769 ms | 1.399 ms | 1.847–1.980× | Historical diagnostic, `348998dc`, 2026-10-18; unqualified |
| [JSON-lines helper](../benchmarks/results/2026-10-19-json-checkpoint/README.md) | 4,096 records / 110,592 bytes | 9.261–9.541 ms | 4.461 ms | 2.076–2.139× | Historical diagnostic, `a22c11b8`, 2026-10-19; unqualified |
| [Copy/checksum/manifest](../benchmarks/results/2026-10-18-manifest-checkpoint/README.md) | Two files / 4,107 bytes | 2.027–2.251 ms | 0.597 ms | — | Historical, `3ea431fd`, 2026-10-18; inconclusive, 30–32% spread and differing controls |

**Current conclusion:** these maintained workload snapshots show remaining
performance gaps. No full representative CPython comparison qualifies today's
HEAD, and no multiplier above is a certified current-release result. A ratio
divides Lython elapsed time by CPython elapsed time; above one means slower.
Ranges contain medians from identical Lython controls, not confidence intervals.
A dash denotes an inconclusive ratio, not zero. Text/JSON control spreads also
limit interpretation; linked receipts retain the details.

## Measurement boundary

The table measures **fresh compilation plus one synchronous execution in an
already-running host**. Each job includes invocation setup, complete file work,
reset/output checks and relevant cleanup. Startup/transport are excluded. Both
engines execute identical Python source and validate complete stdout/file results.
The host supplies efficient ranged reads/appends; whole-file compatibility
fallbacks are not an explanation of these gaps.

Machine: dedicated Azure **Standard_F4as_v7**, Ubuntu 24.04.4 x86-64, four AMD
EPYC 9V45 vCPUs, one thread/core, 16 GiB RAM, no swap. Reference: **CPython
3.13.16**, GCC 13.3, PGO/LTO, ordinary GIL/GC, no JIT/debug mode, launched `-I -S`.
VM toolchain used .NET SDK 10.0.401/runtime 10.0.12; exact binary/toolchain
identities are in each capture. Engines ran sequentially with ordinary GC/tiering.

Keep distinct measurements for:

| Boundary | Includes |
| --- | --- |
| Warm execution | Reused compiled code plus public invocation/setup, workload and declared cleanup; excludes compilation/startup |
| Fresh compile/run | Fresh frontend work plus one complete execution in a warm host |
| Compilation | Frontend/code preparation only; not inferred by subtracting unrelated medians |
| Startup | A new process and its explicitly declared first-result boundary |

For example, the same historical CSV snapshot records warm `RunAsync` at 9.321 ms
versus CPython 3.759 ms (2.480× diagnostic). Its host operations completed
synchronously; it does not measure suspended I/O and cannot be substituted for
the fresh/synchronous row.

## Evidence and reproduction

Use the [existing comparison tools](../benchmarks/COMPARISON.md) and complete
workload goldens. Development uses short focused diagnostics; ten-minute lanes
are infrequent milestones. Qualification requires stable controls and declared
machine/execution boundaries, not a favorable point sample.

[Earlier-Lython improvements and measurement history](../benchmarks/HISTORY.md)
are separate from the CPython baseline. [Experiments](../benchmarks/EXPERIMENTS.md)
retain profiles, rejected changes and inconclusive results. They are historical
evidence, not current architecture or supported runtime contracts.
