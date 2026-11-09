# Contract-preserving private small integers — R176

2026-11-09, private variants based on b44fd910 with production equivalent to
qualified cac4acf8. Both variants are fully withdrawn. This is an allocation
experiment and bounded design assessment, not a CPU speedup or CPython comparison.

The initial source inventory selected a private immutable signed32 scalar: a 24 B
box rather than a 32 B BigInteger box. Larger integers retain BigInteger. CLR ingress
types and public guest BigInteger projection remain unchanged. Typed fresh producers
select the representation before boxing; public projection reserves 32 B before
materializing its result. No scalar cache, identity ledger, reclamation, fuel,
unsafe code or replacement memory allowance was introduced.

The coherent wrapper covered literals, ordinary arithmetic/parsing, shared numeric
classification, native eligibility, integer methods and module consumers. A bounded
refinement carried signed32 through the existing PyNumber view and common arithmetic,
truth and comparisons instead of reconstructing BigInteger at every such use.
The final variant changed58 production files. Sixteen white-box fixtures required
representation-independent integer extraction while preserving values, exact integer
acceptance, refusal, side effects, ownership and cleanup assertions.

## Decision and measured limits

Both variants save 6.91% complete-text allocation, about1.49% CSV,1.54% report and
3.79% Unicode control. Compilation allocation changes are below 0.04% relative to
both unchanged controls. These are CLR allocations, not host allowance estimates.
Elapsed observations are mixed and include adverse results. Shared desktop process
JIT/GC and unstable controls prevent a CPU, equivalence or regression-size claim.
No quiet-VM confirmation was run and no historical CPython ratio is updated.

Withdraw because the dual representation and expanded numeric view do not simplify
the execution architecture, and the current evidence does not justify their source
cost for this milestone. This is not a percentage veto or permanent rejection of
machine integers. The existing text attribution also does not identify scalar CPU
as the dominant removable phase. Preserve the measured native-helper optimization;
continue the independent call/operand design assessment rather than adding further
scalar special cases to this version.

Every pair below is relative to baseline / independent baseline-control. Negative
means less allocation or shorter elapsed. Desktop elapsed is diagnostic throughout.

| Variant | API | Complete script | Allocation | Diagnostic elapsed |
| --- | --- | --- | ---: | ---: |
| Wrapper | warm-sync | csv-validated-records.large | -1.49% / -1.48% | -13.30% / -14.73% |
| Wrapper | warm-sync | text-summary-helpers.large | -6.91% / -6.91% | +6.82% / +10.18% |
| Wrapper | warm-sync | report-render.large | -1.54% / -1.54% | +0.39% / +11.22% |
| Wrapper | warm-sync | unicode-index-report.control | -3.79% / -3.79% | -4.81% / -7.46% |
| Wrapper | warm-sync | config-edit.small | -0.06% / -0.06% | +4.55% / -1.11% |
| Wrapper | warm-async | csv-validated-records.large | -1.49% / -1.49% | -12.35% / -12.05% |
| Wrapper | warm-async | text-summary-helpers.large | -6.91% / -6.91% | +21.51% / +5.33% |
| Wrapper | warm-async | report-render.large | -1.54% / -1.54% | -6.02% / -14.95% |
| Wrapper | warm-async | unicode-index-report.control | -3.78% / -3.79% | -23.31% / +23.24% |
| Wrapper | warm-async | config-edit.small | -0.07% / -0.07% | -1.79% / -2.14% |
| Numeric-view refinement | warm-sync | csv-validated-records.large | -1.49% / -1.48% | -2.05% / -9.57% |
| Numeric-view refinement | warm-sync | text-summary-helpers.large | -6.91% / -6.91% | +2.69% / +6.93% |
| Numeric-view refinement | warm-sync | report-render.large | -1.54% / -1.55% | +7.34% / +5.93% |
| Numeric-view refinement | warm-sync | unicode-index-report.control | -3.79% / -3.79% | +3.96% / -1.73% |
| Numeric-view refinement | warm-sync | config-edit.small | -0.20% / +0.72% | -3.15% / -3.48% |
| Numeric-view refinement | warm-async | csv-validated-records.large | -1.49% / -1.48% | +5.65% / +6.23% |
| Numeric-view refinement | warm-async | text-summary-helpers.large | -6.91% / -6.91% | -3.00% / +4.66% |
| Numeric-view refinement | warm-async | report-render.large | -1.54% / -1.54% | -0.11% / +1.02% |
| Numeric-view refinement | warm-async | unicode-index-report.control | -3.78% / -3.79% | +8.31% / +6.25% |
| Numeric-view refinement | warm-async | config-edit.small | -0.05% / -0.04% | -1.54% / +2.30% |

## Conditions and correctness

Windows desktop, .NET11RC3 pinned SDK, existing complete-jobs driver and five-job
catalog. Two byte-identical qualified baseline roles; candidate changes only core.
Warm runs include mediated file I/O, full independent output checks and prior-result
cleanup; exclude compilation/startup/reset/protocol/final caches. Compilation is a
separate lane; its outer construction/disposal clock includes driver overhead.
All six lane captures pass goldens, zero returned balances, image isolation and
owned-fixture removal. No benchmark framework, policy or quiet-VM work was added.

Initial 176 numeric/lifetime checks pass. The first broad public run passes 16,838 of
16,931 and fails 93: internal concrete BigInteger casts were incompatible with the
new private value. All original conditions/logs remain. Shared validated extraction
fixes the affected 821public checks. The ownership/cancellation selection passes
all 733 existing cases; four new fixture failures are preserved: two required an
unjustified extra charge for an existing minor BigInteger box, and the alias case
included unsupported explicit int.__pos__. Corrected five public cases pass.
They still test unary+, exact promotion, alias methods, mixed numeric keys,
BigInteger projection, both APIs and accounting reuse. The unsupported member is
an existing frontend boundary, not removed supported behavior.

Both final wrapper/refined sources pass 824 focused internal cases, including new
projection admission/first-refusal and ownership checks. White-box failures on
concrete scalar representation and the two fixture compilation mistakes remain
in the original logs. No full-suite, platform, stress or installed-consumer
qualification is claimed for either discarded source.

Restore all production and all 16 modified white-box fixtures exactly; remove
PySmallInteger, its dual numeric view, helpers/guards/projection path and its two
representation-specific unit tests. Keep the five externally observable public
cases. Force fresh nonincremental core builds and run 48 retained numeric/lifetime
checks each actual .NET 11/10: all pass. Runtime remains the already qualified source;
no repeated full platform/package qualification is necessary for this withdrawal.
The substantial whole-script CPU objective remains open.

[Wrapper identity](identity.json), [refined identity](refined/identity.json),
[wrapper observations](desktop.json), [refined observations](refined/desktop.json),
[restored checks](restored-checks.json), [withdrawal](withdrawal.json),
[source variants, measured cores, catalog and commands](source-checks.tar.xz).
Raw lane captures remain in warm-sync/, warm-async/, compile-sync/ and refined/;
original failure/build/check logs remain in qualification/. One intermediate
823-passed/1-failed internal log was overwritten by the corrected824/824 run; its failure
was the same remaining concrete-constant assertion in the preserved nine-failure
log. Raw measurement captures were not overwritten.

[Independent CPython semantic controls](reference-check.json) pass under local CPython3.13.2; they are not timings.
