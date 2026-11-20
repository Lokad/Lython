# B1188 grouped annotation read confirmation

The confirmation carries grouped annotation reads onto the consolidated
function declaration operation. It compares the delivered `ea7cfa8fe`
source with private `f0aa724ba`. The grouped form writes simple names and
constants into admitted annotation storage during creation. Callbacks,
yields and dynamic lookups use the same general expression path.

| Warm workload | Quiet elapsed change | Quiet process CPU change |
| --- | ---: | ---: |
| Annotation callback batch | +0.44% to +1.78% | +1.35% to +1.69% |
| Nested annotated factories | −3.54% to +4.70% | −4.88% to +3.51% |
| Postponed annotated rules | −5.44% to +4.05% | −5.60% to +3.37% |
| Eager annotated rules | −1.66% to +1.69% | −1.64% to +1.88% |
| File-backed CSV invoice | −4.10% to −0.09% | −4.67% to +0.00% |

Keep the prototype private. B1186's eager improvement did not repeat.
The callback effect is smaller than in B1186 and adverse across all
pairings. Its duplicate elapsed spreads are 0.96% in the reference and
0.37% in the candidate. Nested and postponed timing spans zero, with
reference elapsed spreads of 5.17% and 8.40%. The unchanged invoice
also moves. This comparison cannot attribute the callback cost to a
specific instruction or function. Revisit grouped reads if a later design
eliminates work from the general definition path.
[B1186](../2026-11-20-grouped-annotation-reads/README.md).

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 20,
2026 UTC. Each process warmed for at least 15 seconds. Seven rotated rounds
targeted 0.75-second requests. Ranges cover the four pairings of duplicate
process medians. Warm elapsed includes mediated I/O where used, goldens and
prior-result disposal. Process CPU includes the wider batch.

Both platforms passed 522 focused Release checks on Windows .NET 11 and
Ubuntu .NET 10. Both complete worker images matched seven workload goldens
and eight independent CPython probes through both execution modes. Warm
allocation was effectively unchanged and accounted peaks matched or fell
by 24 bytes. The allowance returned to baseline. The VM passed both quiet
gates and all 140 timed batches. All workers joined, fixtures were removed
and the archive was verified under the lease.

[Summary](summary.json) · [Allocation](allocation-summary.json) ·
[Observations](observations.tgz) · [Checks and procedures](checks-and-procedures.tar.xz).
