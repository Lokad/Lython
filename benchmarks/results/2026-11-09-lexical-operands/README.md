# Explicit incoming lexical reads

R139/B498, 2026-11-09. Corrected committed a58/core0216 versus candidate a063.
Both sources contain the explicit-global native proof correction. The corrected
baseline is freshly built without incoming-cell changes and passes 37 class/global
controls. Exact prototype sources are then restored; nonincremental builds precede
606 internal/323 public focused checks on each actual .NET 11/10 and candidate freeze.

One work-state cell reader replaces the frame-only reader and stack-transfer branch.
Existing incoming cells no longer force eager invocation contexts. Missing cells
promote before lexical fallback; writes still promote. New local-cell construction,
mirroring, class/annotation/type-parameter/handler/exception restrictions remain.
Read live cell Value once, preserving original unbound-variable errors/spans.
No new native proof, dispatch, cache, binder, owner or public option. Managed stack,
current cancellation, coarse preflights and surviving alias funding remain.

Existing complete main-wrapped CSV/text/report scripts now read captured parser/skip
marker, separator and newline. Fixtures/full goldens unchanged; explicit source hashes
differ from R138. Ordinary JSON/search are controls. This compares identical scripts
between Lython revisions, not script shapes or CPython ratios.

| Warm complete script | Actual allocation, both APIs | Quiet elapsed change | Wider process CPU change |
| --- | ---: | ---: | ---: |
| Captured CSV | −38.66–38.67% | −7.31–7.93% | −6.45–12.30% |
| Captured CSV, completed-I/O async | −38.66% | −7.50–11.93% | −7.14–10.71% |
| Captured report | −26.06–26.07% | −4.97–9.14% | −1.16–4.88% |
| Captured report, completed-I/O async | −26.06% | −5.67–7.65% | −0.58–7.32% |

Four independent roles, 5s warmups, seven rotated 150ms rounds, normal GC/tiering
on quiet F4as_v7/.NET11RC3. Seven jobs/196.580s. Ranges span both candidate process
medians against both references, not confidence intervals. Report reference spread
3.51%/candidate1.05%; asyncCSV reference3.38%/candidate1.48% remain variable.
All affected elapsed/CPU comparisons favor candidate. JSON control elapsed
−.15..−.30%/CPU0%; search−8.15..+4.29% (reference8.66%,candidate4.50%) inconclusive.
No universal gain/neutrality claim.

Captured text allocation is unchanged; elapsed−3.13..+4.49% is inconclusive
(reference5.39%,candidate2.34%). Its explicit split(separator) still needs a context
in existing receiver dispatch; this read migration does not remove that construction.
Do not add a method special case merely to change this observation. Compile allocation
is unchanged across all five; compile CPU/startup/pending I/O remain unmeasured.
Warm clocks include mediated RAM-file I/O, full stdout/file validation and prior-result
cleanup; exclude compilation/startup/reset/protocol/final cache teardown. CPU is wider.

Original prototype4e94 versus uncorrected345f is retained: desktop asyncCSV+60..64%,
asyncReport+11.7..17.7%, variable controls. Corrected desktop CSV still+12.7..20.1%sync/
12.1..14.4%async, other controls drift up13.34%. Quiet comparisons do not reproduce
that CSV regression consistently; original adversity remains evidence. Retain the
removed phase, allocation reduction and affected-script quiet benefit. Text/control
uncertainty remains debt. No unchanged retiming selected or permanent closure.

All core/auxiliary/source/golden/budget/quiet/Closed/join/RAM-unmount/archive-under-
lease/local-SHA gates pass. Archive8ead6be5724daabc6212db4b61f40779bb1196d9e9d5f329101f1836a399c025.
Terminal cached, no users/fixtures. Initial setup refused headroom before workers.
Fresh exact duplicate-file proofs recovered14,016,512B then6,995,968B; every original
path/byte/alias/owner/group/mode and pre-mutation plan remains. Floors unchanged.

CSV source gate confirms ordinary record bytes/descriptors are already staged and
all ordinary field payloads/row capacity admitted once. No duplicate scope layer or
automatic promotion of historical bulk-copy work. Full platform/original Stress/
installed-package qualification batches after commit, including the lexical-name
fix. Publication postponed; broader performance and independent regex work remain.

[Quiet medians/variability](summary.json), [raw/both exact cores/protocol](observations.tgz),
[corrected local adversity](local-summary.json), [original prototype](prior-prototype-summary.json),
[compile allocation](compile-allocation.json), [focused validation](validation.json),
[corrected baseline](corrected-baseline.json), [captured sources](captured-sources.json),
[identities](identity.json), [terminal/archive receipt](terminal.json),
[preparation/all failed conditions/builds/restores](preparation.tar.xz),
[initial space proof](space-initial.json), [additional space proof](space-additional.json).
