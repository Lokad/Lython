# 0.11.0 development delivery comparison

Measured once **2026-10-29**, source **da0ff81155c27609e9d3904043af8fff2ba76943**,
0.11.0 development, SDK/runtime .NET 11 RC3 **11.0.100-rc.3.26522.148**.
Reference **CPython 3.13.16**, GCC 13.3, PGO/LTO, ordinary GIL/GC, no JIT,
`-I -S`. Dedicated Azure **Standard_F4as_v7**, Ubuntu 24.04.4 x64, four EPYC 9V45
vCPUs, 16 GiB RAM, no swap. All six jobs match full stdout and output-file goldens;
both quiet gates pass. Collection takes **49.79 seconds**. **All timings are
diagnostic and unqualified. No repeat collection or favorable sample selection.**

## CPython is the primary baseline

The existing micro harness times fresh compilation plus one complete synchronous
execution inside already-running workers. Identical maintained Python scripts,
input bytes and complete expected outputs apply to all engines. Per-job setup,
file reset/staging, actual filesystem work, output verification and declared
cleanup are inside the clock. Process startup, transport and digest encoding are
outside. Ordinary worker GC/tiering and containment remain active. The host
implements efficient ranged reads/appends; it does not repeatedly acquire whole
files for every range. Reusable-code execution is a different boundary.

Medians in milliseconds. Ratio = development Lython median / CPython median;
above one means slower. Ratios here are descriptive point estimates, not qualified
current-release multipliers. IQR = interquartile range / median.

| Workload | Input bytes | Lython ms | CPython ms | Point ratio | Lython IQR | Status |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| CSV helpers | 57,361 | 29.402 | 3.701 | 7.94x | 561.8% | Inconclusive, severe variation |
| Text helpers | 35,840 | 28.366 | 1.520 | 18.66x | 25.5% | Diagnostic, substantial variation |
| JSON-lines helpers | 110,592 | 28.744 | 4.608 | 6.24x | 79.3% | Inconclusive |
| Config read/edit/write, small | 181 | 2.155 | 1.551 | 1.39x | 14.4% | Diagnostic |
| Copy/checksum/manifest | 4,107 | 1.812 | 0.597 | 3.03x | 30.1% | Inconclusive |
| File search with early exit | 42,010 | 2.959 | 0.405 | 7.30x | 114.6% | Inconclusive |

The observations still show substantial gaps against CPython. They do not establish
stable ratios or a source regression/improvement. Quiet machine checks cannot
remove engine-internal scheduling, JIT/tiering or GC variation. Those are possible
contributors, not a measured causal explanation. Do not normalize away costs or
repeat until a favorable median appears. The earlier isolated CPU/allocation
diagnostics have their own revision/boundary and cannot replace this collection.

## Published Lython, separately

The earlier comparator loads the **actual published 0.10.0 core DLL/PDB**, source
aff8965fc18a722f19be819a03f4ea16128dd31c, on **.NET 10.0.12**. Its producer SDK is
10.0.300-preview.0.26177.108; its comparison harness was rebuilt with 10.0.401.
Its core DLL SHA256 is fb3aabbba96716cedc6a01faecad5906a46fb46b26c0a5a260927559b1b8a86b.
This is a release-plus-runtime comparison, not an isolated interpreter optimization.

| Workload | Published 0.10 ms | Development 0.11 ms | Old / new IQR |
| --- | ---: | ---: | --- |
| CSV helpers | 15.642 | 29.402 | 936.6% / 561.8% |
| Text helpers | 7.576 | 28.366 | 13.3% / 25.5% |
| JSON-lines helpers | 15.082 | 28.744 | 177.4% / 79.3% |
| Config edit | 2.071 | 2.155 | 18.5% / 14.4% |
| File manifest helpers | 1.983 | 1.812 | 17.7% / 30.1% |
| File search | 1.543 | 2.959 | 9.1% / 114.6% |

Keep the higher development medians visible. No overall elapsed improvement is
claimed. Separate accepted structural evidence demonstrates bounded CLR call-stack
growth and lower compilation allocation, not a qualified complete-script speedup.

## Reproduction and evidence

[collect.py](collect.py) holds the existing VM lease and uses the established
comparison micro CLI, six fixed sources, seven rotating 25 ms batches, one second
of warmup per engine and the thirty-second owned systemd boundary per job. A
prospective ninety-second collection budget includes cleanup. Only the supervisor
disables tiering; workers use ordinary tiering. No ten-minute lane, new benchmark
framework, production instrumentation, private runtime policy or forced GC.

[raw-results.tar.gz](raw-results.tar.gz) preserves every unmodified receipt/log,
the collection record, input/output/source digests, exact core/toolchain identities,
raw rounds and before/after machine gates. It includes the full payloads necessary
to reproduce the fixed cases. Existing [comparison methods](../../COMPARISON.md)
and [current guide](../../../docs/BENCHMARKS.md) define the declared boundaries.
Compilation/startup, runtime phases and genuinely delayed I/O have separate linked
records; none is estimated by subtracting unrelated medians here.
