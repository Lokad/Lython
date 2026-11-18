# B993 retained 0.11.0 workloads against CPython

Measured `d63004a6f` / frozen core `dd0dc932` on 2026-11-18 UTC against
stock CPython 3.13.16 (PGO/LTO, GIL enabled, JIT disabled). The four-vCPU
Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11.0.0-rc.3.26522.148. Two
processes per engine and case verified exact stdout and file goldens before
and after five measured rounds. The same processes alternated compile,
warm and compile-to-result. Each phase warmed for 3–6 seconds. The
collection completed in 253.65 seconds with both quiet checks passing.
Workers joined and RAM-backed fixtures were removed before archive
verification.

| Workload | Lython / CPython compile | Lython / CPython warm | Lython / CPython compile-to-result |
| --- | ---: | ---: | ---: |
| CSV invoice, 4,096 data records | 1.03–1.07× | 1.09–1.12× | 1.08–1.11× |
| Helper text normalization, 4,096 lines | 1.01–1.02× | 1.35–1.36× | 1.29–1.35× |
| Expanded forwarding report, 1,024 records | 0.87–0.88× | 2.81–2.91× | 2.61–2.68× |
| Configuration edit control, 181 B | 1.05–1.06× | 1.22–1.24× | 1.05–1.07× |

Ratios pair the two process medians per engine. The report's warm Lython
and CPython same-source spreads were 2.06% and 1.41%, with maximum IQR
0.84%. Its compilation is faster and the gap is in execution. This result
does not attribute the excess to any individual part of the runtime. The
helper's earlier CPython phase inversion did not recur. Its compile-to-result
CPython control spread was 3.96%.

Warm elapsed includes mediated RAM-backed file I/O, full golden checks and
previous-result cleanup. Compile-to-result adds compilation and compiled-owner
disposal. Compilation includes compiled-owner disposal. Startup, fixture reset,
protocol and final cache teardown are outside these clocks. Process CPU
includes wider worker activity and is retained separately.

An initial staging preflight was short 307,414 bytes after the proposed
recovery and changed no VM files. The successful setup verified additional
archived replicas by path, owner, inode, link count and archive hash. The
exact B992 image and canonical archives were preserved. This is a diagnostic
comparison across engines at a recorded revision. Earlier source-pair
results remain the evidence for individual changes.

[Times, ratios and control spreads](summary.json) ·
[Observations](observations.tgz) · [Procedure and preflight](procedures.tar.xz)
