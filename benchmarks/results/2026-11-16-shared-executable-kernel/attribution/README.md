# Shared-kernel differential CPU attribution

R439/B839,2026-11-16UTC,first prototype91ca4a41 versus qualified0443b5cf.
One bounded199Hzcpu-clock/frame-pointer capture per source after8guest-seconds
warmup,with explicit PerfMap override. Same captured-helper script,data,output
and file goldens. This is diagnostic attribution,not a controlled elapsed/CPU
speedup comparison; source order is fixed and there is one process per source.

| Diagnostic | Reference | Prototype |
| --- | ---: | ---: |
| Complete invocations | 3,840 | 3,840 |
| Active-thread samples | 938 | 974 |
| Unknown leaves | 75 | 80 |
| Combined instruction/call dispatch leaves | 225 | 223 |
| Nearest dictionary-loop frames | 171 | 195 |

Dispatch leaves combine ordinary prefix,native/shared RunCore,Call,CallFunction
and TryExecuteNativeCall. The split changes when methods move; combined samples
do not isolate a new causal binding/call bottleneck. Inclusive entry/body groups
overlap useful/inlined work and are not removable shares. Dictionary/casing,
admission and unknown samples remain. A zero sample count does not prove zero cost.
Retain all leaves,method lists,thread CPU,maps and raw samples. The adverse
[unprofiled B838](../README.md) remains the timing evidence.

Select one existing-kernel refinement:[immutable code/view facts](refinement-decision.json).
Keep an immutable Code reference on the borrowed view and derive its fixed loop
boundary once; PC,live extent and cells keep referencing actual shared metadata.
The existing outer instruction-array snapshot already requires stable code/backing
during a view. No new policy,stored mode,controller,owner or engine. This is a
source hypothesis,not an established explanation of B838. Focused semantics and
one changed-source controlled comparison decide residual debt before retention.
Full platform/stress/package qualification remains pending; main runtime unchanged.

Initial sampler failed before a worker started because the old helper used an
auxiliaryFiles identity field. Its only trace file was empty worker.stderr.
Failure,journal,original code and cleanup are preserved; all RAM bytes were
archived/verified before unmount. Correction changed only per-source full-image
validation to the current imageFiles schema; no runtime/catalog/timing change.
Both failed and corrected terminal states are cached; never requery them.

Corrected capture passes exact full-image/catalog checks,goldens,quiet gates,
Closed/exit/join and persistent RAM trace verification before unmount. No users
or RAM fixtures remain. Quiet F4as_v7/.NET11RC3 normal tiering/workstation GC;
profiling overrides and wider reset/protocol boundary are explicit. Only two
exact committed B838 archive/working-observation replicas were retired under a
fresh idle lease; canonical evidence,current images and disk floors remain.

[Attribution](attribution.json),[raw/maps](observations.tgz),
[failed setup](initial-failed-observations.tgz),
[procedures/original conditions](procedures-and-original-conditions.tar.xz).
