# B1674 CPU attribution for generated ordinary definitions

The control runs 96 generated functions and lambdas with ordinary positional
and keyword defaults. A small helper evaluates defaults, and three of the
created functions are returned and called. Both images verified the complete
output against CPython before sampling. Reference `a45b6da7f` predates the
evaluated singleton integer slot in `01f1f6118`.

The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3. Each worker warmed for
eight seconds, then `perf` sampled at 199 Hz for about six seconds. Each image
verified 18,176 invocations. Sample scope is warm whole-batch worker CPU,
including request framing and verification. There is no guest file I/O or
compilation in the sample. This is attribution; use B1670 for paired timing.

| Nearest resolved runtime region | Before integer slot | With integer slot |
| --- | ---: | ---: |
| Instruction dispatch and call routing | 466 | 482 |
| Function and default construction | 162 | 167 |
| Admission and retirement | 176 | 146 |
| Entry and operand movement | 14 | 22 |
| Arithmetic | 22 | 19 |
| Other resolved methods | 318 | 324 |
| **All samples with a resolved runtime ancestor** | **1,158** | **1,160** |

`RunCore` (254/249) and `NativeExecution.Call` (208/224) account for most of
the dispatch row. `TryStartDirectOperandReturn` accounts for 70/69 of the
other samples. Of the 173/145 nearest `MemoryGovernor.Reserve` samples,
94/68 have `CreateFunctionValue` as their nearest caller, and 72/69 have
`AllocateFunctionDefaults`. These are separate admission points with distinct
failure ordering. `StoreDefaultResult` has 26/25 nearest samples, while
`PublishInteger` has 6/16. In the overlapping ancestry counts, default
storage appears in 161/146 stacks and default consumption in 0/1.

The profile locates work in the executed instruction and call path and in
definition construction. It leaves the ordinary-definition CPU regression
unattributed. A different default layout alone has little support from the
default-consumption counts. Nearest-method counts include inlined work and
do not predict how much CPU a redesign can remove.

The quiet gates, frozen image checks, worker joins and archive verification
passed. The storage receipt in the observations records a verified local
archive of derived outputs moved from an older image before collection.

[Identity](identity.json) · [Attribution](attribution.json) ·
[Observations](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Terminal status](terminal-status.json).
