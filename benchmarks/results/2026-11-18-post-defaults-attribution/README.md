# B1003 warm execution attribution after direct defaults

Profiled the exact B1002 image `53df08565` / `6c4cb50a` on the quiet Ubuntu
Azure F4as_v7 on 2026-11-18 UTC. The worker verified stdout and file goldens
before and after each profile. Each case warmed for at least eight seconds
before a five-second whole-batch CPU capture. Symbol maps and stacks were
archived and SHA-verified before RAM fixture removal. Both quiet gates passed
and all workers joined. The perf-map setting and whole-batch boundary make
this a diagnostic attribution exercise. [B1002 comparison](../2026-11-18-direct-defaults-cpython-checkpoint/README.md).

| Warm script | Runtime ancestry | Observation |
| --- | ---: | --- |
| Rule closures | 1098 of 1106 samples | 318 include lowered default evaluation and 222 include native or borrowed invocation. The closest resolved runtime method is the lowered expression dispatcher in 66 samples and the synchronous evaluator in 48. |
| Expanded forwarding | 982 of 1024 samples | 512 include expanded call construction or binding. The closest resolved runtime method is `BindFunctionArguments` in 48 samples and `AppendStarredDictionaryCore` in 29. |

The groups overlap and their counts include downstream work. Rule closure
defaults still evaluate ordinary function definitions through the lowered evaluator,
while executable lambdas already consume evaluated slots. The next prototype
should move executable `def` input evaluation onto the surrounding code and
share final definition construction, removing the ordinary lowered dispatch and
the captured-expression shells used by yielding definitions where feasible.
Function metadata appears in only 2 resolved rule-closure samples.
Forwarding still warrants a complete expansion-to-invocation review. Its
directory reset and output checks do not account for most sampled worker CPU.

The final classifier narrowed the definition group to exclude invocation through
`PyFunctionBase` and classified lowered work from the original stacks. Offline
regeneration produced a new procedure-tar timestamp. The raw archive retains the
exact executed scripts and both measured worker identities. No timing comparison
was taken under profiling.

[Attribution](attribution.json) · [Raw traces and executed scripts](observations.tgz) ·
[Procedure and classifier](procedures.tar.xz) · [Terminal state](terminal-status.json)
