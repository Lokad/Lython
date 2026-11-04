# Compiler parameter-count metadata and layout reuse: withdrawn

2026-11-04, quiet AzureF4as_v7 Ubuntu24.04.4/.NET11RC3. Baseline fully qualified
ee8020f0/core38c03e5e; candidate
`d6fd5e6b81bc1319547149e9d19d68b4eafde4b81d1b6968fbfef7496cb14dfc`.
This restores separate sealed plan/function objects after the [combined prototype](../2026-11-04-function-construction-withdrawn/README.md),
but borrows compiler parameter-prefix names/maps and stores counts in each code
object. Removes runtime partition arrays and rebuilt maps; defaults remain isolated.
Conservative320B covers function/plan/empty attributes; default storage separate.
No public API/Python behavior change; tiny-budget thresholds can differ.

Nonincremental Release38.04s0warnings/errors;744internal/3013public per actual11/10
pass, including independent CPython regression and actual suspended default-key
lookup, cancellation, refusal, retained aliases and budget reuse. No full platform/
Stress/package qualification for this withdrawn prototype.

Six jobs164s, three isolated images/two unchanged old roles,6s guest warmup/image,
2s settle,9rotated200ms batches. All exact identities/CoreLib/hashes/full independent
goldens/quiet/baseline/finalzero/owned fixture removal/join checks pass. Ordinary
GC/tiering; same frozen auxiliaries, core only differs. Positive signs mean worse.

| Complete script | Phase | Candidate ms | Elapsed vs old / old control | Old spread | Process allocation vs old / old control |
| --- | --- | ---: | ---: | ---: | ---: |
| text-summary-helpers | compile | 0.059990 | +1.02% / +0.06% | 0.96% | +0.03% / +0.03% |
| text-summary-helpers | fresh | 3.162120 | +0.13% / +0.48% | 0.35% | -0.01% / -0.01% |
| text-summary-helpers | warm | 3.194966 | +2.89% / +3.17% | 0.27% | -0.02% / -0.02% |
| csv-summary-helpers | warm | 5.181147 | -1.57% / +1.05% | 2.59% | -0.01% / -0.03% |
| report-render | async | 4.772361 | -0.59% / -0.62% | 0.03% | -0.01% / -0.01% |
| config-edit | warm | 0.196177 | -0.05% / -1.36% | 1.33% | +0.00% / -0.02% |

Text warm **+2.89/+3.17%** with0.27%old spread and0.68%candidate IQR remains
adverse. This does not support the proposed combined-function-size explanation;
there is still no CPU attribution. Fresh text and compile are near flat; CSV and
some other rows are control/dispersion-limited. Allocation savings~0.01–0.03% of
whole-script totals are small; no general speedup claim.

Do not promote this revision. One final simpler design removes the new code-object
count tuple/compiler pass, deriving counts at function creation and borrowing only
the already existing names/map. That avoids moving count work into compilation and
restores the original hot code-object representation. Its performance effect is a
hypothesis; new-source qualification is required. If adverse, withdraw layout work
and continue the broader prepared declaration redesign; no local hint/variant
series is selected. These are evidence-weighted design decisions, not permanent
percentage vetoes. All original/adverse conditions remain linked.

Compile includes public Compile/validity/Dispose. Fresh adds full Run, actual
mediated RAM I/O, full output/file checks and result/compiled cleanup. Warm/async
include invocation/completed I/O/checks/result cleanup. Reset/staging/startup/hash/
final cached teardown excluded, all users joined; process allocation includes
reset/background work. Initial untimed warm setup peaks are not fresh overlap,
RSS, embedding latency or pending-I/O performance. No CPython timing claim.

[Metrics](assessment.json), [premeasurement plan](pre-measurement-plan.md),
[raw observations](observations.tgz), SHA256
`17df8bcc731ca30d208af04d1ddf042942896abaab3d6da23cc13afc0c5e14b0`.
Source snapshot and patch here preserve the withdrawn implementation. Publication held.
