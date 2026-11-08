# Prepared function binding: preserved, not promoted

2026-11-08, R124. Qualified d97/runtime6f76/corec617 versus working prototypeC4bb.
Compiler-owned parameter-layout-guarded argument positions remove repeated static
keyword resolution and CallArgumentValue views for previously emitted same-scope
functions. Shared canonical default completion replaces duplicate default/error
logic; fully supplied calls need no default processing. Actual defaults remain
live; dynamic/expanded/variadic and rebound/decorated calls keep canonical binding.
No runtime cache, owner, ledger, engine, policy or public option.

**Hold this narrow form; no production promotion.** Correctness and source-level
work elimination are established, not a stable complete-script CPU benefit. It
adds a compiler registry/mapping/guard while general binding remains necessary.
Revisit prepared positions within broader compiler-fact/call construction
consolidation that removes more complete machinery. No fixed-percentage veto,
semantic failure, proven universal slowdown or permanent design rejection.

81internal/41public cases peractual.NET11/.NET10, plus262additional public.NET11
pass. New regressions keep changed defaults, decorators/rebinding, actual suspended
default/body I/O, pending argument funding, cancellation, original terminal refusal
and retained aliases/budget reuse. These observable tests survive source restoration.
The prototype itself has no full platform/stress/package qualification.

Five unchanged complete scripts, both APIs, normal desktop diagnostic: warm
allocation neutral, compiler allocation+0.199–0.254%. Elapsed controls drift up to
15%; original async text+6.4% with stable old control remains preserved. No CPU
inference from this same-process/ALC diagnostic. All full goldens/balances/cleanup
and image-isolation gates pass. Initial/refined source/core/outputs survive.

Quiet F4as_v7/Ubuntu24.04.4/.NET11RC3, frozen7cf/optional0.4, normal GC/tiering,
two independent processes per source. B483 four jobs114.346s,5swarmup/role/job,
7rotated150msrounds. All Ready/source/core/auxiliary/golden/balance/quiet/Closed/
join/ownedRAM-unmount/archive/local gates pass:

| Complete script / API | Elapsed change | Process CPU change | Control spread, reference / candidate |
| --- | ---: | ---: | ---: |
| scripts.csv-validated-records.large / warm | -3.15..+0.64% | -6.25..+0.00% | 1.34% / 2.53% |
| scripts.report-render.large / warm | -2.69..-0.93% | +0.00..+3.99% | 0.35% / 1.44% |
| scripts.csv-summary-functions.large / warm | -1.20..+1.92% | -2.17..+4.40% | 2.30% / 0.85% |
| scripts.text-summary-helpers.large / warm-async | -0.31..+3.87% | -5.88..-0.79% | 0.18% / 3.86% |

B484 bounded confirmation109.217s, unchanged source/binaries, only primary CSV
and adverse async text,10swarmup/role/job/7rotated300msrounds. Longer batches reduce
coarse process-CPU tick uncertainty. Same gates pass:

| Complete script / API | Elapsed change | Process CPU change | Control spread, reference / candidate |
| --- | ---: | ---: | ---: |
| scripts.csv-validated-records.large / warm | -3.90..+2.39% | -4.84..+1.69% | 1.90% / 4.33% |
| scripts.text-summary-helpers.large / warm-async | -3.85..-1.62% | -6.86..-2.65% | 1.90% / 0.41% |

Primary CSV remains uncertain: candidate process spread4.33%, effects cross zero.
Async text improves in both confirmation processes, so its first adverse result
is not reproduced consistently; original data is not erased. Report's first
CPU0..+4%possible debt remains. Neither source promotion nor design rejection is
justified by a rigid percentage threshold. No third unchanged collection; new
source-specific evidence or a broader redesign may justify future confirmation.

Both180sfinite owners captured live, all users joined and RAM fixtures removed.
Persistent raw evidence separate from fixtures. Exact-byte space recovery used
fresh idle leases, permitted canonical ownership/writable parents/all-displaced-
inode aliases/SHA/persisted plans; all17 original paths/bytes preserved. B483
post-audit and B484 pre-mutation plan/verified post-operation receipt survive.
No evidence deletion, quota relaxation, abandoned work or publication.

[Decision](decision.json), [design before edits](design.md), [source identity](identity.json),
[desktop](assessment-local.json), [compilation](compile-assessment.json),
[first comparison](initial-assessment.json), [confirmation](confirmation-assessment.json),
[first receipt](initial-receipt.json), [confirmation receipt](confirmation-receipt.json),
[first raw/both exact cores](initial-observations.tgz), [confirmation raw/core archive proof](confirmation-observations.tgz),
[all prototype source/initial binary/local measurements/failures](local-source-and-diagnostics.tar.xz).
Performance objective and independent regex development remain active.


Qualified production source restored without the prototype. Nonincremental rebuilds
follow source restoration;5internal/6public targeted checks pass on each actual.NET11/
.NET10 target (9new semantic cases plus2existing controls). Tests are named for
observable named-binding/default lifetime behavior, independent of internal slots.
[Restoration validation](restored-validation.json), [fresh builds/logs/current tests](restored-validation.tar.xz).
No unchanged full platform/package replay or production performance claim.
