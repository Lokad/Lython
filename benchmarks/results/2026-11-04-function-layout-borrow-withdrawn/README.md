# Borrowing existing compiler parameter storage: withdrawn

2026-11-04, quiet AzureF4as_v7 Ubuntu24.04.4/.NET11RC3. Baseline fully qualified
ee8020f0/core38c03e5e; prototype
`8099b8301f7a570e703b58038d93d9f7c8e5ab56545fdd4057f322764e0ca61f`.
This final bounded revision deletes the new code-object count tuple/compiler pass
from the [prior layout revision](../2026-11-04-function-layout-reuse-withdrawn/README.md).
It restores original compiled-code representation, counts parameters at definition
time, and borrows only existing local names/maps. Separate sealed plan/function
objects, mutable defaults and conservative320B object estimates remain.

Nonincremental Release37.71s0warnings/errors;744internal/3013public per actual11/10
pass. Independent CPython regression exact. Actual suspended default-key equality,
cancellation, refusal, retained defaults and budget reuse pass. No full delivery
qualification for this withdrawn prototype.

Four complete jobs110s, three isolated images/two old roles,6s guest warmup/image,
2s settle,9rotated200ms batches. All DLL/CoreLib/hash/full independent goldens/
quiet/baseline/finalzero/owned-fixture-removal/join gates pass. Ordinary GC/tiering,
same frozen auxiliaries/core-only change. Positive signs mean worse.

| Complete script | Phase | Candidate ms | Elapsed vs old / old control | Old spread | Process allocation vs old / old control |
| --- | --- | ---: | ---: | ---: | ---: |
| text-summary-helpers | warm | 3.090396 | +5.29% / +1.03% | 4.22% | -0.01% / -0.01% |
| text-summary-helpers | fresh | 3.138581 | -1.85% / -1.49% | 0.37% | -0.01% / -0.01% |
| report-render | async | 4.790893 | +0.10% / +0.48% | 0.38% | -0.01% / -0.01% |
| config-edit | warm | 0.214593 | +10.07% / +8.97% | 1.01% | +0.02% / +0.00% |

Fresh text−1.49–1.85% is favorable under these conditions. Warm text+1.03–5.29%
has4.22%old-role spread and is control-limited. Config control+8.97–10.07% with
1.01%old spread is adverse and unexplained: it has no guest helper definition,
so this does not attribute the result to parameter layout. Allocation changes are
tiny (~0.01%helper reduction); report is near flat/control/dispersion-limited.
None of these observations alone proves a failed architecture or CPU cause.

Do not promote. This bounded series does not establish a useful overall outcome
for the limited storage benefit, and leaves adverse/control uncertainty unresolved.
Restore all production files to fully qualified ee8020f0; remove every prototype
count tuple, partition replacement, borrowing constructor, inheritance change and
estimate change. Keep the independently useful public keyword/default-isolation
regression, which must pass on restored source. Continue the broader prepared
declaration redesign; do not add hints/configuration/backends or another local
variant. Reconsideration needs a different coherent design or concrete cause,
not a permanent percentage ban. Publication remains postponed.

Fresh includes public Compile/full Run/mediated RAM I/O/full output+file checks/
result and compiled cleanup. Warm/async includes invocation/completed I/O/checks/
result cleanup. Reset/staging/startup/hash/final cached teardown excluded, all
users joined; process allocation includes reset/background noise. Initial untimed
warm setup peaks are not fresh overlap/RSS/embedding or pending-I/O latency.
No CPython timing claim. [Metrics](assessment.json), [plan](pre-measurement-plan.md),
[raw observations](observations.tgz), SHA256
`26ebb18162accf3bf91dbb8c6a97bacfadfbcc166e249f6e758dfcaf94bbd6fe`.
All earlier adverse conditions/source snapshots remain linked, not replaced.

Restoration verified: nonincremental Release38.81s,0warnings/errors;38 focused
public binding/default checks pass on each actual.NET11/.NET10, including the new
keyword/factory-isolation regression. Production diff against ee8020f0 is empty.
See [restored build](restored-build.log) and target logs alongside it. All prototype
production machinery is removed; no source variant or compatibility switch ships.
