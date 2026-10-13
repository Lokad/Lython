# Rejected universal frame-transfer retention

The candidate passed its correctness checks but slowed the basic integer loop by
about 10% in the one declared short comparison. It is **rejected and reverted**;
these candidate timings do not describe current HEAD. The previous runtime and
its validated stack, storage, async-worker and gzip caller fixes are preserved.

| Warm workload | Input | Before Lython | Rejected candidate | CPython | Status |
| --- | ---: | ---: | ---: | ---: | --- |
| Integer accumulation loop | 16,384 iterations | 1.152796 ms | 1.268065 ms | 0.616185 ms | Short diagnostic; no qualified external multiplier |

Measured on 2026-10-13, dedicated 4-vCPU x64 Ubuntu 24.04.4 VM, .NET 10.0.12
and CPython 3.13.16. Before: `9dcbf6fc`, production `325120a6`. Candidate:
`8366e832bf7de39ec0b8783ac705fe29efa3dea9`, production
`624f8a69704dd95020ca7ace2f58fe9598bfdec1`. Revert: `87b07244`, restoring
production `325120a6`. The portable Release builds used the pinned Windows
SDK 10.0.300-preview.0.26177.108; the VM SDK directory is 10.0.401.

The existing micro tool used identical observer binaries, catalog, output oracle
and ordinary execution settings on both immutable libraries. It compiled once,
warmed persistent workers and measured fresh-state synchronous invocations,
including ordinary projection/capture. Compilation and process startup are
outside the measured invocation. Seven rotated rounds had IQR/median 1.35%
before, 0.93% candidate and 0.71% CPython. The complete owned group including
cleanup took 8.243 seconds. The supervisor observed no paging, major faults,
steal or runtime overrides; idle samples were 0–0.99% busy. The unit finished
inactive/dead with MainPID 0, empty cgroup and an available lease. No ten-minute
lane, replacement samples or second measured variant ran.

This is a synchronous integer-loop diagnostic, not representative-job or async
throughput evidence. It has no milestone qualification or confidence interval.
The cost applies to the combined candidate; the experiment does not isolate
the contribution of each mechanism. CPython remains the external context,
while the adoption decision compares earlier Lython with the candidate.

The candidate exposed executing frames through a dynamic chain of existing
frame objects, retained popped operands in the existing backing array until each
instruction completed, and exposed pending results during observation/preflight.
It replaced immediate per-pop clearing with a retained extent and completion
clearing. There was no extra root array, identity/refcount ledger or partial
reclamation/refund. A failed async probe showed that ordinary async definitions
still constructed lowered functions despite an available executable binding;
the candidate then reused the existing async driver for those bindings and
consolidated definition setup. Lowered fallbacks remained explicit limitations.

Both original frame probes failed before the changes. The final six transfer
checks passed, including real suspension, dynamic caller detachment and successful/
denied observed-result publication. The first denial setup incorrectly left
enough headroom and failed to cause denial; its receipt is preserved separately.
61 focused white and 80 public containment/cancellation controls passed. All
**10,893 candidate Release cases** and the exact candidate Release package consumer
passed. The short solution-build process hit its 28-second limit after producing
the test/worker assemblies; that incomplete command is not called a passing
build. The full suites had their own finite limit and completed in 406.851 seconds.
After reverting, the normal solution build restored the original binaries in
38.320 seconds under its separate finite build limit.
Original failures, candidate source/tests, build cutoff and all receipts remain
private in `.git/agent-notes/frame-transfer-20261013/` and in the commit history.

The stopping criterion rejects the universal retention mechanism because its
recurring cost is material before complete native/helper/child representation
exists. Neither correctness tests nor observable frame operands prove exhaustive
root coverage. Do not enable logical refunds from this partial representation or
silently adopt the async/compiler subset without its own bounded justification.
Next design work should publish transfers at actual allocation/callback/suspension
boundaries and represent native evolving state, avoiding a second stack extent
and unconditional completion work for every instruction. Forced GC remains open.

Raw evidence: [micro receipt](micro.json), [process receipt](process.receipt.json),
[input/library manifest](manifest.json), [validation](validation.json),
[frozen catalog](catalog.json), [supervisor](run-micro.py), [stdout](stdout.txt).
The manifest's binary bundles and immutable packages remain private; their hashes
are preserved. See [BENCHMARKS.md](../../../BENCHMARKS.md) for the authoritative
performance account and the [ownership requirement](../2026-10-12-memory-roots/OWNERSHIP-RULE-2026-10-13.md).
