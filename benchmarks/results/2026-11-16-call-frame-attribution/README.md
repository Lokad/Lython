# Current call/frame and operand attribution

R452/B855,2026-11-16UTC,committed81f60210/frozen a44f65a8. Exact production text
matches the [fully qualified cursor consolidation](../2026-11-16-native-consumer-consolidation/delivery/README.md).
Two short captures,not a timing comparison or source-of-regression diagnosis.

| Complete workload | Guest capture | Active samples / unknown leaves | Selected exclusive mapped leaves |
| --- | ---: | ---: | --- |
| Numeric squared-error report,4,096records |5.075s|994/71|Ordinary instruction prefix102; native helper prefix80; stack transfer74; input dispatch72; index read70; JSON construction50; dictionary probe38 |
| Ordinary positional helper/range,16,384calls |5.131s|1,010/62|Ordinary instruction prefix294; builtin arithmetic113; native helper prefix107; native-call selection85; StartInline56; retire storage35; StartNativeCall26 |

No ambiguous mapped leaves. Inclusive generator/dispatch/call groups overlap
useful work; sampled methods contain inlined operations. These counts cannot be
added into removable shares or translated into a speedup forecast. Retain71/62
unknown leaves and all raw stacks/maps. This does not explain R450's conflicting
numeric elapsed outcomes or eliminate the accepted call/regression debt.

The same fixed five-case catalog42531737,current image32files and existing perf
worker protocol are used. Eight guest seconds warmup per workload,199Hz CPU-clock/
frame-pointer perf with diagnostic perf-map override.128-invocation batches sized
from current1.9–2.0ms source-pair medians keep both captures within the existing12s
aggregate guard. Active execution-thread CPU5.01/5.09s; worker-wide CPU includes
reset/protocol/goldens/prior-result cleanup,not just guest clocks. QuietF4as_v7/
Ubuntu24.04.4,four EPYC9V45vCPUs,16GiB/no swap,.NET11RC3natural workstation GC/
tiering. Not compilation,startup,CPython,heap/RSS or embedding-tail evidence.

**Next bounded design:** preserve scalar index operands through native sequence
reads instead of publishing an integer object only to convert it back. Consolidate
existing tuple-only native/ordinary paths into one existing-slot read for closed
builtin sequences; preserve generic Python protocols,arbitrary-width indices,
canonical identities and error spans/order. No new frame arena,selector cache,
code generator or public option. The native helper loop remains until an actually
justified complete replacement exists; R447 API feasibility alone does not select
JIT compilation. Existing call-arena/control/shared-kernel adversity remains relevant.

Use focused sync/async semantics,current-token/refusal/ownership checks,then short
natural-GC allocation diagnostics and one bounded numeric whole-script warm/fresh
source pair with text/call controls. Remove displaced tuple routes in a retained
migration. Allocation,CPU and source simplification are separate; retain adverse
results and refine only when evidence justifies it. No fixed-percentage veto.

All image/golden/quiet/Closed/exit/join/archive gates pass. Every RAM trace byte
SHAverified persistently under the lease before unmount. No remaining users or
fixtures; terminal cached forever. Only B854working raw JSON replica848,232B
retired after committed archive equality plus fresh UID/path/hash/nlink/all-alias
proof. Canonical evidence,current images/toolchains and254/252MiB floors remain.
Publication/tagging held. The broader whole-script objective continues.

[Attribution/unknowns](attribution.json),[exact image](identity.json),
[raw captures](observations.tgz),[procedures/custody](procedures.tar.xz).
