# Funded operand retirement — withdrawn refinement

2026-11-15, uncommitted **17b04146**, versus qualified **51e3c2c1 /22e5b72c**.
The shared engine stops clearing ordinary dead operand ranges at each retirement;
their backing stays funded until its actual owner relinquishes it. Private
cursors/builders still retire immediately, pending async users stay owned, and
returned workspaces clear used capacity. Orphan native-only helpers are deleted.
This is a changed-source refinement, not an unchanged retry.

| Complete workload | Quiet-VM elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| Invoice | +25.20..29.59% | +24.56..31.09% |
| Fulfillment | +15.86..16.18% | +14.98..16.53% |
| Captured text | +32.23..37.87% | +33.58..36.79% |
| JSON summary | +11.76..13.96% | +13.11..15.65% |
| Protected file search | +34.11..40.68% | +25.53..34.34% |
| Configuration, compile-to-result | −4.00..+1.71% | −21.40..−0.57% |

B788 completes six jobs in172.276s with the same workers, cases, quiet VM,
two processes/source, warmup/batch sizing and boundaries as the [first gate](../README.md).
All source pairings remain. Text reference spread3.99% and configuration
reference spread4.24% stay visible. Other elapsed spreads are at most3.33%.
The CPU workloads remain materially slower. No causal recovery claim follows
from comparing this run to B787. Allocation was not remeasured for17b; the earlier
effectively flat allocation belongs to77a63bea.

**Withdraw the complete activation prototype.** It achieves source consolidation,
but does not justify carrying this broad CPU debt into0.11. Future coherent
call/operand redesigns remain eligible; this is not a permanent ban on unification
or a fixed-percentage veto. No unchanged timing campaign is selected. The larger
whole-script performance objective remains open.

Before withdrawal, focused Release checks pass190internal/516public actual .NET11
and96internal/516public actual .NET10. The two orphan deletions follow the11checks;
they compile on11 and are included in the10checks. These are not full platform,
stress or installed-consumer qualification. No publication/tag follows.

Production restoration verifies all585tracked core inputs against02340b14
(qualified runtime6e276759), normalizing line endings explicitly. Only the three
archived prototype files are removed. Forced nonincremental Release builds for
each target precede matching probe builds and checks: **108internal/72public per
actual .NET11/10**, all pass. The independent missing-keyword/TypeError-handler
case remains in the public suite under both APIs. Prototype-only routing fixes,
buffer architecture tests and retirement-policy changes are not shipped as
independent runtime corrections. Current runtime guides remain accurate.

VM observations are archived and verified under the lease; all workers join,
RAM fixtures are removed, and quiet/golden/balance gates pass. Exact retired
working copies have committed archive custody; qualified images and canonical
evidence stay. B787 and B788 are terminal cached results; never query them again.

[All process medians and withdrawal decision](decision.json),
[exact changed core/source, checks, original procedures and observations](sources-checks-and-observations.tar.xz),
[restoration source/check proof](restoration.json),
[restoration inputs and TRX](restoration-checks.tar.xz).
