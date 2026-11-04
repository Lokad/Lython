# Owner-capacity admission assessment — not retained

2026-11-04, quiet Azure F4as_v7, Ubuntu24.04.4, .NET11 RC3. Baseline runtime
ee2b4529, qualified frozen core1b9b3230. Three prototype images are preserved:
initial327b7976, completion41ab5b4a, small-owner refinement3718cb89. Production
source is restored; these observations do not describe current HEAD performance.
No release or adapter qualification is implied.

The prototype reuses the governor's existing reservation in4KiB increments,
replacing per-object parent resizing with logical balances covered by admitted
capacity. No new allowance, owner, options, tracing or retry. Unused tail stays
charged; major refunds/zero return funding. Completion relinquishes unused tail
before the first public capture while retaining all live-data funding. A later
refinement funds sub4KiB owners exactly to avoid a minimum capacity loan.

## Observations and decision

Both complete12-job comparisons cover CSV/text/JSON/report/config and an
above-range integer control, synchronous/asynchronous warm APIs. Independent
unchanged old roles surround the candidate; normal GC/tiering,3-second warmup,
nine rotating~200ms samples. Compilation/startup and fixture reset are excluded;
invocation, mediated RAM-file I/O, full checks and previous-result disposal are
included. Separate eight-case allocation diagnostics verify isolated images,
goldens and zero balances. All workers/users are joined; both quiet gates pass.

| Observation | Result |
| --- | --- |
| Initial complete scripts | Several elapsed points improve3–6%; text and some async controls remain uncertain |
| Completion synchronous CSV/JSON/report/config | Favorable2.7–8.8%, varying old-role spread; magnitudes remain diagnostic |
| Completion synchronous text | **13.4–16.0% slower**, old roles2.3% apart, IQR0.4–0.7%; process CPU also adverse |
| Allocation | Effectively unchanged; no allocation-reduction claim |
| Admitted peak | Initial five sync jobs increase1–1,827B; up to4,095B tail per governor can remain funded |

A justified **text-only** confirmation uses all three immutable prototypes and two
old roles, reversed role order and6-second warmup. Its48.262-second capture has
all goldens/balances/quiet gates/joins. Old roles differ1.17%; initial is−0.07%,
completion−3.97%, small-owner refinement+2.57% versus the first old role.
Completion IQR2.46%; others0.68–1.06%. This does **not** explain or erase the
original adverse condition. Process/tiering/warmup sensitivity is a hypothesis,
not an established cause. No stable general CPU benefit is claimed.

Completion passes182 internal/179 public cases on each actual runtime. A broader
focused reservation selection yields1,884PASS/229FAIL, mainly tiny thresholds,
next-request injection and logical/root metrics. Small-owner refinement preserves
more original cases:2,043PASS/71FAIL out of2,114. Remaining failures still require
individual semantic/preflight/retention review; they are not waived as obsolete.
The retained-row scenario originally loses captured output because unused guest
headroom competes with projection. Sequential result disposal alone does not fix
it; completion trimming does. All original conditions remain in diagnostics.

**Decision: shelve, do not promote.** Cooperative admission remains unchanged in
production. A simple source change would require a broad fixture migration and
more confidence in complete-script timing; that is not justified as the next
performance delivery. This is a scoped deferral, not evidence that coarse capacity
is inherently invalid or a permanent no-retiming rule. Reopening requires a
coherent boundary/fixture proposal and a bounded measurement addressing the
preserved adverse condition. Continue the independent operand/dispatch roadmap.

The useful public test correction remains: dispose each failed result before the
next API shares its allowance, retain original captured-row assertions, and verify
the allowance returns to zero. No prototype-specific tests or runtime code remain.

## Evidence

Initial archive SHA256:
`dd0ff83670656737349a6e351dd08f092338d03846607f6aa06ca68e5d3058c0`.
Completion:
`71cad48514ca19a9f20c5b91bdc2249f88d0bb5fd12b6281ed5126458ee26c93`.
Text confirmation:
`c91d1959bf88c731b6f093dc7f4438af6d2ae69427d7b3cf98c11afec6b31684`.

Archives retain original source patches, manifests, worker identities, protocol
responses, machine gates, controls and conditions. Summaries and receipts sit
alongside them. `qualification-diagnostics.tgz` preserves focused TRX failures,
pre-code/refinement plans and discarded source/test migrations. These are
development experiments, not full Windows/Ubuntu/stress/installed-package checks.
`source-metadata-correction.json` records stale inherited source-file hashes in
the text-confirmation identity; its core hashes/Ready witnesses/source.patch
remain authoritative. Original metadata is preserved, not silently rewritten.

Current external CPython comparisons remain in [BENCHMARKS.md](../../../docs/BENCHMARKS.md).
