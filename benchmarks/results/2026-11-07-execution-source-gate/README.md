# Execution representation source gate — R27

2026-11-07UTC. Inspect existing source and generated code before selecting another
runtime design. No runtime prototype, elapsed comparison, new allocation result,
CPython refresh or platform qualification. Performance objective remains open.

R24 Linux samples assign21.06% of text leaves to ExecuteInstructionPrefix, but
include useful inlined operations. Calls/binding~2% and integer addition~3%
do not establish a dominant removable call/scalar cost. Current source already
has admitted final locals/operands, safe constant/local forwarding, direct result
stores, direct receiver dispatch, deferred lexical contexts, cached execution
workspaces and native dictionary read/write paths. R20/R25 remove record storage
duplication. Casing/split already write final UTF8; native lower/split is fused.
Do not propose these as missing or revive slower call-arena/control designs.

Closed native wrappers still perform some frame entry/translation, but selected
text operations already bypass them. Removing wrappers alone is not a demonstrated
explanation of the text gap. Any change must prove its managed stack/guest-callback
boundary; no stack guard or cancellation checkpoint was removed in this gate.
Exact integer promotion, aliases/identity/hash/projection remain prerequisites
to a representation change, not reasons to silently introduce tagged-value paths.
Shared lexical facts are delivered; further frontend work must remove an actual
duplicate walk and measure compilation separately from warm execution.

## Existing text workload / JIT inspection

Use frozen R26core9e1dbc38 (source matches c4729a9f production), local existing
worker36a8c77f, .NET11RC3, Windowsx64. These worker build bits differ from the
normal VM's b94fa98c; exact Ready identities are retained. Existing eight-case
catalog, text-summary-helpers.large only, verify twice and three1000-job batches,
normal GC/tiering. DOTNET_JitDisasm/StdOutFile collect code only. No elapsed or
allocation attribution, normal performance qualification or Linux address mapping.

All3,000 text jobs pass complete output/file goldens; Closed/exit0/join and
supervisor-owned fixture removal pass.336,173B raw disassembly includes two
instrumented Tier0 versions (11,145B each) and Tier1 (10,091B). Tier1 reports
synthesized PGO,43inlinees with PGO data,175single-block inlinees and6without PGO
data; its prologue reserves648B before saved-register/shadow details. These are
generated-code facts, not heap/RSS, a performance cost model or removable bytes.
The mixed method needs more precise transport/dispatch attribution before a
wider canonical representation rewrite. No native-wrapper or tagged-value
prototype is selected merely from its aggregate21% sample share.

## Original setup conditions

Initial inspection spends over a minute before any worker is observed, with no
completed data, and is stopped by verified owned PID. It enumerates the shared
Windows temp root; the exact stalled call was not captured. Treat directory
enumeration as a plausible cause, not a measured diagnosis. Restrict subsequent
fixtures to one private root. First private-root setup is rejected explicitly by
the existing supervisor-directory validation before guest execution. Correct
the owned working-directory prefix and parent, then run unchanged runtime/source.
Preserve rejected stdout/stderr and all scripts. Failed setup is not a failed
runtime design or discarded adverse performance measurement.

No new benchmark framework, production instrumentation, unsafe code, policy,
allowance, cache or dispatcher. No VM capture or repeat of completed qualification.
Next milestone requires concrete eliminated work/storage, displaced code,
semantic/cancellation/ownership acceptance and bounded controlled before/after.
R26 async/text debt and larger whole-script gaps remain explicit.

## Bounded allocation attribution / selected next source gate

Reuse the existing D36 EventPipe allocation-tick collector unchanged, on the
same frozen R26 core/current local worker. GC keywords only, no collection,
rundown, heap snapshot, object inventory or runtime instrumentation. Each text/
report worker verifies twice, warms256 complete jobs, settles2s, then captures
1024 complete checked jobs. Postverify, cooperative collector stop, Closed/exit0,
joined processes and removed private fixtures all pass.120s outer ownership bound;
no VM run or elapsed source-pair claim.15,873text/26,720report ticks, zero lost.

Marked-window shares attribute bytes since the previous allocation tick to its
threshold-crossing object. They are statistical diagnostics, not exact per-type
counts or CPU attribution. All worker-request reset/staging/I/O/checks/prior-result
cleanup in the window is included. Compilation/warmup/final teardown are outside.
Report reset/output capacity can contribute large byte arrays; do not label
that54.8% as avoidable guest-string cost. Dominant allocation thread is not
proven CPU-dominant; retain all threads/raw data.

| Sampled allocation type | Text | Report |
| --- | ---: | ---: |
| PyString |31.74%|9.60%|
| Byte[] |26.76%|54.80%|
| Boxed BigInteger |23.71%|7.00%|
| PyList |9.01%|small/remainder|
| Object[] |3.58%|12.36%|
| TemporaryMemoryReservation |5.10%|2.51%|

This supports a text final-storage source gate before a universal scalar rewrite;
it does not establish CPU dominance. PyString samples are56B, integer boxes32B.
Earlier shared windows added8B/string and measured slower. A different bounded
proposal can remove the separate hash-cache flag, use zero as an uncached hash,
and fit a byte offset into the existing object footprint. Recomputed zero hashes
remain correct. Measure footprint, alias funding and whole scripts before promotion.

Do not share arbitrary existing input windows: decoded file windows are refunded
explicitly, so matching MemoryGovernor identity does not establish a retained
backing lifetime. Restrict the first design to a newly constructed immutable
split-result payload, committed directly to the execution owner before its word
windows are created. Metadata refunds must never refund that shared payload.
Existing private-input/refund paths stay outside that sharing. No source-shared
flag, refcount, owner ledger, pool or general slice-ownership audit is required.
Capacity/metadata/replacement estimates, terminal refusal, Unicode/index/hash/
regex-offset semantics and retained results are acceptance gates. Final CPU
and allocation decisions need a bounded controlled source pair; not automatic
promotion or a revival of the earlier design solely because it was rejected.
