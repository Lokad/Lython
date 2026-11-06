# R07 source gate — source2adf73f3, before implementation

Prior turn made progress: Q02 committed/pushed, both installed consumers and
focused/full-local/depth checks pass; actual CI42180013420 is running. This
contract does not claim that receipt covers later changes. Publication postponed.

Authoritative source disproves a missing common instruction loop. Both APIs
already use ExecuteInstructionPrefix outside async state; its driver awaits only
pending work. Native numeric and string binary operations already use synchronous
handlers. Lazy namespaces/direct locals and current frame optimizations likewise
exist. Do not build another dispatcher, call arena or generic call-control layer,
and do not reprofile those absent hypotheses.

Actual remaining difference: Q02 routes every direct dict.get through async hash/
lookup/result adaptation. The same engine already admits a synchronous dictionary
subscript read when key is PyString and HasProtocolKeys is false. This proof
permits no guest key hash or stored-key equality callback. Native string storage
and canonical protocol-entry counts already supply it; no registry/side index.
Argument evaluation can alter the table, so check the proof after all arguments
finish, on the retained actual receiver. Never cache that permission across calls.

Select a coherent common read-prefix change: share the existing positive native
proof across subscript/direct-get/exposed-get/read preparation as justified by
actual callers. Keep direct synchronous result publication when it holds, and the
existing awaited semantic walk otherwise. Remove redundant native ValueTask/hash/
result adaptation in that boundary. Reuse an existing atomic-key semantic fact
only if it is equally proven; do not invent a key-type whitelist, recursively
preclassify tuple/slice children or weaken protocol/equality selection. No retry
or fallback after refusal, new options, owner, cache, frame or execution backend.

Acceptance: native string reads remain Python-correct; key/fallback expressions
may insert a same-hash custom key after receiver selection, forcing the actual
async comparison path. Custom descriptor/body/tuple/slice/dataclass hashing and
native/custom mixed stored keys retain genuine suspension. Preserve first refusal,
cooperative cancellation, callback order, current-entry mutation revalidation,
managed depth, retained aliases and awaited cleanup. Existing Q02 controls and
native direct-get families; add only a real argument-side proof-invalidating gap.

One bounded complete text/CSV warm-async comparison plus synchronous text control
against source2adf73f3, two identical roles/CPython and existing workers/protocol.
No new framework, allocation/CPU prediction or primary-table refresh. Preserve
B334/B335 and their+3.72/+5.13% text debt. Retention requires a decision about
that debt, not a rigid gain threshold. If source disproves applicability, stop
with that evidence; if unchanged controls vary, allow bounded correction without
pretending the design failed. Full platform/stress/package checks batch at the
next coherent source checkpoint; keep independent work moving during CI.
