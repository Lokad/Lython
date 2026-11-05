# Complete-script allocation and activation assessment

2026-11-05, qualified a8e476bf/c6d71108, frozen core **402ae9f1**. Quiet dedicated
F4as_v7/Ubuntu24.04.4/.NET11RC3, ordinary GC/tiering. One **32-second**, two-script
external CLR AllocationTick capture; 1,024 invocations after six guest warmup
seconds each. Independent full output/file goldens, identities, quiet endpoints,
Closed/exit0, collector and worker joins, fixture removal and free lease pass.
**Zero events lost.** No production instrumentation or dependencies added.

This attributes sampled allocation traffic to the triggering type and allocation
size on the observed guest execution thread. It gives **neither exact object
counts nor live storage, CPU savings, RSS or uninstrumented elapsed performance**.
Other threads and phase boundaries remain in the raw evidence. No heap walk,
dump, rundown, forced collection, runtime tracing or native memory access.

| Complete script | Sampled attribution | Consequence for design |
| --- | --- | --- |
| Text summary, 3,072 lines | PyString35.72%, byte arrays18.44%, boxed BigInteger18.09%, temporary reservations11.01%, lists10.39% | Existing counted split can fill its final array under combined capacity/payload admission; no general string-window or scalar rewrite inferred. |
| Ordinary-helper CSV, 4,096 records | Interpreter16.59%, context9.67%, strings15.02%, BigInteger10.21%, object arrays9.04%, namespace dictionary4.56%, cache arrays1.66% | Context already inherits frame and escapes via defining functions. Avoid fusion; remove gratuitous namespace construction and defer unused member-cache tables. Arguments and local slots already share backing. |

The maintained text script uses `lower().split()`, with native UTF-8 file windows;
there is no invented UTF-16/strip intermediate. The CSV frame has nine shared
value slots and one cache slot; direct builtin receivers bypass the generic
cache. Small int arrays include string rune-position storage, not evidence of a
new argument-presence array. Boxing alone does not justify tagged values or an
integer cache. Earlier slower call-arena/control and general shared-string-window
designs retain their lessons.

[Pre-capture contract](plan.md), [sample analysis](assessment.json),
[analysis source](analyze.py), [selected replacement contract](selected-construction-plan.md),
[exact collector/source/raw/gates](observations.tgz), [receipt](archive-receipt.json).
Archive SHA256 `48f791a8754e31f60975cf89627efbbaf9aefc338662fd7c326c1c6f6f8316e6`.
The selected construction changes require separate semantic and controlled
performance validation; this assessment claims no runtime speedup.
