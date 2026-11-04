# Shared module lexical facts and preparation

2026-11-04. Retain module facts on existing ScriptSyntax and reuse them through
lowering and executable construction. Require facts at all builder entries;
remove optional/default constructors, the duplicate declared-fact field and the
ForLoweredStatements/GetSyntax reconstruction switch. Fold module-super discovery
into the existing lexical walk; tag eligible calls only after later/global
declarations are known. Remove the separate ModuleUsesSuper walker. The existing
preparer is renamed LexicalScopePreparation. **57 net production lines deleted.**

Module bindings remain outside lexical class-cell lookup. Actual function scope,
rather than the origin of supplied facts, determines class-cell admission. Module
class-read facts finish after child preparation so their bindings are reused.
The initial valid early-finalization variant and its successful checks remain
archived; it was never timed. No new pass, eligibility bit, cache framework,
runtime backend, option or public API change.

## Controlled complete-script results

Baseline runtime123f9ff1, fully qualified in cached41853526333, frozen159744bd;
candidate1d61912b plus archived patch, frozen0d452f95. [Exact identity](candidate-identity.json)
and snapshots distinguish precommit assembly metadata from retained source.
Quiet Azure F4as_v7/Ubuntu24.04.4, four EPYC9V45 vCPUs/16GiB/no swap, .NET11RC3,
normal GC/tiering; same CoreLib/hash witnesses and all auxiliary bits.

| Complete workload / phase | Candidate elapsed | Change vs both baseline roles | Same-source spread | Candidate IQR |
| --- | ---: | ---: | ---: | ---: |
| CSV helpers / compilation |81.486µs|−3.22% /−7.28%|4.39%; control-limited|0.37%|
| Text helpers / compilation |60.205µs|−7.42% /−6.58%|0.90%|0.31%|
| JSON-to-Markdown / compilation |57.523µs|−5.32% /−4.19%|1.17%|0.46%|
| Config read/edit/write / fresh |0.2973ms|−0.35% /−0.23%|0.12%|0.19%|
| Text helpers / fresh |3.2054ms|−0.04% /−0.05%|0.01%|0.55%|
| Text helpers / warm |3.0368ms|−1.15% /−2.90%|1.80%|0.43%|

Negative means shorter elapsed. Text/report compilation improves; CSV's shifted
identical controls limit its magnitude. Config/fresh text are near unchanged;
warm text shifts modestly despite unchanged emitted instructions. Do not claim
or attribute a broad execution/CPU gain. Whole-process allocation per compilation
falls4.25–4.83%, fresh config1.38–1.41% and fresh text0.33%; warm is unchanged.
Those counters include fixture reset and process activity, not retained heap/RSS.
Retain the coherent deletion and compile benefits without a fixed-percent veto
or another CSV retiming. [Exact metrics](assessment.json).

The existing three-image driver uses six seconds guest warmup/image, alternating
preparation order by case, two seconds settling, nine rotated200ms samples sized
from the warmed baseline only. Compilation includes Compile/validity/disposal;
fresh includes compilation and full public Run, actual mediated RAM-file I/O,
all stdout/file checks and per-job result/owner cleanup. Warm excludes compilation.
Fixture reset/staging, startup and final fixture teardown are outside elapsed;
reflection/shared process GC remain included. No CPython ratio is updated.
The163-second owned collection completes normally below240s. All identities,
CPython3.13.16 goldens, allowance baselines/final zero, fixture cleanup and before/
after quiet gates pass. Service inactive/MainPID0, no users and global lease free.
[Original plan](pre-measurement-plan.md), [all observations](observations.tgz),
SHA256 `3ff176d41c3a2c98506a8b1f1bacec672c918b03224c815bb3074192b99016bc`.

## Semantic and delivery acceptance

Focused Release240 internal/1,889 public per actual.NET11RC3/10.0.8 pass, without
test changes. Scope/class/super, delayed global declarations, annotations,
imports, generators/comprehensions, provider effects, cancellation, terminal
refusal and owner cleanup remain covered. Both targets'27 scripts/71 code objects
have identical active instructions, scope facts, captures/closures, slots, spans,
protected regions and native provenance, including ten explicit module/super
controls. [Source gate](source-gate.json), [validation scope](validation.json),
[exact sources, metadata and all initial/final logs](source-and-validation.tgz).
Release nonincremental builds have zero warnings/errors and matching core/test/
benchmark images. Main97bc43b2 passes [full41863346055](delivery-ci-status.json):
Windows/Ubuntu actual11/10 ordinary, original Stress and installed consumers, all
eight logical gates GREEN/cached. Do not re-query/replay this terminal run. The
cherry-picked source has identical canonical LF text, with working-tree CRLF
normalization recorded in [source equivalence](main-source-equivalence.json);
original frozen binary/source identities are unchanged. Publication held.
