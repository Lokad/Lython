# Floating JSON numerical report attribution

R490/B909, 2026-11-17 UTC. Runtime557381fe/frozend739132f, now
[fully qualified](../2026-11-17-shared-allowance-lock/delivery/README.md).
One existing complete script: parse two arrays of4096 floating values, aggregate
through a helper and generator sum, write a sorted JSON report. Same verified
five-case catalog/32-file image as the [CPython checkpoint](../2026-11-17-json-cpython-checkpoint/README.md).
This is sampled attribution, not a controlled performance comparison.

949 active execution-thread samples/81 unknown leaves; 4.856 guest seconds/4.78
active CPU seconds. Nearest Lython frames: instruction prefix152, JSON number
conversion87, stack transfer85, JSON construction74, sequence indexing56, generic
indexing54, input instruction38, native prefix35, managed stack admission34,
arithmetic28. JSON inclusive190, indexing110, call/frame182, admission1; groups
overlap useful/inlined work and are not removable percentages. The initial
arithmetic classifier omitted TryEvaluateBuiltinArithmetic; the corrected result
and original analysis are both preserved. No boxing-only or full-gap attribution.

Source gate: generator sum already consumes native slots; slots already contain
one managed reference and one long. Ordinary JSON numbers already use scalar
headroom, not per-number reservations. Input contains two flat arrays, not4096
nested row containers. No duplicate scalar format, sum transport, credit cache or
per-record capacity redesign is justified by those false premises.

Select R492: direct scalar parsing into iterative construction, removing default
ValueTask<object> then ValueTask<(object,int)> packaging. Only genuine callbacks
need pending work. Keep one parser/public document async boundary, exact grammar,
raw_decode positions, hooks/error/effect order, large integer admission, retained
aliases, current cancellation and joined cleanup. A bounded floating warm/fresh,
helper JSON and unchanged control comparison determines retention/regression debt;
no predicted magnitude or automatic threshold. Execution/index/stack work remains.

Eight-second warmup/two-second settle/five-second199Hz cpu-clock/frame-pointer
capture on quiet Azure F4as_v7/Ubuntu24.04.4/.NET11RC3, normal GC/tiering,
diagnostic PerfMap only. All actual-image/golden/quiet/Closed/exit/join gates pass.
RAM traces SHA-archived persistently under lease before unmount. Joined root-owned
perf data received ownership correction with unchanged bytes. The first staging
attempt refused below the disk floor before mutation/measurement; preserved with
the exact-committed-replica repair, not treated as a failed design. Protected
images/toolchains/canonical evidence and254/252MiB floors remain. Terminal cached.

[Attribution](attribution.json), [raw](observations.tgz), [catalog](catalog.json),
[identity](identity.json), [custody](archive-receipt.json), [procedures](procedures.tar.xz).
Publication remains held; this capture does not complete the performance objective.
