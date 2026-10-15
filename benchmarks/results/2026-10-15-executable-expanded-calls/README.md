# Expanded calls share executable argument evaluation

Ordinary calls containing `*values` or `**mapping` now share generator
StartCall/AppendCall/FinishCall lowering. One extracted compiler helper handles
target and argument expressions. The ordinary whole-call fallback and its
boolean selection route are removed, along with local mirroring required solely
by that fallback. Required lowered calls remain for other code; embedded
expressions may still have their own fallback. No opcode, runtime handler,
binder, owner or iterator framework is added.

Prerequisite 9e6bdd57 evaluates deferred arguments before rejecting a non-callable
target in shared syntax/lowered sync/async invocation. Previously argument effects,
ValueError precedence and host cancellation could be skipped. The non-callable
branches now expand existing arguments before rejection; valid callable frame
admission stays unchanged. [Pre-fix CPython comparisons](pre-fix-argument-errors.json)
record the failures on b6febb9d; the new public regressions now pass.

| Warm forwarding helper, 2,048 calls | Earlier Lython | Candidate Lython | CPython 3.13.16 |
| --- | ---: | ---: | ---: |
| Forward a tuple and keyword mapping into a small function | 2.412 ms | 2.077 ms | 0.2443 ms |

One [fixed diagnostic](warm.json) observed a **13.9% lower Lython median**;
IQR/median 0.6% / 2.2% / 0.9%, respectively. No qualified multiplier, confidence
interval, complete file-workload or overall speedup is established. The owned
command took 7.548 s including cleanup. No retry or retuning. General call
binding, argument copying and the substantial CPython gap remain.

Baseline 42dfd7747c76950fa28b2890914faa14c94c9744 adds the common helper fixture
to 9e6bdd57. Both roles contain the ordering fix; the candidate differs only in
two frozen compiler files in [the declaration](comparison-declaration.json).
[The workload](catalog-micro.json), timing machinery and CPython worker are
identical. Warm synchronous Run timing includes fresh run state, execution,
projection and prior-result disposal; compilation, process startup, transport and
full hashing are excluded. Actual suspended host callbacks are tested separately.

Measured 2026-10-15 on Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, four AMD EPYC
9V45 vCPUs / 16 GiB RAM; SDK 10.0.401/runtime 10.0.12 and CPython 3.13.16 with
normal GC/GIL, `-I -S` and explicit UTF-8 protocol. Normal Lython public policies,
cooperative allowance, GC/tiering and managed stack protection apply. CPython has
no equivalent containment. This is a microbenchmark, not a complete file script.

[Baseline](baseline-correctness.json) and [candidate](candidate-correctness.json)
match independent expected output at all three core sizes. [Cleanup/source proof](cleanup-source-proof.json)
confirms the two frozen production differences, identical tooling, seven exited
workers, inactive services and an available VM lease. No file fixtures are used.

[Local validation](local-validation.json): 2,330 ordinary white-box, five focused
white-box, 25 focused public, 411 affected semantic and 284 containment/
representative/file-worker cases pass (overlap); each group under 30 s including
cleanup. Controls cover argument error/effect order, sole-star deferral, mapping
key snapshots, duplicate/non-string keys, closures/locals, `locals(*[])`,
`super(*[])`, generator yield/send, genuinely suspended iteration/mapping/callable
protocols, cancellation without replay, admission before calling and retained
aliases after temporary cleanup. Ten [CPython 3.13.2 goldens](cpython-goldens.json)
match independently. One obsolete test assertion requiring starred calls to stay
on fallback was removed; its remaining class-fallback control is retained.
No timeout/budget relaxation, full local public or fresh original Stress claim.
Package/platform gates are recorded at delivery.

The existing accumulator preflights capacity and final-array overlap and releases
temporary storage after invocation or failed/cancelled expansion. Retained aliases
keep their longer-lived owner. Existing prepared-operation checkpoints can differ
from lowered explicit fuel thresholds; SPEC.md records that contract. No general
guest tracing or new ownership layer is introduced.
