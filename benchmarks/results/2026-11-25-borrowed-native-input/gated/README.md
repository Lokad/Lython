# B1703b Gate the borrowed subscript proof

The refinement skips the proof for code without prepared lookup slots
and requires each read of the borrowed binding to be a prepared
subscript. The same 4,096 invoice and 2,048 file-fed map slot copies
disappear. Allocation and admitted peak match on all six local
diagnostics. Actual delayed subscript transfer and the native call
checks pass on Windows .NET 11 and Ubuntu .NET 10, 120 per platform.

The reference is the exact `701f0b1e5` image used by the initial
comparison. The quiet Azure VM, runtime, timing boundaries, paired
worker order, warmup and rotating sampling match that comparison.
Five CPython goldens and all 140 timed batches verify. Both quiet
gates pass, all workers join and the archive verifies under the lease.

| Complete script and boundary | Elapsed change | Wider process CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Quoted CSV invoice, warm sync | −0.93 to +0.17% | −1.05 to 0.00% | 0.37 / 0.73% |
| File-fed map, warm async | −4.25 to +0.12% | −4.29 to −1.64% | 0.71 / 3.83% |
| Shipment callbacks, warm async | −1.15 to +1.38% | −0.89 to +2.27% | 0.91 / 1.64% |
| Defaulted rules, through result | −0.75 to +1.16% | −0.47 to +2.22% | 1.15 / 0.77% |
| Ordinary definitions, through result | −1.37 to +0.38% | +0.93 to +2.34% | 0.62 / 1.15% |

Keep both variants private. The initial invoice gain does not repeat
on both clocks. File-fed map CPU improves in this block, and the
defaulted control spans zero. Ordinary definitions use more CPU,
with no work on the borrowed path. The data do not identify a causal
cost in the compiler gate. A larger replacement should avoid preparing
a second workspace representation as well as copying the operand,
or share a proof the compiler already produces.

[Initial comparison](../README.md) · [Image identity](identity.json) ·
[Routes](routes.json) · [Allocation and admission](allocation-summary.json) ·
[Timings](quiet-summary.json) · [Observations](quiet-observations.tar.xz) ·
[Terminal receipt](quiet-terminal-status.json) · [Builds and procedures](procedures.tar.xz).
