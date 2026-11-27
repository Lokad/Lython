# B1829 Read staged generator operands for closed returns

A profile of production `05c1059de` verifies
2,560 five-field shipment and 10,112
file-fed map invocations in warm async mode. It samples managed thread
stacks on local Ubuntu WSL and .NET 11 RC3 for six seconds per workload.
The table reports milliseconds with each method in the sampled stack.
Ancestry can overlap, and the capture includes parked threads. These weights
locate paths in execution and give no estimate of removable CPU.

| Sampled ancestry | Five-field shipment, thread ms | File-fed map, thread ms |
| --- | ---: | ---: |
| Instruction prefix | 5627.9 | 5008.8 |
| Asynchronous zip unpack | 784.2 | 0.0 |
| Starmap entry and continuation | 695.6 | 0.0 |
| Staged row callback | 70.5 | 0.0 |
| Direct operand return | 92.4 | 583.6 |
| Map advance | 111.9 | 4595.5 |
| Direct field to builtin return | 0.0 | 454.0 |

The candidate reuses materialized operands in a staged generator row while
an existing closed local, conditional or arithmetic return completes. It
retires the row before exposing the result. This skips clearing a native
call workspace and copying the row into it. Dynamic truth, member and field
calls, unpublished operands and pending callbacks keep the ordinary
handoff. A pure return that declines hands the row to the general workspace
before any observable operation.

The frozen reference and candidate differ only in the core DLL. Four
complete scripts match independent CPython 3.13.2 output and file
goldens. Both local Release targets pass 177 public and 18 white-box
checks. The additional sync and async regression confirms that a staged
selector preserves aliases and lets a custom truth protocol resume its
generator. Its output independently matches CPython.

The Windows allocation diagnostic restores the borrowed allowance after
every run. Admitted peaks match in both modes. CLR allocation is equal
within six bytes per invocation across the four scripts.

Warm local WSL .NET 11 uses one logical CPU, two workers per source and
seven rotated batches per worker after five seconds of warmup. Ranges
cover all four pairs of worker medians. Warm elapsed includes mediated
I/O, verification and previous-result disposal. Process CPU also covers
fixture reset and request framing. Startup and compilation sit outside
these lanes.

| Complete workload | Warm lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Three-field shipment | warm-async | −5.17% to −3.33% | −4.62% to +0.00% |
| Bound zip shipment control | warm-async | −1.94% to +1.05% | −4.29% to −0.74% |
| Five-field shipment | warm-async | −1.70% to +2.84% | −4.00% to +6.25% |
| File-fed map control | warm-async | −3.06% to +0.21% | −1.45% to +3.65% |
| Five-field shipment | warm | −0.28% to +0.47% | +0.90% to +2.02% |

The first trial reduces three-field async elapsed by 3.33–5.17%, with CPU
spanning zero. The five-field async lane remains inconclusive, and five-field
sync CPU rises 0.90–2.02% under tight same-source spread. The bound zip
control has lower CPU. Keep the adverse measurements. The trial still scans
every argument for a published value, so the next check removes that scan
and keeps the ordinary handoff when the selected value needs publication.
[Selection-only refinement](selection/README.md).

All 140 timed batches verify. The workers and profilers join, guest
fixtures are removed and the persistent archives verify. The original
profile launcher failed before creating its working directory because
of command quoting. The recorded LF launcher keeps the same cases and
images.

[Profile weights](profile-times.json) · [Profiles](profiles.tar.xz) ·
[Images](identity.json) · [Medians and spreads](summary.json) ·
[Allocation and admission](allocation-summary.json) · [Observations](observations.tar.xz) ·
[Archive receipt](archive-receipt.json) · [Exact patches and checks](procedures.tar.xz).
