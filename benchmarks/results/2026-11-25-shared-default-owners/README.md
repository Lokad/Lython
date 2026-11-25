# B1707 Shared final storage for function defaults

The prototype uses the terminal result-consumer marker already verified by
the compiler. Closed defaults finish their arithmetic without checking the
next opcode. The physical store slot selects the positional or keyword
owner. One producer handles both closed single and paired definitions.
Executable, lowered and generated defaults use one final-slot writer.
The prototype removes both a duplicate writer and a closed-default
producer. Instruction and retained metadata layouts are unchanged.

The reference is the `008546a1f` runtime. Candidate and reference share
31 image files and independently verify all seven CPython goldens. The
quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 25, 2026
UTC. Two processes per image warm for eight seconds and contribute seven
rotated batches per phase. Ranges span all four pairs of medians.

Warm execution includes applicable mediated RAM-backed I/O, output and file
checks, and result cleanup. Compile-only includes compiled-owner cleanup.
Compile-to-result includes both boundaries in an already-running process.
Wider CPU includes reset and request framing. Negative changes mean less
time. Startup and final teardown sit outside both clocks.

| Workload | Phase | Elapsed change | Wider CPU change | Elapsed worker spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Defaulted rules | warm | +2.49% to +5.42% | +2.88% to +5.33% | 1.35% / 1.50% |
| Eager annotations | warm | -4.79% to +8.54% | -4.00% to +7.61% | 8.13% / 5.44% |
| Postponed annotations | warm | +6.21% to +8.32% | +4.87% to +9.81% | 1.86% / 0.12% |
| Defaulted rules | compile-run | +2.82% to +4.04% | +1.17% to +6.15% | 0.64% / 0.54% |
| Ordinary generated definitions | compile-run | -0.82% to -0.23% | -2.56% to -0.46% | 0.07% / 0.52% |
| Generated definitions with live aliases | warm | -1.67% to +0.26% | -0.57% to +0.58% | 0.34% / 1.62% |
| Shipment callback control | warm-async | -1.72% to +1.59% | -1.34% to +2.26% | 1.94% / 1.40% |
| Defaulted rules | compile | +0.15% to +1.11% | +0.07% to +1.84% | 0.49% / 0.46% |

Keep the patch private. Warm defaulted rules use 2.88–5.33% more CPU
and take 2.82–4.04% longer through result. Postponed annotations use
4.87–9.81% more CPU, repeating the direction seen with the earlier
shared evaluator. The ordinary generated case improves slightly through
result. Eager, live-alias and shipment effects remain uncertain.
Compilation and execution allocation match apart from a 6 B execution
difference in the invoice diagnostic. All admitted peaks and compilation
retention match. These measurements isolate the source pair, with the
cost of each changed method unresolved.

Windows .NET 11 and Ubuntu .NET 10 each pass 2,222 focused Release checks
with rollback, cancellation during default admission, callable refusal,
and cleanup. A CPython probe compares wide arithmetic, negatives and a
captured keyword alias in both hosting modes. The frozen candidate has
both direct routes in its tape. Initial test attempts omitted files from
the sparse checkout. The receipts preserve those errors and the corrected
runs. Both quiet gates and all 224 batches passed. The workers joined,
and the fixture cleanup and observation archive were verified under the lease.

[Exact source and image](identity.json) · [Timings](quiet-summary.json) ·
[Execution allocation](allocation-summary.json) · [Compilation allocation](compile-allocation-summary.json) ·
[Wide-default routes](wide-default-routes.json) · [Observations](quiet-observations.tar.xz) ·
[Terminal receipt](quiet-terminal-status.json) · [Procedures](procedures.tar.xz).
