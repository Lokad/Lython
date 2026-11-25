# B1708 Borrow existing short iterator operand storage

A compiled footprint proof lets a positional callback run the existing
native entry in the three slots already owned by short map and starmap.
The argument count is separate from that footprint. A dynamic operation
moves the live slots and instruction position into the existing funded
continuation. Full frame capacity remains admitted before resumption.

A separate instrumented build counts 2,048 eliminated private workspaces
on each file-fed run, in either hosting mode. No loan transfers on those
complete scripts. Invoice still starts all 4,096 private workspaces.
Shipment still starts one. The other controls take no loans.

The reference is the `008546a1f` runtime. Both frozen 32-file images
share all files except the core and verify five independent CPython
goldens. The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on
November 25, 2026 UTC. Two workers per image warm for eight seconds each
and contribute seven rotated batches. Ranges span four pairs of medians.

Warm elapsed includes applicable mediated RAM-backed I/O, output and file
checks, and result cleanup. Compile-to-result adds compilation and
compiled-owner disposal. Wider CPU covers fixture reset and protocol.
Negative changes mean less time. Startup and teardown sit outside both.

| Workload | Phase | Elapsed change | Wider CPU change | Worker spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| File-fed map | warm | -6.28% to +0.17% | -6.93% to +0.65% | 3.65% / 3.12% |
| Shipment report | warm-async | -0.06% to +1.94% | +0.45% to +2.62% | 0.86% / 1.13% |
| File-fed map | warm-async | -6.77% to +0.30% | -4.66% to +0.50% | 6.12% / 1.38% |
| Ordinary definitions | compile-run | -0.75% to +1.04% | -1.22% to +0.76% | 0.97% / 0.82% |
| Quoted invoice | warm | +0.10% to +1.19% | +0.00% to +1.69% | 0.72% / 0.36% |
| Defaulted rules | compile-run | +0.70% to +1.66% | +2.01% to +2.17% | 0.10% / 0.85% |

The file-fed elapsed and CPU ranges cross zero. Shipment warm async CPU
rises 0.45–2.62% and defaulted rules through-result CPU rises 2.01–2.17%.
Neither takes a loan. These observations leave the cost of eager compiler
proof and common callback argument handling unresolved. The prototype
stays private. A useful follow-up can move the invariant footprint check
to iterator creation and remove its cost from unrelated compilation.
The prepared version is recorded in [B1708b](prepared/README.md).
Compilation allocation, retained admission and execution peaks match.
Execution allocation differences in the diagnostic are at most 56 B.

Windows .NET 11 and Ubuntu .NET 10 each pass 4,609 focused Release checks.
Five additional focused cases on the same runtime verify alias identity,
one late-bound call, actual asynchronous file cleanup and terminal refusal
before a replacement can run. The first refusal fixture read its row
from a retired module execution slot. The corrected fixture owns its row
directly. Both quiet gates and all 168 batches passed. The workers joined
and persistent observations verified under the lease.

[Exact source and image](identity.json) · [Timings](quiet-summary.json) ·
[Routes](route-summary.json) · [Execution allocation](allocation-summary.json) ·
[Compilation allocation](compile-allocation-summary.json) · [Observations](quiet-observations.tar.xz) ·
[Terminal receipt](quiet-terminal-status.json) · [Procedures](procedures.tar.xz).
