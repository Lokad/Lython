# Complete file-search cost attribution

2026-11-15, committed **51e3c2c1 / frozen 22e5b72c**. One existing protected
file-search script, eight guest seconds warmup and one five-second 199 Hz
CPU-clock/frame-pointer capture. Same complete worker, inputs, goldens and host.
Perf maps are an explicit diagnostic override; no elapsed/source-pair gain claim.

15,872 invocations contribute 746 guest-thread and 256 other-thread samples.
Measured thread CPU is 238.79 µs/invocation on the execution worker and 83.80 µs
on other threads. Sampling includes the complete worker batch, so other-thread
costs also include fixture reset, protocol and validation outside execution clocks.
There are **129 unresolved guest leaves** and no ambiguous resolved leaves;
202 other-thread leaves are unresolved kernel samples. Do not assign these to a
specific runtime mechanism or infer event loss absent.

| Guest stack contains | Samples | Interpretation |
| --- | ---: | --- |
| Native helper execution | 203 | Includes binding, guards and useful helper/builtin work |
| File/line operations | 210 | Includes host work and admitted line/window creation |
| Iteration | 156 | Includes advancing the actual input |
| Admission | 144 | Includes live shared-budget funding |
| Ordinary instruction prefix | 720 | Includes the useful work above |

**Inclusive counts overlap; they are not removable CPU fractions.** Leading
resolved leaves include ordinary prefix 108, input instructions 50,
`StartNativeCall` 44, native-call selection 42, native loop 38, file iteration 34,
and `StartInline` 22. Root-budget lock entry/exit and host resolution also appear.
This does not justify replacing the whole call engine with another arena.

Bounded source checks establish that the native buffer already has **16 slots**
and direct argument binding; synchronous callers already use `Monitor.Wait`.
Line windows already borrow admitted stable UTF-8 backing, with direct fixed
reserve/commit/rollback rather than a per-line transaction wrapper. Shared
admission currently resizes the borrowed lease on each reserve; batching that
would change admission timing and retained headroom, requiring an explicit
contract rather than an automatic memory redesign.

Close this capture with those facts and kernel/runtime uncertainty. The next
bounded gate is host-call timing attribution using a private forwarding wrapper
around the existing trusted comparison host, preserving capability/path/token/
budget/stream behavior and full goldens. Select a coherent runtime change only
when the remaining work to remove is established. No new runtime policy, coupon
pool, guest tracing, configuration or benchmark framework is selected here.

All identity/golden/quiet/Closed/exit/join gates pass. Owned perf bytes and maps
are archived, hash-verified under the lease **before RAM teardown**; actual users
join before cleanup. No background users or RAM fixtures remain. The first local
retrieval lacked its configuration file; extracting the original launch
configuration corrected observation without restarting the service or capture.

[Attribution and unresolved samples](attribution.json),
[source gate](source-assessment.json), [original raw traces](observations.tgz),
[cleanup receipt](archive-receipt.json), [exact procedure](procedure.tar.xz).
