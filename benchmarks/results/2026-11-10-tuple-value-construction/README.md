# Completed tuple values — R208, active refinement

Two initial private sources are preserved: original9cebdd62 and copy-bound-corrected
379a2d64, both based on committed439e91d9 / source-equivalent dbe94d4d. Neither is
promoted. The shared canonical object-array tuple representation remains unchanged.
A span factory plus native stack borrowing removes temporary reservations/late
scalar admission;36fixed literal sites lose a temporary array copy,33previous
adoptions preflight before final allocation. All69expressions have fixed elements,
no dynamic spread. Existing true owned arrays and suspending materializers remain.

Focused280internal/406public checks pass each actual.NET11/10 on both variants,
with zero-warning forced Release solution builds. New independent-copy, terminal
refusal-before-final-array, alias funding, cancelled-admission rollback and budget
reuse controls pass. Full ordinary final-source checks are recorded separately;
no platform/package qualification is claimed for these uncommitted variants.

Both versions reduce complete CSV allocation about3.04% on each API; other primary
controls flat and config small/variable. Original compilation allocation flat.
Desktop elapsed includes favorable/adverse/unstable controls and is diagnostic.
Both original and corrected full raw phase results remain under [desktop](desktop/).
The copy-bound correction advances by actual final chunk so offset stays within
representation bounds; no arbitrary cutoff or huge test fixture was introduced.

B559: three quiet jobs110.848seconds, F4as_v7/Ubuntu24.04.4/.NET11RC3, ordinary
GC/tiering, four independent roles,5swarmup/role,7rotated400msrounds. All source/
fixture/output/ownership/quiet/Closed/join/RAM/archive-under-lease/localSHA gates pass.

| Complete workload | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| CSV sync | +0.03..+1.71% | +1.22..+2.44% |
| Text sync control | −2.05..+2.82% | −2.62..+5.60% |
| CSV async | −1.68..+1.43% | −1.37..+1.37% |

CSVsync reference spread0.074%, candidate1.58%; asyncreference2.83%, textreference
3.99%. Ranges are all pairings, not confidence intervals. Sync CPU is adverse;
async/text uncertain. Allocation reduction is distinct, no CPU gain or equivalence.
Warm boundary includes mediated RAM I/O, capture/projection and prior-result cleanup;
excludes compile/reset/verification/protocol. Wider CPU includes surrounds/coarse
ticks. No pending-I/O, embedding-tail, startup or CPython comparison here.

Next bounded refinement: remove the introduced factory layer by moving the shared
copying contract into span constructors, replacing array-only copying overloads.
Preserve capacity/scalar admission, bounded long-copy checks, aliases, real adopted
arrays and suspending materializers. No inline tuple representation, new owner,
arity policy or cache. This is source-supported response to actual adversity;
retain the entire original conditions. Recheck semantics/ownership and compare
complete scripts before deciding. The broader performance objective stays open.

VM staging retires only superseded039/E294 core inode groups after matching their
committed canonical archive bytes and every alias/UID/path under an idle lease.
Qualified101 and beforeDbe remain. No missing/retired raw paths are probed; old
source measurements remain available in Git. Fixed240MiBfloor/12MiBheadroom.
The final archive is locally SHA-verified; no workers/RAM fixtures remain.

[Raw cores/observations](observations.tgz), [summary](summary.json),
[receipt](archive-receipt.json), [terminal](terminal-status.json),
[source/commands/tests/original conditions](preparation.tar.xz).
Publication and tagging remain postponed.
