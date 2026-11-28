# B1993 Construct generators from bound arguments

Ordinary direct and bound generator calls keep their existing target-selection
branches. They pass admitted arguments from the binder into generator
construction and publish the generator directly. The exact-positional path
keeps its staged consumer. Both images share the same worker and adapter.
The reference core is `7eebf88d7` and the candidate is the archived patch.

Windows .NET 10 and 11 and Ubuntu WSL .NET 11 each pass 525 public and 105
white-box checks in Release. A regression exercises defaults that require a
host read before either a direct or a bound generator starts its body. Both
APIs preserve refusal, refunds and the staged path. All four independent
CPython 3.13.16 goldens verify. Admission peaks, compiled charges and allowance
growth counts match exactly in both APIs. CLR allocation differs by at most
56 B per invocation in the staged shipment control and matches on the other
workloads.

## Warm comparisons

November 28, 2026 UTC. The local comparison uses Ubuntu WSL and .NET 11 RC3.
The quiet confirmation uses the exact same images on Azure F4as_v7 with
Ubuntu 24.04.4. Each lane uses two workers per image, five seconds of warmup
and seven rotated batches per worker. Ranges pair worker medians. Elapsed
includes applicable RAM-backed I/O, verification and prior-result disposal.
Process CPU includes fixture reset and framing. Compilation and startup are
outside the clock.

In the 84 verified local batches, configuration elapsed rises 1.11–2.71%.
Its CPU range spans zero. Default-heavy generator elapsed falls 0.42–4.17%
in `Run` and 1.12–4.80% in `RunAsync`, with CPU ranges spanning zero.

| Quiet workload | Boundary | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Default-heavy generators | `Run` | −4.28% to −2.95% | −5.28% to −3.73% |
| Default-heavy generators | `RunAsync` | −7.80% to −5.57% | −6.94% to −6.45% |
| Staged CSV shipment control | `RunAsync` | −0.93% to +2.69% | −1.95% to +2.45% |
| Configuration edit control | `Run` | −0.96% to +0.02% | −1.63% to −0.06% |

Both machine gates and all 112 quiet batches verify. The workers join and
their RAM-backed fixtures are removed before the persistent archive is
verified. The earlier trial with shared target selection remains shelved.
[B1991](../2026-11-28-direct-generator-construction/README.md).

[Allocation](allocation.json) · [Admission](admission.json) ·
[Local timing](warm-summary.json) · [Quiet timing](quiet-summary.json) ·
[Quiet terminal status](quiet-terminal-status.json) ·
[Local observations](warm-observations.tar.xz) · [Quiet observations](quiet-observations.tar.xz) ·
[Patch and procedures](procedures.tar.xz).
