# B1923 Construct bound generators without entering the body invoker

The candidate over `245e0135a` sends already-bound generator calls to one
factory across both execution APIs. The factory checks function depth, admits
generator storage, checks cancellation, constructs the frame and transfers
the bound workspace. The ordinary body invoker and the generator's virtual
sync/async body wrappers are outside this path. The async entry keeps its
CLR execution context around the allowance callback.

A regression cancels the command from the allowance callback. The previous
runtime successfully constructs the generator in both internal entry points.
The candidate fails before frame construction and returns the workspace.
Separate refusal cases verify the first structured failure and a balanced
owner. Existing generator lifetime tests now construct their generators
through the production function factory.

Focused Windows .NET 10 and 11 and Ubuntu WSL .NET 11 Release checks each
pass 538 public and 112 white-box cases. Both 32-file images match the four
independent CPython 3.13.16 output and file goldens. The allocation witness
shows identical execution peaks and compiled reservations across eight
observations, with all borrowed allowance returning to baseline. Managed
allocation varies by at most 230 B per run, with all four async medians equal.

## Local timing

November 28, 2026, Ubuntu 24.04 WSL and .NET 11 RC3, one pinned logical CPU.
Two workers per image each warm for at least five seconds and contribute
seven rotated batches per lane. Ranges span pairings of worker medians.
Warm elapsed includes mediated RAM-backed I/O, verification and prior-result
disposal. Process CPU also includes fixture reset and request framing.
Compilation and startup are outside the clocks.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Iterable shipment | Async | −1.68% to +4.43% | −3.73% to +3.11% |
| File-fed map control | Async | −3.65% to +0.74% | −2.80% to −0.92% |
| Bound shipment | Async | −2.19% to −0.71% | −1.85% to +2.74% |
| Shipment with ordinary helper and generator reduction | Async | −2.71% to +1.86% | −1.36% to +0.99% |
| Iterable shipment | Sync | −1.18% to +1.05% | −2.81% to +2.72% |
| Bound shipment | Sync | −1.06% to +1.43% | −0.64% to +2.08% |
| Shipment with ordinary helper and generator reduction | Sync | −0.09% to +4.50% | −0.43% to +1.63% |

All 196 batches verify in 249.4 seconds. Retain the factory for its single construction path and the cancellation fix.
Iterable shipment CPU spans zero in both APIs. Bound async elapsed falls
0.71–2.19%, and its CPU range spans zero. The file-fed map control uses
0.92–2.80% less CPU. Preserve the positive end of the ordinary sync elapsed
range. The timing supports no reliable whole-script improvement.

[Timing](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Source, checks and baseline regression](procedures.tar.xz).
