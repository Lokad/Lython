# B1512 CPU attribution on the retained call checkpoint

The complete 32-file Lython image from `b7fdb4fec` ran on the same quiet
Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 as B1510, on
November 23, 2026 UTC. The three workload goldens match CPython 3.13.16
before profiling and after the run. [B1510 elapsed and CPU comparison](../2026-11-23-retained-call-cpython/README.md).

Linux perf sampled the worker process at 199 Hz for about five guest
seconds per workload. Captures cover whole batches, including boundary
work such as verification, mediated RAM-backed I/O, result disposal and
reset. Short text includes compilation and code disposal in the warm
host. The numerical helper and invoice reuse compiled code. Bounds use
the guest-reported clock. The sampling window also admits CPU on other
worker threads, including JIT work, and uses a JIT symbol map.

| Workload | Guest clock | All samples | Resolved runtime ancestry | Nearest frontend | Nearest result publication | I/O ancestry | Retirement or cleanup ancestry | Unmapped leaves |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Fully supplied arithmetic, warm | 5.07 s | 978 | 971 | 0 | 74 | 0 | 25 | 52 |
| Short nonlocal word report, compile to result | 4.92 s | 1,020 | 913 | 41 | 21 | 82 | 6 | 130 |
| Quote-heavy invoice, warm | 5.40 s | 1,096 | 1,068 | 7 | 63 | 125 | 19 | 93 |

On supplied arithmetic, the nearest resolved runtime method is builtin
arithmetic in 242 samples, prefix dispatch in 233, direct integer return
entry in 92 and result-consumer completion in 74. Admission appears in
one inclusive stack. The tape evaluates two independent integer arguments
before the eligible direct call and also dispatches the accumulated
update afterward. A complete caller-side design needs to remove dispatch
or transport around work that the integer routine still has to do.

The first-use word report has 137 nearest dictionary-loop samples,
108 prefix dispatch and 81 cased-word preparation. The quoted invoice
has 156 nearest prefix dispatch, 117 native loop, 63 result-consumer
completion, 42 builtin arithmetic and 38 physical CSV line reads. These
workloads control changes in shared execution paths. Ancestry counts
overlap, and a nearest resolved method may contain inlined work. These
are attribution samples from one capture per workload. B1510 provides
the matched elapsed comparison.

The original attempt reached only 5.551 guest seconds in its allowed
warmup batches and stopped before attaching the profiler. The corrected
run doubles supplied-helper iterations per batch from 128 to 256 with
the same code, manifest protocol and eight-second warmup requirement.
Both quiet gates complete, all workers join and the owned guest fixtures
are removed. The corrected archive was persisted and verified under the
collection lease. [Image identity](identity.json) · [Attribution](attribution.json) ·
[Terminal receipt](terminal-status.json) · [Raw captures](observations.tar.xz) ·
[Procedures and original attempt](procedures.tar.xz).
