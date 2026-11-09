# Closed nested helpers use existing invocation paths

R138/B497,2026-11-09. Baseline6d73/coreea65 (production unchanged at748493f7)
versus working345f. Remove two blanket lexical-parent exclusions. Existing compiler
capture facts, native proof and lazy-context runner establish eligibility; actual
scope-sensitive work promotes before executing. Rename the module-only helper to
match its broader use. No new binder, arena, dispatch, cache, owner or public option.
Defining closure chains remain retained; captured/local cells and special scopes
keep their existing handling.

Three existing complete CSV/text/report programs are wrapped unchanged in main().
Fixtures/goldens stay identical, explicit source hashes change. Original JSON/search
are controls; this is a same-script Lython source pair, not a CPython ratio or a
comparison against the module-level script shape.

| Warm complete script | Actual allocation change, both APIs | Quiet elapsed change | Wider process CPU change |
| --- | ---: | ---: | ---: |
| Validated CSV/main | −38.67% | −8.16–8.39% | −9.68% |
| Text helpers/main | −46.50–46.52% | −19.62–20.04% | −20.48–22.62% |
| Report/main | −28.49–28.50% | −10.50–13.22% | −8.93–12.77% |
| Report/main, completed-I/O async | −28.49% | −6.24–7.68% | −6.67–10.87% |

Elapsed/CPU ranges span candidate/control process medians against both reference
processes, not confidence intervals. Normal GC/tiering, four independent roles,
5s warmups,7rotated150msrounds; six jobs169.171s on the existing quietF4as_v7 VM.
Report candidate spread2.98% remains variable but all source comparisons favor
candidate. JSON control elapsed−.63..+.23%/CPU0%; search−2.64..+.19%, reference
spread2.49%, widerCPU−4.56..+.29%. No universal gain/neutrality claim. Local desktop
async report+2.25..10.25%, search+6.8..7.6% and variable controls remain preserved;
report adversity did not reproduce consistently on the VM. Compilation allocation
is unchanged across all five; compilation CPU/startup/pending I/O are unmeasured.

Warm clocks include guest-mediated RAM-file I/O, full stdout/file validation and
prior-result cleanup; exclude startup/compilation/reset/protocol/final cache teardown.
Process CPU includes more work. These results justify retention for a complete
removed construction phase and measured affected-script benefit; broader runtime
performance work remains open.

Broad68internal/279public peractual11/10, then final36/76 after method rename and
extra nested capacity-admission cases pass (overlap, not additive). Five independent
CPython goldens cover current globals, locals promotion, real live captures,
unbound locals and class-cell controls. Genuine async rebound protocols, first
refusal/cancellation, retained aliases, capacity callbacks, cleanup and managed
depth remain checked. No runtime policy/checkpoint/stack/estimate change. Retained
whole-frame estimates stay conservative despite fewer actual objects.

All core/auxiliary/source/golden/budget/quiet/Closed/join/RAM-unmount/archive-under-
lease/local-SHA gates pass. Archivee1566047854db1f6ad7085f2ecaa1e2037b71817fee132e81ab2eee2e9ab0d66.
Terminal cached; no users/fixtures. No space recovery or floor/headroom relaxation.
One local early retrieval guard refused eight seconds before its clock without
contacting the VM; the actual terminal observation followed the recorded deadline.
Full Windows/Ubuntu/stress/installed-package qualification batches after commit;
publication remains postponed.

[Quiet summary/all process medians](summary.json), [raw/both cores/protocol](observations.tgz),
[local allocation/adverse elapsed](local-summary.json), [focused checks](validation.json),
[wrapped scripts and original sources](wrapped-sources.json), [exact identities](identity.json),
[terminal/archive receipt](terminal.json), [selection](selection.md),
[logs/catalog/original failed conditions/preparation](preparation.tar.xz).
