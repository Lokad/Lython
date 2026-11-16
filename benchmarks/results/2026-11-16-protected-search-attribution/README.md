# Protected file-search CPU attribution

R441/B844,2026-11-16UTC,qualified312f5911/frozen0443b5cf. One bounded199Hz
cpu-clock/frame-pointer capture after8guest-seconds warmup. Same seven-case
catalog as B842/B843; only the protected early-exit search executes. All images,
stdout/file goldens,quiet gates and joined cleanup pass. This is attribution,
not a controlled speedup comparison. PerfMap is an explicit diagnostic override.

| Sample boundary | Active samples | Unknown leaves |
| --- | ---: | ---: |
| All selected active worker threads | 947 | 319 |
| Lython execution thread | 749 | 140 |
| Protocol/reset thread | 198 | 179 |

Fixture Reset is the nearest managed frame for146protocol samples and ReadOutputs
for35. They belong to the wider batch,not the guest execution timer. Execution-
thread host path resolution/opening and kernel work are still real mediated I/O.
Do not attribute the full CPU sample set or the CPython elapsed gap to dispatch.

On the execution thread,entry/binding/retirement has251inclusive samples; closed
instruction execution has149. These groups overlap useful guest work and each
other. Nearest frames include prefix102,StartNativeCall50,native RunCore44,
ForNext44,arithmetic39,native-call selection38 and Reserve38. StartNativeCall
has49exclusive leaves. Inlining obscures finer attribution; zero explicit
checkpoint leaves does not mean checking is free. Unknown/kernel leaves remain.
No dominant removable call/frame stage is established by these counts.

The capture completes15104invocations/
4.187guest seconds.
One current source and fixed order cannot establish causality or a speedup. Normal
tiering/workstation GC on quiet F4as_v7/.NET11RC3. Full worker CPU also includes
reset/protocol/validation; the existing unprofiled warm comparison includes Run,
mediated RAM I/O,output/file goldens and prior-result cleanup,but excludes reset,
compilation/startup and final cache teardown. CPython uses native buffered I/O
without equivalent host containment. All raw stacks/maps/thread CPU are retained.

Close this bounded attribution with unresolved dominance. Preserve the measured
native engine; do not resurrect a call carrier or remove managed stack checks
from these samples. Next is a bounded complete-call/frame design assessment:
identify a genuinely eliminated stage and compile-to-result tradeoff before a
prototype. Consumed-operand reuse must address the prior slower tail-loan/arena;
managed block compilation must justify its one-shot preparation cost and complete
replacement scope. Neither is selected for implementation by this profile alone.
No broad sampling campaign or unchanged retiming. Publication remains held and
regex development independent; the broader performance objective remains open.

All users joined; every RAM byte archived and SHA-verified under the lease before
unmount. Terminal cached. Only exact committed B843 working replicas were retired
after fresh UID/path/hash/inode/all-alias proof; canonical evidence,current0443
images and254/252MiBfloors preserved. No runtime or qualification change.

[Attribution](attribution.json),[raw/maps](observations.tgz),
[decision](decision.json),[procedure](procedure.tar.xz).
