# Current complete-normalization attribution

R434/B831,2026-11-16UTC, fully qualified **86d9344e /ecd9e956**.
Unchanged complete4,096line/52,224B normalization, stdout/file goldens and
existing worker. Eight guest seconds warmup,199Hz perf CPU-clock/frame-pointer,
4.828guest seconds/4,096invocations sampled. Same quiet F4as_v7/Ubuntu24.04.4,
four EPYC9V45vCPUs,16GiB/no swap,.NET11RC3 natural workstation GC/normal
tiering. Diagnostic perf-map override; **no timing comparison or speedup**.

926active-thread samples,138unmapped leaves,0ambiguous map matches; all retained.
Nearest Lython frames:120ordinary instruction prefix,69string-method operation,
58expandtabs,40splitlines,39rstrip,36split,33Reserve,30native-call selection,
26generator Advance,26replace. Exclusive mapped leaves include119instruction
prefix,69SpanHelpers search,35memmove,33string method,32rstrip,30native-call
selection,27expandtabs and25Advance. Inlining limits exact attribution.

Inclusive groups overlap:531generator/iteration,303text/storage,33admission,
59host/file/projection,50call/binding and913instruction dispatch. These include
useful operations and cannot be added or called removable overhead. Active worker
4.67CPU seconds; background GC.11/JIT.03; all other-thread CPU/samples retained.
Sampling includes mediated RAM I/O, reset/protocol/goldens and prior-result cleanup,
wider than guest timing. It does not separately measure compilation/startup, heap,
pending-I/O latency, contention or embedding tails.

Admission is not established as the dominant cost; do not introduce a reservation
cache or budget policy from this profile. Generator work dominates inclusively,
but exclusive advancement alone does not justify a second generator backend.
Unifying stack-local and persistent operand execution remains a larger open design;
a stack-only Span view cannot simply be retained across suspension.

**Next selected source hypothesis:** ordinary subtext construction still allocates
independent UTF8 arrays/copies, while existing same-owner JSON windows already
preserve immutable backing funding without identity tracing. Consolidate slices
and ordinary split fragments onto that existing representation, preserving aggregate
admission and independent foreign/unowned copies. Remove a storage stage rather
than a collection of method-specific tricks. Current source establishes duplicate
payload construction; these samples do not predict its CPU magnitude. Require
semantic/Unicode tests, retained backing/aliases, refusal before publication,
current cancellation and cleanup, then allocation plus one complete-script pair
with synchronous controls. Conservative sibling retention is an explicit tradeoff.

All image/golden/quiet/Closed/exit/join/archive gates pass. RAM trace bytes verified
persistently before unmount, no users remain, terminal cached. Only the withdrawn
prototype's exact committed core replica was retired, with fresh lease/path/UID/
hash/all-alias proof. Current runtime/catalogs/toolchains and254/252MiB floors kept.
No production source change or repeated unchanged full qualification. Publication
held; the substantial whole-script objective is open.

[All samples/unknowns](attribution.json),[exact image](identity.json),
[raw capture](observations.tgz),[procedures/custody](procedures.tar.xz).
