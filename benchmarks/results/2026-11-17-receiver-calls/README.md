# Shared builtin receiver calls and compact argument transport

R464/B871,2026-11-17UTC. Committed210c9cae / frozen ae389c19 versus private
7a599347,now promoted with identical585production source texts. Five production
files change; public APIs and shared-memory ownership contract stay the same.
Full combined R463/R464 platform/stress/installed-package qualification is pending.
Prior43989114633qualifies R460,not these later sources. Publication remains held.

Ordinary and native instructions now share builtin receiver resolution and closed
invocation. Duplicate native str/dict inventories and selection branches are gone.
The existing16Bargument view can carry one evaluated reference without an array;
array-backed windows remain for actual async users. An exact-list append helper
can complete through its existing guarded descriptor/body without binding owned
continuation storage or constructing a lexical frame. No new owner,arena,policy,
public option,selector cache or interpreter. General protocols/I/O still transfer
before effects; completed prefixes are not replayed. Cased-split and dictionary-
accumulation paths retain their existing eligibility and cleanup.

A global ref-struct argument redesign was not selected: working regex/coercion/
format operations need array windows across real awaits. Keeping that live path
avoids new async snapshots or ownership bridges. The compact single-reference
shape adds an internal branch; observed controls and full qualification decide
its impact rather than assuming a benefit from fewer objects alone.

| Complete script / boundary | Elapsed change | Wider CPU change | Reference/candidate control movement |
| --- | ---: | ---: | ---: |
| Helper-list normalization / warm Run | −40.37..−37.37% | −38.95..−35.47% | 3.16 / 1.82% |
| Same text / compile and Run once | −38.88..−37.40% | −36.89..−35.70% | 1.28 / 1.13% |
| Helper-list JSON/Markdown report / warm Run | −24.96..−23.63% | −25.48..−24.32% | 0.10 / 1.67% |
| Same report / compile and Run once | −24.03..−22.02% | −22.26..−21.23% | 0.91 / 1.73% |
| Ordinary positional calls / warm Run | −0.22..+0.77% | −0.79..+1.20% | 0.01 / 0.98% |
| Numeric report / warm Run | −6.16..−0.61% | −4.72..−2.20% | 1.95 / 3.89% |
| CSV invoice / warm Run | −0.83..+0.21% | −1.37..0.00% | 0.00 / 1.05% |

Four pairings of two process medians/source,ranges not confidence intervals.
Affected complete scripts improve with supporting CPU; text/control movement
limits exact magnitude. Numeric also favors candidate,but3.89%candidate movement
limits magnitude/attribution. Call/CSVpairings straddle zero. Retain this coherent
calling/storage simplification with that uncertainty; do not claim a universal
speedup or retroactively resolve earlier numeric/async CSV debt. No unchanged
retiming pending and no current CPython ratio inferred across collections.

Natural-GC local diagnostics establish helper-text allocation−51.78%,report−33.16%.
Retained accounted peaks each208Blower(essentially flat),not a comparable reduction
in the shared allowance. Other original cases are flat within0.03%allocation;
all raw rows/local first-use/compilation observations remain. Each execution
restores the compiled-budget baseline; compiled cleanup returns zero. No forced
collection,RSS,physical heap bound or embedding-tail claim.

The two additional ordinary script shapes reuse unchanged inputs/renderer/stdout/
file goldens: text appends each rstripped line through a small helper; report
appends each pre-rendered record through a helper before joining/writing. Same
scripts in both images. No causal comparison to the old script's streaming/error
order. Eleven-case preflight passes actual frozen workers and isolated local
CPython before staging. This timing collection is a Lython source pair,not a new
CPython performance comparison. Existing worker/protocol/tooling reused.

Initial Release checks per actualWindows.NET11/10:155internal/152public pass.
Additional coupled.NET11checks96internal/212public pass; filters overlap and are
not summed. Promoted combined filters each251internal/347public pass after a clean
nonincremental full solution build(zero warnings/errors). Eight new public cases
cover alias publication,receiver rebinding,closures/recursive handoff,late faults,
actual suspended writes after an append,cancellation/joined cleanup and terminal
admission refusal through handlers/finally returns. Retained results remain funded;
all budgets return to baseline after users/cleanup. Formatted promoted test source
is included separately from the immutable measured snapshot. Production text
matches; no rebuilt binary equality is inferred. Full delivery remains separate.

Quiet F4as_v7/Ubuntu24.04.4,four EPYC9V45vCPUs,16GiB/no swap,.NET11RC3 natural
workstation GC/normal tiering. Seven jobs213.509s;4s full-request warmups,five
rotated approximately0.5s samples/process. Images differ only in the core. Warm
includes synchronous Run,mediated RAM I/O,complete stdout/file goldens and prior-
result cleanup; excludes compilation/startup,reset/protocol and final cache
teardown. Fresh adds compilation and compiled-owner disposal; CPU clocks wider.
All identities/goldens/quiet/Closed/exit/join/archive gates pass.

Every user joined; SHA-verified persistent archive under lease before RAM teardown.
Only five old raw JSON working replicas,total8,547,268B,retired after committed
canonical equality plus fresh UID/path/hash/nlink/all-inode-alias proof. The old
native-proof canonical remains correctly partial/adverse evidence,not relabeled
as success. Current binaries/catalogs/toolchains and all canonical evidence stay;
254/252MiB staging/final floors preserved. Terminal cached permanently. Continue
the broader runtime roadmap after this retained milestone; publication held.

[Timing](summary.json),[allocation](allocation-summary.json),[qualification status](validation.json),
[source/image identity](identity.json),[source](candidate-sources.tar.xz),
[measured core](measured-core.tar.xz),[raw observations](observations.tgz),
[focused checks](focused-checks.tgz),[procedures/custody](procedures.tar.xz).
