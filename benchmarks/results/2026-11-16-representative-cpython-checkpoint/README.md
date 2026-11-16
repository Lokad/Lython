# Representative CPython checkpoint

R437/B835–B836,2026-11-16UTC. Committed312f5911,measured0443b5cf versus
CPython3.13.16. Twelve complete compile/warm/compile-run jobs, two bounded six-job
collections. [Current guide](../../../docs/BENCHMARKS.md) defines phases,inputs,
ratios and uncertainty; [summary](summary.json) preserves every process/control.

Fixed four-case catalog43b7c6b0 loaded once by every worker in both collections;
same exact source/fixture/goldens and command-v2 workers. All complete stdout/file
goldens, actual library/catalog identities, quiet-before/after, Closed/exit/join,
persisted archive/hash and RAM teardown checks pass. No users abandoned. Normal
tiering/natural GC, five-second full-request warmup/five rotated~0.5s rounds,
four process pairings per row, not confidence intervals. Full wider CPU and all
warmup responses remain; compilation/startup are not part of warm execution.

Quiet F4as_v7/Ubuntu24.04.4,four EPYC9V45vCPUs,16GiB/no swap,.NET11RC3 natural
workstation GC and stock CPython GIL/GC. Lython uses mediated RAM files and shared
admission; CPython uses native buffered files. Warm includes goldens and prior-result
cleanup, excludes reset/protocol/final cache teardown; wider CPU includes reset/protocol.
Compilation includes compiled-owner disposal; fresh includes compilation and one run.
Independent medians are not additive; no heap/startup/pending-I/O/embedding-tail claim.

These are diagnostic external comparisons, not causal source gains. Compilation is
near CPython in CSV/text; normalization still has a substantial execution gap.
Moving controls and previous unexplained CPython shift remain explicit. Identical
catalog setup within this checkpoint does not isolate an older cross-collection cause.
Full platform/stress/package qualification43828954661 runs independently; exact607
source inputs match312f5911 and [the retained image](../2026-11-16-iteration-slot-transport/refined/README.md).

Only exact committed previous1c8e working image aliases retired after fresh owned
lease/UID/path/hash/all-alias checks; all previous image bytes remain in committed
archives. Current0443 image/toolchains and254/252MiB floors kept. No cleanup or
source-golden relaxation. Both measurement terminals cached; publication held.

[Phase one raw](phase-1/observations.tgz),[phase two raw](phase-2/observations.tgz),
[all procedures/custody](procedures.tar.xz),[preceding guide](prior-BENCHMARKS.md).
The preceding guide is an exact snapshot of `docs/BENCHMARKS.md` at312f5911;
its relative links retain that original source location.
