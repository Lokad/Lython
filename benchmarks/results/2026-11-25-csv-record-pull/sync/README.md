# B1706b Direct sync handoff with shared CSV progression

Commit `008546a1f` removes the additional synchronous ValueTask handoff
from B1706 and retains one source loop. The synchronous caller receives
the funded row directly. A file or guest iteration operation that would
suspend goes through the existing record continuation, which can await
multiple pulls. Record ownership, terminal refusal, failed-pull recovery
and awaited file release retain their existing behavior.

Both blocks compare the same frozen 32-file images on the quiet Azure
F4as_v7 running Ubuntu 24.04.4 and .NET 11 RC3 on November 25, 2026 UTC.
The reference is `96d54066a`, and the committed source patch matches the
candidate. Independent goldens cover invoice, shipment and text.
Each uses the same phases and rotations. Ranges span all four pairs
of medians and compare the candidate with the reference.

| Workload | Initial elapsed | Initial wider CPU | Confirmation elapsed | Confirmation wider CPU |
| --- | ---: | ---: | ---: | ---: |
| Quote-heavy invoice | +0.56% to +3.10% | +1.08% to +1.58% | -1.72% to +0.51% | -2.13% to +0.00% |
| Shipment callback report | -0.84% to +0.89% | -0.89% to +0.45% | -2.84% to +2.19% | -3.00% to +1.36% |
| Unicode text control | -15.69% to +2.94% | -13.25% to +1.57% | -3.45% to +5.88% | -1.79% to +6.99% |

The initial block records all 84 batches and fails its final quiet check
at 15% background CPU. The exact-image confirmation passes both checks
and all 84 batches. Workers join and temporary fixtures are removed.
The initial invoice penalty does not recur in the confirmation. Shipment
and text changes are inconclusive. Retain the deletion of the two input
loops and the explicit pending-operation boundary. These blocks establish
no throughput claim.

The focused Windows .NET 11 run passes 144 checks. Ubuntu .NET 10 passes
902. Both include the new regression for repeated suspended pulls, a failed
partial record, recovery and allowance return. The allocation probes match
admitted peaks, and every sample returns its allowance. The largest
difference in measured CLR allocation is 56 B per invocation.

[Source and image identity](identity.json) · [Both blocks](comparison-summary.json) ·
[Allocation](allocation-summary.json) · [Initial observations](observations.tar.xz) ·
[Failed quiet check](terminal-status.json) · [Confirmation observations](confirmation/quiet-observations.tar.xz) ·
[Confirmation receipt](confirmation/quiet-terminal-status.json) · [Procedures](procedures.tar.xz).
