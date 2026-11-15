# Complete method-format report attribution

R417 / B811, qualified**b06b8593 / frozen8afb146e** on2026-11-15UTC.
One existing complete JSON-to-Markdown report,1,024records/189,441B JSON,
ordinary str.format; same stdout/file goldens as the f-string version.
Quiet F4as_v7 Ubuntu24.04.4/four EPYC9V45vCPUs/16GiB/no swap,.NET11RC3
normal GC/tiering. One current source,8s full-request warmup,5.55s guest clocks,
199Hz perf frame-pointer sampling. DOTNET_PerfMapEnabled=3 is diagnostic.
Whole worker includes mediated RAM I/O, validation/reset/protocol and prior-result
cleanup, wider than execution clocks. Not a timing pair or removable-share estimate.

**1,052 active-thread samples**,161unknown leaves; all unknowns, overlapping stacks
and other-thread CPU retained. Template/field paths appear in476samples, call/argument
paths161, frame/transport78, actual format/append work195; categories overlap useful
and inlined work. Nearest-frame evidence includes77template,64field,44format-root,
37argument-wrapper and27native-spec-parser samples. Admission Reserve has87 nearest
samples; no conclusion that reservation itself can be removed. Source facts confirm
both report helpers native-eligible/unmirrored, but str.format lacks a registered
receiver method and promotes at LoadCallMember. Promotion frequency is not instrumented.

**Decision:** investigate one reusable format layout per existing execution-owned
bound method, replacing repeated template/field/spec metadata construction for both
format/map through the same executor. Preserve effect/error order, custom hooks,
tokens, retained ownership and genuine suspension. This is a selected redesign,
not a measured improvement. Native frame promotion and argument transport remain
separate debt; no controller or parallel native formatter added.

Capture passes full-image identity/golden/quiet/Closed/exit/join/archive gates.
RAM traces archived/hash-verified persistently under lease before unmount; no users
remain;254/252MiB floors preserved. Three initial pre-worker staging failures remain:
insufficient conservative headroom, then an older root-owned archive failing the
UID check, then the same check with diagnostic detail. No trace or partial worker
was started. Final retirement only removes exact committed archived lokad-owned
replicas after full path/hash/link/all-alias proof. Local early retrieval was refused
before its75s observation bound; final terminal state is cached forever.

[All samples](attribution.json),[compiler facts](compiler-facts.json),
[complete image](identity.json),[raw capture](observations.tgz),
[procedures/original staging conditions](procedures-and-failed-staging.tar.xz).
The fully qualified binary/source proof is [R416](../2026-11-15-formatting-final-sink/delivery/README.md).
[CPython phase gap](../2026-11-15-format-cpython-checkpoint/README.md) is independent.
