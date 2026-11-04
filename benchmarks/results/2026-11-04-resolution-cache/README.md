# Binding-state resolution cache

2026-11-04. Retain one cache keyed by the identity of each immutable syntax node.
Every binding mutation already clears it; remove the version counter, entry
wrapper and comparison, and recursive structural record hashing/equality.
Fourteen net frontend lines disappear. No new cache, feature bit, skipped pass,
global memo, dependency or public option. Annotation/binding analysis retains
its real provider effects even without annotated syntax.

This reconsiders the [earlier cache trial](../2026-11-02-analysis-cache-unqualified/README.md),
which improved compilation but missed the former fixed 10% requirement. Its
3.84–7.35% compile observations and 15.23% fresh-config control drift remain
unchanged. They establish a reason to reassess on current source, not a prior
qualified gain. The separate invalid annotation-pass skip stays withdrawn.

## Current complete-script comparison

Baseline runtime c6ac2ae5/source HEAD7535e07a, frozen core3522b972. Candidate is
that source plus the archived cache patch, frozen159744bd. Exact source/core/
auxiliary hashes: [identity](candidate-identity.json). All other image files and
CoreLib/hash witnesses match. Quiet Azure Standard_F4as_v7, Ubuntu24.04.4,
four EPYC9V45 vCPUs/16GiB/no swap, .NET11RC3, normal GC/tiering.

| Complete workload / phase | Candidate elapsed | Change vs both baseline roles | Same-source spread | Candidate IQR |
| --- | ---: | ---: | ---: | ---: |
| CSV helpers / compilation |86.061µs|−3.90% /−6.48%|2.76%|0.60%|
| Text helpers / compilation |63.932µs|−8.35% /−8.39%|0.04%|0.28%|
| JSON-to-Markdown / compilation |61.448µs|−3.09% /−3.09%|0.00%|1.05%|
| Config read/edit/write / fresh |0.3011ms|−1.08% /−1.75%|0.68%|0.67%|
| Text helpers / fresh |3.1798ms|−0.46% /−0.30%|0.16%|0.85%|
| Text helpers / warm |3.0502ms|−1.81% /+0.39%|2.19%|0.55%|

Negative means shorter elapsed. Compilation improves against both controls;
fresh text is within dispersion and warm text straddles its controls. Do not
claim execution or broad CPU gains. Whole-process allocation per compilation
falls about0.25–0.46%, fresh0.03–0.17%; warm text is unchanged. These counters
include fixture reset/process activity and do not measure retained heap or RSS.
No external CPython ratio is refreshed by this source pair.

Six seconds guest warmup per image, alternating preparation order by case,
two seconds settling, nine rotated counterbalanced samples. Batch size uses
only the warmed baseline to target200ms; all original observations remain.
The existing shared-CoreLib three-image driver is adapted privately, without
runtime instrumentation or a benchmark framework change. [Exact metrics](assessment.json),
[original plan](pre-measurement-plan.md), [sources and driver](source-and-driver.tgz).

Compilation includes public Compile, validity and compiled-owner disposal;
fresh adds complete public Run, mediated RAM-file I/O, stdout/file verification
and per-job result/owner cleanup. Warm excludes compilation. Fixture reset/staging,
startup and final fixture teardown are outside clocks. Shared CoreLib/process GC
and reflection remain part of this diagnostic; no guest-only CPU claim.
The source/input/output/file goldens are unchanged and independently match
CPython3.13.16. All before/after quiet gates, identities, per-job/final zero
allowance, fixture cleanup and normal process exits pass. Owned collection163s,
below240s; terminal service inactive/MainPID0 and global lease independently free.
[Complete raw observations](observations.tgz), SHA256
`4894498423080de7dd285e26e50a346f88f9cd73ad9142e425baa27b97697161`.

## Correctness and remaining qualification

Focused Release109 internal/807 public per actual.NET11RC3/10.0.8 pass.
Existing tests cover resolution mutation/withdrawal/clone/replace/join, diagnostics
and ordering, annotations/directives, regex analysis/capabilities, compiler/parser
cancellation, terminal refusal and retained owners. No tests or assertions change.
One initial probe subprocess used the system dotnet without.NET11; the corrected
PATH rerun passes unchanged. Preserve that failure in [validation logs](validation-logs.tgz)
and [validation scope](focused-validation.json).

Full Windows/Ubuntu, original Stress and installed Release consumers are pending
at this coherent delivery boundary. Earlier D1 compilation debt versus454 is
not erased by these separate source-pair percentages; any direct confirmation
must retain the exact older image and its own controls. Publication postponed.
