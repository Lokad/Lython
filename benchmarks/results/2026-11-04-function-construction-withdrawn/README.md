# Combined function/binding representation: withdrawn

Measured2026-11-04 against fully qualified ee8020f0/core38c03e5e on the quiet
AzureF4as_v7 Ubuntu24.04.4 VM, .NET11RC3. Prototype core
`d1481c48c61691ef7bc3b2f1a6c14a225418366c4398620e57635ab01872fbb9`.
No public API/Python behavior change. Publication remains postponed.

The prototype borrowed compiler parameter-prefix names/maps/counts instead of
rebuilding partition arrays and a signature map. It also made the function itself
inherit binding/default state, eliminating a separate plan allocation. Defaults,
keyword key values, closures and retained aliases remained execution/result-owned.
A conservative320B estimate replaced128B plus separate signature layout charges.
Some parameterless/lowered functions could therefore reject earlier.

Final nonincremental Release36.71s,0warnings/errors;744internal/3013public per
actual11/10 pass. This includes real suspended default equality, cancellation,
terminal refusal, retained defaults, class cells/generators and sequential budget
reuse. New public regression covers body-local/positional-only/variadic keyword
overflow and factory defaults isolation; independent CPython3.13.2 output exact.
The initial local raw-byte assertion saw native Windows CRLF; explicit UTF8 text
capture fixed that harness check, with no guest change or timed run. A pre-upload
gate initially compared unused newly built auxiliary identities. Both timed images
use identical previously frozen auxiliaries; all SHA/CoreLib/hash/quiet/golden/
baseline/finalzero/fixture-removal/join checks pass. No full delivery qualification
was run for this withdrawn source.

Nine complete jobs244s. Three isolated images/two unchanged old roles, ordinary
GC/tiering,6s guest warmup/image,2s settle,9rotated200ms batches. Signs are candidate
elapsed/allocation versus each old role; positive is worse. Process allocation is
separate from elapsed, includes fixture reset/background noise and is near flat.

| Complete script | Phase | Candidate ms | Elapsed vs old / old control | Old spread | Process allocation vs old / old control |
| --- | --- | ---: | ---: | ---: | ---: |
| text-summary-helpers | compile | 0.060197 | +1.80% / +0.60% | 1.20% | +0.03% / +0.03% |
| text-summary-helpers | fresh | 3.190286 | -1.03% / +0.60% | 1.62% | -0.01% / -0.01% |
| text-summary-helpers | warm | 3.182075 | +5.78% / -3.11% | 9.18% | -0.02% / -0.02% |
| csv-summary-helpers | fresh | 5.317394 | +2.31% / +0.16% | 2.15% | -0.01% / -0.03% |
| csv-summary-helpers | warm | 5.081697 | +0.76% / -0.52% | 1.29% | -0.01% / -0.03% |
| report-render | compile | 0.057195 | -1.56% / -0.14% | 1.42% | +0.03% / +0.06% |
| report-render | fresh | 4.948107 | -3.04% / -1.48% | 1.59% | -0.01% / -0.01% |
| report-render | async | 4.808993 | +0.10% / -0.12% | 0.23% | -0.01% / -0.01% |
| config-edit | warm | 0.197646 | -0.29% / +0.86% | 1.14% | +0.00% / +0.02% |

Original warm text +5.78/-3.11% with9.18%old spread is INCONCLUSIVE, not proof of
a failed design. One bounded fresh-process confirmation preserved identical source
and boundaries, only text/control jobs;57s, all gates/cleanup pass:

- text warm3.104378ms, **+3.14/+2.60%**, old spread0.52%, candidate IQR0.61%;
  process allocation−0.02%.
- config warm0.197512ms, +0.95/+1.50%, old spread0.54%, candidate IQR0.55%;
  allocation0.00..+0.02%.

The confirmation establishes adverse text elapsed under its conditions. It does
not identify CPU cause. Whole-script allocation savings are tiny; structural copy
removal alone does not establish an overall performance gain. Withdraw the combined
representation rather than incur this debt for a small storage benefit. This is
an evidence-weighted decision, not a permanent percentage veto. One revised design
restores the original separate sealed plan/function objects while testing compiler
layout reuse independently; its source/results must be qualified separately.

Initial untimed warm setup admission is64B lower for helper scripts and identical
for config. These are not compile-only/fresh overlap, process/RSS or embedding
latency measurements. Compile includes Compile/validity/Dispose. Fresh adds full
Run, actual mediated RAM I/O, all output/file goldens, result/compiled disposal.
Warm/async include invocation/completed I/O/checks/result cleanup; async does not
measure pending host latency. Reset/staging/startup/hash/final cached teardown
excluded; all users joined. No CPython timing claim from this source pair.

[All metrics](assessment.json), [confirmation](confirmation/assessment.json),
[original plan](pre-measurement-plan.md), [confirmation plan](confirmation/pre-measurement-plan.md).
Raw SHA256: `3cb8abab2be19911afd9b5a18cfdd73dcd2087e3f5857c0e691b306f6b9d4410`;
confirmation `21907600735a175832fffbb88f66d8573251073abae20852973d15caebff5ba3`.
Both raw archives and the withdrawn source snapshot/patch are preserved here.
