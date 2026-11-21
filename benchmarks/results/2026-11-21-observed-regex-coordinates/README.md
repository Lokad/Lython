# B1275 observe regex subgroup positions on demand

Compared the retained reference `b140244f0` with two candidates.
`e4713ce4a` stores each match's original window. `5526a5aa9` shares the
window across matches from one operation. Both project whole-match
offsets along the cursor, then project subgroup positions on first
observation. They remove scratch held by the prepared provider subject
and use bounded managed stack scratch for small observations. Large
observations preflight heap scratch and release it after counting.

Each candidate passed 66 white-box and 415 public Release checks per
platform on Windows .NET 11 and Ubuntu .NET 10. Coverage includes
backward-scan cancellation, denied wide scratch, original-window
coordinates, and 42 retained subgroups after cursor disposal and purge.
Each complete 32-file image matched six independent CPython goldens.
The initial white-box failures came from tests coupled to the displaced
private projector. The archive preserves those failures and the
updated cancellation and admission checks.

## Local allocation and exploratory timings

The first candidate added about 16 CLR bytes per retained match. Unicode
group-text and subgroup-span allocations rose 1.44% and 1.46%. The
shared-window refinement brings those allocations back to baseline.
Its wide-register script allocates 4.75% less. Budget baselines restore.
The local timing initially favored replacement callbacks and whole
offsets, while subgroup spans and group-text extraction slowed.
The shared-window trial favors group text and callbacks but has
a 23.8% subgroup-span reference spread. The local timing therefore
does not establish the subgroup-span impact.

## Quiet confirmation

Compared the shared-window refinement with the reference on November 21,
2026 UTC on Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. Both images
use the same optional 0.4.0 adapter. Four independent warmed workers
contribute seven rotated batches per lane. The range covers all pairings
of the two process medians per image. Negative changes indicate faster
runs.

| Complete warm workload | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| Unicode group text | −1.99 to −0.07% | −1.69 to +0.18% |
| Unicode text-only replacement callback | −4.29 to −0.46% | −4.74 to 0.00% |
| Unicode subgroup spans | +3.08 to +3.42% | +3.54 to +4.84% |
| ASCII group text control | +1.77 to +2.39% | +1.42 to +1.47% |
| Unicode wide registers | +1.30 to +3.11% | +0.55 to +1.57% |

Elapsed includes execution, golden observation and prior-result
cleanup. Process CPU includes surrounding worker requests. Both quiet
gates and all 140 batches passed in 280 seconds. Workers joined, guest
fixtures were removed and the archive verified under the lease.

Keep both variants private. The workload that reads subgroup spans
consistently runs slower with higher CPU and stable controls. The
ASCII and wide-register results also run slower. A further design
needs to avoid repeated positioning work or the added retained window
indirection while preserving efficient access to original-subject
coordinates. The smaller CLR allocation in the wide-register script
does not establish a CPU improvement.

[Local results](summary.json) · [Quiet comparison](quiet-summary.json)
· [Raw observations](observations.tgz)
· [Source and local checks](checks-and-procedures.tar.xz)
· [Quiet procedures](quiet-procedures.tar.xz).
